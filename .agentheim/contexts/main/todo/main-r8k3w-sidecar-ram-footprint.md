---
id: main-r8k3w
title: Cut the Python sidecar's RAM footprint (~2.4 GB) — measure where it goes, then decide the lever
status: todo
type: spike
context: main
created: 2026-06-28
completed:
depends_on: []
blocks: []
tags: [ram, sidecar, performance, pocket-tts, python]
related_adrs: [0024, 0025, 0002, 0028]
related_research: [pocket-tts-german-support-2026-05-18, kyutai-tts-2026-05-01]
prior_art: [main-036, main-039, main-037, main-038]
---

## Why

The pocket-tts Python sidecar holds ~2.4 GB resident while idle on the user's
Windows box, with two `TTSModel` instances preloaded (per ADR 0024 / main-039).
That is a lot of standing memory for a tray app that mostly waits. The user
wants it leaner.

The intuitive fix — "the model is multilingual now, so load one checkpoint for
both languages" — does **not** work with this engine, and the research already
settled why (see Notes). So this is a measure-first spike: find out where the
2.4 GB actually goes, then pull the lever that moves it. The refinement below
(architect pass, 2026-06-28) already overturned the task's original premise
against the real code — start from the corrected facts, not the intuition.

## What

Investigate and document the sidecar's real resident-memory breakdown on
Windows, then evaluate the candidate levers and recommend one — and, per the
user's call, **implement the quantization lever inline if (and only if) the
numbers prove it the clear RAM-cut-per-effort winner and it doesn't regress
voice cloning.** Anything architectural defers to a follow-up task.

### Corrected starting facts (from the 2026-06-28 refinement — verified against code)

These overturn the original capture's assumptions. Do not re-derive them; they
were checked against the running code and the installed package:

1. **The live sidecar loads `english` + `german_24l`, NOT `english` + `german`.**
   `SidecarHost.cs:291` launches `serve … --language english --language german_24l`.
   The second resident model is the **24-layer preview** model, not the distilled
   6-layer `german` that ADR 0025 / main-037 chose as production. On disk:
   `english` 208 MB, `german` (distilled) 208 MB, **`german_24l` 641 MB** — ~3×.
   `english.yaml` `flow_lm.transformer.num_layers: 6`; `german_24l.yaml`: `24`.
   So the resident set is **dominated by german_24l's flow_lm (~1.2 GB float32
   est.)**, not "~270 MB for two models." The original `~135 MB/model` estimate
   is wrong by ~an order of magnitude — **struck.**
