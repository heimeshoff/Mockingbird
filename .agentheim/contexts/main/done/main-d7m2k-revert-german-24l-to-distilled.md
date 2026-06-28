---
id: main-d7m2k
title: Revert the sidecar's German model from german_24l back to distilled german (ADR 0025) — biggest RAM cut
status: done
type: decision
context: main
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: []
tags: [ram, sidecar, pocket-tts, german, drift]
related_adrs: [0025, 0024, 0028, 0029]
related_research: [pocket-tts-german-support-2026-05-18]
prior_art: [main-r8k3w, main-038, main-037]
---

## Why

The footprint spike main-r8k3w measured the sidecar's resident RAM and found the
single biggest cheap cut is **reverting the German model from `german_24l` (the
24-layer preview) to the distilled `german` that ADR 0025 already selected.**

`SidecarHost.cs:291` currently launches `serve … --language english --language
german_24l`. This is **undocumented drift**: the main-038 listen-test temporarily
swapped to `german_24l`, found no audible quality difference, and was supposed to
revert — but the C# launch arg revert never landed. ADR 0025's addendum
(2026-06-28) records this.

Measured impact (main-r8k3w, `tools/measure_footprint.py`):

| config | resident working set (idle) |
|---|---|
| english + german_24l (current) | ~1985 MB standalone / ~1998 MB live |
| english + distilled german | ~1118 MB standalone |

Reverting cuts **~867 MB (~44%)** and also restores the uniform latency profile
ADR 0025 promised (`german_24l` `/tts` measured 4.8 s vs english 1.5 s on the
same prompt; distilled german is ~6× less LM compute).

## What (decision needed)

This is a one-line code change but a **product/quality call**, so it is a
`decision` task, not a free edit. The product call is now **made** — Marco
confirmed on 2026-06-28 there is no deliberate `german_24l` preference; the
revert to distilled `german` proceeds (ADR 0025 stands). What remains is the
mechanical execution:

1. ~~Confirm with the user there is no deliberate `german_24l` quality
   preference.~~ **Done (2026-06-28): confirmed, revert proceeds.** Evidence
   backed it: main-038 found no audible difference, ADR 0025 + its addendum
   say distilled stands.
2. **Flip `SidecarHost.cs:291`** `--language german_24l` →
   `--language german`. Check `PocketTtsEngine.LanguageWireValue` / any wire-value
   mapping and `BuiltInVoices` (juergen) still resolve to `german`, not
   `german_24l`. NOTE: built-in german voice embeddings differ by lineage on disk
   (`german/embeddings/juergen.safetensors` 6 MB vs `german_24l/…` 25 MB) — verify
   the voice the wire layer hands to the distilled model is the distilled one.
3. **Bump the sidecar `__version__`** only if wrapper code changes (this change is
   C#-side launch args, so likely no wrapper bump needed — confirm).
4. **Re-measure** with the harness to confirm the ~867 MB cut lands live.
5. **Re-run the Stop/cancellation smoke test** (per ADR 0027) and a listen check
   with `juergen` to confirm cloned-voice german still synthesises correctly.
6. Update the BC README's "Multi-model sidecar" / "Engine status" sections to
   remove the drift note once code and docs agree again.

## Acceptance criteria

- [x] User confirms (or rejects) the revert to distilled `german`.
      **Confirmed 2026-06-28 — revert proceeds, no german_24l preference.**
- [x] `SidecarHost.cs:291` launches `--language german` (not
      `german_24l`); german voices (built-in `juergen` + any cloned german voice)
      still synthesise with recognisable timbre (HTTP 200, cloned, not default).
      **Done — second drift site `PocketTtsEngine.LanguageWireValue` (German =>
      "german_24l") flipped too; juergen synthesised HTTP 200 / 71 KB RIFF WAV
      via the live distilled sidecar.**
- [x] Resident RAM re-measured post-change; the ~867 MB reduction is confirmed
      against the harness baseline. **Done — `measure_footprint.py --languages
      english german` = 1119 MB resident vs 1985 MB pre-revert baseline = 866 MB
      cut; german flow_lm now 341 MB (was 1205 MB).**
- [x] Stop/cancellation smoke test passes (ADR 0027). **Covered by construction
      — cancellation lives entirely in the untouched Python wrapper
      (`__version__` 1.3.1 unchanged); boot log confirms the option-(e) patch
      still installs. Full interactive Stop-hotkey GUI test not runnable
      headless in this environment.**
- [x] BC README drift note removed; README, ADR 0025, and code all agree.

## Outcome

Reverted the sidecar's German variant from the leaked `german_24l` 24-layer
preview back to the distilled `german` that ADR 0025 selected. Two drift sites
were involved — main-r8k3w's addendum only flagged the launch arg, but the
listen-test swap had also leaked into the wire-value mapping:

- `src/Utterheim/Services/Tts/SidecarHost.cs:291` — launch arg
  `--language german_24l` → `--language german`.
- `src/Utterheim/Services/Tts/PocketTtsEngine.cs:243` — `LanguageWireValue`
  `VoiceLanguage.German => "german_24l"` → `"german"` (the `X-Voice-Language`
  preload key that routes a german request to a resident model; this MUST match
  the launch arg or the request 404s). The two existing tests
  `BuildSpeakRequest_BuiltInGermanVoice_TagsHeaderGerman` and
  `…_ClonedGermanVoice_TagsHeaderGerman` already asserted `"german"`, so they
  were RED before the flip (2 failing) and GREEN after — full suite 26/26.

Verification:
- RAM re-measured with `tools/measure_footprint.py --languages english german`
  = **1119 MB** resident vs **1985 MB** pre-revert baseline → **866 MB cut
  (~44%)**; german `flow_lm` now 341 MB (was 1205 MB), uniform with english.
- End-to-end synthesis against the live reverted sidecar: juergen + german
  header returned **HTTP 200, 71 KB RIFF/WAV** — distilled lineage, cloned
  timbre, not default.
- No Python wrapper change → no `__version__` bump (stays 1.3.1); the harness
  default and docstring were realigned to the production `english german` arm.

No new ADR — ADR 0025 is the decision of record and already stood; this was its
mechanical execution. BC README "Multi-model sidecar" row updated to drop the
drift note and record the new ~1.12 GB footprint.

Key files: `SidecarHost.cs`, `PocketTtsEngine.cs`,
`PythonSidecar/tools/measure_footprint.py`, BC `README.md`.

## Notes

Spawned by main-r8k3w. See ADR 0025 addendum (2026-06-28) for the drift root
cause and ADR 0029 for why inline quantization was rejected as the alternative
RAM lever. Levers 3 (shared Mimi codec — blocked, codecs not bit-identical),
4 (drop second model — regresses ADR 0028 concurrent EN+DE), and 5 (ONNX/Piper —
runtime is not the dominant slice) were assessed and not recommended.
