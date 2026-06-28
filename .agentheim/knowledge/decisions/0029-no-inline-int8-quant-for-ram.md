---
id: 0029
title: Do not enable inline int8 quantization for RAM reduction (torch.ao fallback yields no resident cut on torch 2.12)
scope: main
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [main-r8k3w, main-039, main-027]
related_research: [pocket-tts-german-support-2026-05-18]
---

# ADR 0029: No inline int8 quantization for RAM — the available backend doesn't cut resident memory

## Context

The pocket-tts sidecar holds ~2.0 GB resident idle (measured live, main-r8k3w),
and the user wanted it leaner. pocket-tts 2.1.0 ships a ready-made lever:
`serve --quantize` → `TTSModel.load_model(quantize=True)` →
`apply_dynamic_int8(flow_lm, {"attention","ffn"})`, documented as "~48% runtime
memory reduction, ~27% faster on x86, WER unchanged." Turning it on is a one-line
change to `SidecarHost.cs:291`. main-r8k3w was authorised to implement it inline
**iff** it proved the clear RAM-cut-per-effort winner and didn't regress voice
cloning.

Two facts gate that promise (verified in main-r8k3w):

1. **The documented "~48%" applies to the `torchao` backend, which is absent.**
   `quantization._get_backend()` returns `torchao` only when torchao is installed
   with working C++ extensions. The Utterheim runtime is deliberately zero-dep and
   version-checked by the bootstrapper (main-027 / ADR 0011); torchao is not
   installed and must not be pip-installed into it. So `_get_backend()` falls to
   the **deprecated `torch.ao.quantization.quantize_dynamic`** path — and the
   docstring itself scopes that fallback to "torch 2.5–2.9," while the runtime
   ships **torch 2.12.0+cpu**.

2. **Measured: the fallback runs end-to-end but does not reduce resident RAM.**
   The main-r8k3w harness (`tools/measure_footprint.py`, ctypes
   `GetProcessMemoryInfo`) loaded `english` + `german_24l` with and without
   `--quantize`:

   | config | working set (idle, standalone loader) |
   |---|---|
   | `english` + `german_24l` (baseline) | **1985 MB** |
   | `english` + `german_24l` `--quantize` | **2045 MB** (+60 MB) |

   Quantization made the resident set slightly **larger**, not smaller, and added
   ~2 s to load time (german_24l load 1.6 s → 3.6 s). The state_dict tensor sum
   appears to drop (flow_lm 1205 MB → 53 MB), but that figure under-counts the
   packed quantized params and is contradicted by the true working set: the
   load-float32-then-quantize sequence pays the full float32 high-water mark and
   the freed pages are not returned to the OS by the torch CPU allocator, so the
   int8 weights land *on top of* the float32 footprint rather than replacing it.

Because the lever fails the RAM-cut criterion on measurement alone, the
cloning-regression gate was not exercised — there is no RAM win to justify
shipping it.

## Decision

**Utterheim does not enable `--quantize` on the sidecar `serve` invocation for the
purpose of cutting resident RAM.** The `--quantize` flag remains wired in the
sidecar (`main.py` serve, threaded into `load_model`) and untouched — this ADR
only declines to turn it on in `SidecarHost.cs`.

The decision is conditional on the runtime: it holds **as long as torchao is
absent and torch is 2.10+**. If a future runtime ships torchao with working C++
extensions (the `pocket-tts[quantize]` extra), the lever should be re-measured —
the optimized backend may deliver the documented ~48% cut and would then be
re-evaluated against the cloning gate.

## Consequences

### Positive
- No effort spent wiring, version-bumping, and cloning-gating a lever that the
  measurements show buys nothing on this runtime.
- The zero-dep, version-checked runtime contract (main-027) stays intact — we did
  not pip torchao into it to chase the quant path.
- The real RAM lever is identified instead: the resident set is **model-weight
  dominated** (german_24l's 24-layer flow_lm is 1205 MB of the 1985 MB), so the
  cut comes from the model variant, not from quantizing it. See the main-r8k3w
  follow-up on reverting `german_24l` → distilled `german`.

### Negative
- The advertised "~27% faster inference" from quant is also forgone. Acceptable:
  the latency budget (ADR 0013) is met today, and the fallback backend's speed
  characteristics on torch 2.12 were not validated either.

### Neutral
- Reversible: if the runtime gains torchao, re-open with a fresh measurement arm
  (the harness already supports `--quantize`).

## Alternatives considered

1. **Enable `--quantize` inline (the authorised conditional).** Rejected on
   measurement: +60 MB working set and +2 s load on the only available backend.
2. **Install torchao into the runtime to get the optimized backend.** Rejected:
   violates the zero-dep, version-checked runtime contract (main-027 / ADR 0011);
   torchao C++ extensions also add distribution weight and platform risk.
3. **Quantize-on-disk / load pre-quantized weights.** Not available from upstream
   pocket-tts 2.1.0; would be a separate engineering effort with its own ADR.

## References
- main-r8k3w — the footprint spike; `tools/measure_footprint.py` and the measured
  table above.
- main-027 / ADR 0011 — zero-dep, version-checked embeddable runtime; the reason
  torchao cannot simply be installed.
- ADR 0024 — preload EN+DE concurrently; quant was the one lever that kept both
  resident instances (so it was the inline-eligible arm) but it doesn't pay off.
- ADR 0013 — first-chunk latency budget; quant's forgone speedup is acceptable
  because the budget is already met.