2. **There is a doc/decision drift to resolve.** The BC README ("Multi-model
   sidecar" + "Engine status") and ADR 0025 / main-037 all say the sidecar runs
   distilled `german`; the code runs `german_24l`. main-038 ("listen-test german
   vs german_24l") is the likely reason the launch args moved to `german_24l` —
   confirm whether german_24l is an intentional quality choice that superseded
   ADR 0025, or undocumented drift. This is itself a RAM lever (see lever 1).
3. **Mimi codec is loaded TWICE** — one independent `MimiModel` per `TTSModel`
   (`tts_model.py:174`, built per `load_model`). Not shared. Small relative to
   german_24l's flow_lm, but real.
4. **Environment is ready for in-environment measurement.** `%LOCALAPPDATA%\
   Utterheim\runtime\python\` has `python.exe`, `pocket_tts` **2.1.0**, `torch`
   **2.12.0**, `utterheim_sidecar`; snapshots for english/german/german_24l are
   cached under `…\models\pocket-tts\hub\`. **`psutil` and `torchao` are NOT
   installed** — both shape the plan below.

### Steps

1. **Document the current load path** (already mostly done above; capture in the
   write-up): `serve` builds `_RESIDENT_MODELS` in
   `PythonSidecar/utterheim_sidecar/main.py` (load loop ~lines 859–877, call
   `TTSModel.load_model(**load_kwargs)` at ~872); `load_model` lives in installed
   `pocket_tts/models/tts_model.py` (232–315), Mimi built at 174, combined
   `model.safetensors` (flow_lm + mimi) loaded at 201–210.

2. **Build and run the measurement harness in this environment.** New
   `src/Utterheim/PythonSidecar/tools/measure_footprint.py`, stage-incremental,
   recording resident + commit memory after each stage:
   - **Memory probe: ctypes, not psutil** (psutil is absent and the runtime is
     deliberately zero-dep / version-checked by main-027 — do NOT pip-install
     into it). Call `psapi`/`kernel32` `GetProcessMemoryInfo` for `WorkingSetSize`
     (≈ RSS) and `PagefileUsage` (≈ commit).
   - **Stages:** (a) bare interpreter, (b) `import torch`, (c) `TTSModel.load_model
     (language="english")`, (d) `…("german_24l")` — expect the biggest jump here,
     (c′) in a separate run `…("german")` (distilled) so the german_24l→german
     swap is quantified, not guessed, (e) per-model Mimi share via
     `size_of_dict(model.mimi.state_dict())` (already imported in the package) and
     a `mimi.state_dict()` equality check between the two models (gates lever 3).
   - **Invocation/env:** run with the embeddable `python.exe`; set
     `HF_HOME=%LOCALAPPDATA%\Utterheim\models\pocket-tts` (matches
     `SidecarHost.cs:300`), `HF_HUB_OFFLINE=1` (weights are cached — never hit the
     network), `PYTHONIOENCODING=utf-8`.
   - **Cross-check against the live process.** A standalone loader skips
     uvicorn/FastAPI/Starlette + middleware (tens of MB) and the post-first-request
     high-water mark. Also sample the **running sidecar** working set (idle and
     after one warm `/tts`) so the recommendation targets the real resident peak,
     not just load-time. No multiprocessing multiplier (pocket-tts uses threads).

3. **Evaluate levers against the measured numbers**, in expected-payoff order:
   - **Lever 1 — `german_24l` → distilled `german` (likely the biggest cheap cut).**
     ~641 MB → ~208 MB on disk before any quant; honours ADR 0025 / main-037.
     **Architectural / product call (deferred):** it's a quality decision
     (german_24l may have been chosen deliberately — see drift, fact 2) and a
     one-line `SidecarHost.cs:291` change, but it revisits ADR 0024/0025 and the
     main-038 listen-test. Recommend + follow-up `decision` task; do NOT flip it
     inside this spike.
   - **Lever 2 — quantization (the inline-implementable arm).** `apply_dynamic_int8`
     quantizes **only `flow_lm`** attention+FFN linears (`quantization.py`), Mimi
     and embeddings stay float32 — so the win concentrates exactly where the cost
     is (german_24l's flow_lm → ~25% size). Already wired: `serve` has a
     `--quantize` flag (`main.py:799`, threaded at 863–864 into `load_model`'s
     `quantize=` at `tts_model.py:241,312`). Implementing = adding `--quantize` to
     the `SidecarHost.cs:291` args + verification. **GATE:** torchao is absent, so
     `_get_backend()` falls to the **deprecated `torch.ao.quantization.quantize_dynamic`**
     path on **torch 2.12** — the harness must prove `apply_dynamic_int8` runs
     end-to-end before any quant RAM number is trusted. If it throws, the inline
     arm is blocked.
   - **Lever 3 — share one Mimi codec across both models** (`german_model.mimi =
     english_model.mimi`, IF the two `mimi.state_dict()`s are bit-identical).
     Feasible but **minor** (tens of MB) and wants its own ADR (aliasing a shared
     submodule across two `nn.Module`s). Deferred, low priority.
   - **Lever 4 — drop the second model** (single-language-per-process /
     reload-on-change). Only worth it if model weights dominate (they do, but this
     **regresses concurrent EN/DE narration** — ADR 0024 / 0028). Deferred.
   - **Lever 5 — move the narrator path off PyTorch (Piper/ONNX, no torch in the
     process).** Biggest cut, but a quality + architecture change superseding
     ADR 0002's warm-sidecar framing. Deferred to a `decision` task if the torch
     runtime turns out to be the dominant non-model slice.

4. **Recommend** the best RAM-cut-per-effort lever. If it's quantization AND it
   clears the cloning gate → implement inline (see ACs). If it's architectural
   (lever 1/4/5) → spawn a follow-up `decision` task, don't decide here. If the
   footprint is irreducible without a runtime change → say so explicitly.

## Acceptance criteria

- [ ] `tools/measure_footprint.py` exists, uses the ctypes memory probe (no
      psutil), and runs against the embeddable runtime to produce a **4-way (or
      finer) breakdown** of resident memory: torch/Python runtime, Mimi codec
      (per model), `english` flow_lm, `german_24l` flow_lm — plus the distilled
      `german` figure from the (c′) run for the swap comparison.
- [ ] The breakdown is **cross-checked against the live sidecar process** (idle
      and post-warm-synthesis working set), with any standalone-vs-live gap noted.
- [ ] The current model-load path is documented (file + the main-039
      `_RESIDENT_MODELS` map), so any future refactor starts from fact.
- [ ] Each lever (1–5) is assessed against the measured numbers with an estimated
      RAM delta and a one-line cost (quality / latency / architecture).
- [ ] The torch-2.12-without-torchao quant gate is resolved empirically: a
      recorded yes/no on whether `apply_dynamic_int8` runs end-to-end, with the
      quantized RSS delta if it does.
- [ ] A single recommendation is written: the lever to pursue (with a follow-up
      `decision` task stub if it's architectural — lever 1/4/5), or an explicit
      "acceptable, no change."
- [ ] **Conditional inline implementation:** IF quantization is the clear winner
      AND the cloning regression passes, then `--quantize` is added to
      `SidecarHost.cs:291`, the sidecar `__version__` is bumped (currently 1.3.1
      → so the bootstrapper re-copies the wrapper, ADR 0011/0016), the RAM delta
      is re-measured to confirm, and the Stop/cancellation smoke test is re-run
      (quant changes per-step timing — ADR 0027). ELSE: recommendation only.
- [ ] **Cloning regression gate (must pass before any inline quant ships):** with
      `--quantize` on, `/export-voice` → `/tts-with-state` produces recognisable
      cloned-timbre audio (HTTP 200, not a generic/default voice — the pre-2.1.0
      failure mode) in **both** english and german_24l. If it regresses, the quant
      arm reverts regardless of RAM.
- [ ] The "load one multilingual model for both languages" idea is explicitly
      resolved as **not available** (load-time-bound pocket-tts — see Notes), so
      it doesn't get re-proposed later.
- [ ] The `german_24l`-vs-`german` doc/decision drift (fact 2) is resolved in the
      write-up: confirm whether german_24l is intentional (supersedes ADR 0025) or
      drift, and reconcile the BC README / ADR 0025 accordingly via the follow-up.

## Notes

**The one-model premise is already closed by research.** pocket-tts binds
language at instance-load time: `TTSModel.load_model(language=...)`, and the
generation calls take no language argument. A single resident instance produces
exactly one language for the process lifetime. The model *file* is multilingual,
but the *loaded instance* is not — unlike Chatterbox-Multilingual / Coqui XTTS-v2.
See `pocket-tts-german-support-2026-05-18` §3, §7 (lines 167–173, 300–305) and
ADR 0024, which considered exactly this and chose two resident instances on purpose.

**Where the RAM actually is (corrected).** german_24l's 24-layer flow_lm is the
elephant (~1.2 GB float32 est.; 641 MB on disk vs 208 MB for english/distilled).
The remaining ~1.2 GB is the torch/Python runtime + the two Mimi codecs + process
overhead. This reorders the levers: the cheapest cut is the **model swap** (lever
1), and quantization's payoff is concentrated in german_24l's flow_lm (lever 2).
"Consolidate to one model" is NOT a real RAM lever and is closed above.

**Quant is already wired; the gate is torch.ao on torch 2.12.** `serve` exposes
`--quantize`; `load_model(quantize=True)` calls `apply_dynamic_int8(flow_lm,
RECOMMENDED_CONFIG)`. torchao is absent → deprecated `torch.ao.quantization.
quantize_dynamic` fallback on torch 2.12 (engine `fbgemm`, fine on x86). Prove it
runs before trusting any number. `speaker_proj_weight` is a bare `nn.Parameter`
(not quantized) and Mimi stays float32, so the **encode/clone** path is structurally
unaffected — the cloning risk is on the **generation** side consuming the cloned
state, which is exactly what the cloning gate re-verifies.

**Touches several settled decisions — handle as revisits, not free edits:**
- ADR 0024 (preload EN+DE concurrently) — its "trivially reversible if RAM becomes
  a constraint" clause + "Resident-RAM measurement on Windows" follow-up is this
  spike's trigger. Levers 1/3/4 revisit it; quantization (lever 2) does NOT (keeps
  both instances, just shrinks them) — which is why quant is inline-eligible.
- ADR 0025 / main-037 (distilled `german`, not `german_24l`) — **contradicted by
  the live launch args**; lever 1 and the drift resolution touch this directly.
- ADR 0002 (pocket-tts as warm Python sidecar) — only the ONNX/Piper lever (5)
  supersedes it; quant/swap/shared-codec leave process topology untouched.
- ADR 0028 (narrator EN+DE voice-pair config) — depends on both languages being
  serveable concurrently; only lever 4 (drop second model) would regress it.

**Architect refinement note (2026-06-28):** full code-grounded design — load-path
line refs, the ctypes harness stages, the quant-wiring locations, and the
shared-codec feasibility — was produced by the `architect` during this refinement
and is summarised inline above. Symbol/line refs: `main.py` serve 782–901 (load
872, `--quantize` 799/863–864), `tts_model.py` load_model 232–315 (quant 312–313,
Mimi 174), `quantization.py` apply_dynamic_int8 60–88 / backend 24–42,
`SidecarHost.cs` launch 291 / HF_HOME 300; installed `pocket_tts` 2.1.0, torch
2.12.0, torchao + psutil absent.
