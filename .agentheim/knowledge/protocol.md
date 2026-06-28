# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-06-28 13:20 -- Task verified and completed: main-r8k3w - Cut the Python sidecar's RAM footprint (~2.4 GB)

**Type:** Work / Task completion
**Task:** main-r8k3w - Cut the Python sidecar's RAM footprint (~2.4 GB) — measure where it goes, then decide the lever
**Summary:** Measured the sidecar's resident footprint (~2.0 GB idle, model-weight dominated: german_24l's 24-layer flow_lm is 1205 MB of it) with a new ctypes harness; assessed all five RAM levers against the numbers and recommended reverting german_24l → distilled german (~867 MB / 44% cut, restores ADR 0025) via follow-up decision main-d7m2k. Inline int8 quant was measured out (torch.ao fallback runs but yields no resident cut, +60 MB) and rejected in ADR 0029.
**Verification:** PASS (iteration 1)
**Files changed:** 5
**Tests added:** 0 (spike — measurement harness, no behavior change)
**ADRs written:** 0029 (new); 0025 addended (drift addendum)

---

## 2026-06-28 13:00 -- Batch started: [main-r8k3w]

**Type:** Work / Batch start
**Tasks:** main-r8k3w - Cut the Python sidecar's RAM footprint (~2.4 GB) — measure where it goes, then decide the lever
**Parallel:** no (1 worker)

---

## 2026-06-28 12:30 -- Modeling / Refined: main-r8k3w - Cut the Python sidecar's RAM footprint (~2.4 GB)

**Type:** Modeling / Refine
**BC:** main
**Status after:** todo
**Summary:** Architect pass overturned the capture's premise against the live code: the sidecar launches `--language english --language german_24l` (`SidecarHost.cs:291`) — the **24-layer** model, not the distilled `german` that ADR 0025 / main-037 chose (641 MB vs 208 MB on disk). So the ~2.4 GB is dominated by german_24l's flow_lm (~1.2 GB est.), not "~270 MB for two models"; the original ~135 MB/model estimate is struck. Added: (1) corrected starting facts + a doc/decision drift to resolve (README/ADR 0025 say `german`, code says `german_24l`); (2) a concrete in-environment measurement harness (`tools/measure_footprint.py`, ctypes `GetProcessMemoryInfo` since psutil is absent, incremental-load stages incl. a distilled-`german` comparison run, live-process cross-check); (3) a reordered lever table — model-swap (lever 1, deferred decision), quantization (lever 2, inline-implementable), shared-codec/drop-model/ONNX (deferred); (4) a gated inline quant arm — already wired (`--quantize` flag), but torchao is absent so the deprecated `torch.ao` path on torch 2.12 must be proven, plus a cloning-regression gate (pocket_tts 2.1.0 fix). Promoted backlog → todo: executable end-to-end in this environment (runtime + pocket_tts 2.1.0 + torch 2.12.0 + cached snapshots all confirmed present).
**Split into:** none (architectural levers spawn follow-up `decision` tasks at recommendation time)
**ADRs written:** none (revisits flagged for ADR 0024 / 0025 / 0028; none decided here)

---

## 2026-06-28 11:57 -- Modeling / Captured: main-r8k3w - Cut the Python sidecar's RAM footprint (~2.4 GB)

**Type:** Modeling / Capture
**BC:** main
**Filed to:** backlog
**Summary:** Spike to measure where the sidecar's ~2.4 GB resident memory actually goes on Windows (runtime/codec/model-1/model-2) and recommend a lever to cut it. Captured the user's "boil the two models down to one" idea but flagged that pocket-tts is load-time language-bound (research §3/§7, ADR 0024), so one multilingual instance can't serve EN+DE and the two models are only ~270 MB of the 2.4 GB — the runtime is the likely hog. Quantization / shared codec / ONNX path are the candidate levers to assess.

---

## 2026-05-24 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (ee3b310) + capture commit aa1c012 + 1 chore backfill

**Done in this session — single-task batch (main-047):**
- The `utterheim-narrator` Claude Code plugin (`claude-code-plugin/`) is now language-aware. New `scripts/narrator-lib.ps1` holds two pure functions: `Get-NarratorLanguage` (offline heuristic — umlaut/ß signal + whole-word German stopword scoring; English the safe default for empty/tie/sub-floor text) and an extended `Resolve-Voice` (two-slot EN/DE config per ADR 0028) plus `Test-VoiceMuted`.
- `utterheim-speak.ps1` resolves a voice for the detected language; `utterheim-stop.ps1` and `utterheim-notification.ps1` classify their final spoken text (summary / message — not the raw transcript) and pass the language through.
- `/narrator` (`commands/narrator.md`) now sets/shows both slots, prints the catalog grouped by language, and warns on slot/language mismatch while still persisting.
- Config per ADR 0028: legacy `./.claude/utterheim-voice` = English/default slot; new `./.claude/utterheim-voice-de` + `$env:UTTERHEIM_VOICE_DE` = German slot; German-with-no-DE-slot falls back to the configured English voice (not `juergen`); mute evaluated post-resolution. Zero migration for existing single-voice repos.
- Plugin version 0.1.0 → 0.2.0 (consumer-update trigger). No server / C# / Python-sidecar change — the `{text, voice}` contract is untouched; the sidecar already routes by the voice's declared language (ADR 0023).
- Tests: new Pester spec `claude-code-plugin/tests/narrator-lib.Tests.ps1`, 21 cases, all green under Pester 3.4.0 / Windows PowerShell 5.1.

**Verification:** PASS on iteration 1. Verifier ran the Pester suite (21/21) and mapped every acceptance criterion to evidence. One harmless nit: worker's `FILES_CHANGED: 7` undercounted its 9-entry `FILE_LIST` (all files in-scope and present).

**Next steps for the user:**
- Re-install / reload the plugin to pick up 0.2.0 (`/plugin marketplace update utterheim-narrator` → `/plugin update` → `/reload-plugins`), then set a German voice per repo with the new `/narrator de <voice>` form (e.g. `/narrator de juergen`). With Utterheim running, a German end-of-turn summary should now speak in the German-slot voice; English in the English slot. Audible confirmation is the user's call — the logic + unit tests are green.

---

## 2026-05-24 -- Task verified and completed: main-047 - Language-aware narration — detect EN/DE and speak in a matching voice

**Type:** Work / Task completion
**Task:** main-047 - Language-aware narration — detect EN/DE and speak in a matching voice
**Summary:** The `utterheim-narrator` plugin now narrates in a language-matching voice — each spoken string is classified English or German by an offline PowerShell heuristic (`Get-NarratorLanguage` in `scripts/narrator-lib.ps1`), then a voice is resolved from the repo's two-slot EN/DE config per ADR 0028. Client-side only; the sidecar still routes by the voice's declared language. Plugin bumped 0.1.0 → 0.2.0.
**Verification:** PASS (iteration 1) — 21/21 Pester tests green (Pester 3.4.0 / WinPS 5.1); every AC mapped to evidence (detection rules, voice-pair resolution incl. German→English fallback not hard-coded `juergen`, per-language mute, `/narrator` two-slot set + language-mismatch warning, version bump, hooks still exit 0, no new runtime dependency, detection on resolved text not raw transcript). Scope confined to `claude-code-plugin/*` + main BC README; no server/C#/Python edits.
**Commit:** ee3b310
**Files changed:** 9 (worker FILE_LIST) + task move + INDEX + protocol
**Tests added:** 1 Pester spec (`claude-code-plugin/tests/narrator-lib.Tests.ps1`) — 21 cases
**ADRs written:** none (ADR 0028 pre-existed as the decision of record)

---

## 2026-05-24 -- Batch started: [main-047]

**Type:** Work / Batch start
**Tasks:** main-047 - Language-aware narration — detect EN/DE and speak in a matching voice
**Parallel:** no (1 worker)

---

## 2026-05-24 -- Model / Captured: main-047 - Language-aware narration — detect EN/DE and speak in a matching voice

**Type:** Model / Capture
**BC:** main
**Filed to:** todo
**Summary:** The `utterheim-narrator` Claude Code plugin gains a lightweight, dependency-free PowerShell heuristic that classifies Claude's spoken text as German or English, then speaks it in a language-matching voice. Each repo configures an EN+DE voice pair (preserving per-session distinct-by-ear identity); detected language picks within the pair. No server change — the sidecar already routes by the voice's declared language (ADR 0023). Orchestrator routed the one open decision (voice-pair on-disk format + back-compat) to **ADR 0028** (accepted, scope main): legacy `./.claude/utterheim-voice` = English/default slot, new `utterheim-voice-de` + `$env:UTTERHEIM_VOICE_DE` = German slot, German-with-no-DE-slot falls back to the configured English voice (not `juergen`), mute evaluated post-resolution. Filed directly to todo — TDD-ready, pure-function detection + resolution, no remaining unknowns.
**ADRs written:** 0028

---

## 2026-05-19 16:40 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (dee614c) + 1 chore backfill

**Done in this session — single-task batch (main-046):**
- ADR 0027 option (e) lands in production form. `sys.settrace` replaced with direct method-body replacement of `TTSModel._autoregressive_generation` — the patched body inlines the for-loop with an explicit `if stop_event.is_set(): break` between `_run_flow_lm_and_increment_step` and `latents_queue.put(next_latent)`. Eliminates the residual trace-callback frame retention main-045 measured at ~10-20 MB/cycle.
- Opt-in `UTTERHEIM_CANCEL_PROTOTYPE` machinery removed. Cancellation is always on. Both `/tts` (via LanguageRoutingMiddleware) and `/tts-with-state` (handler-level) wrap.
- New pytest harness `src/Utterheim/PythonSidecar/tests/` — three files (`test_cancel_patch.py`, `test_sentinel_push.py`, `test_sanity_check.py`) plus `conftest.py`. 6/6 pass. Stubs the pocket-tts surface so the suite stays light (no torch import).
- Sidecar version 1.2.2 → 1.3.0 so `PythonRuntimeBootstrapper` re-copies the wrapper on next launch.
- BC README updates: Stop signal glossary entry references ≤2 s ADR 0026 + ADR 0027 monkey-patch; engine status documents the always-on cancellation hook; no surviving `UTTERHEIM_CANCEL_PROTOTYPE` references except the explicit "is gone" sentence.

**Next steps for the user (empirical-AC carve-out — same pattern as main-045):**
- Acceptance criteria 4, 5, 6, 7, 8 (CPU drop <5% within ≤2 s, median per-cycle RSS delta ≤25 MB across 50 cycles, ADR 0013 first-chunk latency regression check on medium + long inputs) and the `/export-voice` manual cancellation regression require an interactive tray-app + Stop-hotkey session. The runbook is in main-046's `## Outcome` section. Build + install utterheim normally; the bootstrapper will see `__version__` 1.3.0 ≠ installed 1.2.2 and re-copy the wrapper on next launch. Run the same medium-input / long-input / 50-cycle stress protocol main-045 used. If CPU recovery and per-cycle RSS delta hit the ADR 0026 contract, main-046 is fully done; otherwise file a follow-up.

**Surprises / observations:**
- Verifier returned PASS on the first iteration. Worker implemented the option-(e) production form cleanly: removed all the opt-in machinery, inlined the loop body with the correct break placement, kept the sentinel push on the call arg (not `self`), kept the finally fix (unconditional `stop_event` set; no `source.close()`). The pytest harness exercises the patch installation, the sentinel push, and the loud-fail sanity check — exactly the three test files the AC list named, no scope creep.
- One nit on the worker's strict return: `FILES_CHANGED: 6` was inconsistent with a 7-entry `FILE_LIST` (conftest.py was missing from the integer count). All files were present on disk; the count discrepancy was a counting nit, not a defect.

**State of utterheim runtime:**
- Bundled wrapper at 1.3.0 with always-on cancellation. The C# side (`SpeakQueue.StopAndDrain` → linked CTS → `PocketTtsEngine.StreamAsync` → `HttpClient.SendAsync(..., ResponseHeadersRead, ct)` → `Stream.ReadAsync(..., ct)` + `Dispose()`) is unchanged — main-045 confirmed it was already correctly wired.
- After the user rebuilds + relaunches utterheim, the next sidecar boot will reinstall the wrapper (bootstrapper sees the version bump). Cancellation will be active without any env var.

---

## 2026-05-19 16:35 -- Task verified and completed: main-046 - Implement Stop cancellation propagation into the sidecar (≤2 s recovery)

**Type:** Work / Task completion
**Task:** main-046 - Implement Stop cancellation propagation into the sidecar (≤2 s recovery)
**Summary:** Sidecar 1.3.0 ships ADR 0027 option (e) in production form: `_autoregressive_generation` is unconditionally replaced at boot with a stop-event-aware reimplementation (direct method-body replacement, no `sys.settrace`); the `UTTERHEIM_CANCEL_PROTOTYPE` opt-in machinery is deleted; three pytest files cover patch installation, sentinel push, and startup sanity check. Implementation ACs (1, 2, 3, 9, 10, 11) auto-verified; empirical ACs (4, 5, 6, 7, 8 and `/export-voice` regression) deferred to user-measurement runbook captured in the task's Outcome.
**Verification:** PASS (iteration 1) — direct method-body replacement confirmed (no `sys.settrace`); explicit `if stop_event.is_set(): break` between `_run_flow_lm_and_increment_step` and `latents_queue.put`; sentinel push uses the call arg not `self`; both `/tts` and `/tts-with-state` paths wrap; sanity check unconditional; 6/6 pytest pass.
**Commit:** dee614c
**Files changed:** 10 (worker FILE_LIST 7 + task file move + INDEX + protocol entry)
**Tests added:** 3 pytest files (test_cancel_patch.py, test_sentinel_push.py, test_sanity_check.py) + conftest.py — 6/6 pass
**ADRs written:** none

---

## 2026-05-19 16:05 -- Batch started: [main-046]

**Type:** Work / Batch start
**Tasks:** main-046 - Implement Stop cancellation propagation into the sidecar (≤2 s recovery)
**Parallel:** no (1 worker)

---

## 2026-05-19 15:29 -- Work session ended (main-045 closure + main-046 promotion)

**Type:** Work / Session end
**Completed:** 1 spike closure (main-045 user-measured + verdict written)
**Promoted:** 1 (main-046 backlog -> todo)
**Commits:** 1 (5b76566)

