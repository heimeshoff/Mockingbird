# Context: main

## Purpose

The single bounded context for Utterheim. Owns everything from voice acquisition through synthesis to delivery: voice profile management, sample capture, speak-request queueing, streaming TTS playback, the Claude Code integration surface, and the tray UI that wraps it all.

This is a personal tool with one user and one primary consumer (Claude Code). The whole app is one coherent subject; see `../../context-map.md` for why the candidate sub-boundaries (synthesis, voice-library, voice-capture, claude-bridge, tray-ui) were rejected as separate BCs.

## Classification

**Mixed — primarily core, with supporting and generic regions inside it.**

- **Core**: the *voice diversity* loop — letting the user clone arbitrary voices from any audio source they have, persist them as `.safetensors` profiles, and route them per Claude session. This is the differentiator. No off-the-shelf product does exactly this for this user's workflow.
- **Supporting**: the speak queue, stop semantics, output device selection, tray UI shell. Real work, but not the differentiator — these exist to make the core loop usable.
- **Generic**: audio capture plumbing (WASAPI loopback, microphone), global hotkeys, settings persistence, tray icon, the synthesis engine itself (pocket-tts is consumed, not built). These are reused from WhisperHeim or vendored from libraries.

The fact that the BC straddles all three classifications is a normal property of small personal tools — there isn't enough domain mass to justify cutting along the classification line.

## Core language

From the vision's seed glossary, plus terms that surfaced during boundary analysis:

