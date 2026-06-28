using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Utterheim.Services.Voices;

namespace Utterheim.Services.Tts;

/// <summary>
/// Real <see cref="ITtsEngine"/> backed by the pocket-tts Python sidecar (ADR 0002),
/// extended in main-015 with the utterheim sidecar wrapper (ADR 0015) so cloned
/// voices route through <c>/tts-with-state</c> while built-ins keep using <c>/tts</c>.
///
/// Synthesis flow (per <c>pocket_tts/main.py</c>):
///  - Built-in: POST <c>/tts</c> with form-encoded <c>text</c> and <c>voice_url</c>
///    (the built-in voice name).
///  - Cloned:   POST <c>/tts-with-state</c> with form-encoded <c>text</c> plus the
///    voice's <c>profile.safetensors</c> bytes uploaded as <c>voice_state</c>.
///
/// In both cases the server returns a <c>StreamingResponse</c>: WAV header followed
/// by 16-bit mono PCM at the model's sample rate (24 kHz for the English models).
/// We strip the WAV header on the way through and yield raw PCM chunks straight to
/// <see cref="Speak.AudioPlayer"/> — no buffering of the whole utterance, so first
/// audio arrives well within the 2 s end-to-end latency budget.
/// </summary>
public sealed class PocketTtsEngine : ITtsEngine
{
    // Pocket-tts ships built-in voices with the predefined-voice mapping in
    // pocket_tts/utils/utils.py. The eight English voices are derived from
    // Les Misérables; main-040 adds `juergen` (German), the first non-English
    // built-in, per ADR 0023 + the pocket-tts 2.1.0 German support research.
    // Cloned voices come from VoiceLibraryService and are surfaced via
    // VoiceCatalog (main-015).
    private static readonly IReadOnlyList<VoiceDescriptor> BuiltInVoices =
    [
        new("alba",    "Alba",    "pocket-tts", true, VoiceLanguage.English),
        new("marius",  "Marius",  "pocket-tts", true, VoiceLanguage.English),
        new("javert",  "Javert",  "pocket-tts", true, VoiceLanguage.English),
        new("jean",    "Jean",    "pocket-tts", true, VoiceLanguage.English),
        new("fantine", "Fantine", "pocket-tts", true, VoiceLanguage.English),
        new("cosette", "Cosette", "pocket-tts", true, VoiceLanguage.English),
        new("eponine", "Eponine", "pocket-tts", true, VoiceLanguage.English),
        new("azelma",  "Azelma",  "pocket-tts", true, VoiceLanguage.English),
        new("juergen", "Juergen", "pocket-tts", true, VoiceLanguage.German),
    ];

    private static readonly HashSet<string> BuiltInIds = new(
        BuiltInVoices.Select(v => v.Id), StringComparer.OrdinalIgnoreCase);

    // Default English models all use 24 kHz mimi sample rate, mono, 16-bit.
    // (Verified by reading pocket_tts/config/english.yaml + data/audio.py StreamingWAVWriter.)
    public AudioFormat OutputFormat { get; } = new(SampleRate: 24000, Channels: 1, BitsPerSample: 16);

    private readonly SidecarHost _sidecar;
    private readonly VoiceLibraryService _library;
    private readonly ILogger<PocketTtsEngine> _logger;
    private readonly HttpClient _http;

