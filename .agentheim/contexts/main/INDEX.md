# main -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 0
- **Doing:** 0
- **Done:** 49
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- no tasks in todo -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **main-d7m2k** -- Revert the sidecar's German model from german_24l back to distilled german (ADR 0025) — biggest RAM cut -- 2026-06-28 -- `done/main-d7m2k-revert-german-24l-to-distilled.md`
- **main-r8k3w** -- Cut the Python sidecar's RAM footprint (~2.4 GB) — measure where it goes, then decide the lever -- 2026-06-28 -- `done/main-r8k3w-sidecar-ram-footprint.md`
- **main-047** -- Language-aware narration — detect EN/DE and speak in a matching voice -- 2026-05-24 -- `done/main-047-language-aware-narration.md`
- **main-046** -- Implement Stop cancellation propagation into the sidecar (≤2 s recovery — implementation; empirical CPU/RSS/latency ACs deferred to user) -- 2026-05-19 -- `done/main-046-implement-stop-cancellation-propagation.md`
- **main-045** -- Diagnose pocket-tts cancellation surface and prototype Stop propagation (spike, partial — prototype delivered, empirical measurement deferred to user) -- 2026-05-19 -- `done/main-045-sidecar-leak-on-stop-cycle.md`
- **main-038** -- Listen-test german vs german_24l (spike) -- 2026-05-18 -- `done/main-038-listen-test-german-vs-24l.md`
- **main-042** -- German reading prompt for the clone-a-new-voice flow -- 2026-05-18 -- `done/main-042-german-reading-prompt.md`
- **main-041** -- Voices page — language picker in clone flow + per-voice language column -- 2026-05-18 -- `done/main-041-voices-page-language-picker.md`
- **main-039** -- Sidecar — load English + German models concurrently and route by voice's language -- 2026-05-18 -- `done/main-039-sidecar-multi-model-serve.md`
- **main-040** -- Voice library — add language field; populate built-ins including `juergen` -- 2026-05-18 -- `done/main-040-voice-library-language-field.md`
- **main-044** -- Add Utterheim.Tests xUnit project — establishes test infrastructure -- 2026-05-18 -- `done/main-044-add-utterheim-tests-project.md`
- **main-037** -- Production German is distilled `german`, not `german_24l` (decision) -- 2026-05-18 -- `done/main-037-distilled-german-not-24l.md`
- **main-036** -- Sidecar preloads English + German concurrently (decision) -- 2026-05-18 -- `done/main-036-preload-english-and-german.md`
- **main-043** -- Drop dead `TypeError` fallback around `language=` in sidecar -- 2026-05-18 -- `done/main-043-drop-dead-typeerror-fallback.md`
- **main-035** -- Voice profile carries its language (decision) -- 2026-05-18 -- `done/main-035-voice-carries-language.md`
- **main-030** -- Speak page hero + button row above text; Voices clone card above list -- 2026-05-05 -- `done/main-030-speak-voices-layout.md`
- **main-029** -- Adopt WhisperHeim styling wholesale — Light theme, brand palette, card spec, Appearance picker -- 2026-05-05 -- `done/main-029-whisperheim-styling-adoption.md`
- **main-028** -- Logo redesign — voice human-head mark -- 2026-05-05 -- `done/main-028-logo-waveform-tail.md`
- **main-031** -- Editable data path with folder-picker dialog -- 2026-05-05 -- `done/main-031-data-path-folder-picker.md`
- **main-034** -- Rainbow Passage prompt for microphone voice cloning -- 2026-05-05 -- `done/main-034-rainbow-passage-prompt.md`
- **main-033** -- Design corrections — menu font/logo, voices order, settings layout, right-Ctrl hotkey, error strings -- 2026-05-05 -- `done/main-033-design-corrections-menu-voices-settings.md`
- **main-032** -- Relocate engine diagnostics; redesign About to match WhisperHeim -- 2026-05-05 -- `done/main-032-about-relocate-engine-status.md`
- **main-023** -- Diagnose first-chunk latency on long input (~9s for 200-word paragraph) -- 2026-05-04 -- `done/main-023-first-chunk-latency-on-long-input.md`
- **main-017** -- About page — logo, tagline, version, engine status, retry -- 2026-05-04 -- `done/main-017-about-page.md`
- **main-016** -- Settings page — output device, default voice, startup, read-only diagnostics -- 2026-05-04 -- `done/main-016-settings-page.md`
- **main-024** -- Implement first-chunk latency fix to meet ≤2s budget -- 2026-05-04 -- `done/main-024-implement-first-chunk-latency-fix.md`
- **main-027** -- Bootstrapper — self-heal stale or partially-installed utterheim_sidecar -- 2026-05-04 -- `done/main-027-bootstrapper-self-heal.md`
- **main-026** -- Voices page — per-row delete affordance for cloned voices -- 2026-05-04 -- `done/main-026-voices-delete-affordance.md`
- **main-025** -- Voice cloning UI — recording controls + source toggle on the Voices page -- 2026-05-04 -- `done/main-025-voice-cloning-ui.md`
- **main-015** -- Voice cloning backend — VoiceLibraryService + sidecar /export-voice -- 2026-05-04 -- `done/main-015-voice-cloning.md`
- **main-013** -- Speak page — primary daily-use UI -- 2026-05-04 -- `done/main-013-speak-page.md`
- **main-014** -- Voices page — voice library list with preview -- 2026-05-04 -- `done/main-014-voices-page.md`
- **main-021** -- Bootstrap skips pocket-tts install when state file outlives runtime; smoke-test stderr is invisible -- 2026-05-03 -- `done/main-021-bootstrap-skips-pocket-tts-install.md`
- **main-022** -- Tray Exit leaves the python.exe sidecar alive as a zombie -- 2026-05-03 -- `done/main-022-tray-exit-leaves-zombie-python.md`
- **main-019** -- Claude Code hook sample — make the speak endpoint actually used -- 2026-05-03 -- `done/main-019-claude-hook-sample.md`
- **main-018** -- Clean-machine first-run verification of main-011 -- 2026-05-03 -- `done/main-018-first-run-verification.md`
- **main-020** -- Navigation shell — wpfui NavigationView with four-page skeleton -- 2026-05-02 -- `done/main-020-navigation-shell.md`
- **main-004** -- Stop signal drains the queue by default (configurable) -- 2026-05-01 -- `done/main-004-stop-signal-semantics.md`
- **main-005** -- Voice profiles as folder-per-voice + library.json index -- 2026-05-01 -- `done/main-005-persistence-layout.md`
- **main-006** -- Reuse WhisperHeim infrastructure via copy-and-modify in v1 -- 2026-05-01 -- `done/main-006-whisperheim-reuse-form.md`
- **main-001** -- Confirm and document the .NET 9 / WPF / WPF-UI / Win x64 stack -- 2026-05-01 -- `done/main-001-stack-confirmation.md`
- **main-002** -- Run pocket-tts as a managed Python sidecar over loopback HTTP -- 2026-05-01 -- `done/main-002-pocket-tts-integration.md`
- **main-003** -- Expose the speak endpoint over loopback HTTP (JSON) -- 2026-05-01 -- `done/main-003-claude-transport.md`
- **main-010** -- Styleguide — adapt WhisperHeim's design language and the speaking-person logo -- 2026-05-01 -- `done/main-010-styleguide.md`
- **main-011** -- Real pocket-tts engine — Python sidecar bootstrap and PocketTtsEngine -- 2026-05-01 -- `done/main-011-pocket-tts-real-bootstrap.md`
- **main-012** -- Rasterise the speaking-person logo to PNG sizes + .ico for tray -- 2026-05-01 -- `done/main-012-logo-rasterisation.md`
- **main-007** -- Speak queue lives in the C# host as a Channel<T> -- 2026-05-01 -- `done/main-007-queue-mechanism.md`
- **main-008** -- Cross-cutting — Serilog, fail-loud-to-tray, model bootstrap, zip distribution -- 2026-05-01 -- `done/main-008-cross-cutting-concerns.md`
- **main-009** -- Walking skeleton — Claude hook → HTTP → sidecar → audio out -- 2026-05-01 -- `done/main-009-walking-skeleton.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- no tasks in backlog -->
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0009** -- Page navigation via wpfui NavigationView with INavigableView pages -- 2026-05-01 -- `../../knowledge/decisions/0009-navigation-shell-wpfui.md`
- **0010** -- MVVM via CommunityToolkit.Mvvm source generators -- 2026-05-01 -- `../../knowledge/decisions/0010-mvvm-via-inotifypropertychanged.md`
- **0011** -- Bootstrap state — on-disk presence is authoritative, JSON flags are advisory -- 2026-05-03 -- `../../knowledge/decisions/0011-bootstrap-state-reconciliation.md`
- **0012** -- Bind the python sidecar to a Win32 Job Object with KILL_ON_JOB_CLOSE -- 2026-05-03 -- `../../knowledge/decisions/0012-sidecar-jobobject-kill-on-close.md`
- **0022** -- Stop hotkey watches Right Ctrl (double-tap), not Left Ctrl -- 2026-05-05 -- `../../knowledge/decisions/0022-stop-hotkey-double-tap-right-ctrl.md`
- **0023** -- Voice profile carries its language; speak request body unchanged -- 2026-05-18 -- `../../knowledge/decisions/0023-voice-carries-language.md`
- **0024** -- Sidecar preloads English + German concurrently (multi-language preload) -- 2026-05-18 -- `../../knowledge/decisions/0024-sidecar-multi-language-preload.md`
- **0025** -- Production German is distilled `german`, not `german_24l` (match English's variant) -- 2026-05-18 -- `../../knowledge/decisions/0025-german-distilled-default.md`
- **0026** -- Stop cancels in-flight synthesis within <=2 s (amends ADR 0004) -- 2026-05-19 -- `../../knowledge/decisions/0026-stop-cancels-in-flight-synthesis.md`
- **0027** -- Cancellation propagation mechanism into pocket-tts (proposed; hybrid wrapper + monkey-patch recommended) -- 2026-05-19 -- `../../knowledge/decisions/0027-cancellation-propagation-mechanism.md`
- **0028** -- Narrator stores the EN+DE voice pair as two sibling files; legacy single file is the English/default slot -- 2026-05-24 -- `../../knowledge/decisions/0028-narrator-voice-pair-config.md`
- **0029** -- Do not enable inline int8 quantization for RAM reduction (torch.ao fallback yields no resident cut on torch 2.12) -- 2026-06-28 -- `../../knowledge/decisions/0029-no-inline-int8-quant-for-ram.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **pocket-tts-upstream-cancellation-posture** -- kyutai-labs/pocket-tts upstream posture on cancellation / stop / interrupt support (H5 for main-045) -- 2026-05-19 -- `../../knowledge/research/pocket-tts-upstream-cancellation-posture-2026-05-19.md`
- **pocket-tts-german-support** -- Kyutai pocket-tts German language support — model variants, runtime selection, voice cloning, plugin integration -- 2026-05-18 -- `../../knowledge/research/pocket-tts-german-support-2026-05-18.md`
- **kyutai-tts** -- Kyutai Pocket TTS for a local Windows tray TTS service with sample-based voice cloning -- 2026-05-01 -- `../../knowledge/research/kyutai-tts-2026-05-01.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- no concept pages yet -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