| Term | Meaning |
|---|---|
| **Voice profile** | A named, persistent representation of a voice that pocket-tts can reuse instantly. `.safetensors` file plus metadata. The unit of voice identity. |
| **Sample clip** | The 5–20-second audio snippet used to create a voice profile. Source: microphone, WASAPI loopback, or imported file. |
| **Built-in voice** | A voice profile shipped with pocket-tts (alba, marius, javert, jean, fantine, cosette, eponine, azelma — all English; plus juergen for German, added by main-040). |
| **Cloned voice** | A user-created voice profile. |
| **Voice language** | Pocket-tts language attribute every voice profile carries (ADR 0023). v1: `english` or `german`. Drives sidecar `TTSModel` routing — the C#-host-facing speak request body stays `{text, voice}`, but on the C#→sidecar-internal hop `PocketTtsEngine` resolves the voice's language and tags the request with the `X-Voice-Language` header (`english` / `german`), which the sidecar's `LanguageRoutingMiddleware` reads to pick the matching resident model (main-039). Lives on `meta.json` + the `library.json` index row; legacy entries default to `english` per the ADR migration rule. |
| **Multi-model sidecar** | The python sidecar holds one `TTSModel` per preloaded language in a `_RESIDENT_MODELS` dict (main-039 / ADR 0024). `serve --language english --language german` preloads both concurrently at startup; per-request routing swaps `pocket_tts.main.tts_model` to the matching entry before pocket-tts's `/tts` handler runs. Adding a third language is a config change (extend `--language` flags, restart sidecar), not a code change. **Resident footprint (measured, main-r8k3w):** ~2.0 GB idle, model-weight dominated (torch ~176 MB + pocket_tts ~84 MB runtime; each model ~420 MB resident, of which flow_lm is the bulk). **Known drift:** the live `SidecarHost.cs:291` currently launches `german_24l` (the 24-layer preview, ~1205 MB flow_lm) instead of the distilled `german` (~341 MB flow_lm) that ADR 0025 selected — this row documents the intended state; the revert (~867 MB cut) is tracked by follow-up `main-d7m2k`. Inline int8 quantization was measured and rejected as a RAM lever (ADR 0029). |
| **Speak request** | An incoming call carrying `{text, voice ID}`. The unit of work the BC queues, synthesizes, and plays. |
| **Speak queue** | FIFO of pending speak requests. Head plays; tail is appended. Advances on completion or stop. |
| **Stop signal** | A user action (double-tap RCtrl) that halts current playback. Drain-vs-keep semantics is an open question (see vision). Cancellation honours the ≤2 s ADR 0026 CPU-drop budget; the sidecar wrapper monkey-patches `_autoregressive_generation` at boot to observe a `threading.Event` in the inference loop (ADR 0027). |
| **Loopback capture** | Recording the system output device via WASAPI loopback. The "record what I'm hearing" source for voice cloning. |
| **First-chunk latency** | Time from speak request to first audio sample at the speakers. Target ≤2 s; measured ~190 ms warm / ~320 ms cold (median, alba) and input-length-independent after main-024 / ADR 0013. |
| **Streaming synthesis** | Producing audio in chunks during generation so playback starts before the full utterance is rendered. |
| **Capture session** | An interactive recording episode that produces (or rejects) a single sample clip. |
| **Engine** | The TTS implementation behind a profile. v1 has one engine (pocket-tts). A profile records its engine so future multi-engine selection is possible. |
| **Speak endpoint** | The localhost-only HTTP/IPC surface Claude Code calls to enqueue speak requests. The published interface of this BC. |
| **Voice library** | The on-disk catalog of cloned voices: `<dataPath>\voices\library.json` (master index) plus `<dataPath>\voices\<id>\` (per-voice profile + meta + optional sample). Persistence layer for "own-your-voices". |
| **Voice id** | Stable lowercase-kebab folder name (`marco`, `marco-a3f2`). Generated from display name; never renamed. Collisions with built-in voice ids are rejected; collisions among cloned voices get a 4-hex disambiguator. |
| **Library reconciliation** | Startup pass that brings `library.json` and on-disk voice folders into agreement: prune missing-on-disk entries, reinsert orphan folders from `meta.json`, log warnings for unreadable / future-schema files. |
| **Sidecar wrapper** | The `utterheim_sidecar` Python module that mounts utterheim-owned routes (`/export-voice`, `/tts-with-state`) on top of pocket-tts's FastAPI app, sharing its resident `tts_model`. Per ADR 0015. |

## Key actors

- **The user** (single developer) — clones voices, manages the library, configures settings, hits the stop hotkey, watches the tray status. Episodic interaction.
- **Claude Code sessions** (multiple, parallel) — the primary speak-request producers. They call the speak endpoint and expect audio to come out within ~2 s. They do not know about the queue, the voice library, or the engine; they only know `{text, voice}`.
- **pocket-tts** (external engine, consumed) — the conformist dependency. Utterheim adapts to its API.

## Relationships

This BC is the only domain BC. External relationships:

- **Upstream conformist** to **pocket-tts**: utterheim adapts to whatever shape pocket-tts exposes (`TTSModel`, voice states, Mimi encoding, `.safetensors`). Wrapped behind a thin internal engine interface to leave room for a second engine later.
- **Open host (published language)** to **Claude Code**: the speak endpoint is a stable, documented localhost surface that any Claude hook can call. The wire format (`{text, voice}` plus stop / status) is the published language.
- **Library consumer** of **WhisperHeim shared services**: audio capture, hotkeys, settings, startup. Not a BC relationship — these are technical libraries with no domain language.

## UI / styleguide gate

This BC has substantial frontend surface (tray icon + tray window with voice library, capture flow, voice-test playback, settings — modeled on WhisperHeim's design.md). 

**Rule:** every frontend-bearing task in this BC `depends_on` the styleguide task **`main-010`** (`done/main-010-styleguide.md`, artefact at [`docs/styleguide.md`](../../../docs/styleguide.md)). The gate is **OPEN** as of 2026-05-01 — Marco Heimeshoff signed off on the styleguide. Frontend feature tasks may now be promoted from `backlog/` to `todo/`. This protects the "feels like a first-party Windows app — Mica backdrop, Fluent controls, Segoe UI Variable, the WhisperHeim aesthetic" success criterion in the vision.

Note: the walking skeleton (`main-009`) was always exempt — its UI is a placeholder shell whose purpose is foundation, not presentation.

## Code structure

The walking skeleton (main-009) materialises ADRs 0001–0008 as code. main-020
adds the navigation shell on top per ADRs 0009 and 0010. Top-level layout:

```
utterheim.sln
src\
  Utterheim\                        WPF tray app, net9.0-windows x64
    EntryPoint.cs                     composition root + IHost lifecycle
    App.xaml(.cs)                     WPF Application + WPF-UI theme dictionary
    Views\
      MainWindow.xaml(.cs)            Mica FluentWindow + ui:NavigationView shell
                                      (sidebar + Speak/Voices/Settings in MenuItems +
                                      About in FooterMenuItems per main-032 + status
                                      footer) + tray:NotifyIcon menu +
                                      RootContentDialogPresenter that hosts in-window
                                      ContentDialogs (main-026)
      BootstrapDialog.xaml(.cs)       first-run dialog (placeholder in v1)
      Pages\
        SpeakPage.xaml(.cs)           Stub in main-020 — main-013 fills it
        VoicesPage.xaml(.cs)          Voice library list with per-row preview (main-014)
                                      and per-row Delete on cloned rows (main-026)
        SettingsPage.xaml(.cs)        Audio / App / Diagnostics setting cards (main-016)
        AboutPage.xaml(.cs)           main-032 redesign: pure identity surface
                                      mirroring WhisperHeim's About — BrandHeroControl
                                      with the "Local voices for Claude Code" tagline,
                                      profile-and-contact card (140x140 ringed
                                      heimeshoff.jpg + bio + heimeshoff.de / Bluesky /
                                      LinkedIn rows), Support & GitHub card (Ko-fi
                                      gradient button → AppInfo.KofiUrl + GitHub link →
                                      AppInfo.GithubUrl), credits. Engine diagnostics
                                      moved to Settings (per ADR 0021).
      Dialogs\
        DeleteVoiceDialog.xaml(.cs)   Fluent ContentDialog for the per-row Delete
                                      affordance on cloned voices (main-026).
      Controls\
        BrandHeroControl.xaml(.cs)    Reusable hero block (main-030) — 88x88
                                      logo badge + "Utterheim" 40pt ExtraBold
                                      + version tag in BrandDeepMutedBrush +
                                      optional `Tagline` DependencyProperty
                                      (collapsed when null/empty). Reads version
                                      from AppInfo.Version. Used by Speak page
                                      (no tagline) and About page in main-032.
      Converters\
        SharedConverters.cs           NullOrEmptyToVisibilityConverter +
                                      EngineStateToPipBrushConverter consolidated
                                      under one file (main-032). Both registered
                                      as App.xaml resources alongside the WPF
                                      built-in BoolToVisibilityConverter so any
                                      Page can reference them via {StaticResource}
                                      without per-page namespace imports. Replaces
                                      the deleted ViewModels/Pages/AboutPageConverters.cs
                                      and the duplicate copy in VoicesPageConverters.cs.
    ViewModels\
      EngineStatusViewModel.cs        Backs the persistent footer (HTTP +
                                      Engine state, live via SidecarHost.StateChanged)
      EngineStatusCardViewModel.cs    Engine-status card sub-VM (main-032) — extracted
                                      from the former AboutPageViewModel; carries
                                      EngineState / Healthy / Port / LastError /
                                      EngineStateLabel / PortLabel / IsRetryEnabled +
                                      RestartEngine / OpenLogs commands. Composed onto
                                      SettingsPageViewModel.EngineStatus; Attach() /
                                      Detach() drive the SidecarHost.StateChanged
                                      subscription on Settings page lifecycle.
                                      Registered transient (per ADR 0021).
      Dialogs\
        DeleteVoiceDialogViewModel.cs Backs DeleteVoiceDialog (main-026) — voice id
                                      + display name, IsDeleting / ErrorMessage flags,
                                      Delete / Cancel commands; on success hides itself,
                                      on IO failure surfaces an inline error and stays open.
      Pages\
        SpeakPageViewModel.cs         Speak page VM — Text / Voices / SelectedVoice /
                                      StatusLabel + Play / Stop / Save commands (main-013)
        VoicesPageViewModel.cs        Voices page VM (main-014) — BuiltInVoices /
                                      ClonedVoices / per-row PreviewCommand routed
                                      through SpeakService.Enqueue (ADR 0014); plus a
                                      VoiceRowViewModel inner class for each list row.
                                      Composes a VoiceCloningViewModel for the cloning
                                      sub-panel (main-025).
        VoiceCloningViewModel.cs      Cloning sub-VM (main-025) — drives the source
                                      toggle (Microphone | System Audio), device
                                      selectors, level meter, duration / progress,
                                      voice-name validation, and the Save flow
                                      (render WAV → POST /export-voice →
                                      VoiceLibraryService.AddAsync). Minimum 5 s,
                                      soft cap 30 s, hard cap 60 s auto-stop.
        RainbowPassage.cs             Static class holding the canonical first
                                      two sentences of the Rainbow Passage
                                      (Fairbanks 1960) — the Mic-mode reading
                                      prompt rendered above the audio level
                                      meter (main-034). x:Static-bound from
                                      VoicesPage.xaml; no localisation in v1.
        NordwindUndSonne.cs           Static class holding the canonical first
                                      two sentences of Nordwind und Sonne
                                      (Aesop, gemeinfrei) plus its caption
                                      ("Lies bitte vor:") and attribution —
                                      the German Mic-mode reading prompt
                                      rendered when the language picker is
                                      set to German (main-042). x:Static-bound
                                      from VoicesPage.xaml; parallel to
                                      RainbowPassage, mutually exclusive via
                                      the cloning VM's visibility flags.
        VoicesPageConverters.cs       CloningSourceToBoolConverter for the cloning
                                      panel (main-025). NullOrEmptyToVisibilityConverter
                                      moved to Views/Converters/SharedConverters.cs in
                                      main-032 (registered as App.xaml resource).
        SettingsPageViewModel.cs      Settings page VM (main-016 / main-031 / main-032) —
                                      Voices / OutputDevices / SelectedDefaultVoice /
                                      SelectedOutputDevice / StartMinimised /
                                      LaunchAtStartup + diagnostics (HttpEndpoint /
                                      StopHotkeyLabel / DataPath, the last live-bound
                                      to DataPathService.DataPathChanged) +
                                      BrowseDataPath / ResetDataPath commands
                                      (folder-picker dialog, writability validation,
                                      restart-required MessageBox per main-031 / ADR 0020) +
                                      composed EngineStatus sub-VM
                                      (EngineStatusCardViewModel) for the relocated
                                      engine-status card + View logs link per main-032 /
                                      ADR 0021. Persists to UserSettings except
                                      LaunchAtStartup which writes HKCU\…\Run directly
                                      (ADR 0017). Attach() / Detach() drive both the
                                      data-path-changed subscription and the engine-status
                                      sub-VM's SidecarHost.StateChanged subscription.
        AboutPageViewModel.cs         About page VM (main-032 redesign) — collapsed to a
                                      single Version property sourced from AppInfo.Version.
                                      No commands, no sidecar subscriptions. The Ko-fi /
                                      GitHub / hyperlink handlers live in the page
                                      code-behind because the URLs are app-wide constants
                                      on AppInfo.
    Services\
      AppInfo.cs                      Static helper (main-030 / main-032) —
                                      `AppInfo.Version` returns the user-facing
                                      version string (`AssemblyInformationalVersionAttribute`
                                      with any `+sha` stripped, fallback to
                                      `AssemblyName.Version` 3-part, then `"unknown"`);
                                      `AppInfo.KofiUrl` = "https://ko-fi.com/heimeshoff"
                                      and `AppInfo.GithubUrl` =
                                      "https://github.com/heimeshoff/utterheim" are
                                      the support links the About page surfaces
                                      (main-032). Single-app, no DI seam.
      Navigation\PageService.cs       Thin IPageService → IServiceProvider adapter (ADR 0009)
      Tts\
        ITtsEngine.cs                 the seam every TTS engine plugs into
        StubTtsEngine.cs              440 Hz test tone (replaced by main-011)
        SidecarHost.cs                Owns python sidecar lifecycle; raises
                                      StateChanged for the footer VM. Spawns
                                      `python -m utterheim_sidecar serve`
                                      (per ADR 0015) — same uvicorn banner so
                                      port discovery is unchanged from pocket_tts.
                                      RestartAsync (main-017 / ADR 0018) is the
                                      user-initiated cycle: cancel supervisor +
                                      kill child + reset handles + StartAsync.
                                      JobObject preserved across restart.
        SidecarStateLabels.cs         Static Format(SidecarState) → friendly
                                      label (e.g. Running → "pocket-tts").
                                      Shared by EngineStatusViewModel (footer)
                                      and AboutPageViewModel so the two
                                      surfaces can't drift (main-017).
        PocketTtsEngine.cs            Real engine: built-in voices route /tts
                                      with voice_url; cloned voices resolve to
                                      profile.safetensors via VoiceLibraryService
                                      and route /tts-with-state (ADR 0015 / main-015).
        ProcessJobObject.cs           Win32 Job Object wrapper (ADR 0012) —
                                      KILL_ON_JOB_CLOSE keeps the python tree
                                      from outliving the host (main-022)
      Speak\
        SpeakRequest.cs               unit of work
        SpeakQueue.cs                 Channel<T> worker (ADR 0007, 0004) +
                                      RequestStarted / RequestCompleted events (main-013)
        AudioPlayer.cs                NAudio WaveOutEvent wrapper. Reads
                                      UserSettings.OutputDeviceId at WaveOutEvent
                                      construction time — once per utterance, so
                                      device changes apply to the next request only
                                      (main-016).
        VoiceCatalog.cs               Single source of truth for the voice list —
                                      shared by HTTP /voices and the Speak page
                                      picker (main-013, Q4). Composes engine
                                      built-ins ∪ VoiceLibraryService.ListClonedAsync
                                      (main-015) and re-fires VoicesChanged on
                                      every LibraryChanged.
        SpeakService.cs               In-process seam shared by HTTP /speak and the
                                      Speak page Play button. Owns request construction,
                                      surfaces the four-label status state-machine, and
                                      provides off-queue RenderToFileAsync for Save
                                      (main-013, Q2 / Q5 / Q6)
      Voices\
        ClonedVoiceMeta.cs            Schema records (ClonedVoiceMeta v1,
                                      ClonedVoiceIndexEntry, VoiceLibraryFile,
                                      VoiceSource enum, LibraryChangedArgs,
                                      VoiceValidationException) per ADR 0005 +
                                      main-015 schema ratification.
        VoiceLibraryService.cs        Owns <dataPath>\voices\* — temp+rename for every
                                      mutation (profile.safetensors → meta.json →
                                      library.json), id sanitisation + built-in
                                      collision rejection, startup reconciliation
                                      between library.json and on-disk folders,
                                      LibraryChanged event for the catalog.
        VoiceLibraryStartup.cs        Hosted-service shim: runs LoadAsync once on
                                      host start so the catalog has cloned rows
                                      before page VMs resolve. main-031: also
                                      subscribes to DataPathService.DataPathChanged
                                      and re-runs LoadAsync on each event so a
                                      runtime data-path swap repopulates the
                                      catalog live.
        VoiceCloningClient.cs         HTTP client wrapping the sidecar's POST
                                      /export-voice endpoint (ADR 0015) — uploads
                                      a sample WAV, receives .safetensors bytes
                                      that the C# host persists.
      Http\SpeakServer.cs             Kestrel minimal API on 127.0.0.1:7223 (ADR 0003).
                                      /speak and /voices route through SpeakService /
                                      VoiceCatalog (main-013).
      Audio\                            Mic + WASAPI loopback capture for voice
                                        cloning (main-025). Adapted from
                                        WhisperHeim @ 911bff0 per ADR 0006.
        IAudioCaptureService.cs       Mic capture interface (16 kHz mono 16-bit).
        AudioCaptureService.cs        NAudio WaveInEvent implementation.
        IHighQualityLoopbackService.cs WASAPI loopback interface (native format,
                                        no resampling — pocket-tts handles it).
                                        SaveAsVoice removed — persistence routes
                                        through VoiceLibraryService per ADR 0005.
        HighQualityLoopbackService.cs WasapiLoopbackCapture impl writing the
                                        captured audio to a temp WAV under
                                        %TEMP%\Utterheim\.
        AudioDeviceInfo.cs            Shared device-info record.
        AudioDeviceResolver.cs        WaveIn + WaveOut enumeration with Core Audio
                                        name resolution. EnumerateOutputDevices added
                                        for the Settings page picker (main-016).
        AudioRingBuffer.cs            Thread-safe lock-free ring buffer used by
                                        AudioCaptureService.
      Hotkey\
        NativeMethods.cs              copied from WhisperHeim @ 911bff0
        DoubleTapDetector.cs          utterheim-specific RCtrl gesture (ADR 0006;
                                      switched from LCtrl to RCtrl in main-033 for
                                      right-hand reachability on Marco's keyboard)
      Settings\
        DataPathService.cs            ADR 0005 path layout (adapted from WhisperHeim).
                                      main-031: ValidatePath + SetDataPath + DataPathChanged
                                      event for runtime data-path swap (pointer-only per
                                      ADR 0020); bootstrap.json writes via temp+rename.
        UserSettings.cs               Typed wrapper over %LOCALAPPDATA%\Utterheim\
                                      settings.json — DefaultVoiceId (main-013),
                                      OutputDeviceId, StartMinimised (main-016).
                                      Forward-compatible JSON shape; unknown fields
                                      ignored on read.
        StartupRegistration.cs        HKCU\…\Run\Utterheim helper (main-016) —
                                      IsRegistered / Register / Unregister. Registry
                                      is the source of truth for "Launch at startup";
                                      not stored in settings.json (ADR 0017).
    appsettings.json                  default port + hotkey window
    PythonSidecar\
      utterheim_sidecar\            utterheim-owned Python wrapper around pocket_tts
                                      (ADR 0015 / main-015). Bundled next to the .exe;
                                      bootstrapper copies into runtime\python\Lib\
                                      site-packages\utterheim_sidecar\ on first launch.
                                      Adds /export-voice and /tts-with-state to the
                                      pocket_tts FastAPI app while keeping its TTSModel
                                      resident across requests.
        __init__.py                   package marker
        __main__.py                   `python -m utterheim_sidecar` entry point
        main.py                       FastAPI route definitions + typer `serve`
                                      command (multi-language preload + the
                                      LanguageRoutingMiddleware that swaps
                                      pocket_tts.main.tts_model per request
                                      based on X-Voice-Language — main-039)
  Utterheim.Cli\                    utterheim-speak — single-file CLI wrapper
  Utterheim.Tests\                  xUnit test project (main-044) — net9.0-windows
                                      x64, ProjectReference → Utterheim. Smoke
                                      `[Fact]` only at infrastructure tier; real
                                      per-domain tests land starting with main-040.
                                      Run `dotnet test --configuration Release`
                                      from the repo root.
assets\branding\                      brand mark + raster outputs
  utterheim-logo.svg                source artwork — final, signed off 2026-05-05
                                      (main-028 draft 3): filled orange head profile
                                      + three blue Wi-Fi-style arcs from the mouth.
                                      Two-colour, not theme-adaptive.
  utterheim-logo-{16..512}.png      rasterised PNGs (rasteriser from main-012;
                                      regenerated from the final SVG by main-028)
  utterheim.ico                     multi-resolution tray + taskbar icon (final)
assets\people\                        portraits used by identity surfaces
  heimeshoff.jpg                      Marco's portrait (main-032) — copied verbatim
                                      from WhisperHeim's Assets/heimeshoff.jpg so the
                                      About profile card stays in sync across the two
                                      apps. Rendered as a 140x140 ringed ImageBrush.
Tools\RasteriseLogo\                  one-shot helper that produces the PNG/ICO
                                      from the source SVG. Outside utterheim.sln.
README.md
```

Path layout at runtime (per ADR 0005):

```
%APPDATA%\Utterheim\bootstrap.json    machine-local data-path pointer
%LOCALAPPDATA%\Utterheim\
  logs\utterheim-YYYYMMDD.log         Serilog rolling sink (ADR 0008)
  runtime\python\                       embeddable Python + pocket_tts +
                                        utterheim_sidecar (main-011 / main-015)
  models\pocket-tts\                    (main-011 will populate this)
  cache\
  bootstrap-state.json                  first-run completion marker
  settings.json                         UserSettings — DefaultVoiceId in v1 (main-013)
<dataPath>\voices\library.json          { schemaVersion: 1, voices: [...] }
                                        — id/name/engine/source/language/createdAt
                                        per row (main-015 + main-040)
<dataPath>\voices\<id>\                 per cloned voice (main-015)
  profile.safetensors                   exported voice state from /export-voice
  meta.json                             schemaVersion 1 + sampleSeconds + tags
                                        + language (main-040)
  sample.wav                            optional retained sample
```

## UI shell

As of main-020 the tray window hosts a **wpfui `NavigationView`** with the
canonical four-page set (Speak / Voices / Settings / About) per ADR 0009.
main-032 split the placement: Speak / Voices / Settings sit in
`MenuItems` (the regular sidebar list); About sits in `FooterMenuItems`
(bottom-left of the nav pane), mirroring WhisperHeim and visually
separating the everyday workflow from the identity / credits surface.
main-033 brought the chrome itself in line with WhisperHeim: the sidebar
header now renders the Speak-page brand mark (`utterheim-logo-256.png`
via the same `Image` source the `BrandHeroControl` uses) beside a
**16pt Bold** "Utterheim" wordmark, and the four nav items use the
WhisperHeim list typography (uppercased label, `FontSize=11`,
`FontWeight=Medium`, applied via the `NavItemStyle` resource). The
inline-drawn portrait that previously sat next to the wordmark is gone.
Pages are real WPF `Page` subclasses, registered transient in DI, and
implement both `Wpf.Ui.Controls.INavigableView<TViewModel>` (typed VM
accessor) and `Wpf.Ui.Controls.INavigationAware` (`OnNavigatedTo` /
`OnNavigatedFrom` lifecycle hooks). A thin `PageService` delegates wpfui's
`IPageService` calls to the host's `IServiceProvider`. View-models use the
`CommunityToolkit.Mvvm` source-generator attributes (`[ObservableProperty]`,
`[RelayCommand]`) per ADR 0010 — no hand-rolled `ObservableBase` /
`RelayCommand` helpers.

A persistent **status footer** below the navigation area shows
`HTTP {host}:{port}  •  Engine: {state}`. Backed by
`EngineStatusViewModel`; the engine label updates live as
`SidecarHost.StateChanged` fires (`starting` → `running` → `restarting` →
`failed` → `stopping`). When the stub engine is selected
(`UTTERHEIM_USE_STUB_ENGINE=1`) the label reads `stub` and never changes.
The four feature pages (main-013/014/016/017) replace each stub with real
content. Per main-017 Q2 the persistent footer **stays** alongside the
richer status panel — main-032 / ADR 0021 relocated the panel from About
to the end of Settings → Diagnostics, but the dual surface invariant is
unchanged: both consume the same `SidecarHost.StateChanged` event via
`SidecarStateLabels.Format` so they cannot drift. Revisit if the dual
surface feels redundant after live use.

## Engine status

As of main-011 the **real pocket-tts engine is wired in**:

- `PocketTtsEngine` posts text to the Python sidecar and streams 24 kHz mono 16-bit
  PCM chunks (after stripping the WAV header) straight to `AudioPlayer` via the
  existing `IAsyncEnumerable<byte[]>` path. **Built-in voices** route to `POST /tts`
  with form-encoded `voice_url`; **cloned voices** (main-015) resolve to
  `<dataPath>\voices\<id>\profile.safetensors` via `VoiceLibraryService` and route
  to `POST /tts-with-state` with the profile uploaded as `voice_state`. Both calls
  use `HttpCompletionOption.ResponseHeadersRead` per ADR 0013 — the default
  `ResponseContentRead` would buffer the entire WAV, scaling first-chunk latency
  linearly with input size and breaking the ≤2 s budget on anything beyond a short
  sentence. Every outgoing request also carries the `X-Voice-Language` header
  (`english` / `german`) so the multi-model sidecar can pick the matching
  resident `TTSModel` (main-039 / ADR 0023). Built-in voices source the language
  from `BuiltInVoices`; cloned voices source it via `VoiceLibraryService.TryResolveLanguage`.
  The Claude-Code-facing `POST /speak` contract (ADR 0003) is unchanged — the
  language header lives only on the C# host-to-sidecar-internal hop. `BuildSpeakRequest`
  is the internal seam tested by `Utterheim.Tests`.
- `SidecarHost` owns the `python.exe -m utterheim_sidecar serve --host 127.0.0.1
  --port 0 --language english --language german` process (per ADR 0015 — the wrapper
  module imports pocket_tts's FastAPI app and mounts `/export-voice` +
  `/tts-with-state` so cloning + cloned-voice synthesis share the same resident
  `tts_model`; per ADR 0024 the `--language` flag is repeated to preload both
  English and German models concurrently into `_RESIDENT_MODELS`): parses the
  assigned port from
  Uvicorn's startup banner (unchanged regex — uvicorn logs the same line), polls
  `/health`, redirects stdout/stderr into Serilog under a `sidecar` source, terminates
  on host shutdown, and restarts on crash with capped exponential backoff (5 attempts).
  Per ADR 0012, every spawned python is bound to a Win32 Job Object with
  `KILL_ON_JOB_CLOSE`, so the entire process tree (uvicorn workers, multiprocessing
  spawn, etc.) dies atomically on tray Exit — *and* on abrupt host death — with no
  zombie `python.exe` left in Task Manager. A `_shuttingDown` flag suppresses the
  auto-restart loop during host shutdown so the supervisor cannot respawn the
  python we just killed.
- `PythonRuntimeBootstrapper` runs once on first launch: downloads Python 3.12.7
  embeddable to `%LOCALAPPDATA%\Utterheim\runtime\python\`, enables `site` in the
  `._pth` file, bootstraps pip, pip-installs `pocket-tts>=2.0,<3` (which pulls torch
  CPU plus deps, ~600 MB), copies the bundled `utterheim_sidecar` wrapper from the
  install folder into `runtime\python\Lib\site-packages\utterheim_sidecar\`
  (main-015), and smoke-tests both imports. Progress is persisted to
  `bootstrap-state.json` so a half-finished run resumes on restart. Per ADR 0011,
  on-disk sentinel files (`python.exe`, `pip`, `pocket_tts/__init__.py`,
  `utterheim_sidecar/__init__.py` + `utterheim_sidecar/main.py`) are
  authoritative — a stale state file cannot trick the bootstrapper into skipping a
  step whose artefacts have been wiped — and any subprocess that exits non-zero
  surfaces its captured stderr tail in both the file log (at `Error`) and the
  thrown exception (visible in the `BootstrapDialog`). Per ADR 0016 / main-027,
  the launch-time gate (`IsBootstrapped`) is **strict and version-aware**: it
  delegates to the same helpers the install path uses (`PocketTtsActuallyInstalled`
  / `UtterheimSidecarActuallyInstalled`) and additionally compares the bundled
  `utterheim_sidecar/__init__.py`'s `__version__` against the installed copy's
  `__version__`. Any missing wrapper file or version mismatch returns false →
  bootstrap dialog opens → install step re-runs and `File.Copy(overwrite: true)`
  heals the tree. Bumping `__version__` in the bundled `__init__.py` is therefore
  the wrapper's update mechanism — no separate migration code path.
- `BootstrapDialog` drives the bootstrapper with per-step progress, cancel, and retry.
- `StubTtsEngine` is preserved behind `UTTERHEIM_USE_STUB_ENGINE=1` for offline /
  CI testing; the env flag also disables the sidecar and bootstrap-dialog wiring.
- `GET /voices` returns the union of the pocket-tts built-ins (`alba`, `marius`,
  `javert`, `jean`, `fantine`, `cosette`, `eponine`, `azelma` — all English — plus
  `juergen` for German, added by main-040) with `isBuiltIn: true` and a `language`
  attribute per ADR 0023, plus every cloned voice from `library.json` with
  `isBuiltIn: false`. The union is
  composed by `VoiceCatalog` (main-013) which now consumes `VoiceLibraryService`
  (main-015) alongside the engine. Voice cloning UI lands in main-025; the per-row
  delete affordance ships with main-026.
- `GET /status` reports `sidecar.state` (notstarted / starting / running / restarting
  / failed / stopping), `sidecar.healthy`, `sidecar.port`, and `sidecar.lastError`.
- **Stop cancellation propagation (main-046, sidecar 1.3.0, always on).** Every
  `serve` startup patches `pocket_tts.models.tts_model.TTSModel._autoregressive_generation`
  with a stop-event-aware reimplementation that inlines the for-loop body from
  pocket-tts 2.x's `tts_model.py:744-779` with an explicit
  `if stop_event.is_set(): break` between the per-step inference call and the
  `latents_queue.put`. Both `/tts` (wrapped at `LanguageRoutingMiddleware`) and
  `/tts-with-state` (wrapped at the handler) poll `request.is_disconnected()`
  every ~200 ms; on disconnect the per-model `threading.Event` is set and the
  patched method breaks out within one inference step, pushing the `None`
  sentinel to the `latents_queue` positional arg so the decoder thread unwinds
  cleanly. ADR 0026 budget: CPU <5 % within ≤2 s; median per-cycle RSS delta
  ≤25 MB across 50-cycle stress. The startup sanity check (signature
  introspection + patch-identity assertion) runs every boot and refuses to
  start on a pocket-tts internals mismatch. The opt-in `UTTERHEIM_CANCEL_PROTOTYPE`
  env var from sidecar 1.2.x is gone — cancellation is unconditional in 1.3.0.
  Direct method-body replacement was chosen over `sys.settrace` (main-045's
  prototype mechanism) to eliminate the ~10-20 MB/cycle frame-retention residual
  the trace-callback machinery contributed. See ADR 0027 § "Mechanism" for the
  patched method body and § "Startup sanity check" for the refuse-to-start
  guards.

The "stub-engine plays a 440 Hz tone" note from the skeleton is now superseded.

## Speak page

As of main-013 the Speak page replaces the main-020 stub with the real daily-use
surface — the page mirrors WhisperHeim's TTS section A. main-030 reordered the
rows: the page now opens with `<controls:BrandHeroControl />` (no tagline) at
the top, followed by a single horizontal controls row (voice picker + Play +
Stop + Save in WhisperHeim TTS order, no separator gap), then the dominant
multi-line `ui:TextBox` filling the remaining vertical space, then the status
`TextBlock` at the bottom. The page chrome matches the styleguide
(40,36,40,32 outer margin, MaxWidth=900, centered); the page root is a
4-row `Grid` (not a `ScrollViewer`) so the `*` row can actually expand and
the textbox dominates.

- A four-row layout per main-030: hero (`controls:BrandHeroControl` with
  no `Tagline`), controls row (voice `ComboBox` MinWidth=240 + Play
  Primary + Stop Secondary + Save Secondary, all in one `StackPanel`),
  dominant multi-line `ui:TextBox` (`AcceptsReturn`, `MinHeight=240`,
  internal scrollbar) filling the `*` row, and a status `TextBlock` under
  it. Save retains its inline `ui:ProgressRing` driven by the command's
  `IsRunning`.
- View-model `SpeakPageViewModel` (`CommunityToolkit.Mvvm`,
  `[ObservableProperty]` + `[RelayCommand]` per ADR 0010). `Play`'s and
  `Save`'s `CanExecute` reactivities come from
  `[NotifyCanExecuteChangedFor]` on `Text` and `SelectedVoice`. Save uses
  `[RelayCommand]` async, which yields `IsRunning` for the inline
  `ui:ProgressRing`.
- The page implements both `INavigableView<SpeakPageViewModel>` (typed VM) and
  `INavigationAware` (`OnNavigatedTo` / `OnNavigatedFrom`); on navigated-to it
  refreshes voices, applies the latest `SpeakStatus`, and subscribes to
  `SpeakService.StatusChanged` + `VoiceCatalog.VoicesChanged`. On
  navigated-from it unsubscribes and cancels in-flight refreshes.

### In-process seams (Q2 + Q4)

Both the HTTP API and the page are routed through the same singletons so
adding cloned voices (main-015) or augmenting request construction lands in
both surfaces in one place:

- `VoiceCatalog.ListAsync(ct)` is the single source of truth for the voice
  list. v1 delegates to `ITtsEngine.ListVoicesAsync` and fires
  `VoicesChanged` once on first population. main-015 will fold cloned voices
  into the same return.
- `SpeakService.Enqueue(text, voiceId)` constructs the `SpeakRequest`
  (Guid, voice-id fallback, validation) and calls `SpeakQueue.Enqueue`.
  `SpeakService.StopAndDrain()` is a thin pass-through to the queue.
  `SpeakService.RenderToFileAsync(...)` is the off-queue Save path: it
  streams `ITtsEngine` output directly into a `NAudio.Wave.WaveFileWriter`
  and never touches `SpeakQueue` or `AudioPlayer` — Save can never interrupt
  a live playback request.
- `SpeakServer.MapPost("/speak", ...)` calls `SpeakService.Enqueue`,
  `MapPost("/stop", ...)` calls `SpeakService.StopAndDrain`,
  `MapGet("/voices", ...)` calls `VoiceCatalog.ListAsync`.

### Status state-machine (Q6)

`SpeakService` exposes a `StatusChanged` event carrying a `SpeakStatus`
record `(Kind, VoiceId)` with four `Kind` values:

- `Idle` — no `_current` request, queue empty.
- `Synthesising` — `RequestStarted` fired, no PCM has hit the device yet.
- `Playing` — `AudioPlayer.IsPlaying` flipped to true (poll loop, 100 ms).
- `Stopped` — transient label after `StopAndDrain`; auto-clears to `Idle`
  after 2000 ms via a Task.Delay timer (cancelled / replaced if a new
  Stop arrives in the window).

The label format is `{kind}` for `idle` / `stopped`, `{kind} — {voiceId}`
for `synthesising` / `playing`. `synthesising` may be invisibly brief
because pocket-tts streams chunks with ~200 ms first-chunk latency — the
poll catches `Playing` quickly. `RequestStarted` and `RequestCompleted`
were added on `SpeakQueue` to drive the machine.

### UserSettings (Q7)

The voice picker pre-selects `UserSettings.DefaultVoiceId` if it is set and
present in the catalog; otherwise it falls back to the alphabetically-first
voice (`alba` for the eight pocket-tts built-ins). `UserSettings` reads /
writes `%LOCALAPPDATA%\Utterheim\settings.json` with an atomic
temp+replace; unknown JSON fields are ignored so main-016 can extend the
schema without breaking v1 reads. v1 ships only the storage layer — UI to
mutate the default voice is main-016's responsibility. The HTTP `/speak`
endpoint is unaffected (callers always specify a voice explicitly).

### Verification note

The build is clean (`dotnet build utterheim.sln -c Debug` → 0 errors,
0 warnings). The interactive UI behaviours — textbox dominates the layout,
SaveFileDialog opens with the correct filter, Stop drains the queue,
status line transitions visibly, picker refreshes on navigation — are
**not interactively re-tested** in this pass; the code is in place per
the main-013 spec and any regression will surface during the next
manual run.

## Voices page

As of main-014 the Voices page replaces the main-020 stub with the real voice
library surface — a read-only list shell that main-015 will fold cloned
voices into without touching this page's code:

- A two-row Grid with a 16 px outer margin: a header (`FontWeight="Light"` page
  title plus a small `{TotalCount} voices` sub-label) and a scrollable list
  underneath. Inside the `ScrollViewer`, two grouped sections — **Cloned**
  first, **Built-in** second (main-033 swap; cloned voices are the user's
  own work and the differentiator, so they lead) — both with `SemiBold`
  section headers. Each row layout (main-041): **name + meta** (engine /
  source) | **language chip** (small `Border`, `CornerRadius=8`, subtle-fill,
  carrying the two-letter ISO 639-1 code — `EN` or `DE` — from
  `VoiceRowViewModel.LanguageLabel`) | **Preview** button (`Play24` icon,
  `Appearance="Secondary"`) | (cloned only, main-026) **Delete** button |
  **active-request indicator** (`Speaker224` symbol, hidden when idle).
  The chip surfaces a voice's language at a glance per ADR 0023 — it's how
  the user tells `juergen` (DE) apart from `alba` (EN).
- View-model `VoicesPageViewModel` (`CommunityToolkit.Mvvm`,
  `[ObservableProperty]` per ADR 0010) exposes two
  `ObservableCollection<VoiceRowViewModel>`s (`BuiltInVoices`, `ClonedVoices`),
  populated by partitioning `VoiceCatalog.ListAsync()`'s result on
  `IsBuiltIn`. The page **does not read `library.json`** — main-015 owns
  that layer end-to-end and feeds the catalog. `ClonedSectionIsEmpty`
  drives the inline empty-state message (`"No cloned voices yet — see
  main-015 to clone your own."`) that ships in v1.
- The page implements both `INavigableView<VoicesPageViewModel>` and
  `INavigationAware`. On navigated-to it seeds the engine state from
  `SidecarHost.GetStatus()`, applies the current `SpeakStatus`, refreshes
  voices, and subscribes to `VoiceCatalog.VoicesChanged`,
  `SpeakService.StatusChanged`, and `SidecarHost.StateChanged`. On
  navigated-from it unsubscribes and cancels in-flight refreshes.

### Preview routes through the speak queue (ADR 0014)

Per-row Preview calls `SpeakService.Enqueue($"Hello, this is {DisplayName}.",
voiceId)` — the same in-process seam main-013 wired up for the Speak page's
Play button and that `POST /speak` already uses. **Not** a direct
`ITtsEngine.StreamAsync`, **not** a side `AudioPlayer`. ADR 0014 governs;
the rationale is the queue's single-arbiter invariant: a direct off-queue
path would race Claude-driven utterances for the output device and break
the stop-hotkey contract (ADR 0004).

Behavioural consequences inherited for free:

- Click Preview while another preview / Claude utterance is in flight →
  enqueues behind it (FIFO per ADR 0007, no barge in v1).
- Double-tap RCtrl during a preview → drains the queue per ADR 0004,
  identical to tray Stop and `POST /stop`. The row's active-request
  indicator flips off; status footer briefly shows `stopped` then `idle`.
- First-chunk latency = full queue latency (~190 ms warm for the canned
  phrase, per ADR 0013). Lanes for preview-bargeing are deferred to v1.5.

### Engine-state visibility

The page binds three coarse view-states to `SidecarHost.State`:

- `starting` / `restarting` / `notstarted` → centred `ui:ProgressRing` +
  "Voice engine is starting..." in place of the list. Per-row Preview
  buttons would be disabled anyway (no rows).
- `failed` → an inline error banner using `SystemFillColorCriticalBackgroundBrush`:
  "Voice engine failed to start. See the About page for details and retry."
  (main-017 will surface the richer panel and a retry control.)
- `running` → the normal two-section list. Preview buttons enabled per row.

`VoicesPageViewModel.ApplyEngineState(...)` flips both the placeholder
visibility flags (`IsLoading` / `IsFailed` / `IsRunning`) and each row's
`CanPreview` (which guards the Preview command) in one shot.

### Per-row active-request indicator

A page-VM-level subscription to `SpeakService.StatusChanged` finds the row
whose `VoiceId` matches the active request and toggles its `IsActiveRequest`
flag. When status flips to `Idle` / `Stopped`, all rows return to inactive.
The dispatcher hop matches the pattern in `EngineStatusViewModel`.

### Refresh behaviour

`OnNavigatedTo` and `VoiceCatalog.VoicesChanged` both call
`RefreshVoicesAsync` (re-entrancy-guarded so a `VoicesChanged` event
arriving mid-`OnNavigatedTo` doesn't double-refresh). When the engine
transitions into `running` from a starting / restarting state mid-page-life,
the code-behind also forces a refresh so the list populates without
re-navigation. **No `library.json` file watcher** — the catalog is the
single source of truth (main-015 will fire `VoicesChanged` on save / delete).

### Cloning panel (main-025, relocated by main-030)

As of main-025 the Voices page grows a **Clone a new voice** sub-panel; main-030
moved it to sit **above** the voice library list (clone first, browse second)
and re-skinned its outer container as a single WhisperHeim card (`Border`,
`CornerRadius=12`, `Padding=24` per styleguide §Card spec) with a `SemiBold`
heading on the card itself. The panel is always visible while the engine is
running (no collapse / expand in v1). Its view-model is
`VoiceCloningViewModel` — composed into `VoicesPageViewModel.Cloning`,
**not** a separate page.

- **Language picker** (main-041 / ADR 0023) — a `ComboBox` above the source
  toggle binds to `Cloning.Languages` (`[English, German]`) and two-way
  binds the selection into `Cloning.Language`. English is the default
  selection per Marco's product call ("default will always be English").
  The chosen language flows into both ends of the Save flow: the
  `/export-voice` request carries `X-Voice-Language: english|german` so the
  sidecar swaps to the matching resident `TTSModel` before encoding the
  audio prompt (main-041 extended `_route_paths_needing_model` to include
  `/export-voice`), and `VoiceLibraryService.AddAsync(..., language: ...)`
  persists the chosen language on the voice's `meta.json` and the
  `library.json` index row.
- **Source toggle** — two `RadioButton`s side by side: Microphone (`Mic24`)
  and System Audio (`Speaker224`). Two-way bound through
  `CloningSourceToBoolConverter` so the radios stay in sync with
  `Cloning.SelectedSource`. Flipping the toggle swaps the device selector
  and the tip line; the recording controls beneath are identical.
- **Mic mode** uses `IAudioCaptureService` (16 kHz mono 16-bit PCM —
  pocket-tts resamples internally per main-015 Q6, so no client-side
  resampling). Default device is `-1` (system default, NAudio maps to 0).
- **System Audio mode** uses `IHighQualityLoopbackService` (WASAPI loopback
  at the native render-endpoint format, typically 48 kHz IEEE-float stereo).
  Capture writes a temp WAV under `%TEMP%\Utterheim\` for the duration of
  the session; the path is consumed by Save and deleted on success.
- **Reading-prompt blocks** (main-034 + main-041 + main-042) — Mic mode
  only: two parallel subtle-fill `Border` blocks
  (`SubtleFillColorSecondaryBrush`, 1-pt `ControlStrokeColorDefault`,
  4-pt corner) sit between the mic tip and the audio level meter, one per
  language. Exactly one is visible at a time (or none, in System Audio
  mode):
  - **English** — Rainbow Passage (main-034). "Read this aloud:" heading,
    the canonical first two sentences of the Rainbow Passage (Fairbanks
    1960, *Voice and Articulation Drillbook* — public domain), captioned
    "The Rainbow Passage — University of York". Text is a `const string`
    on `RainbowPassage` (`ViewModels/Pages/RainbowPassage.cs`) bound via
    `{x:Static}`. Visibility is keyed off
    `Cloning.IsRainbowPassageVisible` (`IsMicMode && Language == English`).
  - **German** — Nordwind und Sonne (main-042). "Lies bitte vor:" heading
    (informal du-form — load-bearing convention for all later German UI
    copy), the canonical first two sentences of Nordwind und Sonne (Aesop,
    gemeinfrei), captioned "Nordwind und Sonne (Aesop, gemeinfrei)". Text,
    caption, and attribution are `const string`s on `NordwindUndSonne`
    (`ViewModels/Pages/NordwindUndSonne.cs`) bound via `{x:Static}`.
    Visibility is keyed off `Cloning.IsGermanReadingPromptVisible`
    (`IsMicMode && Language == German`).
  No view-model property holds the passage text — the prompts are static
  per-language constants with no localisation framework. Both blocks are
  display-only: no auto-scroll, no current-sentence highlight, no
  interactive elements.
- **Recording controls** (shared, identical in both modes per styleguide
  Reusable component map):
  - Audio level meter — horizontal `ProgressBar` driven by the capture
    service's RMS event, dispatched at ~30 Hz.
  - Duration display — `mm:ss` `TextBlock`, updated by a
    `DispatcherTimer` ticking every 33 ms.
  - Progress bar — fills 0..100% across the first 5 s; stays at 100%
    after that (visible "you have enough sample" cue).
  - Voice name input — `MaxLength=40`, validated on every keystroke
    against the same rules as `VoiceLibraryService.AddAsync` (empty,
    >40 chars, sanitised collision with one of the built-in ids — the eight
    English + `juergen` as of main-040).
    Inline error under the input.
  - Start / Stop / Cancel / Save Voice buttons. Cancel is only visible
    while capturing; Save is enabled only when **all four** of (capture
    not running) AND (≥5 s captured) AND (name valid) AND (no save in
    flight) hold.
- **Sample length policy**: minimum 5 s (Save disabled until reached),
  soft cap 30 s (status message "You have plenty of audio. You can stop
  now." — capture continues), hard cap 60 s (auto-stop with status
  "Capture auto-stopped at 60 s.").
- **Stop before 5 s** — buffer discarded, status "Recording too short —
  at least 5 s needed." **Cancel** at any point — buffer discarded
  unconditionally.
- **Save flow** (in `SaveAsync`):
  1. Re-validate name client-side (mirror of `VoiceLibraryService` rules).
  2. Render to a temp WAV — mic mode writes float→16-bit PCM to a fresh
     file under `%TEMP%\Utterheim\`; loopback mode reuses the file the
     capture service already wrote.
  3. Quiet-buffer guard — peak RMS < 0.01 short-circuits with
     "Recording was very quiet. Try again closer to the mic." (no POST).
  4. POST to `/export-voice` via `VoiceCloningClient` (per ADR 0015).
     4xx surfaces as "Pocket-tts couldn't read the recording. …" with the
     response body in dim detail text; 5xx / network as "Voice profile
     encoding failed. See the engine status footer or About page."
  5. Hand the `.safetensors` bytes (and the WAV bytes) to
     `VoiceLibraryService.AddAsync` so the sample lands at
     `<dataPath>\voices\<id>\sample.wav` per ADR 0005.
  6. `LibraryChanged` → `VoicesChanged` → main-014's existing subscription
     refreshes the rows; the new voice appears in the **Cloned** section
     **without page re-navigation**. Form clears, status reads
     "Voice 'Marco' saved."
- **Failure UX** — every error type renders inline above the Save button
  (no toasts, no modals). The captured buffer is **preserved** through
  every failure type; only an explicit Cancel or a successful Save
  clears it, so the user can fix the cause and re-Save without
  re-recording.

The audio-capture services (`IAudioCaptureService`,
`IHighQualityLoopbackService`) are registered **transient** in DI — each
cloning session gets a fresh capture instance.

### Per-row delete affordance (main-026)

Cloned voice rows ship with a per-row Delete button between Preview and the
active-request indicator (built-in rows keep the 3-column layout — Delete is
never offered for the eight pocket-tts built-ins). The button is icon-only
(`Delete24`, `Appearance="Secondary"`, `ToolTip="Delete voice"`), always
visible (not hover-only) per WhisperHeim's consistency rule — the user always
knows the affordance exists.

Click → Fluent **`ui:ContentDialog`** opens via `IContentDialogService`
(registered singleton in DI; MainWindow binds the host `ContentPresenter`
on Loaded). Visual composition matches WhisperHeim's
`DeleteConfirmationDialog`: 40×40 rounded red-tinted icon block (`#20E81224`
background, `#FFE81224` icon), "Delete voice?" title (`Bold`, 16pt), "This
action cannot be undone." subtitle, voice-name card, right-aligned Cancel
(`Secondary`) + Delete (solid `#FFE81224`, white text) buttons. The
ContentDialog `IsFooterVisible="False"` — the dialog body owns its own
button row so we can style the destructive button as red. Cancel / Esc /
click-outside dismiss with no action.

On confirm: `DeleteVoiceDialogViewModel.DeleteAsync` calls
`VoiceLibraryService.DeleteAsync(voiceId, ct)`. On success the dialog hides
itself and the row vanishes via the `LibraryChanged` → `VoicesChanged`
chain that main-014 already wires. On `IOException` (file lock, sidecar
still mmap'ing the `.safetensors` from a just-finished preview) the dialog
**stays open** with an inline red error: "File is locked. Stop playback
and try again." `UnauthorizedAccessException` surfaces the analogous
permission message; any other exception falls through to its `.Message`.
The user can retry Delete or hit Cancel — the dialog is sticky on failure
by design (per task spec, no toast, no auto-dismiss).

Per main-015's delete ordering, `library.json` is pruned **before** the
folder is deleted, so a file-locked folder still removes the row from the
catalog; the orphan folder is cleaned up by `VoiceLibraryStartup`'s
reconciliation on the next launch. Active-playback during delete is **not**
guarded — the synthesis request continues; if the sidecar errors mid-stream
on a now-missing `.safetensors`, the existing `SpeakService` error path
flips the row indicator off and surfaces the error in the status footer.

The page VM's `RequestDelete` constructs a fresh
`DeleteVoiceDialogViewModel` per click (cheap; no shared state to leak),
attaches the dialog instance via `AttachDialog`, and `await`s
`IContentDialogService.ShowAsync`. The Delete column is only allocated on
cloned-row templates (separate `ItemsControl.ItemTemplate` from the
built-in section), so built-in rows keep their original 3-column layout
unmodified.

### Out-of-scope reminders for v1

- No bulk delete / multi-select — single-row only in v1 (main-026).
- No undo — modal confirm is the only safety net.
- No search / filter / tags — vision-deferred until ~15 voices.
- No per-session voice routing UI — env-var-only per main-019.
- No per-row preview error toasts — footer + log file is sufficient signal.
- No "import existing clip from disk" cloning source — backend supports it,
  no UI in v1 (deferred per main-015 Q11).
- No editing of voice metadata (rename, retag), no per-voice quality /
  temperature controls, no de-duplication, no marketplace / sharing.

## Settings page

As of main-016 the Settings page replaces the main-020 stub with three sections
of Fluent setting cards — Audio, App, Diagnostics — laid out vertically inside
a `ScrollViewer`. WhisperHeim-style: each card = label + one-line description +
control on the right. Mica backdrop, Segoe UI Variable. main-029 added a fourth
section, **Appearance**, with a Light / Dark / System tile picker that swaps the
active theme live and persists to `settings.json` (per ADR 0019). main-029 also
re-skinned every existing card from `ui:CardControl` to the WhisperHeim
`Border CornerRadius=12 Padding=24` pattern documented in the styleguide
(§Card spec / §Section header / §Page chrome).

main-033 then re-ordered the page so user-facing cards lead and technical /
diagnostic cards sink to the bottom. Card order top-to-bottom: **Default voice
→ Output device → Data path → Appearance → Start minimised → Launch at startup
→ HTTP port → Stop hotkey → Engine status → View logs**. Data path is no
longer rendered under the Diagnostics header (it is its own free-standing
card between Audio and Appearance); HTTP port + Stop hotkey + Engine status
+ View logs remain inside Diagnostics, now at the foot of the page.

### Audio

main-033 reflowed both Audio cards from the right-aligned label/control
DockPanel pattern to the styleguide's stacked-content composition (label +
description on top, full-width control below) so they match WhisperHeim's
Settings page.

- **Default voice** — `ComboBox` sourced from `VoiceCatalog.ListAsync()`
  (built-ins first, cloned voices second). Selection writes through
  `UserSettings.DefaultVoiceId`. Empty selection = "no preference"
  (alphabetical fallback per main-013, Q7).
- **Output device** — `ComboBox` of WaveOut devices enumerated via
  `AudioDeviceResolver.EnumerateOutputDevices()`, with a "System default"
  sentinel (`DeviceIndex = -1`) at the top. Selection persists as
  `UserSettings.OutputDeviceId` (`int?`, `null` = system default mapped to
  `WaveOutEvent.DeviceNumber = -1`). **Applies to the next utterance only** —
  `AudioPlayer` reads the active device id at `WaveOutEvent` construction
  time, so current playback continues on the previous device and there are
  no live-switch invariants to manage. Inline tip text: *"Takes effect on
  the next speech request."*

### App

- **Start minimised** — `ui:ToggleSwitch`, persists as
  `UserSettings.StartMinimised` (default false). Honoured by `EntryPoint`
  on initial launch: when true, the window is shown and immediately hidden
  so the tray:NotifyIcon initialises but the user starts in the tray
  instead of an activated window. The HTTP / hotkey / sidecar surfaces
  are independent of window visibility.
- **Launch at startup** — `ui:ToggleSwitch`, persists by writing /
  removing the `Utterheim` value under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` via
  `StartupRegistration`. **Not stored in `settings.json`** — the registry
  is the source of truth (ADR 0017), so an external uninstaller / cleanup
  tool that drops the Run entry stays reflected accurately on the next
  page load. Toggle reads the registry on every `OnNavigatedTo`, writes /
  deletes on change.

### Diagnostics (read-only in v1)

- **HTTP port** — displays `{SpeakServer.Host}:{SpeakServer.Port}` as
  monospaced text. Read-only; editing with auto-restart is **out of
  scope** for v1 (deferred to a future refinement if a port collision
  surfaces).
- **Stop hotkey** — displays "Double-tap Right Ctrl". Read-only per
  ADR 0006 (no rebinding UI in v1). main-033 switched the watched key
  from Left Ctrl to Right Ctrl.
- **Data path** — displays the active path from `DataPathService.DataPath`
  (which reads `bootstrap.json` per ADR 0005). Editable as of main-031:
  a `Browse...` button opens `Microsoft.Win32.OpenFolderDialog`, the
  selection is writability-validated (`DataPathService.ValidatePath`)
  before the pointer in `bootstrap.json` is flipped via temp+rename, and
  a MessageBox info reminds the user to restart for full effect. A
  `Reset` button (no MessageBox) clears the override back to
  `%APPDATA%\Utterheim\`. **Pointer-swap only** — old voices at the
  previous path are not moved, copied, or deleted (ADR 0020). Only
  `<dataPath>\voices\` follows the swap; `runtime/`, `models/`,
  `cache/`, `logs/`, `bootstrap-state.json`, `settings.json` stay
  anchored to `LocalRoot`. `VoiceLibraryService` re-runs `LoadAsync` on
  `DataPathChanged` so the Voices page rows reflect the new path's
  `library.json` live, without page re-navigation. The previous v1
  `Open in Explorer` affordance was dropped with this change.
- **Engine status** (relocated from About by main-032 / ADR 0021) —
  state pip + label, port, healthy indicator (only while Running),
  last-error block, Restart Engine button. Bound through
  `SettingsPageViewModel.EngineStatus` (an `EngineStatusCardViewModel`
  composed sub-VM mirroring the cloning-panel pattern). Subscribes to
  `SidecarHost.StateChanged` on `SettingsPage.OnNavigatedTo` →
  `EngineStatus.Attach()`; unsubscribes on `OnNavigatedFrom` →
  `EngineStatus.Detach()`. `Restart Engine` calls
  `SidecarHost.RestartAsync`; disabled mid-transition.
- **View logs** — `ui:HyperlinkButton` underneath the engine card, opens
  `%LOCALAPPDATA%\Utterheim\logs\` in Explorer (falls back to
  `LocalRoot` if the directory hasn't been created yet). Bound to
  `EngineStatus.OpenLogsCommand`.

### Refresh behaviour

`OnNavigatedTo` calls `SettingsPageViewModel.LoadAsync` which: refreshes
the voice list from the catalog, re-enumerates WaveOut devices,
re-applies `UserSettings.StartMinimised`, and re-reads the registry for
`LaunchAtStartup`. A `_suspendPersist` guard around the load prevents
the auto-generated `OnXChanged` partial methods from firing
spurious writes during the population pass. `OnNavigatedFrom` cancels
the in-flight load CTS.

### Appearance (main-029)

A fourth section sits at the bottom of Settings: a single card containing a
`UniformGrid Columns="3"` of Light / Dark / System tiles. Tile clicks call
`Wpf.Ui.Appearance.ApplicationThemeManager.Apply(...)` /
`ApplySystemTheme()` for an immediate live swap (no app restart) and write
through `UserSettings.AppearanceMode`, which persists the JSON string to
`settings.json` per ADR 0019. The active tile gets a 10% blue tint
(`#19005FAA`) over transparent; the others stay transparent.

Default for installs whose `settings.json` has no `appearanceMode` field is
`Light`, applied **in memory only** — the file is not rewritten on read.
Startup (`EntryPoint`) calls the same `ApplicationThemeManager` helper once
before `MainWindow.Show()` so the persisted preference paints on first
launch with no Light → Dark flicker.

### Out of scope for v1

Captured in main-016's spec — repeated here so future tasks see the
boundary: HTTP port editing with auto-restart, stop-hotkey rebinding UI,
language / update-check toggles, per-Claude-session voice routing UI
(env-var-only via `examples/claude-hooks/`), output-device level meter /
test-tone button, engine status panel (main-017's About page owns it).
The theme toggle that was on this list is **lifted as of main-029** —
see §Appearance above. The data-path change-with-migration entry is
**lifted as of main-031** — see §Diagnostics above; pointer-swap only
per ADR 0020, no migration ever in v1.

## About page

As of main-032 the About page is a **pure identity / credits surface** that
mirrors WhisperHeim's About section-for-section. Engine diagnostics
(state pip, port, healthy indicator, last-error block, Restart Engine,
View logs) moved to Settings → Diagnostics per ADR 0021. About also moved
to `ui:NavigationView.FooterMenuItems` so it sits at the bottom-left of
the nav pane like WhisperHeim, separating "everyday workflow" from
"about this app".

The composition, top to bottom in a single-column ScrollViewer with the
styleguide's 40,36,40,32 page chrome and MaxWidth=900:

1. **Hero** — `<controls:BrandHeroControl Tagline="Local voices for Claude
   Code" />` from main-030. 88x88 brand-blue badge with the main-028
   utterheim mark inside, "Utterheim" 40pt ExtraBold, version tag in
   `BrandDeepMutedBrush`, and the signed-off tagline beneath.
2. **Profile & Contact card** — `Border` with
   `CardBackgroundFillColorSecondaryBrush`, `CornerRadius=12`, `Padding=28`.
   Inside: a 140x140 ringed (`#FF25abfe` 3-px border) portrait of Marco
   sourced from `assets/people/heimeshoff.jpg` (verbatim copy of
   WhisperHeim's), Marco's two-paragraph bio (paragraph 1 verbatim from
   WhisperHeim about DDD + collaborative modeling, paragraph 2
   utterheim-specific: *"I built Utterheim so my Claude Code sessions
   could speak in different voices — local, offline, with whatever voices I
   want to clone."*), divider, and the "Get in Touch" rows
   (heimeshoff.de Globe, Bluesky butterfly @Heimeshoff.de,
   linkedin.com/in/heimeshoff). Glyph paths and hyperlinks are lifted
   verbatim from WhisperHeim.
3. **Support & GitHub card** — `Border` with the same card chrome as the
   profile card. Contains the "If you enjoy Utterheim and want to support
   my open-source work…" line, a Ko-fi gradient button (`#FF25abfe →
   #FF005FAA`, `CornerRadius=10`, "Buy me a coffee on Ko-fi" white text)
   that opens `AppInfo.KofiUrl` in the default browser, the closing
   "Otherwise, just enjoy using Utterheim for free" line, and a
   "View on GitHub" link routed through `AppInfo.GithubUrl` (=
   `https://github.com/heimeshoff/utterheim`, distinct from
   WhisperHeim's repo).
4. **Credits** — `Synthesis powered by pocket-tts (Kyutai Labs).` Minimal,
   unchanged from main-017.

The Bento grid (Philosophy + AI Models) at the bottom of WhisperHeim's
About is **not** copied — it's WhisperHeim-specific and overlaps with
utterheim's Settings → Engine status.

### Code shape

`AboutPageViewModel` carries only `Version` (sourced from
`AppInfo.Version`); no commands, no event subscriptions, no
`Attach()` / `Detach()`. The page code-behind handles the static external
links — `Hyperlink_RequestNavigate` opens `e.Uri.AbsoluteUri` via
`Process.Start { UseShellExecute = true }`; `KofiButton_Click` does the
same with `AppInfo.KofiUrl`. URLs are app-wide constants on
`AppInfo`, not per-instance VM values, so the code-behind shortcut is the
right shape.

### Out of scope for v1

Captured in main-017's spec, repeated here so future tasks see the boundary:
ADR / changelog text on the About page, update-check / auto-update
infrastructure (vision non-goal in v1), localisation. The engine-status
boundaries (bootstrap-style retry, per-engine status when a second engine
ships, "Copy logs path" / "Open log file") moved with the panel — see
§Settings page → Diagnostics → Engine status.

### Verification note

The build is clean (`dotnet build utterheim.sln -c Debug` → 0 errors,
0 warnings). The interactive UI behaviours — Ko-fi / GitHub / hyperlink
navigation, About appearing in the nav footer rather than the main menu,
the engine-status card on Settings → Diagnostics responding to live state
transitions — are **not interactively re-tested** in this pass; the code
is in place per the main-032 spec and any regression will surface during
the next manual run.

## Voice library

As of main-015 the cloned-voice **backend** is real (no UI ships in this task —
main-025 builds the cloning sub-flow and main-026 the per-row delete):

- **`VoiceLibraryService`** owns `<dataPath>\voices\*` per ADR 0005. Singleton in
  DI. Every mutation is temp+rename (`profile.safetensors.tmp` → final, then
  `meta.json.tmp` → final, then `library.json.tmp` → final). Order matters: the
  master index (`library.json`) is written **last** so a crash mid-Add leaves a
  recoverable orphan folder rather than an index pointing at nothing. `DeleteAsync`
  inverts the order — index pruned **first**, then the folder — so a file-locked
  folder (e.g. sidecar still has `profile.safetensors` open from a just-finished
  preview) doesn't keep the row visible. One 200 ms retry on `IOException`; if
  the folder still won't delete the warning is logged and startup reconciliation
  cleans up next launch.
- **`VoiceLibraryStartup`** is a hosted-service shim that runs `LoadAsync` once on
  host start so the catalog has cloned rows ready before page VMs resolve.
  Reconciliation is bidirectional: library entries without folders are dropped (with
  a warning + a re-write of `library.json`), folders without entries are reinserted
  from `meta.json` (with a warning), and `meta.json` files declaring
  `schemaVersion > 1` are skipped with a warning rather than crashing.
- **`VoiceCatalog`** subscribes to `VoiceLibraryService.LibraryChanged` and re-fires
  its own `VoicesChanged` so main-014's Voices page refreshes live. `ListAsync`
  returns engine built-ins first, cloned voices second.
- **`VoiceCloningClient`** is the C# side of the sidecar's `POST /export-voice`
  contract. Uploads a sample WAV via multipart, returns the `.safetensors` bytes
  the C# host then hands to `VoiceLibraryService.AddAsync` for persistence. The
  sidecar is stateless between requests — no on-disk profile lives on the Python
  side. main-041 added the `language` parameter and stamps the same
  `X-Voice-Language` header the speak path uses (`PocketTtsEngine.LanguageWireValue`),
  so the sidecar's middleware swaps to the matching resident `TTSModel`
  before encoding the audio prompt — a German clone is encoded by the German
  model, an English clone by the English one. The middleware's
  `_route_paths_needing_model` set was extended to include `/export-voice`
  in main-041 so the header is honoured on the cloning hop.
- **Sidecar wrapper module** `utterheim_sidecar` is bundled next to the binary
  under `src\Utterheim\PythonSidecar\utterheim_sidecar\` and copied into the
  Python runtime's site-packages by the bootstrapper (per ADR 0015). It mounts
  `POST /export-voice` and `POST /tts-with-state` on top of pocket-tts's existing
  FastAPI app, and re-exports a `serve` typer command that mirrors
  `pocket_tts serve` — so `SidecarHost.cs`'s spawn argument string only changes
  the module name (`pocket_tts` → `utterheim_sidecar`).

### Voice profile schema (locked v1)

Per-voice `meta.json`:

```json
{
  "schemaVersion": 1,
  "id": "marco",
  "name": "Marco",
  "engine": "pocket-tts",
  "pocketTtsVersion": null,
  "source": "Mic",
  "language": "english",
  "createdAt": "2026-05-04T12:34:56+00:00",
  "sampleSeconds": 12,
  "samplePath": "sample.wav",
  "tags": []
}
```

Master `library.json`:

```json
{
  "schemaVersion": 1,
  "voices": [
    { "id": "marco", "name": "Marco", "engine": "pocket-tts",
      "source": "Mic", "language": "english",
      "createdAt": "2026-05-04T12:34:56+00:00" }
  ]
}
```

The library mirrors a strict subset of `meta.json` so the catalog can populate
the picker without reading N per-voice files. Full per-voice metadata (sample
length, source path, tags, pocket-tts version) lives in `meta.json` only;
catalog consumers that need them can lazy-read on demand.

`language` (added by main-040 per ADR 0023) is a lower-case string from a
constrained set — `"english"` and `"german"` for v1 (room to add `"french"`,
`"italian"`, `"spanish"`, `"portuguese"` later once a built-in or clone for
the language is needed). Backed by the `VoiceLanguage` enum in
`Services\Voices\ClonedVoiceMeta.cs`. Legacy `meta.json` / `library.json` files
written before main-040 lack the field; `System.Text.Json` applies the init
default (`VoiceLanguage.English`) on read, which is also what ADR 0023 says
the migration default should be — first save after upgrade persists the field.
No `schemaVersion` bump because the field is additive with a documented default.

### Naming rules

- **`id`** — lowercase-kebab, `[a-z0-9-]`, 1–40 chars. Generated from the display
  name by lowercasing, mapping whitespace / `_` / `.` to `-`, dropping anything
  else, and trimming leading/trailing dashes. Stable per voice — never renamed.
- **Built-in collision** — if the generated id matches one of the reserved
  built-ins (`alba`, `marius`, `javert`, `jean`, `fantine`, `cosette`, `eponine`,
  `azelma`, plus `juergen` since main-040), `AddAsync` throws
  `VoiceValidationException` with a "pick a different name" message rather
  than disambiguating. The user explicitly typed
  "Alba" and we won't silently shadow the built-in.
- **Cloned-id collision** — if the id collides with another *cloned* voice or an
  on-disk folder (orphan, ID-typo recovery), a 4-hex `Guid.NewGuid()` suffix
  is appended (`marco`, `marco-a3f2`).
- **Display name** — up to 40 chars after trim + whitespace-collapse; not
  required unique (two voices both called "Marco" coexist via different ids).

### Engine resolution flow

`PocketTtsEngine.StreamAsync(text, voiceId, ct)` branches on whether `voiceId`
matches one of the reserved built-in ids (case-insensitive set; the eight
English voices plus `juergen` as of main-040). If yes,
the existing `/tts` + `voice_url` path runs unchanged. If no, the engine asks
`VoiceLibraryService.TryResolveProfilePath(voiceId)`; null → throws
`InvalidOperationException("Unknown voice id …")` which surfaces through
`SpeakService` to the page status line and to the HTTP `/speak` 500 body. Non-null
→ POST the file as `voice_state` to `/tts-with-state`. The streaming response
shape (24 kHz mono 16-bit PCM after a 44-byte WAV header) is identical for both
endpoints, so the WAV-stripping + chunk-yielding tail of `StreamAsync` is shared.

### What is *not* in main-015

- Cloning UI (mic capture, loopback capture, sample preview) — main-025.
- Per-row delete button + confirmation dialog — main-026.
- File-import path (`source: "Import"`) — backend supports it; no UI in v1.
- Voice rename / re-tag — no v1 task.
- Active-playback guard on delete — delete during preview is allowed; the
  playback cancellation path covers the file-just-disappeared race.
- Tray-warning popup for orphan folders — warning log is enough in v1.

## Claude Code integration kit

As of main-019, the bridge from "the speak endpoint exists" to "Claude
Code sessions actually talk to me" ships as documentation + a sample
script under `examples/claude-hooks/`:

- `utterheim-hook.ps1` — small PowerShell shim that POSTs `{text, voice}`
  to `http://127.0.0.1:7223/speak`. Reads voice from
  `$env:UTTERHEIM_VOICE` (default `alba`) and endpoint from
  `$env:UTTERHEIM_ENDPOINT` (default per ADR 0003). `-Silent` swallows
  all failures so a missing sidecar can never block Claude Code itself.
- `README.md` — wiring recipe for Claude Code's `Stop` and `Notification`
  hooks, voice-assignment convention (env var per terminal), the parallel
  two-session worked example that demonstrates the v1 payoff, and a
  troubleshooting section covering the failure modes surfaced by
  main-018 verification (sidecar not running, port collision, cold-start
  latency on long input, watchdog vs `/status`-polling races).

Voice routing is **caller-side by design** — the BC has no concept of a
session id, and per-session voice is purely an env-var contract between
the user's shell and the hook script. Server-mediated routing would be
a separate decision task.

### The productized plugin (`claude-code-plugin/`, language-aware)

The shipping artifact is the `utterheim-narrator` Claude Code plugin under
`claude-code-plugin/` (a marketplace + hooks + the `/narrator` slash command),
not the older `examples/claude-hooks/` sample above. As of main-047 it is
**language-aware** (ADR 0028): each spoken string is classified English or
German by an offline, dependency-free PowerShell heuristic
(`Get-NarratorLanguage` in `scripts/narrator-lib.ps1` — umlaut/ß signal plus
whole-word German stopword scoring; English is the safe default for empty /
tie / sub-floor input), then a voice is resolved for the detected language. The
sidecar routes by the named voice's declared language (ADR 0023), so the wire
body stays `{text, voice}` — this change is **client-side only**, no server work.

Each repo configures a **pair of voice slots** (ADR 0028):

- **English / default slot** — `./.claude/utterheim-voice` (the legacy file,
  unchanged meaning) → `$env:UTTERHEIM_VOICE` → `alba`.
- **German slot** — `./.claude/utterheim-voice-de` → `$env:UTTERHEIM_VOICE_DE`
  → fall back to the resolved English/default slot (never a hard-coded
  `juergen`). There is no `utterheim-voice-en` file; the English slot *is* the
  legacy file.

`Resolve-Voice` (same lib) implements that chain; an explicit `-Voice`
parameter overrides everything and bypasses detection. The `off`/`none`/`-`
mute marker is evaluated on the **finally-resolved** voice for the detected
language: an English slot of `off` mutes English while a real German slot still
speaks German, and a repo with only `utterheim-voice` = `off` is fully muted.
The `/narrator` command sets/shows both slots and warns (but still persists)
when a voice's `language` from `GET /voices` doesn't match the slot it's written
to. Built-in voices: 8 English (`alba, marius, javert, jean, fantine, cosette,
eponine, azelma`) + 1 German (`juergen`); more German voices require cloning.

Ubiquitous language: *English slot* / *German slot*, *the resolved voice*,
*mute*, *the sidecar routes by language*. (Not "locale" / "i18n" — this is voice
selection, not localization.) The pure helpers are unit-tested with Pester in
`claude-code-plugin/tests/narrator-lib.Tests.ps1`; the C# host's
`Utterheim.Tests` xUnit project is not the home for plugin PowerShell.

## Notes for the architect

ADRs 0001–0008 are committed. The walking skeleton (main-009) materialised them as
code; main-011 replaced the stub engine with the real pocket-tts sidecar (see "Engine
status" above). Every architectural seam — HTTP, queue, NAudio playback, hotkey,
tray, logging, path layout, CLI wrapper, sidecar lifecycle, runtime bootstrap — is
real and end-to-end verified.

Historical: the boundary analysis surfaced four architectural decisions which
have now been resolved by the ADRs above:

1. Python-vs-C# integration shape for pocket-tts → ADR 0002 (Python sidecar).
2. Claude-to-utterheim transport → ADR 0003 (HTTP loopback on :7223).
3. Stop-signal semantics → ADR 0004 (drain queue by default).
4. WhisperHeim reuse form → ADR 0006 (copy-and-modify in v1).