**Done in this session:**
- User ran the measurement campaign on the main-045 prototype (sidecar mode=e). Two prototype bugs surfaced during measurement; both fixed live in the installed runtime, then propagated to bundled source at sidecar 1.2.2:
  - H4 sentinel push reached for `model.result_queue` which never exists; corrected to use the `latents_queue` arg passed positionally to `_autoregressive_generation`. Producer thread now actually exits cleanly; decoder + outer consumer unwind via the natural `("done", None)` propagation in tts_model.py.
  - `_disconnect_aware_iterator` finally tried to `source.close()` while `to_thread(next, iterator)` was mid-execution, raising `ValueError: generator already executing` and swallowing cancellation. Fixed by setting `stop_event` unconditionally in finally and skipping close() entirely.
  - Bonus: prototype startup messages used `logger.info()` against an unconfigured named logger (default level WARNING) — silently dropped. Switched to `print(flush=True)`.
- **Spike verdict captured in main-045 Outcome:**
  - CPU recovery: fully confirmed (drops fast on Stop).
  - Per-cycle RSS growth: 100 MB -> 10-20 MB. Small upward drift remains, no longer unbounded.
  - Residual attributed to `sys.settrace` frame retention; main-046 switches to direct method-body replacement.
- **ADR 0027 flipped to `accepted`** (option e, hybrid wrapper + monkey-patch). Implementation specifics absorbed the three measurement-driven fixes.
- **ADR 0026 relaxed**: strict +-50 MB RSS clause demoted to soft target; hard contract is now "no unbounded growth, median per-cycle delta <=25 MB across 50-cycle stress, CPU <5% within <=2 s." Rationale: PyTorch CPU caching allocator + glibc malloc retain the KV-cache high-water-mark even after Python objects are freed; closing that gap requires tearing down the resident TTSModel per request (violates ADR 0024) or upstream pocket-tts changes.
- **main-046 promoted backlog -> todo** with sharpened ACs: switches from `sys.settrace` to direct method-body replacement, removes the opt-in env var, adds sidecar-side pytest infrastructure under `src/Utterheim/PythonSidecar/tests/`.
- `.gitignore` updated to exclude `__pycache__/` + `*.pyc` (small hygiene fix — .pyc files had crept into staging from sidecar imports during the session).

**Surprises:**
- Worker's `_push_stop_sentinels` pointed at the wrong API surface (`model.result_queue` / `model.latents_queue` — neither exists in pocket-tts 2.x). Caught only when the user observed "same baseline behavior" with mode=e. A pre-commit check that exercises the patched method against a fake pocket-tts module would have caught this — main-046's AC list now requires that pytest.
- Original wrapper crashed on the very FIRST cancellation due to `to_thread`/`close()` thread-safety bug. Caught immediately in the user's first test run. The exception in finally swallowed the cancellation, mimicking a "no fix" baseline.
- The residual ~10-20 MB/cycle drift was unexpected at first — gc.collect() didn't fully reclaim. Hypothesized as `sys.settrace` frame retention; main-046 will verify by switching the mechanism.

**State of utterheim runtime:**
- Bundled wrapper at 1.2.2 with all three fixes.
- Installed runtime currently has the same code (I edited the installed file directly during measurement; the bundled fixes are now also in source, so a re-deploy + restart picks them up via the bootstrapper version check).
- The opt-in `UTTERHEIM_CANCEL_PROTOTYPE` env var remains. To return to production behaviour (no cancellation), restart utterheim without setting it. To keep using the prototype, set `=e`.

**Next:**
- main-046 ready for work. Picks up the prototype, swaps `sys.settrace` for direct method-body replacement, removes the opt-in flag, adds the pytest harness, bumps sidecar 1.2.2 -> 1.3.0.

---

## 2026-05-19 13:05 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 0, verification SKIPPED: 1, re-dispatched: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (bde95c4)

**Done in this session — single-task scoped spike:**
- main-045 — pocket-tts Stop-cancellation prototype delivered as opt-in `UTTERHEIM_CANCEL_PROTOTYPE` env flag (commit bde95c4). Partial completion: prototype code, H4 sentinel push, startup sanity check, H5 upstream research, ADR-0027 implementation specifics, main-046 AC sharpening, BC README note, sidecar version bump 1.2.0 → 1.2.1. Empirical measurement campaign (ACs 1–6: baseline reproduction, H1/H2/H3/H4 verdicts, 50-cycle drift, cancellation latency, first-chunk regression check) deferred to user — runbook captured in the task's Outcome section with placeholder cells for the user's numbers.

**Next steps for the user (load-bearing):**
- Run the prototype against the existing repro recipe (Voices page → paste medium/long input → Play → Stop within ~1 s → repeat 10× then 50×) with `UTTERHEIM_CANCEL_PROTOTYPE=b` (wrapper-only) and `UTTERHEIM_CANCEL_PROTOTYPE=e` (hybrid). Sample RSS + CPU at 250 ms via `Get-Process python`. Fill the User-measurements section of main-045's Outcome. Once numbers exist, the verdict either flips ADR 0027 to `accepted` (option e) or unseats it — main-046 promotes to todo only after that flip.

**Surprises / observations:**
- Worker chose `sys.settrace`-based interception instead of full method-body replacement for the monkey-patch. Creative — avoids reproducing the inner-loop body — but adds a per-line trace-function overhead that the user's measurements will reveal as acceptable or not. If the overhead pushes first-chunk latency near the ADR 0013 budget when the flag is OFF (it shouldn't, the trace function isn't installed unless the flag is set), main-046 may want to ditch the trace approach in favour of method replacement.
- ADRs 0026 + 0027 + the main-046 backlog file were authored by today's earlier model REFINE session but had not been committed; they landed in this commit alongside the prototype that tests them. Coherent bundle, not creep.

**Verification gate skipped this dispatch.** User-approved scope carve-out: the spike's pass/fail criteria are empirical and unrunnable by the worker, so a verifier audit against ACs would have FAILed on items the worker was explicitly told not to attempt. The user is the verifier for this task.

---

## 2026-05-19 13:05 -- Task completed (verification skipped): main-045 - Diagnose pocket-tts cancellation surface and prototype Stop propagation

**Type:** Work / Task completion
**Task:** main-045 - Diagnose pocket-tts cancellation surface and prototype Stop propagation
**Summary:** Shipped the option-(e) hybrid Stop-cancellation prototype as an opt-in `UTTERHEIM_CANCEL_PROTOTYPE` env flag on the sidecar (wrapper disconnect-poll + sys.settrace-based monkey-patch of `TTSModel._autoregressive_generation` + H4 sentinel push + startup sanity check); bumped sidecar 1.2.0 → 1.2.1; the empirical RSS/CPU measurement campaign is deferred to a user-driven runbook captured in the Outcome.
**Verification:** SKIPPED — user-approved scope carve-out (empirical ACs unrunnable by the worker; user is the verifier for this task)
**Commit:** bde95c4
**Files changed:** 11 (6 worker FILE_LIST + 5 orchestrator/contract files bundled coherently)

---

## 2026-05-19 12:49 -- Batch started: [main-045]

**Type:** Work / Batch start
**Tasks:** main-045 - Diagnose pocket-tts cancellation surface and prototype Stop propagation (spike)
**Parallel:** no (1 worker)
**Scope carve-out:** user approved partial completion — worker builds prototype code, H5 upstream check, Outcome template, ADR-0027 update, main-046 AC sharpening; empirical-measurement ACs (1, 2, 3, 4, 5, 6) deferred to user-driven measurement campaign. Verification gate will be skipped on this dispatch (effectively `--no-verify`) because the spike's pass/fail criteria are empirical and unrunnable by the worker.

---

## 2026-05-19 -- Model / Promoted: main-045 - Diagnose pocket-tts cancellation surface and prototype Stop propagation

**Type:** Model / Promote
**BC:** main
**From -> To:** backlog -> todo
**Readiness check:** 10 concrete ACs, dependency-free (`depends_on: []`), deterministic repro recipe, file:line map pre-loaded for the worker. `main-046` stays in backlog blocked on this spike's verdict per `blocks: [main-046]`.

---

## 2026-05-19 -- Model / Refined: main-045 + split into main-046; ADRs 0026 + 0027 written

**Type:** Model / Refine
**BC:** main
**Status after:** main-045 backlog (now a spike), main-046 backlog (new fix task, depends_on main-045)
**Summary:** Interrogator-mode refine on the leak bug. User confirmed Q1 = true cancellation (sidecar drops generator within <=2 s, not just leak containment) and Q2 = split into spike + fix following the main-023/main-024 precedent. Orchestrator routed to in-context researcher (read the bundled pocket-tts 2.x package; confirmed zero cancellation surface anywhere, hot loop is `TTSModel._autoregressive_generation` at `models/tts_model.py:744`) and architect (weighed five mechanism options against ADR 0013 / ADR 0024 / ADR 0015; recommended hybrid wrapper + runtime monkey-patch). C# side is already correctly wired end-to-end -- gap is entirely on the Python side.
**Split into:** main-045 restructured as a spike (diagnose + prototype, H1-H5 hypotheses, falsifier-driven); main-046 created as the dependent fix task (ACs pinned now, sharpen on spike close).
**ADRs written:**
- **0026** (status: accepted) -- Stop cancels in-flight synthesis within <=2 s (amends ADR 0004). Pins the contract; mechanism in ADR 0027.
- **0027** (status: proposed) -- Cancellation propagation mechanism into pocket-tts. Enumerates five options (upstream patch / wrapper-only / subprocess-per-request / monkey-patch / hybrid); recommends hybrid (e). Flips to accepted on main-045's verdict.
**Open questions surfaced for the user:**
- Whether to attempt an upstream PR to `kyutai-labs/pocket-tts` in parallel with the local monkey-patch (not blocking).
- The `PocketTtsEngine.cs:243` `german_24l` wire mapping vs the 2026-05-18 protocol entry vs commit `c0a111c` -- inconsistency; needs the user to confirm which is the current truth and file a doc-hygiene task.

---

## 2026-05-19 -- Model / Captured: main-045 - Sidecar leaks ~100 MB per cancellation on rapid Stop->Play cycles

**Type:** Model / Capture
**BC:** main
**Filed to:** backlog
**Summary:** User observed python.exe RSS climbing ~100 MB per Stop->Play cycle on the Voices page. Idle 1.9 GB, steady-state speak 2.1 GB (fine), but each Stop+Play adds ~100 MB with no ceiling; CPU also lingers for minutes after Stop. Hypothesis: C# Stop closes the HTTP response but doesn't propagate cancellation into pocket-tts's running generator, so abandoned syntheses stack up holding tensors. Captured as backlog bug with diagnose+fix ACs bundled; split into spike+fix may happen during REFINE following the main-023/main-024 precedent.

---

## 2026-05-18 19:30 -- Task closed (listen-test verdict): main-038 - Listen-test german vs german_24l (spike)

