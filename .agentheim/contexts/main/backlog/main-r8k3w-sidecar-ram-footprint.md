---
id: main-r8k3w
title: Cut the Python sidecar's RAM footprint (~2.4 GB) — measure where it goes, then decide the lever
status: backlog
type: spike
context: main
created: 2026-06-28
completed:
depends_on: []
blocks: []
tags: [ram, sidecar, performance, pocket-tts, python]
related_adrs: [0024, 0002, 0025]
related_research: [pocket-tts-german-support-2026-05-18, kyutai-tts-2026-05-01]
prior_art: [main-036, main-039, main-037, main-038]
---

## Why

The pocket-tts Python sidecar holds ~2.4 GB resident while idle on the user's
Windows box, with English and German models loaded as two separate `TTSModel`
instances (per ADR 0024 / main-039). That is a lot of standing memory for a
tray app that mostly waits. The user wants it leaner.

The intuitive fix — "the model is multilingual now, so load one checkpoint for
both languages" — does **not** work with this engine, and the research already
settled why (see Notes). So this is a measure-first spike, not a pre-decided
refactor: find out where the 2.4 GB actually goes, then pick the lever that
moves it.

## What

Investigate and document the sidecar's real resident-memory breakdown on
Windows, then evaluate the candidate levers and recommend one (or conclude the
footprint is acceptable / irreducible without a runtime change).

Concretely:

1. **Document the current load path.** How `serve` instantiates the per-language
   `TTSModel` map today (`PythonSidecar/utterheim_sidecar/main.py`, the
   `language → TTSModel` map from main-039), what stays resident, and what the
   Mimi/Moshi codec contributes.
2. **Measure the breakdown on Windows** — the "Resident-RAM measurement on
   Windows" question the user de-prioritised in ADR 0024 is now the whole point.
   Split the 2.4 GB into: PyTorch/Python runtime + CUDA/MPS context, the Mimi
   audio codec, model-1 weights, model-2 weights. Confirm or refute the
   research's ~135 MB-per-model / ~270 MB-for-both estimate against reality.
3. **Evaluate levers against the measured breakdown**, in rough order of
   expected payoff:
   - Quantization (`--quantize` / `quantize=True`, supported since pocket-tts
     2.0.0; 2.1.0 fixed a voice-cloning bug under quant) — halves weights.
   - Sharing one Mimi codec / encoder instance across both resident models
     instead of paying for it twice, if the API allows.
   - Dropping the second model and accepting a constraint (reload-on-change, or
     single-language-per-process) — only worth it if model weights turn out to
     be a large slice, which the research suggests they are not.
   - Moving off PyTorch entirely for the narrator path (Piper/ONNX, no torch in
     the process) — biggest cut, but a quality and architecture change; would
     supersede ADR 0002's warm-pocket-tts-sidecar framing for that path.
4. **Recommend** the lever with the best RAM-cut-per-effort, or conclude the
   footprint is acceptable. If the recommendation is architectural (e.g. ONNX
   path, or revisiting ADR 0024), spawn a follow-up `decision` task rather than
   deciding here.

## Acceptance criteria

- [ ] A written breakdown of the ~2.4 GB resident set on the user's Windows
      machine, attributing it across runtime / codec / model-1 / model-2.
- [ ] The current sidecar model-load path documented (file + the main-039
      `language → TTSModel` map), so any future refactor starts from fact.
- [ ] Each candidate lever assessed against the measured numbers with an
      estimated RAM delta and a one-line cost (quality / latency / architecture).
- [ ] A single recommendation: the lever to pursue (with a follow-up task
      stub if it's architectural), or an explicit "acceptable, no change".
- [ ] The "load one multilingual model for both languages" idea explicitly
      resolved (it is not available with load-time-bound pocket-tts — see Notes),
      so it doesn't get re-proposed later.

## Notes

**The one-model premise is already closed by research.** pocket-tts binds
language at instance-load time: `TTSModel.load_model(language=...)`, and the
generation calls (`generate_audio`, `generate_audio_stream`) take no language
argument. A single resident instance produces exactly one language for the
process lifetime. The model *file* is multilingual, but the *loaded instance*
is not. This is unlike Chatterbox-Multilingual or Coqui XTTS-v2 (which take a
per-request `language_id`). See `pocket-tts-german-support-2026-05-18` §3 and §7
(lines 167–173, 300–305), and ADR 0024, which considered exactly this and chose
two resident instances on purpose.

**So consolidating to one model is unlikely to be the RAM lever.** The research
estimates ~135 MB per resident model (~270 MB for en+de) — a single-source
community number, FP32 weights ~400 MB on disk, mmap'd safetensors smaller
resident. If that holds on Windows, the two models are ~270 MB of the 2.4 GB and
the remaining ~2.1 GB is PyTorch + the Mimi codec + process overhead. Dropping
the German model would reclaim ~135 MB and break the concurrent EN/DE narration
that main-039 / ADR 0024 / ADR 0028 were built for. Hence: measure first.

**Touches several settled decisions** — handle as revisits, not free edits:
- ADR 0024 (preload EN+DE concurrently) — its "trivially reversible if RAM
  becomes a constraint" clause and "Resident-RAM measurement on Windows" open
  follow-up are exactly this spike's trigger.
- ADR 0025 (distilled `german`, not `german_24l`) — variant affects footprint;
  distilled is already the lighter choice.
- ADR 0002 (pocket-tts as warm Python sidecar) — an ONNX/Piper lever would
  supersede this for the narrator path.
- ADR 0028 (narrator EN+DE voice-pair config) — depends on both languages
  being serveable concurrently; a single-language fallback would regress it.

Quantization is the most promising in-place lever (keeps quality roughly,
halves weights, already supported). Out of scope to implement here — this spike
measures and recommends; implementation is a follow-up task.
