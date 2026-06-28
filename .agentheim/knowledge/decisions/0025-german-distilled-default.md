---
id: 0025
title: German model variant matches the English production variant (distilled `german`, not `german_24l`)
scope: main
status: accepted
date: 2026-05-18
supersedes: []
superseded_by: []
related_tasks: [main-037, main-038, main-039, main-r8k3w]
related_research: [pocket-tts-german-support-2026-05-18]
---

# ADR 0025: German uses the distilled `german`, not `german_24l`, because English has no 24l counterpart

## Context

pocket-tts 2.1.0 ships two model lineages per non-English language: a default
6-layer **distilled** model (`german`, `french`, `italian`, `spanish`,
`portuguese`) and a 24-layer undistilled preview variant
(`german_24l`, `french_24l`, …) which Kyutai describe as "higher quality but
slower… here only as preview" and for which they actively solicit bug
reports against the distilled version (research
`pocket-tts-german-support-2026-05-18` §2 and §1). English has **no `*_24l`
counterpart** — `english` (alias `english_2026-04`) is the only production
choice; the older `english_2026-01` is a legacy distilled variant, not a
larger preview.

ADR 0023 fixed that voice profiles carry their language and the speak HTTP
body stays unchanged. ADR 0024 fixed that the sidecar preloads one resident
`TTSModel` per configured language (for v1: `english` + one German variant).
Neither of those ADRs picked **which** German model to load. That is this
ADR's job.