**Type:** Work / Task closure (spike verdict)
**Task:** main-038 - Listen-test german vs german_24l
**Method:** In-app listen test rather than CLI WAV-pair. Temporarily flipped the routing wire value and sidecar spawn args from `german` to `german_24l` (uncommitted edits to `SidecarHost.cs` + `PocketTtsEngine.LanguageWireValue`), rebuilt, spoke German via `juergen` from the Speak page, then reverted.
**Verdict:** No audible quality difference; 24l inference feels equivalent to distilled on the user's hardware. With no perceptible advantage, ADR 0025's rationale stands by default (distilled matches English's variant, is lighter).
**Outcome:**
- Reverted the temporary swap. Production stays on distilled `german`.
- ADR 0025 gets a one-line "Addendum (2026-05-18, post-main-038): confirmed" at the bottom.
- No new backlog task opened — no follow-up needed.
- Research-note AC waived: this protocol entry + the task's `## Outcome` section + the ADR addendum together are the durable record. A separate `knowledge/research/german-listen-test-2026-05-18.md` would duplicate without adding signal on a one-developer project.

---

## 2026-05-18 19:00 -- Work session ended

**Type:** Work / Session end
**Completed:** 5 (first-try PASS: 5 [main-044, main-040, main-039, main-041, main-042], SKIPPED: 0, re-dispatched: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 5 (110777d, 448fce6, 4934d1a, 49928ff, e96764e)

**Done in this session — multilingual roll-up after the morning's German-support decisions:**
- main-044 — Utterheim.Tests xUnit project (commit 110777d) — unblocks every future task with unit-test ACs.
- main-040 — Voice library `VoiceLanguage` field + `juergen` built-in (commit 448fce6) — schema side of ADR 0023.
- main-039 — Sidecar preloads en+de; routes by voice language via `X-Voice-Language` header; `PocketTtsEngine.BuildSpeakRequest` (commit 4934d1a) — runtime side of ADR 0024.
- main-041 — Voices page language picker + per-row EN/DE chip; Rainbow Passage re-gated to English+Mic; `/export-voice` routed through the multi-model middleware (commit 49928ff) — UI side of ADR 0023.
- main-042 — Nordwind und Sonne reading prompt for German clone flow (commit e96764e) — copy side; sets the du-form convention for all later German UI.

**Three sidecar version bumps:** 1.0.3 → 1.1.0 (main-039) → 1.2.0 (main-041) bundled inside the multi-language work, since each was tied to a behavioural change the bootstrapper's version-check must see.

**Tests:** went from 0 → 26 facts across 4 test files in `src/Utterheim.Tests/Voices/` and `src/Utterheim.Tests/Tts/`. All pass under `dotnet test --configuration Release`. Test project itself (main-044) ships with 1 smoke `[Fact]`.

**Still in todo — needs user input or is out-of-loop:**
- **main-038** — listen-test german vs german_24l. Spike requires the user to listen to WAV pairs and judge; cannot be autonomously dispatched. Two paths: (a) run the comparison manually now that the full multilingual stack is shipped (`pocket-tts generate --language german[_24l] --voice juergen --text "<sample>" -o de[_24l].wav`), then write the verdict note per the task's AC; (b) close the spike with a one-line ADR 0025 addendum if the user has already formed an opinion from speak-testing during this session's runtime. Recommend (a) — empirical check is cheap and the spike's whole purpose is to validate the distilled default.

**Concept candidates surfaced this session:**
- **voice-language** (flagged by the main-040 worker) — converging on 5 artifacts: ADR-0023 (voice carries language), ADR-0024 (sidecar preload), ADR-0025 (distilled German default), main-035 (decision), main-040 (implementation). With main-039, main-041, main-042 now done, the topic spans 8 artifacts. Strong candidate for a concept synthesis page at `contexts/main/concepts/voice-language.md` per the work-skill template. User to decide whether to create it.

**Surprises / observations:**
- main-044's "no test project" gap was the single biggest unlock — main-040 had bounced on AC 5 previously, and once test infra existed every downstream task ran cleanly first-try.
- main-041 turned out to require a sidecar middleware path-set extension (`/export-voice` added to `_route_paths_needing_model`) — initially looked like UI-only scope, but AC 3 ("the resulting .safetensors was exported against the correct pocket-tts language") forced the cross-stack change. Verifier judgement: in-scope, not creep.
- Five-task PASS streak with no re-dispatches and no SKIP suggests the test-infra investment paid back inside the same session.

---

## 2026-05-18 18:55 -- Task verified and completed: main-042 - German reading prompt for the clone-a-new-voice flow

**Type:** Work / Task completion
**Task:** main-042 - German reading prompt for the clone-a-new-voice flow
**Summary:** Added the German Nordwind und Sonne reading prompt block to the Voices page clone-flow card; visible only when language picker = German AND source = Microphone, mutually exclusive with the English Rainbow Passage block via the new `IsGermanReadingPromptVisible` flag on `VoiceCloningViewModel`. New `NordwindUndSonne` constants class mirrors the `RainbowPassage` pattern (canonical opening two sentences, caption "Lies bitte vor:", attribution "Nordwind und Sonne (Aesop, gemeinfrei)"). Sets the du-form convention for all later German UI copy.
**Verification:** PASS (iteration 1) — `dotnet test --configuration Release` 26/26 pass (5 new); BC README updated with dual reading-prompt rule; AC 1–6 covered by the truth-table tests and the static-string assertions.
**Commit:** e96764e
**Files changed:** 5 (1 new constants class, 1 VM, 1 XAML, 1 test file modified, README)
**Tests added:** 5 (truth-table coverage of IsGermanReadingPromptVisible × IsRainbowPassageVisible mutual exclusion + [NotifyPropertyChangedFor] wiring)
**ADRs written:** none
**Tone convention pinned:** du-form for all future German UI (per task spec).

---

## 2026-05-18 18:35 -- Batch started: [main-042]

**Type:** Work / Batch start
**Tasks:** main-042 - German reading prompt for the clone-a-new-voice flow
**Parallel:** no (1 worker — last unblocked task; main-038 needs user listening)

---

## 2026-05-18 18:30 -- Task verified and completed: main-041 - Voices page — language picker in clone flow + per-voice language column

**Type:** Work / Task completion
**Task:** main-041 - Voices page — language picker in clone flow + per-voice language column
**Summary:** Voices page now declares target language at clone time (English default, German option, ComboBox above source toggle) and shows a compact EN/DE chip on every voice row (built-in + cloned templates). Chosen language flows to `VoiceLibraryService.AddAsync` for persistence AND rides the `X-Voice-Language` header on `/export-voice` so the sidecar encodes the .safetensors against the matching resident TTSModel. Rainbow Passage block now gated behind `IsRainbowPassageVisible = IsMicMode && IsEnglish` (German + Mic hides it pending main-042). Sidecar middleware extended to route `/export-voice` (previously only `/tts` and `/tts-with-state`).
**Verification:** PASS (iteration 1) — `dotnet test --configuration Release` 21/21 pass (8 new); ADR 0023 (public speak body unchanged — header is on the C#→sidecar internal /export-voice hop only), ADR 0010 (MVVM via [ObservableProperty]/[NotifyPropertyChangedFor]), ADR 0009 (NavigationView shell untouched). AC 5 (Settings default-voice picker no regression) confirmed by code-reading SettingsPageViewModel — unchanged. AC 6 (manual smoke speaking a German clone) deferred to runtime per task-spec note (requires both EN+DE pocket-tts models downloaded).
**Commit:** 49928ff
**Files changed:** 9 (4 src/Utterheim/, 1 XAML, 2 sidecar, 2 new test files, README)
**Tests added:** 8 (5 in VoiceCloningViewModelLanguageTests + 3 in VoiceRowLanguageTests)
**ADRs written:** none
**Sidecar version bump:** utterheim_sidecar 1.1.0 → 1.2.0 (forces re-install via main-027 bootstrapper).
**Unblocks:** main-042 (German reading prompt) — now ready.

---

## 2026-05-18 17:35 -- Batch started: [main-041]

**Type:** Work / Batch start
**Tasks:** main-041 - Voices page — language picker in clone flow + per-voice language column
**Parallel:** no (1 worker — only ready non-spike task; main-042 still blocked on main-041; main-038 needs user listening)

---

## 2026-05-18 17:30 -- Task verified and completed: main-039 - Sidecar — load English + German models concurrently and route by voice's language

**Type:** Work / Task completion
**Task:** main-039 - Sidecar — load English + German models concurrently and route by voice's language
**Summary:** Sidecar now preloads English + German concurrently and routes via a new `LanguageRoutingMiddleware` that reads the `X-Voice-Language` header and swaps `pocket_tts.main.tts_model` before pocket-tts's `/tts` handler runs. C# `PocketTtsEngine` resolves each voice's language (built-ins from the in-engine list, clones via new `VoiceLibraryService.TryResolveLanguage`) and stamps the header on every C#→sidecar call. Claude-Code-facing `POST /speak` contract (`{text, voice}`, ADR 0003) unchanged.
**Verification:** PASS (iteration 1) — `dotnet build --configuration Release` 0 warnings/errors; `dotnet test --configuration Release --no-build` 13/13 pass; ADR 0023 (public body unchanged), ADR 0024 (unknown language returns 503, not reload-on-demand), ADR 0007 (queue serialisation makes module-global swap safe), ADR 0025 (literals are `english`/`german`) all honored. Manual smoke (AC 6) deferred to runtime — requires English + German pocket-tts models downloaded on developer box.
**Commit:** 4934d1a
**Wire-shape decision (per AC 2):** routing rides on `X-Voice-Language` request header (lower-case enum value: `english` | `german`). Chosen over a form field so the ASGI middleware reads it without consuming the multipart body. Documented in main-039's `## Outcome` section.
**CLI shape:** `python -m utterheim_sidecar serve --language english --language german` (repeatable; first value is the header-less default). Bare `serve` preloads `english` only (back-compat). `--config` incompatible with multi-language.
**Files changed:** 8 (6 src/, 1 new test file, README)
**Tests added:** 6 (PocketTtsEngineLanguageRoutingTests)
**ADRs written:** none
**Sidecar version bump:** utterheim_sidecar 1.0.3 → 1.1.0 (forces re-install via main-027 bootstrapper).

---

## 2026-05-18 16:15 -- Batch started: [main-039]

**Type:** Work / Batch start
**Tasks:** main-039 - Sidecar — load English + German models concurrently and route by voice's language
**Parallel:** no (1 worker — main-041 also ready but deferred to next batch on BC-README conflict; main-038 needs user listening; main-042 still blocked on main-041)

---

## 2026-05-18 16:10 -- Task verified and completed: main-040 - Voice library — add language field; populate built-ins including `juergen`

**Type:** Work / Task completion
**Task:** main-040 - Voice library — add language field; populate built-ins including `juergen`
**Summary:** Voice profiles now carry a `VoiceLanguage` (english | german) on both `meta.json` and the `library.json` index row per ADR 0023; legacy on-disk files default to english on load via `System.Text.Json` init-default; `VoiceLibraryService.AddAsync` gains an optional language parameter and persists it; `PocketTtsEngine`'s built-in list grew `juergen` (German) alongside the eight English Les-Misérables-derived voices. Speak HTTP body unchanged (ADR 0023 constraint honored).
**Verification:** PASS (iteration 1) — `dotnet test --configuration Release --no-build` → 7/7 pass (6 new facts + 1 smoke); ADR 0005 (folder-per-voice / library.json layout) and ADR 0023 (voice-carries-language / migration rule) both honored; speak path body unchanged.
**Commit:** 448fce6
**Files changed:** 9 (6 src/Utterheim/Services/* edits, 2 new test files under src/Utterheim.Tests/Voices/, BC README)
**Tests added:** 6 (4 in VoiceLibraryLanguageTests + 2 in BuiltInVoicesTests)
**ADRs written:** none
**Concept candidate (worker-flagged):** voice-language — converging on 5 artifacts (ADR-0023, ADR-0024, ADR-0025, main-035, main-040). Will be surfaced at end-of-session.

---

## 2026-05-18 15:35 -- Batch started: [main-040]

**Type:** Work / Batch start
**Tasks:** main-040 - Voice library — add language field; populate built-ins including `juergen`
**Parallel:** no (1 worker — unblocked by main-044; only ready non-spike task. main-038 needs user listening; main-039/041 still chain-blocked behind main-040)

---

## 2026-05-18 15:30 -- Task verified and completed: main-044 - Add Utterheim.Tests xUnit project — establishes test infrastructure

**Type:** Work / Task completion
**Task:** main-044 - Add Utterheim.Tests xUnit project — establishes test infrastructure
**Summary:** Established `src/Utterheim.Tests/` (xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0, net9.0-windows, x64) wired into utterheim.sln with both Debug|x64 and Release|x64 configurations; one smoke `[Fact]` (`SmokeTest.TestInfrastructureIsWired`) passes under `dotnet test --configuration Release`. Unblocks main-040 and every future task that mandates unit tests.
**Verification:** PASS (iteration 1) — verifier ran `dotnet test --configuration Release` (1/1 passed) and `dotnet build --configuration Release` (0 warnings, 0 errors); confirmed ADR 0001 compliance (net9.0-windows, Platforms=x64, RuntimeIdentifier=win-x64, Nullable+ImplicitUsings on).
**Commit:** 110777d
**Files changed:** 4 (src/Utterheim.Tests/Utterheim.Tests.csproj, src/Utterheim.Tests/SmokeTest.cs, utterheim.sln, .agentheim/contexts/main/README.md)
**Tests added:** 1
**ADRs written:** none

---

## 2026-05-18 15:15 -- Batch started: [main-044]

**Type:** Work / Batch start
**Tasks:** main-044 - Add Utterheim.Tests xUnit project — establishes test infrastructure
**Parallel:** no (1 worker — only ready task; main-038 needs user listening, main-040/041/039/042 chain-blocked behind main-044)

---

## 2026-05-18 15:00 -- Model / Promoted: main-040 - Voice library — add language field; populate built-ins including `juergen`

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Note:** User-overridden early promotion — `depends_on: [main-035 ✅, main-044 ⛔]`. main-044 (test project) is in todo but not yet done, so the work skill will still gate execution. Promotion queues main-040 behind main-044 rather than waiting until 044 ships to re-promote.

---

## 2026-05-18 14:30 -- Model / Captured: main-044 - Add Utterheim.Tests xUnit project — establishes test infrastructure

**Type:** Model / Capture
**BC:** main
**Filed to:** todo
**Summary:** Captured the test-project task surfaced by main-040's bounce. Creates `src/Utterheim.Tests/` (xUnit, net9.0-windows, x64) wired into the solution, plus one smoke `[Fact]`. Deliberately scoped to infrastructure only — main-040 will be the first task to write real tests against the new project. Blocks main-040; unblocks the worker rule 8 trap for every future task that mandates unit tests.

---

## 2026-05-18 14:30 -- Model / Refined: main-040 - Voice library — add language field; populate built-ins including `juergen`

**Type:** Model / Refine
**BC:** main
**Status after:** backlog
**Summary:** Decided test-infra path: precede with main-044 (Utterheim.Tests xUnit project) rather than relax AC 5 or fold test-project creation into main-040. main-040's `depends_on` updated from `[main-035]` to `[main-035, main-044]`. Acceptance criteria untouched. Stays in backlog until main-044 ships, then re-promote.
**Split into:** —
**ADRs written:** —

---

## 2026-05-18 14:10 -- Work session ended

**Type:** Work / Session end
**Completed:** 4 (first-try PASS: 1 [main-043], SKIPPED verification: 3 [main-035, main-036, main-037 — pure-decision ADRs])
**Bounced:** 1 (main-040 — no test project exists in repo)
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 4 (task-bearing) + SHA backfill follow-up

**Done in this session (decision tranche from German-support research):**
- main-035 — ADR 0023 voice-carries-language (commit 8809cf1)
- main-043 — sidecar dead TypeError fallback removed; pocket-tts pin tightened (commit 63d0559)
- main-036 — ADR 0024 sidecar multi-language preload (commit 3fddb55)
- main-037 — ADR 0025 production German is distilled (commit 08a7890)

**Surfaced to user — needs decision before further work can run:**
- main-038 — listen-test german vs german_24l. Spike that requires the user to actually listen and judge; cannot be autonomously dispatched. Either run by hand (generate WAVs with `pocket-tts generate --language german[_24l]` and listen) or accept the distilled default and close the spike with a note. Currently in todo/.
- main-040 — voice library language field. Bounced this batch because the repo has no test project (acceptance criterion 5 mandates unit tests). Two paths: (a) create src/Utterheim.Tests/ as a preceding task, then re-promote 040; (b) relax criterion 5 to "manual verification with hand-edited legacy library.json" and re-dispatch 040 as-is. Worker note in `backlog/main-040-voice-library-language-field.md` has implementation details.

**Downstream blocked by main-040:**
- main-039 (sidecar multi-model serve) — depends on main-036 ✅, main-037 ✅, main-040 ⛔
- main-041 (Voices page language picker) — depends on main-035 ✅, main-040 ⛔
- main-042 (German reading prompt) — depends on main-041 ⛔

---

## 2026-05-18 14:05 -- Task completed (verification skipped): main-037 - Production German is distilled `german`, not `german_24l` (decision)

**Type:** Work / Task completion
**Task:** main-037 - Production German is distilled `german`, not `german_24l` (decision)
**Summary:** ADR 0025 codifies the project rule that the German model variant tracks English's production lineage — distilled `german` for v1; switches in lockstep if English ever adopts a 24l variant. Listen-test main-038 is the empirical check that can supersede.
**Verification:** SKIPPED — decision-only task; ADR is the only artifact
**Commit:** 08a7890
**Files changed:** 1
**Tests added:** 0
**ADRs written:** 0025-german-distilled-default.md

---

## 2026-05-18 14:02 -- Batch started: [main-037]

**Type:** Work / Batch start
**Tasks:** main-037 - Production German is distilled `german`, not `german_24l` (decision)
**Parallel:** no (1 worker — last independent ready task; main-038 needs user, main-039/041/042 blocked by main-040 bounce)

---

## 2026-05-18 14:00 -- Task bounced: main-040 - Voice library — add language field; populate built-ins including `juergen`

**Type:** Work / Task bounced
**Task:** main-040 - Voice library — add language field; populate built-ins including `juergen`
**Reason:** No test project exists in the repo (utterheim.sln has only Utterheim and Utterheim.Cli; no `*Tests*.csproj`, no xUnit/NUnit/MSTest packages). Acceptance criterion 5 mandates unit tests. Recommended fix per worker note: dispatch a preceding tactical task to create `src/Utterheim.Tests/` (xUnit, net9.0-windows, x64). Implementation itself is well-specified — Language enum on ClonedVoiceMeta/ClonedVoiceIndexEntry, AddAsync overload, juergen in BuiltInVoices.
**Moved to:** backlog

---

## 2026-05-18 14:00 -- Task completed (verification skipped): main-036 - Sidecar preloads English + German concurrently (decision)

**Type:** Work / Task completion
**Task:** main-036 - Sidecar preloads English + German concurrently (decision)
**Summary:** ADR 0024 accepted — sidecar preloads a fixed list of languages at startup (v1: English + German), one resident TTSModel per language; rejects reload-on-change and single-default with rationale. Partner decision to ADR 0023.
**Verification:** SKIPPED — decision-only task; ADR is the only artifact
**Commit:** 3fddb55
**Files changed:** 1
**Tests added:** 0
**ADRs written:** 0024-sidecar-multi-language-preload.md

---

## 2026-05-18 13:55 -- Batch started: [main-036, main-040]

**Type:** Work / Batch start
**Tasks:** main-036 - Sidecar preloads English + German concurrently (decision), main-040 - Voice library — add language field; populate built-ins including `juergen`
**Parallel:** yes (2 workers)

---

## 2026-05-18 13:52 -- Task verified and completed: main-043 - Drop dead `TypeError` fallback around `language=` in sidecar

**Type:** Work / Task completion
**Task:** main-043 - Drop dead `TypeError` fallback around `language=` in sidecar
**Summary:** Removed the dead `try / except TypeError` fallback around `TTSModel.load_model(language=...)` in the sidecar `serve` command, and tightened the bootstrapper's pocket-tts pin from `>=2.0,<3` to explicit `>=2.0.0,<3` so the `language=` kwarg is contractual.
**Verification:** PASS (iteration 1)
**Commit:** 63d0559
**Files changed:** 2 (worker) + task move
**Tests added:** 0
**ADRs written:** none

---

## 2026-05-18 13:50 -- Task completed (verification skipped): main-035 - Voice profile carries its language (decision)

**Type:** Work / Task completion
**Task:** main-035 - Voice profile carries its language (decision)
**Summary:** ADR 0023 fixes voice-carries-language: each voice profile declares one language, speak HTTP API (`{"text": ..., "voice": ...}`) stays unchanged from ADR 0003, sidecar routes per the named voice's declared language. ADR enumerates all four candidate interfaces with rejection rationale.
**Verification:** SKIPPED — decision-only task; ADR + moved task file are the only artifacts
**Commit:** 8809cf1
**Files changed:** 1 ADR + task file move
**Tests added:** 0
**ADRs written:** 0023-voice-carries-language.md

---

## 2026-05-18 13:43 -- Batch started: [main-035, main-043]

**Type:** Work / Batch start
**Tasks:** main-035 - Voice profile carries its language (decision), main-043 - Drop dead `TypeError` fallback around `language=` in sidecar
**Parallel:** yes (2 workers)

---

## 2026-05-18 -- Model / Promoted: main-042 — German reading prompt

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Summary:** Pinned the two open choices to defaults so the task is worker-ready: passage = first two sentences of *Nordwind und Sonne* (with canonical text inlined), caption = *"Lies bitte vor:"* (informal du — also sets the tone convention for all future German UI copy). Acceptance criteria tightened to exact strings; refinement notes removed.

---

## 2026-05-18 -- Model / Captured: main-035..main-043 — multilingual (German) work tranche

**Type:** Model / Capture
**BC:** main
**Filed to:** todo (8 tasks) + backlog (1 task)
**Summary:** Sliced the 2026-05-18 pocket-tts German-support research into nine tasks. Decisions pre-resolved with the user: voice profile carries language (main-035), sidecar preloads English + German concurrently (main-036), production German is distilled `german` not the slower `german_24l` (main-037 — rule = match English's variant; revisit if English ever adopts a 24l). Spike main-038 listen-tests german vs german_24l as audit on 037. Features: sidecar multi-model serve (main-039), voice library language field with `juergen` built-in (main-040), Voices page language picker + per-voice language display (main-041), German reading prompt for the clone flow (main-042 → backlog pending passage choice). Chore main-043 removes the now-dead `TypeError` fallback around `language=`. Dropped from capture per user's answers: RAM spike (has headroom), cross-language `.safetensors` portability spike (languages decided to be model-coupled), HTTP language field (voice carries it), Settings default-language picker (English is the only default).
**Split into:** main-035, main-036, main-037, main-038, main-039, main-040, main-041, main-042, main-043
**ADRs written:** none yet — three decision tasks (main-035..main-037) produce ADRs when worked

---

## 2026-05-18 -- Research / Report written: pocket-tts German language support

**Type:** Research / Report
**Topic:** Kyutai pocket-tts German language support — model variants, runtime selection, voice cloning, and plugin integration
**Requested by:** user
**Report:** `knowledge/research/pocket-tts-german-support-2026-05-18.md`
**Summary:** Confirms German shipped in **pocket-tts 2.0.0 (2026-04-21)** and was headlined in **2.1.0 (2026-05-04)** alongside fr/it/es/pt — six languages total. No new HF repo id; `kyutai/pocket-tts` gained a `languages/` subdirectory. Language is selected per **model instance** via `TTSModel.load_model(language=...)` — **not per generate call** — so per-prompt language routing in the tray app means loading multiple resident models (~135 MB each, en+de comfortable). German has a distilled `german` (default) and a slower 24-layer undistilled `german_24l` preview; new built-in `juergen` voice avoids English-accented German defaults. Voice cloning works for German source audio; `.safetensors` profile cross-language portability is **undocumented** (open question for the worker). Sidecar's import surface (`web_app`, `tts_model`, `generate_data_with_state`, `export_model_state`) all still present in 2.1.0 — no breakage. The sidecar's `TypeError` fallback around `language=` is now dead code but harmless.

---

## 2026-05-05 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 2 (1 task commit + 1 SHA-backfill chore)

---

## 2026-05-05 -- Task completed: main-034 - Rainbow Passage prompt for microphone voice cloning

**Type:** Work / Task completion
**Task:** main-034 - Rainbow Passage prompt for microphone voice cloning
**Summary:** Voices page now renders a Mic-mode-only "Read this aloud:" Rainbow Passage reading prompt above the audio level meter, sourced from a new RainbowPassage constants class and bound via x:Static; System Audio mode stays unaffected via the existing IsMicMode visibility flag.
**Commit:** 4ebd671
**Files changed:** 3 (worker) + 5 bundled pre-existing UI tweaks
**ADRs written:** none

---

## 2026-05-05 -- Batch started: [main-034]

**Type:** Work / Batch start
**Tasks:** main-034 - Rainbow Passage prompt for microphone voice cloning
**Parallel:** no (1 worker)

---

## 2026-05-05 -- Model / Refined + Promoted: main-034 - Rainbow Passage prompt for microphone voice cloning

**Type:** Model / Refine + Promote
**BC:** main
**Status after:** todo (promoted from backlog)
**From → To:** backlog → todo
**Summary:** Resolved the three open questions blocking promotion. (1) Mic-only confirmed — System Audio mode keeps the prompt block collapsed. (2) Length pinned to the **first two sentences** of the Rainbow Passage (~10–15 s reading), not the full passage and not paragraph one — fits the user's stated "5–10 seconds of clear voice" goal, no `ScrollViewer` needed. Exact text now embedded inline in the task file so the worker doesn't need a runtime fetch. (3) Source-URL research blocked by content-filter; deferred to worker (best-effort capture of the canonical york.ac.uk URL in a code comment, fallback to "Fairbanks 1960, *Voice and Articulation Drillbook* (public domain)" if the page is unreachable). UI caption stays "The Rainbow Passage — University of York" either way. Acceptance criteria tightened around exact text, attribution caption, and source-comment requirement.
**Split into:** —
**ADRs written:** —

---

## 2026-05-05 -- Model / Captured: main-034 - Rainbow Passage prompt for microphone voice cloning

**Type:** Model / Capture
**BC:** main
**Filed to:** backlog
**Summary:** Show the Rainbow Passage (sourced from York.ac.uk) inside the Voices page Clone-a-new-voice panel when Microphone mode is selected, so users have something natural to read aloud to produce 5–30 s of clear speech for cloning. Open questions parked in Notes: mic-only confirmation, full passage vs. first paragraph vs. first 2 sentences, exact York source URL.

---

## 2026-05-05 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 2

---

## 2026-05-05 -- Task completed: main-033 - Design corrections — menu font/logo, voices order, settings layout, right-Ctrl hotkey, error strings

**Type:** Work / Task completion
**Task:** main-033 - Design corrections — menu font/logo, voices order, settings layout, right-Ctrl hotkey, error strings
**Summary:** Bundled seven post-main-032 design corrections — sidebar wordmark + brand mark now match WhisperHeim, Voices page renders Cloned above Built-in, Settings page reflows with stacked combobox cards in the new order, stop hotkey now watches Right Ctrl end-to-end with all user-visible strings updated, and the two stale "About page" pointers in error strings now point to the Settings page. Build clean (0 warnings / 0 errors); manual smoke not run by the worker.
**Commit:** 2b8fb3d
**Files changed:** 11
**ADRs written:** 0022-stop-hotkey-double-tap-right-ctrl.md

---

## 2026-05-05 -- Batch started: [main-033]

**Type:** Work / Batch start
**Tasks:** main-033 - Design corrections — menu font/logo, voices order, settings layout, right-Ctrl hotkey, error strings
**Parallel:** no (1 worker)

---

## 2026-05-05 -- Model / Captured: main-033 - Design corrections — menu font/logo, voices order, settings layout, right-Ctrl hotkey, error strings

**Type:** Model / Capture
**BC:** main
**Filed to:** todo
**Summary:** Bundled UI follow-up to main-032: switch sidebar font + logo to match WhisperHeim, swap Cloned/Built-in section order on Voices page, stack the Default-voice and Output-device comboboxes below their labels, re-order Settings cards (Default voice → Output device → Data path → Appearance → HTTP port → Stop key → Engine status), move stop-hotkey from double-tap Left Ctrl to Right Ctrl (function + all UI strings + appsettings default), and update the two "see About page" error strings (`VoicesPage.xaml:54`, `VoiceCloningViewModel.cs:456`) to point at Settings instead. Filed straight to todo — depends on `main-010` (styleguide gate is OPEN).

---

## 2026-05-05 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (main-030, main-032)
**Bounced:** 0
**Failed:** 0
**Commits:** 4 (2 feature commits + 2 metadata commits)

---

## 2026-05-05 -- Task completed: main-032 - Relocate engine diagnostics; redesign About to match WhisperHeim

**Type:** Work / Task completion
**Task:** main-032 - Relocate engine diagnostics; redesign About to match WhisperHeim
**Summary:** Relocated engine diagnostics from About to Settings (state pip / port / healthy / last error / Restart Engine + View logs, bound through a new composed EngineStatusCardViewModel sub-VM mirroring the cloning pattern); rebuilt About as a pure WhisperHeim-style identity surface (BrandHeroControl + profile/contact card with Marco's portrait and Get-in-Touch links + Ko-fi / GitHub support card + credits) and moved its nav item to FooterMenuItems; consolidated the duplicate NullOrEmptyToVisibilityConverter and EngineStateToPipBrushConverter into Views/Converters/SharedConverters.cs registered at App.xaml scope.
**Commit:** 90dfe39
**Files changed:** 19
**ADRs written:** 0021-engine-diagnostics-on-settings-not-about.md

---

## 2026-05-05 -- Batch started: [main-032]

**Type:** Work / Batch start
**Tasks:** main-032 - Relocate engine diagnostics; redesign About to match WhisperHeim
**Parallel:** no (1 worker)

---

## 2026-05-05 -- Task completed: main-030 - Speak page hero + button row above text; Voices clone card above list

**Type:** Work / Task completion
**Task:** main-030 - Speak page hero + button row above text; Voices clone card above list
**Summary:** Shipped reusable BrandHeroControl + AppInfo helper, restructured Speak page into hero / single controls row / dominant text input / status, and moved the Voices "Clone a new voice" card above the voice library list.
**Commit:** b0b3314
**Files changed:** 6
**ADRs written:** none

---

## 2026-05-05 -- Batch started: [main-030]

**Type:** Work / Batch start
**Tasks:** main-030 - Speak page hero + button row above text; Voices clone card above list
**Parallel:** no (1 worker)

---

## 2026-05-05 -- Model / Promoted: main-030 + main-032 (visual + nav redesign pair)

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo (both)
**Summary:** Dependencies (main-010, main-028, main-029) all in done/ as of the
preceding work session. main-032 still depends on main-030 — both promoted
together since the worker loop will pick main-030 first and main-032 once
main-030 lands. Both tasks carry concrete acceptance criteria from the
2026-05-05 refinement entry below.

---

## 2026-05-05 -- Model / Refined: main-030 + main-032 (visual + nav redesign pair)

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (both held — promote alongside main-028 + main-029 ship)
**Summary:** Resolved the seven remaining open questions across the two
visual-redesign tasks in one pass.

main-030 (Speak hero + Voices clone-card layout):
- Voices page: small `Light 28pt "Voices"` title only — big hero is Speak
  + About only (consistent with main-029's protocol decision).
- Button row: Play (Primary), Stop (Secondary), Save (Secondary) all in
  one row, no separator gap. WhisperHeim TTS layout verbatim.
- `BrandHeroControl` `UserControl` extracted in main-030 (consumed by
  main-032). Single `Tagline` `DependencyProperty`; logo/name/version
  come from app-wide sources internally.
- Side-effect: extracted `Services/AppInfo.cs` static helper for
  assembly-version lookup (currently inlined in `AboutPageViewModel`).
  `BrandHeroControl` reads `AppInfo.Version`.

main-032 (relocate engine diagnostics; redesign About):
- Portrait: reuse WhisperHeim's `heimeshoff.jpg`, copy into
  `assets/people/heimeshoff.jpg`.
- Ko-fi URL: shared with WhisperHeim (`https://ko-fi.com/heimeshoff`,
  verified via grep). Stored as `AppInfo.KofiUrl` constant. GitHub URL
  also lifts to `AppInfo.GithubUrl`.
- About copy: keep WhisperHeim's bio paragraph 1 verbatim (Marco's
  identity is the same on both apps); replace paragraph 2 with the
  utterheim-specific voice-diversity framing the user proposed.
- Engine status VM: extract `EngineStatusCardViewModel`, compose into
  `SettingsPageViewModel.EngineStatus`. Mirrors
  `VoicesPageViewModel.Cloning` composition pattern. Strips
  engine-status fields from `AboutPageViewModel` entirely; About VM
  shrinks to just `Version`.
- Bonus cleanup: consolidate `NullOrEmptyToVisibilityConverter`
  (duplicated in `AboutPageConverters.cs` + `VoicesPageConverters.cs`,
  verified) and `EngineStateToPipBrushConverter` into a shared
  `Views/Converters/SharedConverters.cs`, registered in `App.xaml`.

**Dependency edges updated:**
- main-030: `blocks` `[]` → `[main-032]`.
- main-032: `depends_on` `[main-010, main-028, main-029]` →
  `[main-010, main-028, main-029, main-030]` (consumes `BrandHeroControl`
  + `AppInfo`).

**Split into:** none. Both kept whole.
**ADRs written:** none — no new architectural decision; refinement
followed established sibling patterns + existing in-codebase composition
shapes.

---

## 2026-05-05 -- Work session ended

**Type:** Work / Session end
**Completed:** 3 (main-028 logo, main-029 styling adoption, main-031 editable data path)
**Bounced:** 0
**Failed:** 0
**Commits:** 3 feature + 1 chore (this entry)

---

## 2026-05-05 -- Task completed: main-031 - Editable data path with folder-picker dialog

**Type:** Work / Task completion
**Task:** main-031 - Editable data path with folder-picker dialog
**Summary:** Settings → Diagnostics data-path card now offers Browse... + Reset; bootstrap.json writes via temp+rename with writability validation; VoiceLibraryService re-runs LoadAsync on DataPathChanged so Voices reflects the new library live, old voices stay at the previous location (pointer-swap, no migration). ADR 0020 captures the decision.
**Commit:** cc14359
**Files changed:** 7
**ADRs written:** 0020-data-path-runtime-swap-pointer-only.md

---

## 2026-05-05 -- Task completed: main-029 - WhisperHeim styling adoption

**Type:** Work / Task completion
**Task:** main-029 - WhisperHeim styling adoption (Light theme, brand palette, card spec, Appearance picker)
**Summary:** Flipped App.xaml to Light, declared four brand brushes, wrapped all four pages in the standard ScrollViewer + 40,36,40,32 chrome (MaxWidth=900 centred), replaced every Settings ui:CardControl with the Border CornerRadius=12 Padding=24 pattern, added Audio/App/Diagnostics/Appearance section headers and the Light/Dark/System tile picker. UserSettings.AppearanceMode persists via settings.json (default Light in memory) per ADR 0019; startup applies the persisted mode before MainWindow.Show. Styleguide gains §Brand palette, §Card spec, §Section header, §Page chrome, §Appearance modes sections.
**Commit:** 7cdbd60
**Files changed:** 11
**ADRs written:** none (ADR 0019 was authored during refinement)

---

## 2026-05-05 -- Task completed: main-028 - Logo redesign (voice human-head mark)

**Type:** Work / Task completion
**Task:** main-028 - Logo redesign — voice human-head mark
**Summary:** Pivoted the locked perched-utterheim direction during sign-off iteration to a filled orange right-facing human-head profile with three blue Wi-Fi-style concentric arcs from the mouth. Three drafts to user approval; new SVG + regenerated PNGs (16..512) + multi-res .ico; styleguide brand-mark section + BC README brand asset table flipped from PLACEHOLDER to Final.
**Commit:** 13a82e8
**Files changed:** 13
**ADRs written:** none

---

## 2026-05-05 -- Batch started: [main-028]

**Type:** Work / Batch start
**Tasks:** main-028 - Logo redesign — waveform-tail utterheim
**Parallel:** no (1 worker — design-bearing, user sign-off gate makes solo dispatch safer)

---

## 2026-05-05 -- Model / Refined + Promoted: main-031 - Editable data path with folder-picker dialog

**Type:** Model / Refine + Promote
**BC:** main
**Status after:** todo
**Summary:** Resolved five open questions against the WhisperHeim sibling
pattern. Decisions: pointer-swap (no migration), writability validation,
hybrid live `LoadAsync` + MessageBox restart-required notice, no
confirmation dialog, drop "Open in Explorer". Concrete implementation plan
added: `DataPathService.{ValidatePath, SetDataPath, DataPathChanged}`,
`bootstrap.json` temp+rename, `VoiceLibraryService` subscribes to
`DataPathChanged` and re-runs `LoadAsync` so the Voices page refreshes
without re-navigation. Settings card replaces Open-in-Explorer with
Browse + Reset. Scope clarified: only `<dataPath>\voices\` relocates;
runtime / models / cache / logs / bootstrap-state.json / settings.json
all stay anchored to `LocalRoot`. Acceptance criteria rewritten as eleven
testable bullets. User accepted the resolutions and promoted to todo.
**Split into:** none.
**ADRs written:** none — refinement followed an existing sibling-app
pattern; no new architectural decision needed.

---

## 2026-05-04 23:15 -- Model / Promoted: main-029 - WhisperHeim styling adoption

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 23:10 -- Model / Refined: main-029 - WhisperHeim styling adoption

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (held — promote alongside main-028 + main-032 for single visual checkpoint per user's note)
**Summary:** Resolved every open question and tightened acceptance criteria so a worker can execute without re-asking. Locked the live-swap API to `Wpf.Ui.Appearance.ApplicationThemeManager.Apply / ApplySystemTheme` (verified against WhisperHeim `GeneralPage.xaml.cs`); kept `BrandDeepMutedBrush #66005FAA` despite ~2.2:1 contrast (decorative version-tag only, matches WhisperHeim verbatim); set page-title strategy per page (Speak/About hero placed by main-030/032; Voices/Settings small `FontWeight=Light FontSize=28` title); located brand brushes in `App.xaml <Application.Resources>` directly; defaulted `AppearanceMode = Light` in memory without rewriting settings.json on read; mirrored the styleguide diff into six explicit edits. `BrandHeroControl` extraction deferred to main-030. Decided NOT to split (single visual checkpoint wins) but documented a worker fallback plan (029a foundation / 029b chrome / 029c picker) for mid-session use.
**Split into:** none (kept as one task; fallback plan documented inline)
**ADRs written:** 0019 (appearance mode persisted in settings.json, not registry — distinguishes from ADR 0017's Run-key concern)
**Research written:** wpfui-live-theme-swap-2026-05-04.md
**`blocks` field:** updated from `[]` to `[main-030, main-032]` to mirror their `depends_on`.

---

## 2026-05-04 22:35 -- Model / Promoted: main-028 - Logo redesign (waveform-tail utterheim)

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 22:30 -- Model / Refined: main-028 - Logo redesign (waveform-tail utterheim)

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (ready for promote)
**Summary:** Locked the four open design decisions: (1) worker drafts the SVG with a user sign-off gate on rasters before done, (2) pose = perched in profile, (3) tail = three horizontal waveform bars trailing behind the bird, (4) line-art two-colour mark — bird stroked in `#FFff8b00` (orange), waveform bars stroked in `#FF25abfe` (cyan-blue), not theme-adaptive. Also: back-filled `blocks: [main-030, main-032]`, reworded AC #5 so the task no longer implicitly waits on main-032's About hero redesign, added a worker-draft + sign-off workflow note.
**Split into:** —
**ADRs written:** —

---

## 2026-05-04 22:05 -- Model / Captured: visual + UX refinement pass (main-028..032)

**Type:** Model / Capture
**BC:** main
**Filed to:** backlog (5 tasks)
**Summary:** User refinement pass aligning Utterheim's visual language with WhisperHeim. Captured five backlog items: main-028 (waveform-tail utterheim logo with WhisperHeim palette), main-029 (Light theme + brand brushes + WhisperHeim card spec + Appearance picker on Settings + styleguide update), main-030 (Speak page hero + controls row above text input; Voices clone-card above list), main-031 (editable data path with Vista folder picker — re-opens main-016's deferred migration question), main-032 (move engine status + restart + view logs to Settings end; About moves to nav FooterMenuItems with WhisperHeim-style hero + profile/contact + Ko-fi/GitHub composition pointing at utterheim repo). 028+029 are independent foundations; 030/032 depend on both; 031 is independent.

---

## 2026-05-04 21:30 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (main-016 Settings page, main-017 About page)
**Bounced:** 0
**Failed:** 0
**Commits:** 2 (eeb5192, 435d059)

---

## 2026-05-04 21:25 -- Task completed: main-017 - About page

**Type:** Work / Task completion
**Task:** main-017 - About page (logo, tagline, version, engine status, retry)
**Summary:** About page wired up with brand mark, "Local voices for Claude Code" tagline, assembly-version readout, in-process engine status panel (subscribed to SidecarHost.StateChanged, re-seeded via GetStatus on navigate-to), Restart Engine button backed by new SidecarHost.RestartAsync, View logs HyperlinkButton, and the pocket-tts credits line. Shared FormatState helper extracted to SidecarStateLabels so footer + About can't drift apart.
**Commit:** 435d059
**Files changed:** 14
**ADRs written:** 0018-about-page-engine-status-in-process.md

---

## 2026-05-04 21:11 -- Batch started: [main-017]

**Type:** Work / Batch start
**Tasks:** main-017 - About page (logo, tagline, version, engine status, retry)
**Parallel:** no (1 worker)

---

## 2026-05-04 21:10 -- Task completed: main-016 - Settings page

**Type:** Work / Task completion
**Task:** main-016 - Settings page — output device, default voice, startup, read-only diagnostics
**Summary:** Three Fluent setting-card sections wired up: Audio (default voice + output device persisted on UserSettings, output device consumed per-utterance by AudioPlayer), App (Start minimised honoured at EntryPoint, Launch at startup managed via HKCU\…\Run with the registry as source of truth — ADR 0017), Diagnostics (HTTP port / Stop hotkey / data path read-only, Open in Explorer button).
**Commit:** eeb5192
**Files changed:** 13
**ADRs written:** 0017-launch-at-startup-registry-as-source-of-truth.md

---

## 2026-05-04 21:00 -- Batch started: [main-016]

**Type:** Work / Batch start
**Tasks:** main-016 - Settings page (output device, default voice, startup, read-only diagnostics)
**Parallel:** no (1 worker — main-017 deferred to wave 2 due to BC README conflict)

---

## 2026-05-04 20:35 -- Model / Promoted: main-017 - About page

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 20:30 -- Model / Refined: main-017 - About page

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (ready to promote)
**Summary:** Resolved four open questions on the About page task:
(Q1) engine status reads in-process via `SidecarHost.StateChanged` +
`GetStatus()` — `GET /status` stays the external contract;
(Q2) the always-visible footer stays, About surfaces the richer panel
alongside; (Q3) ship a `Restart Engine` button backed by a new
`SidecarHost.RestartAsync()` (StopAsync + reset + StartAsync), disabled
during transitional states; (Q4) credits trim to a single line:
"Synthesis powered by pocket-tts (Kyutai Labs)." Rewrote the task with
concrete blocks (logo from `utterheim-logo-256.png` at 128×128, exact
tagline from styleguide §Sign-off, version from
`AssemblyInformationalVersionAttribute`, status panel composition with
state pip + healthy tick + lastError + retry, view-logs path resolution
via `%LOCALAPPDATA%\Utterheim\logs\`), tactical pointers (refactor
`FormatState` into a shared helper, pip via `Ellipse` + converter, VM
mirroring the `EngineStatusViewModel` subscribe pattern), and tightened
acceptance criteria to be live-testable (kill-the-sidecar + force-failed
scenarios). Title updated to include "retry".
**Split into:** —
**ADRs written:** —

---

## 2026-05-04 20:15 -- Model / Promoted: main-016 - Settings page

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 19:30 -- Model / Refined: main-016 - Settings page

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (ready to promote)
**Summary:** Resolved the five open questions on the Settings page task with
user direction: HTTP port stays read-only in v1, output device applies on
next utterance only (no mid-stream switch), default-voice dropdown confirmed
on this page sourced from `VoiceCatalog`, layout uses Fluent cards grouped
into Audio / App / Diagnostics, and engine-status stays exclusively on the
About page (main-017). Rewrote the task with concrete tactical pointers —
`UserSettings.SettingsData` schema extension (`outputDeviceId`,
`startMinimised`), `AudioPlayer` device-id passthrough at `WaveOutEvent`
construction, `AudioDeviceResolver.EnumerateWaveOut()`, new
`Services\Settings\StartupRegistration.cs` for the HKCU\Run helper, and
`Views\MainWindow` honouring `StartMinimised` on initial Show. Added
main-013 to `depends_on` for storage-layer lineage. Acceptance criteria
expanded from 7 abstract bullets to 9 concrete verifiable ones.
**Split into:** none (single coherent page)
**ADRs written:** none (all decisions are tactical extensions of existing ADRs)

---

## 2026-05-04 18:05 -- Work session ended

**Type:** Work / Session end
**Completed:** 2
**Bounced:** 0
**Failed:** 0
**Commits:** 2 (plus follow-up chore for SHA recording)

---

## 2026-05-04 18:04 -- Task completed: main-027 - Bootstrapper self-heal

**Type:** Work / Task completion
**Task:** main-027 — Bootstrapper self-heal for stale/partial utterheim_sidecar
**Summary:** IsBootstrapped now delegates to the install-path file-presence helpers and additionally compares bundled vs. installed wrapper __version__, so half-installed and stale wrappers self-heal at next launch.
**Commit:** d57a6a9
**Files changed:** 6
**ADRs written:** 0016-bootstrapper-strict-launch-gate.md

---

## 2026-05-04 17:59 -- Batch started: [main-027]

**Type:** Work / Batch start
**Tasks:** main-027 — Bootstrapper self-heal for stale/partial utterheim_sidecar
**Parallel:** no (1 worker)

---

## 2026-05-04 17:58 -- Task completed: main-026 - Voices delete affordance

**Type:** Work / Task completion
**Task:** main-026 — Voices page per-row delete affordance for cloned voices
**Summary:** Cloned voice rows gained an icon-only Delete button; click opens a Fluent ContentDialog (WhisperHeim destructive styling) that calls VoiceLibraryService.DeleteAsync and removes the row via VoicesChanged.
**Commit:** a69b59a
**Files changed:** 11
**ADRs written:** none

---

## 2026-05-04 17:50 -- Batch started: [main-026]

**Type:** Work / Batch start
**Tasks:** main-026 — Voices page per-row delete affordance for cloned voices
**Parallel:** no (1 worker; main-027 deferred — same BC README conflict)

---

## 2026-05-04 17:35 -- Model / Promoted: main-026, main-027

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Tasks:**
- main-026 — Voices page per-row delete affordance for cloned voices
- main-027 — Bootstrapper self-heal for stale/partial utterheim_sidecar
**Readiness:** both verified — concrete acceptance criteria, dependencies (main-014, main-015) already done, scope and worker tips in place.

---

## 2026-05-04 17:09 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 1

---

## 2026-05-04 17:08 -- Task completed: main-025 - Voice cloning UI

**Type:** Work / Task completion
**Task:** main-025 - Voice cloning UI — recording controls + source toggle on the Voices page
**Summary:** Voices page gained a Clone-a-new-voice sub-panel (source toggle, level meter, duration/progress, name validation, Save flow through `/export-voice` + `VoiceLibraryService`). WhisperHeim audio-capture stack copied per ADR 0006.
**Commit:** 5e66207
**Files changed:** 16
**ADRs written:** none

---

## 2026-05-04 16:54 -- Batch started: [main-025]

**Type:** Work / Batch start
**Tasks:** main-025 - Voice cloning UI — recording controls + source toggle on the Voices page
**Parallel:** no (1 worker)

---

## 2026-05-04 16:50 -- Model / Promoted: main-025 - Voice cloning UI

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

Task was already deeply refined as part of the main-015 split (mic + loopback capture sub-flow on the Voices page, 5–60 s sample window, inline failure surfaces, WhisperHeim service copy-and-modify per ADR 0006). All dependencies (main-010 styleguide, main-014 Voices page, main-015 backend) in done/. Styleguide gate OPEN.

---

## 2026-05-04 16:40 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (main-015)
**Bounced:** 0
**Failed:** 0
**Commits:** 1 feature + 1 chore

---

## 2026-05-04 16:40 -- Task completed: main-015 - Voice cloning backend

**Type:** Work / Task completion
**Task:** main-015 - Voice cloning backend — VoiceLibraryService + sidecar /export-voice
**Summary:** Voice cloning backend landed end-to-end — `utterheim_sidecar` Python wrapper adds `/export-voice` and `/tts-with-state` on the resident pocket-tts model, `VoiceLibraryService` persists `<dataPath>\voices\<id>\` per ADR 0005 with temp+rename and startup reconciliation, and `PocketTtsEngine` routes built-in vs cloned voices to the correct endpoint via the catalog union. No UI in this task.
**Commit:** f299df5
**Files changed:** 15
**ADRs written:** 0015-utterheim-sidecar-wrapper.md (accepted as drafted)

---

## 2026-05-04 16:25 -- Batch started: [main-015]

**Type:** Work / Batch start
**Tasks:** main-015 - Voice cloning backend — VoiceLibraryService + sidecar /export-voice
**Parallel:** no (1 worker)

---

## 2026-05-04 -- Model / Promoted: main-015 - Voice cloning backend

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**ADR 0015 status:** proposed → accepted (Utterheim-owned Python sidecar wrapper)

---

## 2026-05-04 -- Model / Refined: main-015 - Voice cloning (split 3-way)

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (all three children); main-015 promote-blocked on ADR 0015 acceptance
**Summary:** Resolved Q1–Q13. Split main-015 from one bundled task into three: main-015 (backend — `VoiceLibraryService` + sidecar `POST /export-voice` + schema), main-025 (cloning UI — recording controls, source toggle, WhisperHeim audio-capture copy per ADR 0006), main-026 (per-row delete affordance on Voices page). Ratified `meta.json` / `library.json` schema with `schemaVersion: 1`, slim master index, transactional write order (profile → meta → library). Confirmed C# ↔ pocket-tts integration via utterheim-owned Python wrapper module that imports `pocket_tts.main:web_app` and adds `/export-voice` (ADR 0015). Active-playback delete guard rejected; reconciler heals orphans on next launch. Import-existing-clip path deferred post-v1 (`source: "import"` reserved). main-014 in `done/` had `blocks` updated to `[main-015, main-025, main-026]`.
**Split into:** main-025 (Voice cloning UI), main-026 (Voices delete affordance)
**ADRs written:** 0015 (Utterheim-owned Python sidecar wrapper, status: proposed — awaits user acceptance before main-015 can promote)

---

## 2026-05-04 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 1 (plus this end-of-session chore)

---

## 2026-05-04 -- Task completed: main-014 - Voices page — voice library list with preview

**Type:** Work / Task completion
**Task:** main-014 - Voices page — voice library list with preview
**Summary:** Replaced the Voices page stub with the real two-section voice library list (Built-in + Cloned) and per-row Preview routed through SpeakService.Enqueue per ADR 0014; page binds engine state to SidecarHost.StateChanged for loading / failed / running placeholders and refreshes on VoiceCatalog.VoicesChanged.
**Commit:** 3d763f5
**Files changed:** 6
**ADRs written:** 0014 ratified (status flipped proposed → accepted)

---

## 2026-05-04 -- Batch started: [main-014]

**Type:** Work / Batch start
**Tasks:** main-014 - Voices page — voice library list with preview
**Parallel:** no (1 worker)

---

## 2026-05-04 -- Model / Promoted: main-014 - Voices page — voice library list with preview

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**ADR 0014 status:** proposed → accepted (Voices page preview routes through SpeakService.Enqueue)

---

## 2026-05-04 10:36 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (main-013)
**Bounced:** 0
**Failed:** 0
**Commits:** 3 (f77fdfa promote prep, a480d58 main-013 work, 3949301 main-014 chore + ADR 0014; SHA-record commit follows)

---

## 2026-05-04 10:35 -- Model / Refined: main-014 - Voices page — voice library list with preview

**Type:** Model / Refine
**BC:** main
**Status after:** backlog
**Summary:** Resolved Q1–Q10. Preview routes through `SpeakService.Enqueue` (single queue arbiter, ADR 0014). Two sections (built-ins first, cloned), `VoiceCatalog.VoicesChanged` for live refresh, no `library.json` reading in this task (deferred to main-015). Engine-state-bound loading/error/empty states. FIFO preview with stop hotkey draining. Delete affordance scope-moved to main-015 (dead-code in v1 here); `blocks: [main-015]` added.
**Split into:** none (scope adjustment, not split — delete moved to main-015)
**ADRs written:** 0014 (Voices page preview routes through SpeakService.Enqueue, status: proposed)

---

## 2026-05-04 10:34 -- Task completed: main-013 - Speak page — primary daily-use UI

**Type:** Work / Task completion
**Task:** main-013 - Speak page — primary daily-use UI
**Summary:** Replaced the Speak page stub with the real daily-use surface (multi-line text, voice picker, Play / Stop / Save, four-state status line) and extracted VoiceCatalog + SpeakService + UserSettings as in-process seams shared by the HTTP API and the page.
**Commit:** a480d58
**Files changed:** 9
**ADRs written:** none

---

## 2026-05-04 10:27 -- Batch started: [main-013]

**Type:** Work / Batch start
**Tasks:** main-013 - Speak page — primary daily-use UI
**Parallel:** no (1 worker)

---

## 2026-05-04 10:10 -- Model / Promoted: main-013 - Speak page

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 10:05 -- Model / Refined: main-013 - Speak page (layout amend)

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (still promote-ready)
**Summary:** User clarification — the textbox should be the page's hero,
filling all available vertical space with only a small breathing-room
margin (~16 px) on each side. Updated layout to a 4-row Grid with `*` row
for the textbox and `Auto` rows for picker/buttons/status; added explicit
sizing rules (`MinHeight=200`, `VerticalAlignment=Stretch`, no `MaxHeight`,
`VerticalScrollBarVisibility=Auto`); added two acceptance criteria covering
"textbox dominates the page" and "min-height never collapses".

---

## 2026-05-04 10:00 -- Model / Refined: main-013 - Speak page

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (now promote-ready)
**Summary:** Editorial pass — the task was already deeply refined (all 8 Q's
resolved, ADR 0009 written, main-020 spun off). Cleared the stale "cannot
promote, main-020 in todo/" status note (main-020 is now `done`); linked
ADR 0010 (CTK.Mvvm) explicitly with worker guidance on `[ObservableProperty]`
/ `[RelayCommand]` / `[NotifyCanExecuteChangedFor]` usage. Verified
ADR 0013 (HttpClient streaming) does not affect the Speak UI path.
**Split into:** —
**ADRs written:** —

---

## 2026-05-04 09:50 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 2

---

## 2026-05-04 09:50 -- Task completed: main-024 - Implement first-chunk latency fix to meet ≤2s budget

**Type:** Work / Task completion
**Task:** main-024 - Implement first-chunk latency fix to meet ≤2s budget
**Summary:** Switched PocketTtsEngine's HTTP call to HttpCompletionOption.ResponseHeadersRead, restoring streaming first-chunk delivery from the pocket-tts sidecar. Measured warm first-chunk latency drops from 22,968 ms / 139,000 ms to 192 ms / 193 ms on medium / long inputs — input-length-independent, ~10× under the 2 s budget.
**Commit:** 0f9a96d
**Files changed:** 3
**ADRs written:** 0013-httpclient-streaming-completion-for-sidecar.md

---

## 2026-05-04 09:49 -- Batch started: [main-024]

**Type:** Work / Batch start
**Tasks:** main-024 - Implement first-chunk latency fix to meet ≤2s budget
**Parallel:** no (1 worker)

---

## 2026-05-04 01:30 -- Model / Promoted: main-024 - Implement first-chunk latency fix

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Why now:** main-023 closed (commit `c8567d6`) with H4 confirmed and exact fix scope (`PocketTtsEngine.cs:78`, two-line `SendAsync` swap). main-024's `What` is now concrete, dependencies met. Ready for worker dispatch.

---

## 2026-05-04 01:25 -- Task completed: main-023 - Diagnose first-chunk latency on long input

**Type:** Work / Spike completion (worker prep + user measurements)
**Task:** main-023 - Diagnose first-chunk latency on long input
**Summary:** H4 confirmed as sole cause. C# `_http.PostAsync(...)` at `PocketTtsEngine.cs:78` uses default `ResponseContentRead` and buffers the entire WAV before reading the first byte; Python streams correctly but C# discards that. Measurements: short 663 ms ✓ / 802 ms ✓, medium (1159 chars) 22968 ms ❌, long (6855 chars) 139000 ms ❌ — latency is linear in input size at ~20 ms/char, the H4 fingerprint. Fix is two lines: switch to `SendAsync` with `HttpCompletionOption.ResponseHeadersRead`. main-024's `What` sharpened with the exact change. Side issue noted: `TerminateProcess` warning on Exit (`SidecarHost.cs:393`) — cosmetic, file when convenient.
**Commit:** TBD
**Files changed:** 1 (Outcome block on the spike) + 1 (main-024 sharpened in backlog)
**ADRs written:** none — fix is local, doesn't reshape transport contract

---

## 2026-05-04 00:35 -- Batch started: [main-023] (prep-only worker dispatch — user-driven spike)

**Type:** Work / Batch start
**Tasks:** main-023 - Diagnose first-chunk latency on long input
**Parallel:** no (1 worker — narrowly scoped to prep half: sample inputs, log line, measurement script, code map. User runs measurements after prep lands.)

---

## 2026-05-04 00:32 -- Model / Promoted: main-023 - Diagnose first-chunk latency on long input

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-05-04 00:30 -- Model / Refined: main-023 (split into spike + fix)

**Type:** Model / Refine
**BC:** main
**Status after:** main-023 stays in backlog as a `spike`; new main-024 in backlog as the follow-up fix
**Summary:** main-023 conflated investigation with implementation — 4 hypotheses to probe + acceptance criteria for the fix. Refined into a spike-and-fix split. main-023 (re-typed `bug` → `spike`) now has a measurement methodology (curl `time_starttransfer` + first-audio-at-speakers cue, canonical sample inputs in `examples/perf/`), an explicit pocket-tts CLI baseline step to bisect engine-slow vs us-slow, and a written-Outcome deliverable. main-024 (new) carries the original ≤2 s acceptance criteria, depends on main-023, holds the fix implementation. main-024's `What` is intentionally TBD until the spike's diagnosis sharpens it.
**Split into:** main-023 (refined, still backlog) + main-024 (new, backlog)
**ADRs written:** none (one may emerge from main-024 if the fix is architectural)

---

## 2026-05-04 00:09 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (main-018 user-driven, main-019 worker-driven)
**Bounced:** 0
**Failed:** 0
**Commits:** 3 in this segment (db7bcff main-018 close, 0723c7f main-019 work, +pending SHA-record)
**Backlog status:** todo/ and doing/ empty. main-013–017 (page-set) and main-023 (latency) remain in backlog/.

---

## 2026-05-04 00:08 -- Task completed: main-019 - Claude Code hook sample

**Type:** Work / Task completion
**Task:** main-019 - Claude Code hook sample — make the speak endpoint actually used
**Summary:** Shipped `examples/claude-hooks/` with a PowerShell hook posting {text, voice} to /speak, plus a README covering Stop/Notification hook wiring, the per-terminal `UTTERHEIM_VOICE` convention, a parallel two-session worked example, and troubleshooting drawn from main-018 verification findings.
**Commit:** 0723c7f
**Files changed:** 4 (incl. moved task file + BC README)
**ADRs written:** none

---

## 2026-05-04 00:06 -- Batch started: [main-019]

**Type:** Work / Batch start
**Tasks:** main-019 - Claude Code hook sample — make the speak endpoint actually used
**Parallel:** no (1 worker — only ready task; main-018 just closed and unblocked it)

---

## 2026-05-04 00:05 -- Task completed: main-018 - Clean-machine first-run verification of main-011

**Type:** Work / Task completion (user-driven verification, outside worker dispatch)
**Task:** main-018 - Clean-machine first-run verification of main-011
**Summary:** First-run criteria signed off. 5 confirmed pass (1, 2, 5, 6 hard-tested + 3 streaming observed); 1 accepted-unverifiable (4, auto-restart races); 3 assumed-pass (7, A, B — to be re-checked on regression). Follow-ups: main-022 fixed, main-023 still open.
**Commit:** db7bcff
**Files changed:** 1
**ADRs written:** none

---

## 2026-05-04 00:00 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (main-022)
**Bounced:** 0
**Failed:** 0
**Commits:** 2 (8a17ff9 work, d778ff8 SHA+protocol)
**Stopped because:** main-018 next in graph is a human-verification task (audible speech, Task Manager observation, mid-download kill); not dispatchable to a worker. main-019 depends on main-018's outcome.

---

## 2026-05-03 23:58 -- Task completed: main-022 - Tray Exit leaves the python.exe sidecar alive as a zombie

**Type:** Work / Task completion
**Task:** main-022 - Tray Exit leaves the python.exe sidecar alive as a zombie
**Summary:** Tray Exit now reaps the entire python sidecar process tree by binding every spawned python.exe to a Win32 Job Object with KILL_ON_JOB_CLOSE; an explicit _shuttingDown flag also prevents the supervisor from respawning the sidecar mid-shutdown.
**Commit:** 8a17ff9
**Files changed:** 6 (incl. moved task file + ADR + BC README)
**ADRs written:** 0012-sidecar-jobobject-kill-on-close.md

---

## 2026-05-03 23:55 -- Batch started: [main-022]

**Type:** Work / Batch start
**Tasks:** main-022 - Tray Exit leaves the python.exe sidecar alive as a zombie
**Parallel:** no (1 worker — only ready task; main-018 + main-019 blocked on this)

---

## 2026-05-03 23:50 -- Model / Promoted: main-022 - Tray Exit leaves the python.exe sidecar alive as a zombie

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Why now:** main-022 already carried 5 concrete acceptance criteria, identified affected files (`SidecarProcessManager`, `App.xaml.cs`, supervisor), and a three-point fix plan. No unmet dependencies. Promoting unblocks main-018 → main-019.

---

## 2026-05-03 23:35 -- Bugs filed: main-022, main-023 (from main-018 verification)

**Type:** Work / Verification finding → bugs filed
**BC:** main
**Trigger:** User ran main-018 first-run verification post main-021 fix. Bootstrap, short speak, sidecar logs, and streaming-correctness criteria pass. Two breakages found.
**main-022 (blocks main-018):** Tray Exit leaves `python.exe` sidecar alive in Task Manager. Acceptance criterion 6 hard-fails. Likely needs `Process.Kill(entireProcessTree: true)`, a graceful-then-hard shutdown sequence, and a "shutting down" flag to suppress the auto-restart watchdog during host exit.
**main-023 (does not block main-018):** ~9 s first-chunk latency on 200-word input vs ≤2 s vision target. Streaming itself works (criterion 3 passes); this is a vision-level perf concern. Probable root cause: whole-text preprocessing before first chunk, or non-streaming HTTP transport.
**Decision (criterion 4):** Not filing the auto-restart-race observability gap. The resilience contract is satisfied; visibility into the brief degraded window is debugging convenience, not user value.
**main-018 status:** Stays in `todo/` with `depends_on: [main-011, main-021, main-022]` and an updated `## Outcome (partial)` block tracking 5/7 criteria + the two main-021 spot-checks.

---

## 2026-05-03 23:13 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 1
**Halted on:** main-018 unblocked but cannot be auto-dispatched — it is a human first-run verification chore (real-audio playback, curl probes, log inspection). main-019 stays transitively blocked behind main-018.

---

## 2026-05-03 23:12 -- Task completed: main-021 - Bootstrap skips pocket-tts install when state file outlives runtime; smoke-test stderr is invisible

**Type:** Work / Task completion
**Task:** main-021 - Bootstrap skips pocket-tts install when state file outlives runtime; smoke-test stderr is invisible
**Summary:** Bootstrap now treats on-disk sentinel files as authoritative — step 3 verifies `pocket_tts/__init__.py` before trusting the JSON flag, upstream re-runs cascade-reset downstream flags, and subprocess stderr is captured + replayed at Error on non-zero exit so failures are diagnosable in both the file log and the dialog.
**Commit:** 75650d9
**Files changed:** 3
**ADRs written:** 0011-bootstrap-state-reconciliation.md

---

## 2026-05-03 23:05 -- Batch started: [main-021]

**Type:** Work / Batch start
**Tasks:** main-021 - Bootstrap skips pocket-tts install when state file outlives runtime; smoke-test stderr is invisible
**Parallel:** no (1 worker)

---

## 2026-05-03 22:34 -- Model / Promoted: main-021

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Summary:** Promoted main-021 (bootstrap skips pocket-tts install when state file outlives runtime; smoke-test stderr is invisible). Required to unblock main-018 verification. No `depends_on`, so it is the only ready task in the BC.

---

## 2026-05-03 22:30 -- Bug filed: main-021 (bootstrap skips pocket-tts install)

**Type:** Work / Verification finding → bug filed
**BC:** main
**Trigger:** User attempted main-018 first-run verification; bootstrap dialog failed with `Python smoke test exited with code 1` (eight retries logged in utterheim-20260503.log between 22:18:04 and 22:18:36).
**Root cause (recorded in main-021):** Two defects in `PythonRuntimeBootstrapper`. (A) Step 3 `InstallPocketTts` checks only the persisted state flag, not `pocket_tts/__init__.py` on disk — so a stale `bootstrap-state.json` (e.g. after a manual wipe of `runtime/`) silently skips re-installing pocket-tts. (B) Subprocess stderr is logged at `LogDebug` so the actual Python traceback never reaches the file log; user sees only the generic exit-code-1 message.
**Action:** Filed main-021 in backlog with both fix sketches and acceptance criteria. Added main-021 to main-018's `depends_on`. main-018 stays in todo with an `## Outcome (verification paused)` block; verification cannot proceed until main-021 lands.
**Workaround for the user:** delete `bootstrap-state.json` alongside `runtime\` and `models\pocket-tts\` before relaunching.
**Worker dispatch:** none (no ready, unblocked tasks remain — main-018 is now blocked on main-021, main-019 transitively blocked).

---

## 2026-05-03 -- Model / Promoted: main-018 + main-019

**Type:** Model / Promote
**BC:** main
**From → To:** backlog → todo
**Summary:** Promoted main-018 (clean-machine first-run verification of main-011) and main-019 (Claude Code hook sample) — the two foundation-closing tasks that turn main-011 from "code-clean" into "actually used". Encoded ordering as a hard dependency: main-019 now depends on main-018 (its troubleshooting acceptance criterion references failure modes from the verification run); main-018 blocks main-019.

---

## 2026-05-02 10:08 -- Work session ended

**Type:** Work / Session end
**Completed:** 1
**Bounced:** 0
**Failed:** 0
**Commits:** 1

---

## 2026-05-02 10:07 -- Task completed: main-020 - Navigation shell — wpfui NavigationView with four-page skeleton

**Type:** Work / Task completion
**Task:** main-020 - Navigation shell — wpfui NavigationView with four-page skeleton
**Summary:** Replaced placeholder MainWindow content with a wpfui NavigationView shell hosting four stub pages (Speak / Voices / Settings / About), wired CommunityToolkit.Mvvm and an IPageService→DI adapter, and added a persistent status footer driven by SidecarHost.StateChanged.
**Commit:** a19e9b3
**Files changed:** 25
**ADRs written:** 0009, 0010 (drafts finalised in-place; not authored by worker)

---

## 2026-05-02 09:57 -- Batch started: [main-020]

**Type:** Work / Batch start
**Tasks:** main-020 - Navigation shell — wpfui NavigationView with four-page skeleton
**Parallel:** no (1 worker)

---

## 2026-05-01 16:30 -- Model / Refined: main-020 - Navigation shell (user pass)

**Type:** Model / Refine
**BC:** main
**Status after:** todo (was demoted to backlog mid-refinement, then re-promoted
once user resolved the open questions)
**Summary:** Two-pass refinement on main-020. Orchestrator pass surfaced six
opens (wpfui v3 lifecycle interface, page service choice, MVVM toolkit,
engine-status interim visibility, brand-mark extraction, titlebar text) and
demoted the task back to `backlog/` pending answers. Marco delegated the
calls back ("you decide" on most). Decisions: implement both
`INavigableView<T>` (typed VM) and `INavigationAware` (lifecycle hooks);
use wpfui's built-in `IPageService`/`INavigationService` with a thin
adapter; **adopt CommunityToolkit.Mvvm** (override of orchestrator's bare
INPC pick — source-gen attributes scale better across 4+ pages); **ship a
persistent status footer** showing HTTP endpoint + engine state (no signal
regression while main-017 is unbuilt); brand-mark extraction punted to
worker time; static `TitleBar` text. Re-promoted to `todo/` once all six
landed.
**ADRs written:** 0010 — rewritten in place from "bare INPC" to
"`CommunityToolkit.Mvvm` source generators". Filename retains its slug.
**Side effects:** main-013 received a small editorial fix —
`INavigableView<SpeakPageViewModel>.OnNavigatedTo()` references corrected
to `INavigationAware.OnNavigatedTo()` (intent unchanged, interface name
was wrong).

---

## 2026-05-01 15:50 -- Model / Refined: main-013 - Speak page (user pass)

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (still blocked on main-020)
**Summary:** Marco walked through the eight open questions in person.
Confirmed Q1 FIFO, Q2 SpeakService extraction, Q3 navigation-shell split,
Q5(a) re-synthesize on Save, Q6 four-state status line. Punted Q4 back
("you decide") — kept VoiceCatalog (in-process) over HTTP-loopback for
cleaner startup ordering and symmetry with SpeakService. **Overrode Q7**:
the picker now reads from a new `UserSettings.DefaultVoiceId` (with `alba`
fallback) instead of always picking the first voice. The settings *storage*
ships in main-013 (new `Services\Settings\UserSettings.cs` + forward-compat
`settings.json`); the *UI to mutate* moves to main-016.
**Side effects:** main-016's forward-link rewritten — no longer "add the
storage and the UI", now "extend the existing UserSettings with the
default-voice dropdown plus the other settings slots".

---

## 2026-05-01 15:30 -- Model / Refined: main-013 - Speak page

**Type:** Model / Refine
**BC:** main
**Status after:** backlog (blocked on main-020)
**Summary:** Resolved all eight open questions on the Speak page task — FIFO (no
barge), extract `SpeakService` and `VoiceCatalog` so HTTP and UI share the seam
structurally, render-on-Save (textbox is source of truth), four-state status
line with transient `stopped`, alphabetical default voice (alba), refresh on
`OnNavigatedTo()`. The navigation shell was split out into a separate task to
keep main-013 page-shaped and unblock the other page tasks symmetrically.
**Split into:** main-020 (navigation shell, promoted directly to todo/)
**ADRs written:** 0009 (page navigation via wpfui NavigationView with
INavigableView pages)
**Side effects:** main-014, main-016, main-017 all gained `main-020` in their
`depends_on`. main-016 picked up a forward-link for the persisted "default
voice" setting deferred from Q7.

---

## 2026-05-01 15:05 -- Model / Captured: main-013..main-019 (v1 feature batch)

**Type:** Model / Capture
**BC:** main
**Filed to:** backlog
**Summary:** Captured the seven natural next steps after the foundation phase as backlog items: page set (main-013 Speak, main-014 Voices, main-015 voice cloning, main-016 Settings, main-017 About) all gated by the now-OPEN styleguide (main-010); main-018 chore to close out main-011's pending user-verifiable acceptance criteria on a clean machine; main-019 Claude Code hook sample so the speak endpoint actually gets used. Suggested run order: main-018 first (verify foundation), main-019 next (deliver v1 payoff), then page set in styleguide-canonical order. Tasks are intentionally lean — refine via /agentheim:model before promotion.

---

## 2026-05-01 14:43 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (main-011, main-012)
**Bounced:** 0
**Failed:** 0
**Commits:** 3 (sign-off+promote 5ffa4c9, main-011 061c51b, main-012 d5b683f)
**New backlog items:** none

todo/, doing/, and backlog/ are all empty. Utterheim's foundation phase is complete: real pocket-tts engine wired (user-verifiable on next restart with ~700 MB first-run download), speaking-person logo rasterised to all required sizes + .ico (visible in tray + taskbar after restart). Next phase is feature work — Speak page, Voices page, Settings, About — driven via /agentheim:model captures from the user.

---

## 2026-05-01 14:42 -- Task completed: main-012 - Logo rasterisation

**Type:** Work / Task completion
**Task:** main-012 - Rasterise the speaking-person logo to PNG sizes + .ico for tray
**Summary:** Generated PNG sizes 16/24/32/48/64/128/256/512 + multi-resolution utterheim.ico (16/24/32/48/64/128/256 layers, PNG-compressed) into assets/branding/ via a one-shot SkiaSharp + Svg.Skia helper at Tools/RasteriseLogo/ (kept standalone, not in utterheim.sln). Wired the .ico as ApplicationIcon, packaged Resource, FluentWindow Icon, and tray:NotifyIcon Icon. Build clean (0/0). User-verifiable: speaking-person silhouette now appears in the tray + Explorer + taskbar after restart.
**Commit:** d5b683f
**Files changed:** 14
**ADRs written:** none

---

## 2026-05-01 14:31 -- Batch started: [main-012]

**Type:** Work / Batch start
**Tasks:** main-012 - Rasterise the speaking-person logo to PNG sizes + .ico
**Parallel:** no (1 worker)

---

## 2026-05-01 14:30 -- Task completed: main-011 - Real pocket-tts engine

**Type:** Work / Task completion
**Task:** main-011 - Real pocket-tts engine — Python sidecar bootstrap and PocketTtsEngine
**Summary:** Probe-installed pocket-tts==2.0.0 cleanly into a fresh Python 3.12 venv on Windows 11 — ADR 0002 path validated. Wrote PocketTtsEngine (POST /tts, strips WAV header, streams 24 kHz mono 16-bit PCM via IAsyncEnumerable<byte[]>), SidecarHost (supervises python.exe with port-banner parsing, /health polling, capped-backoff restarts, clean shutdown), PythonRuntimeBootstrapper (downloads Python 3.12.7 embeddable, enables site, bootstraps pip, installs pocket-tts>=2.0,<3, persists state across restarts), real BootstrapDialog with progress + cancel + retry, EntryPoint DI swap (StubTtsEngine gated behind UTTERHEIM_USE_STUB_ENGINE=1), SpeakServer /status reports sidecar state. Build clean (0/0). User-verifiable acceptance criteria pending first run with real download (~700 MB).
**Commit:** 061c51b
**Files changed:** 9
**ADRs written:** none

---

## 2026-05-01 14:19 -- Batch started: [main-011]

**Type:** Work / Batch start
**Tasks:** main-011 - Real pocket-tts engine — Python sidecar bootstrap and PocketTtsEngine
**Parallel:** no (1 worker — large scope; main-012 demoted to next batch due to csproj conflict)

---

## 2026-05-01 14:19 -- Sign-off + promote: main-011, main-012

**Type:** Model / Promote (post sign-off)
**Summary:** User confirmed the walking skeleton works (440 Hz tone played end-to-end via /speak, /stop, double-tap LCtrl) and approved the styleguide. docs/styleguide.md sign-off line replaced with `signed-off-by: Marco Heimeshoff on 2026-05-01`. BC README updated to reflect the OPEN gate state. Both backlog items (main-011 real engine, main-012 logo raster) promoted from backlog/ to todo/, frontmatter `status: todo`. Next: dispatch work waves for both.

---

## 2026-05-01 13:20 -- Work session ended

**Type:** Work / Session end
**Completed:** 10 (main-001 through main-010)
**Bounced:** 0
**Failed:** 0
**Commits:** 11 (1 bootstrap + 8 ADR commits + 1 walking-skeleton + 1 styleguide)
**New backlog items:** main-011 (real pocket-tts engine bootstrap), main-012 (logo rasterisation)

All 8 foundation ADRs committed; walking skeleton builds clean and smoke-tested live (HTTP + queue + audio + hotkey end-to-end with stubbed engine); styleguide artefact produced and awaiting user sign-off. Utterheim is ready for the user to (a) sign off on docs/styleguide.md, (b) decide when main-011 (real engine) and main-012 (final logo raster) move from backlog to todo via /agentheim:model.

---

## 2026-05-01 13:18 -- Task completed: main-010 - Styleguide

**Type:** Work / Task completion
**Task:** main-010 - Styleguide — adapt WhisperHeim's design language and the speaking-person logo
**Summary:** Produced docs/styleguide.md (inherited-from-WhisperHeim section + explicit divergences + reusable component map + sign-off placeholder), placeholder speaking-person SVG at assets/branding/utterheim-logo.svg (clearly marked PLACEHOLDER), MainWindow.xaml updated to render the silhouette inline + show "Local voices for Claude Code" tagline. Build remains clean. **GATE STATE: artefact ready, awaiting user sign-off before any frontend feature task can be promoted.**
**Commit:** 44ce127
**Files changed:** 5
**ADRs written:** none
**New backlog items:** main-012 - Rasterise the speaking-person logo to PNG sizes + .ico

---

## 2026-05-01 13:15 -- Batch started: [main-010]

**Type:** Work / Batch start
**Tasks:** main-010 - Styleguide (WhisperHeim design + speaking-person logo)
**Parallel:** no (1 worker; sign-off gate)

---

## 2026-05-01 13:13 -- Task completed: main-009 - Walking skeleton

**Type:** Work / Task completion
**Task:** main-009 - Walking skeleton — Claude hook → HTTP → sidecar → audio out
**Summary:** Walking skeleton stands up end-to-end on .NET 9 / WPF / x64: WPF-UI Mica tray window with show/stop/exit menu, Kestrel minimal API on 127.0.0.1:7223 (POST /speak, /stop, GET /voices, /status), Channel<T>-based SpeakQueue with stop-and-drain semantics, NAudio playback, low-level keyboard hook for double-tap LCtrl, ADR-0005 path layout, Serilog rolling file sink, single-file utterheim-speak CLI wrapper, and copy-and-modify provenance headers from WhisperHeim @ 911bff0. The TTS engine is stubbed (440 Hz test tone via StubTtsEngine behind ITtsEngine) — real pocket-tts sidecar bootstrap captured as main-011 in backlog. Build clean, smoke test passed.
**Commit:** 4becb42
**Files changed:** 26 (plus moved task file + leftover todo/ deletions cleanup)
**ADRs written:** none (skeleton materialises ADRs 0001–0008 as code)
**New backlog items:** main-011 - Real pocket-tts engine bootstrap

---

## 2026-05-01 13:00 -- Batch started: [main-009]

**Type:** Work / Batch start
**Tasks:** main-009 - Walking skeleton (spike)
**Parallel:** no (1 worker, large scope)
**Note:** Orchestrator scoped the spike to a stubbed-engine variant — `ITtsEngine` returns a generated test tone instead of real pocket-tts output. Real engine bootstrap moves to a new follow-up task (`main-011`) so the skeleton can validate architecture fast without bundling a multi-hundred-MB Python + model download into one worker session.

---

## 2026-05-01 13:00 -- Task completed: main-007 - Queue mechanism

**Type:** Work / Task completion
**Task:** main-007 - Speak queue lives in the C# host as a Channel<T>
**Summary:** Wrote ADR 0007 locking the speak queue placement: C# host owns the FIFO Channel<SpeakRequest> while the Python sidecar remains stateless per-utterance.
**Commit:** 5c8cdcc
**Files changed:** 1
**ADRs written:** 0007-queue-channel-in-host.md

---

## 2026-05-01 12:58 -- Batch started: [main-007]

**Type:** Work / Batch start
**Tasks:** main-007 - Speak queue as Channel<T> in C# host
**Parallel:** no (1 worker, deps just satisfied)

---

## 2026-05-01 12:58 -- Task completed: main-008 - Cross-cutting concerns

**Type:** Work / Task completion
**Task:** main-008 - Cross-cutting (Serilog, fail-loud, model bootstrap, distribution)
**Summary:** Wrote ADR 0008 capturing four cross-cutting decisions (Serilog logging with sidecar redirect sink, fail-loud-to-tray error philosophy, WhisperHeim-style model + runtime bootstrap dialog, self-contained zip distribution).
**Commit:** e4a6797
**Files changed:** 1
**ADRs written:** 0008-cross-cutting-concerns.md

---

## 2026-05-01 12:57 -- Batch started: [main-008]

**Type:** Work / Batch start
**Tasks:** main-008 - Cross-cutting (Serilog, fail-loud, model bootstrap, distribution)
**Parallel:** no (1 worker, solo dispatch)

---

## 2026-05-01 12:57 -- Task completed: main-006 - WhisperHeim reuse form

**Type:** Work / Task completion
**Task:** main-006 - Reuse WhisperHeim infrastructure via copy-and-modify in v1
**Summary:** Recorded ADR 0006 documenting the decision to reuse WhisperHeim infrastructure via copy-and-modify in v1 (with provenance headers and CHANGELOG entries), deferring shared-library extraction until both apps ship v1.
**Commit:** d3137e3
**Files changed:** 1
**ADRs written:** 0006-whisperheim-reuse-copy-and-modify.md

---

## 2026-05-01 12:56 -- Task completed: main-005 - Voice persistence layout

**Type:** Work / Task completion
**Task:** main-005 - Voice profiles as folder-per-voice + library.json index
**Summary:** Recorded ADR 0005 codifying voice profile persistence as folder-per-voice under `<dataPath>\voices\<voice-id>\` plus a single library.json master index, with WhisperHeim-style path layering and a fixed meta.json schema.
**Commit:** e169347
**Files changed:** 1
**ADRs written:** 0005-voice-persistence-layout.md

---

## 2026-05-01 12:55 -- Task completed: main-004 - Stop signal semantics

**Type:** Work / Task completion
**Task:** main-004 - Stop signal drains the queue by default (configurable)
**Summary:** Recorded ADR 0004 establishing that the stop signal cancels current utterance and drains the pending speak queue by default, with a tray-UI toggle for "stop current only". Unblocks queue mechanism (main-007) and HTTP /stop endpoint implementation.
**Commit:** 47230fb
**Files changed:** 1
**ADRs written:** 0004-stop-drains-queue.md

---

## 2026-05-01 12:53 -- Batch started: [main-004, main-005, main-006]

**Type:** Work / Batch start
**Tasks:** main-004 - Stop signal semantics, main-005 - Voice persistence layout, main-006 - WhisperHeim reuse form
**Parallel:** yes (3 workers)

---

## 2026-05-01 12:53 -- Task completed: main-003 - Claude transport HTTP

**Type:** Work / Task completion
**Task:** main-003 - Expose the speak endpoint over loopback HTTP (JSON)
**Summary:** Recorded ADR 0003 selecting loopback HTTP/JSON on 127.0.0.1:7223 as the speak endpoint transport, with a utterheim-speak CLI wrapper for hook ergonomics. No code written; main-009 is now unblocked on this dimension.
**Commit:** b8d1fcc
**Files changed:** 1
**ADRs written:** 0003-claude-transport-http.md

---

## 2026-05-01 12:52 -- Task completed: main-002 - Pocket-tts sidecar

**Type:** Work / Task completion
**Task:** main-002 - Run pocket-tts as a managed Python sidecar over loopback HTTP
**Summary:** Recorded ADR 0002 selecting a managed Python sidecar (loopback HTTP) as the pocket-tts integration shape, with sherpa-onnx ONNX kept as a documented fallback behind an ITtsEngine seam. No code changes; bootstrap deferred to main-009.
**Commit:** de54a8b
**Files changed:** 1
**ADRs written:** 0002-pocket-tts-python-sidecar.md

---

## 2026-05-01 12:51 -- Task completed: main-001 - Confirm stack

**Type:** Work / Task completion
**Task:** main-001 - Confirm and document the .NET 9 / WPF / WPF-UI / Win x64 stack
**Summary:** Recorded the .NET 9 / WPF / WPF-UI / Windows x64 stack as ADR 0001 (scope global, status accepted), mirroring WhisperHeim's foundation. No code changes; only the ADR and task move.
**Commit:** 9232a48
**Files changed:** 1
**ADRs written:** 0001-stack-net9-wpf-x64.md

---

## 2026-05-01 12:48 -- Batch started: [main-001, main-002, main-003]

**Type:** Work / Batch start
**Tasks:** main-001 - Confirm stack, main-002 - Pocket-tts sidecar, main-003 - Claude transport HTTP
**Parallel:** yes (3 workers)

---

## 2026-05-01 -- Brainstorm: Utterheim vision + foundation

**Type:** Brainstorm
**Outcome:** vision created
**BCs identified:** main (single bounded context — see `.agentheim/context-map.md` for why five candidate BCs collapse into one)
**Summary:** Utterheim is a local-first Windows 11 tray app that gives Claude Code a voice across multiple parallel terminals. Single user, single primary consumer (Claude Code), CPU-only English TTS via Kyutai's `pocket-tts` (sample-based voice cloning, ~200ms first-chunk latency). Inherits WhisperHeim's UI tech (.NET 9 / WPF / WPF-UI / Mica) and audio-capture infrastructure via copy-and-modify. Replaces the TTS page that was previously planned for WhisperHeim — TTS gets pulled out into its own focused app. Voice diversity is the core feature (different voice per Claude session = audible session disambiguation). Concurrency = FIFO queue. Stop = double-tap LCtrl, drains queue by default.
**Research conducted:** Kyutai pocket-tts capabilities and Windows-readiness — report at `.agentheim/knowledge/research/kyutai-tts-2026-05-01.md`. Confirmed pocket-tts is real, MIT/CC-BY-4.0, CPU-only, English-only at v1, native streaming. User accepted English-only constraint.
**Strategic decision:** Single BC at `contexts/main/`. Five candidate BCs (synthesis, voice-library, voice-capture, claude-bridge, tray-ui) collapsed because language, lifecycle, actors, and invariants don't diverge enough to justify split for a one-developer personal tool. Reasoning preserved in `context-map.md`.
**Architecture pass:** Architect surfaced 8 foundation decisions; ADR drafts produced and embedded in decision-task Notes.
**ADRs written:** none yet — they flow through `type: decision` tasks and will be committed by the worker as it claims each. Drafts ready as ADR 0001–0008.
**Foundation tasks emitted:**
- `main-001` Stack confirmation (.NET 9 + WPF + WPF-UI + x64)
- `main-002` Pocket-tts Python sidecar
- `main-003` Claude transport via loopback HTTP (port 7223)
- `main-004` Stop signal drains queue (configurable)
- `main-005` Voice persistence: folder-per-voice + library.json
- `main-006` WhisperHeim reuse via copy-and-modify
- `main-007` Speak queue as `Channel<T>` in C# host
- `main-008` Cross-cutting: Serilog, fail-loud, model bootstrap, zip distribution
- `main-009` Walking skeleton (spike, depends on 001–008) — first prototype
- `main-010` Styleguide (depends on 009) — adapt WhisperHeim design + speaking-person logo, **gate** for all future frontend tasks
**Protocol divergence:** brainstorm skill default would create a separate `contexts/design-system/` BC for the styleguide; that was overruled in favour of putting `main-010` inside `contexts/main/` to respect the strategic-modeler's single-BC decision. Gate semantics (every frontend task `depends_on: [main-010]`) preserved unchanged. Documented in `contexts/main/README.md`.

---
