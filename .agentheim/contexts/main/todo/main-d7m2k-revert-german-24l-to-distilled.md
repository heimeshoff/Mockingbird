---
id: main-d7m2k
title: Revert the sidecar's German model from german_24l back to distilled german (ADR 0025) — biggest RAM cut
status: todo
type: decision
context: main
created: 2026-06-28
completed:
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
- [ ] `SidecarHost.cs:291` launches `--language german` (not
      `german_24l`); german voices (built-in `juergen` + any cloned german voice)
      still synthesise with recognisable timbre (HTTP 200, cloned, not default).
- [ ] Resident RAM re-measured post-change; the ~867 MB reduction is confirmed
      against the harness baseline.
- [ ] Stop/cancellation smoke test passes (ADR 0027).
- [ ] BC README drift note removed; README, ADR 0025, and code all agree.

## Notes

Spawned by main-r8k3w. See ADR 0025 addendum (2026-06-28) for the drift root
cause and ADR 0029 for why inline quantization was rejected as the alternative
RAM lever. Levers 3 (shared Mimi codec — blocked, codecs not bit-identical),
4 (drop second model — regresses ADR 0028 concurrent EN+DE), and 5 (ONNX/Piper —
runtime is not the dominant slice) were assessed and not recommended.