The German-support research's open-questions section flagged this exact
choice ("Whether the modeling skill should expose `german_24l` as a
user-selectable variant"). Two facts shape the decision:

- **Performance asymmetry.** The distilled `german` matches the English RTF
  profile (~6× real-time, ~200 ms first chunk on the user's hardware
  envelope per the 2026-05-01 English benchmark). `german_24l` is a
  24-layer vs 6-layer transformer — roughly a 4× compute increase in the
  language-model stack, so expect roughly 4× the real-time factor (research
  §6). Choosing 24l for German would create a UX where one language feels
  noticeably slower than the other in the same tray app.
- **Preview status.** Kyutai themselves label the `*_24l` variants as
  preview and ask for bug reports against the distilled lineage. That is
  the opposite of a production signal.

## Decision

**Rule:** the German model variant tracks the English production variant.
With English shipping the distilled `english` (no `*_24l` counterpart
exists), Utterheim loads the distilled `german` as the German model and
does **not** load `german_24l`.

Stated as a forward-looking rule rather than a one-time pick:

> Whatever lineage English runs in production, German runs the same
> lineage. If Kyutai ever ship an `english_24l` and the user adopts it
> for English, German switches to `german_24l` in lockstep.

This is a project-level rule on top of ADR 0024's "preload these
languages" decision: ADR 0024 says *which languages*; this ADR says
*which variant per language*.

Spike `main-038` is the empirical listen-test that closes the loop: a
side-by-side audition of `german` vs `german_24l` on representative
prompts using `juergen`. If 038 turns up a specific failure mode in
distilled `german` that `german_24l` avoids, this ADR is superseded —
the rule still holds, but the qualifying observation forces a re-pick.

## Consequences

### Positive
- **Uniform latency profile across languages.** English and German both
  run on the 6-layer distilled stack, so the tray app's first-chunk
  latency and real-time factor are language-invariant. No "this language
  feels sluggish" UX surprise.
- **No per-voice "model variant" axis on the schema.** Voices are
  language-tagged (ADR 0023); the language deterministically picks the
  variant via this project rule. The voice library's `meta.json` does not
  grow a `variant` field, the Voices page's cloning flow does not grow a
  "model variant" picker, and the sidecar config does not grow a
  per-language variant override. The voice → language → variant chain is
  data + rule, not data + data.
- **Aligned with Kyutai's stated production posture.** Distilled is the
  default; 24l is preview. Following upstream's production lineage keeps
  Utterheim on the well-tested path and inherits Kyutai's bugfixes
  without needing to track two release streams.
- **Codifies a rule, not a pick.** Future contributors (or future-Marco)
  reading the voice/sidecar code don't have to re-derive *why* `german`
  was chosen over `german_24l` — the rule is captured and the
  contingency for English switching is spelled out.

### Negative
- **If distilled German has a quality gap vs 24l on real prompts, we
  accept it for v1.** The research notes Kyutai have not published MOS,
  WER, or blind-test numbers for German specifically (§7), and the
  distillation step is the most likely source of any German-specific
  regressions (§2). Spike `main-038` is the empirical hedge; if its
  listen-test produces a clear "24l is materially better and the speed
  cost is acceptable for some workflows" finding, the project can
  re-open this decision, but doing so would also force adding the
  per-voice variant axis we are explicitly avoiding here.

### Neutral
- The sidecar `serve --language` configuration (ADR 0024's
  implementation in main-039) takes the literal model name (`german`,
  not `german_24l`). No code-level enum to keep in sync — the model
  name is just a string passed through to `TTSModel.load_model`.
- ADR 0024's "sixth-language threshold" open follow-up already flagged
  "add a `german_24l` variant alongside `german`" as a triggering event
  for revisiting the preload strategy. Both ADRs point at the same
  reconsideration boundary if the variant question ever re-opens.

## Alternatives considered

1. **Distilled `german` (selected).** Matches English's lineage, matches
   Kyutai's production default, keeps the per-language latency profile
   uniform, keeps the schema variant-free.
2. **`german_24l` as the production default.** Rejected for v1: it is
   labelled preview by upstream, has no benchmark numbers, and is ~4×
   slower than the distilled stack, which would create a within-app
   latency asymmetry the user would have to explain. Reconsider only if
   `main-038`'s listen-test or a future user-reported issue makes the
   quality delta load-bearing.
3. **Expose both variants per voice (per-voice "model variant" axis).**
   Rejected: ADR 0023's voice-carries-language model gets its simplicity
   from voices being keyed only on language. Adding a `variant` field to
   `meta.json` (and a picker to the Voices page, and a per-language map
   in the sidecar) doubles the configuration surface for a preview
   feature with no quantified user benefit. Revisit only if production
   experience demonstrates the variant matters for real workflows.

## References

- ADR 0023 — voice profile carries its language; speak request body
  unchanged. Establishes that voices are language-keyed, which makes
  this ADR's "language → variant by rule" possible without a schema
  change.
- ADR 0024 — sidecar preloads English + German concurrently. Establishes
  which *languages* are preloaded; this ADR pins which *variant* per
  language.
- Research: `pocket-tts-german-support-2026-05-18` — §1 (Kyutai's own
  framing of `*_24l` as preview and the bug-report-against-distilled
  request), §2 (the six-language table showing English has no 24l), §6
  (the ~4× compute estimate for 24l vs distilled), §7 open question on
  exposing `german_24l` as a user-selectable variant (this ADR closes
  that question for v1 with "no").
- main-038 — empirical listen-test of `german` vs `german_24l` using
  `juergen`. The audit that can supersede this ADR if it produces a
  qualitative finding that overrides the rule.
- main-039 — sidecar multi-language preload implementation. Consumes
  this ADR by passing the literal model name `german` (not
  `german_24l`) to `TTSModel.load_model`.

## Addendum (2026-05-18, post-main-038)

**Confirmed.** main-038's in-app listen test (temporary swap to
`german_24l` in `SidecarHost.cs` + `PocketTtsEngine.LanguageWireValue`,
spoken via `juergen` from the Speak page, then reverted): no audible
quality difference; 24l inference latency feels equivalent to distilled
on the user's hardware. With no perceptible quality advantage to 24l,
the rationale above stands by default — distilled matches English's
variant and is lighter. No follow-up task opened.

## Addendum (2026-06-28, main-r8k3w — drift discovered)

**The live code contradicts this ADR.** `SidecarHost.cs:291` launches
`serve … --language english --language german_24l` — the 24-layer preview
model, not the distilled `german` this ADR selected. The BC README and
main-039 both still document distilled `german`, so the drift is in the
**code only**, not the docs.

Root cause: the main-038 listen-test temporarily swapped the launch arg to
`german_24l` and was supposed to be reverted; the revert of the C# launch
arg never landed (the experimental `german_24l` leaked into committed code).
This is undocumented drift, not an intentional supersession of ADR 0025 —
main-038 itself found *no* audible quality advantage to 24l.

The drift carries a real RAM cost, measured in main-r8k3w: `german_24l`'s
24-layer flow_lm is **1205 MB** in memory vs **341 MB** for distilled
`german`. The two-model resident set is **~1985 MB** (en + german_24l) vs
**~1118 MB** (en + distilled german) — reverting the drift cuts ~867 MB
(~44%) and also restores the uniform latency profile this ADR promised
(german_24l `/tts` measured 4.8 s vs english 1.5 s on the same prompt).

ADR 0025 stands. The revert (a product/quality call per the spike's lever
framing) is deferred to follow-up decision task **main-d7m2k**, which will
confirm with the user that no german_24l quality preference exists and then
flip `SidecarHost.cs:291` back to distilled `german`.