    public PocketTtsEngine(
        SidecarHost sidecar,
        VoiceLibraryService library,
        ILogger<PocketTtsEngine> logger)
    {
        _sidecar = sidecar;
        _library = library;
        _logger = logger;
        // Long timeout — synthesis of long passages can take ~1/6 of audio length on CPU.
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public Task<IReadOnlyList<VoiceDescriptor>> ListVoicesAsync(CancellationToken ct)
    {
        return Task.FromResult(BuiltInVoices);
    }

    public async IAsyncEnumerable<byte[]> StreamAsync(
        string text,
        string voiceId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var voice = string.IsNullOrWhiteSpace(voiceId) ? "alba" : voiceId.Trim();

        await _sidecar.EnsureReadyAsync(ct).ConfigureAwait(false);

        var built = BuildSpeakRequest(_sidecar.BaseUrl, text, voice);
        var request = built.Request;
        var contentDisposable = built.ContentDisposable;
        var isBuiltIn = built.IsBuiltIn;

        // ResponseHeadersRead is load-bearing: the default ResponseContentRead buffers the
        // entire WAV before the awaited Task returns, throwing away the StreamingResponse on
        // the Python side. Per main-023's diagnosis (H4 confirmed) and ADR 0013, this is
        // input-length-independent ~200-500 ms first audio vs ~20 ms/char buffering.
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch
        {
            request.Dispose();
            contentDisposable.Dispose();
            throw;
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Pocket-tts {(isBuiltIn ? "/tts" : "/tts-with-state")} returned " +
                    $"{(int)response.StatusCode}: {body}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            // Strip the WAV header. Standard PCM RIFF header is 44 bytes (RIFF/WAVE/fmt /data).
            // The sidecar's StreamingWAVWriter writes a `wave` module header which is 44 bytes for
            // mono 16-bit PCM. We could parse it formally, but pocket-tts always emits this exact
            // shape — we sanity-check the magic bytes and skip 44.
            await SkipWavHeaderAsync(stream, ct).ConfigureAwait(false);

            var buffer = new byte[8192];
            int n;
            while ((n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                // Copy out — caller may hold the chunk past the next read.
                var chunk = new byte[n];
                Buffer.BlockCopy(buffer, 0, chunk, 0, n);
                yield return chunk;
            }
        }
        finally
        {
            response.Dispose();
            request.Dispose();
            contentDisposable.Dispose();
        }
    }

    /// <summary>
    /// Build the outgoing sidecar speak request for <paramref name="voiceId"/>,
    /// stamping the voice's <see cref="VoiceLanguage"/> on the wire so the
    /// multi-model sidecar (main-039) routes to the matching resident
    /// <c>TTSModel</c>. The routing hint rides on the <c>X-Voice-Language</c>
    /// request header — chosen over a form field so the sidecar's ASGI
    /// middleware can read it without consuming the multipart body (see
    /// main-039 Notes). The Claude-Code-facing <c>POST /speak</c> contract
    /// (ADR 0003 — <c>{text, voice}</c>) is untouched: this header lives only
    /// on the C# host-to-sidecar-internal hop.
    ///
    /// Surfaces as <c>internal</c> so the test project can assert the wire
    /// shape without spinning up an HTTP listener — production callers go
    /// through <see cref="StreamAsync"/>.
    /// </summary>
    internal BuiltSpeakRequest BuildSpeakRequest(string baseUrl, string text, string voiceId)
    {
        var voice = string.IsNullOrWhiteSpace(voiceId) ? "alba" : voiceId.Trim();
        var isBuiltIn = BuiltInIds.Contains(voice);

        HttpRequestMessage request;
        IDisposable contentDisposable;
        VoiceLanguage language;

        if (isBuiltIn)
        {
            var builtIn = BuiltInVoices.First(v =>
                string.Equals(v.Id, voice, StringComparison.OrdinalIgnoreCase));
            language = builtIn.Language;

            var url = baseUrl + "/tts";
            var content = new MultipartFormDataContent
            {
                { new StringContent(text), "text" },
                { new StringContent(voice), "voice_url" },
            };
            request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            contentDisposable = content;
            _logger.LogInformation(
                "PocketTtsEngine synthesising {Chars} chars for built-in voice '{Voice}' ({Language})",
                text.Length, voice, language);
        }
        else
        {
            var profilePath = _library.TryResolveProfilePath(voice);
            if (profilePath is null || !File.Exists(profilePath))
                throw new InvalidOperationException($"Unknown voice id '{voice}'.");

            // VoiceLibraryService.TryResolveLanguage MUST return non-null when
            // TryResolveProfilePath did — both lookups hit the same in-memory
            // index. Default to English defensively only if the race-prone case
            // of a delete-between-calls hits (the request is going to fail at
            // the sidecar anyway; English is the safer fallback since the
            // English model is always preloaded per ADR 0024).
            language = _library.TryResolveLanguage(voice) ?? VoiceLanguage.English;

            var url = baseUrl + "/tts-with-state";
            // FileStream lifetime is managed via the disposed content below;
            // ASP.NET copies the bytes into the request before SendAsync returns.
            var profileStream = new FileStream(
                profilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
            var content = new MultipartFormDataContent
            {
                { new StringContent(text), "text" },
                { new StreamContent(profileStream), "voice_state", "profile.safetensors" },
            };
            request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            contentDisposable = content;
            _logger.LogInformation(
                "PocketTtsEngine synthesising {Chars} chars for cloned voice '{Voice}' ({Language}) from {Profile}",
                text.Length, voice, language, profilePath);
        }

        // Tag the request with the resolved language. The sidecar's middleware
        // reads this header and swaps the active resident TTSModel before the
        // pocket-tts /tts handler (which keys off the module-level tts_model
        // global) runs. The literal values match the lower-case JSON the
        // VoiceLanguage enum serialises as, so the same lookup table works
        // wire-end-to-wire-end without a separate dictionary.
        request.Headers.Add("X-Voice-Language", LanguageWireValue(language));

        return new BuiltSpeakRequest(request, contentDisposable, isBuiltIn);
    }

    /// <summary>
    /// Lower-case wire value for <paramref name="language"/> matching the JSON
    /// the <see cref="Voices.VoiceLanguage"/> enum serialises as
    /// (<c>english</c> / <c>german</c>). The sidecar's middleware expects
    /// these literals; ADR 0024 / 0025 pin them.
    /// </summary>
    internal static string LanguageWireValue(VoiceLanguage language) => language switch
    {
        VoiceLanguage.English => "english",
        // ADR 0025: German tracks English's production variant — distilled
        // "german", not the 24-layer "german_24l" preview. main-038's
        // listen-test swap to "german_24l" found no audible advantage and was
        // reverted here (main-d7m2k); the leaked swap cost ~867 MB resident RAM
        // (main-r8k3w). The preload key in SidecarHost.cs must match this.
        VoiceLanguage.German => "german",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    private static async Task SkipWavHeaderAsync(Stream stream, CancellationToken ct)
    {
        // Read enough to cover RIFF + fmt + the start of `data` chunk header. 44 bytes is the
        // standard layout for 16-bit mono PCM written by Python's `wave` module.
        const int HeaderSize = 44;
        var header = new byte[HeaderSize];
        int read = 0;
        while (read < HeaderSize)
        {
            int got = await stream.ReadAsync(header.AsMemory(read, HeaderSize - read), ct).ConfigureAwait(false);
            if (got <= 0)
                throw new InvalidOperationException("Pocket-tts response ended before WAV header was complete.");
            read += got;
        }

        // Sanity check: RIFF....WAVE....data
        if (header[0] != 'R' || header[1] != 'I' || header[2] != 'F' || header[3] != 'F'
            || header[8] != 'W' || header[9] != 'A' || header[10] != 'V' || header[11] != 'E')
        {
            throw new InvalidOperationException("Pocket-tts response did not start with a RIFF/WAVE header.");
        }
    }
}

/// <summary>
/// Internal carrier for <see cref="PocketTtsEngine.BuildSpeakRequest"/> so the
/// test project can assert wire shape without spinning up HTTP. The
/// <see cref="ContentDisposable"/> owns the multipart parts (text +
/// voice_url / voice_state stream); production callers dispose it after the
/// response is read.
/// </summary>
internal sealed record BuiltSpeakRequest(
    HttpRequestMessage Request,
    IDisposable ContentDisposable,
    bool IsBuiltIn);
