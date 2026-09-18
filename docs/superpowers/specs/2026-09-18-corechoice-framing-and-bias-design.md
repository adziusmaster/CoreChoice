# CoreChoice Framing, Domain-Aware Advice and Onboarding — Design

**Date:** 2026-09-18
**Status:** Accepted
**Ships as:** 1.2.0 (`ApplicationDisplayVersion` 1.2.0, `ApplicationVersion` **3**)
**Sequences behind:** the multi-language design (1.1.0/2), which is specced but not yet in the csproj

## Goal

Three faults reported from real use, which turn out to share one cause:

1. A first-time user does not know what to do. The app opens on its most demanding screen,
   and the personality test — the thing that makes every answer worth having — is buried in
   a tab nobody lands on.
2. The two-option form is unclear. People arrive with a situation, not a pair of options.
3. The advice is biased on relationship questions and short of context.

The cause shared by (2) and (3): **the form manufactures the binary**. `DilemmaViewModel`
requires both options before it will submit, and the shared prompt opens *"helping one person
decide between exactly two options."* Someone whose situation is "things have been bad for
months" must compress it into *leave* / *stay* before the app will listen. Once framed that
way, "stay" reads as *do nothing* and argues badly against a vivid list of grievances.

After this work: the person describes their situation in their own words, the app proposes
what it thinks they are weighing, they correct it, and decisions that affect an absent person
get an answer that says what it could not know instead of a confident verdict.

## What the research says

Findings that drove specific decisions here, not background reading:

- **Sycophancy is the mechanism.** Models affirm the user's existing view roughly 49% more
  often than a human would, and people *prefer* the flattering model even when its advice is
  worse. Someone agonising over a partner describes that partner unfavourably; the model
  agrees; "leave him" is what agreement sounds like.
- **Framing changes the effect.** A model addressed as an advisor in an authoritative role
  holds its independence better than one in a warm, personal register. This is why the
  hardening in §3 is global rather than only applied to relationship questions.
- **Conflict sensitivity is unmeasured.** Models routinely flatten context and mishandle
  asymmetric situations, and no safety institute tests for it. This is why §5 specifies an
  eval suite rather than trusting unit tests over prompt text.
- **Volume.** Nearly half of US adults under 30 have already sought relationship advice from
  AI. This class of question is not an edge case to be handled defensively; it is a main
  path.

Sources are listed at the foot of this document.

## Governing decision: two or three options, never more

`OptionA`/`OptionB` are baked three layers deep — `DecisionAnalysis`, the wire contracts in
`Endpoints/Contracts.cs`, and the on-device `DecisionRow` (`OptionA`, `OptionB`,
`OptionAJson`, `OptionBJson`). All of it moves to a list: `IReadOnlyList<OptionAssessment>`
in the domain and on the wire, an `OptionsJson` column replacing the four option columns, the
`AnswerView` layout, and the model's JSON output schema.

This is affordable because **there is no shipped history to migrate** — the closed test has no
testers yet. It would not have been affordable later, which is the only reason the earlier
draft of this spec held at two.

**Three is a hard ceiling, and the app proposes rather than the person adding.** The framing
step returns two or three options, deciding for itself whether a genuine third path exists.
The person may delete down to two; there is no "add another" beyond three. The product's own
thesis is that more options is the problem — an open-ended list rebuilds the spiral the app
exists to stop. Three is enough to break a false binary without reopening it.

A third option is not a substitute for §3's reframe. When someone's real answer is *"say the
thing you haven't said to him yet"*, that still belongs in **what you would need to find
out** — an action that changes the situation is not a fourth thing to choose between.

## 1. The framing endpoint

`POST /api/dilemma/frame`. **Costs one coin, and the analysis it leads to is then free.**
Follows the existing contract style in `Endpoints/Contracts.cs` — `internal sealed record`,
mapped to the domain at the boundary so a malformed payload is a 400 and never an exception
raised mid-assembly.

```csharp
internal sealed record FrameDilemmaRequest(Guid DeviceId, string Description);

internal sealed record FrameDilemmaResponse(
    Guid FramingId,                    // the receipt; redeemable once, for one analysis
    IReadOnlyList<string> Options,     // two or three, never more
    bool Interpersonal,                // an absent person is affected by this decision
    bool ContextThin,                  // too little supplied to advise well
    string? ContextPrompt,             // one concrete question, non-null only when ContextThin
    int Balance);
```

### Two paths, one coin

| Path | Coin spent at | Then |
|---|---|---|
| Write the options yourself | the analysis | as today |
| Describe the situation | the framing | that analysis is free |

Exactly one coin per decision either way, charged where the first model call happens. This
also removes a weakness a free endpoint would have had: an uncharged endpoint that calls a
model is a way to spend the maintainer's money, which would have leaned entirely on the
origin cap and rate limiter to contain. Coin-gated, that pressure largely disappears — the
limiter stays, but it is no longer the only thing standing between a script and the bill.

**The server stores a receipt, not the words.** `FramingId` plus a redeemed flag, and nothing
else. `GenerateDecisionRequest` presents the id; the server checks it is unredeemed, marks it
spent and skips the charge. What persists server-side is an id and a boolean, which is the
same shape of thing `UsageLog` already keeps. The dilemma text is never written down — see
§2 for where saved framings actually live.

**Edits do not void the receipt.** Someone who rewrites the options heavily before asking is
still not charged again. The coin bought the decision, not a particular wording, and metering
that would be both user-hostile and impossible to explain.

**Framing joins the refund discipline.** Coin spent, anything downstream fails, coin returned
— the same invariant every other spending path already holds, including a caller
disconnecting mid-request.

**Classification is server-side and re-derived, never trusted.** `GenerateDecisionRequest`
gains `bool Interpersonal`, but `/api/decisions` re-derives it rather than believing the
client. The flag is an optimisation; the derivation is the authority. A client that could
send `Interpersonal: false` and switch off the safeguard would make the safeguard decorative.

**`ContextPrompt` is one question, not a form.** The existing context field is optional and is
skipped by exactly the people whose situations most need it. Asking one concrete question —
*"how long has this been going on?"* — at the moment the person is already engaged and before
any coin is spent, beats a permanently mandatory field that everyone learns to fill with
"n/a".

**Failure is non-blocking.** If framing errors or times out, the coin is refunded and the
person falls through to manual entry (§2, state 2, empty). A degraded feature must never be
the thing that stops someone asking.

**Cost, stated plainly.** Analyses run 700–950 tokens; framing adds roughly 300–500 more. A
described decision therefore costs more to serve than a typed one while earning the same
single coin. Framings that are paid for and never asked are the compensating case: the coin
is taken, only the cheaper call is made, and the unredeemed receipt sits waiting. Someone
exploring three phrasings of the same question pays three coins, which is both defensible and
simple to explain.

## 2. The describe-then-confirm screen

**The description becomes the context.** This is what actually fixes fault (3). The story the
person writes first *is* the context, passed into `GenerateDecisionRequest.Context`. The model
stops being starved by design rather than by nagging.

`Dilemma.MaxContextLength` rises from 1000 to **1500**. One field, one constant, one source of
truth: the description writes into it and any answer to `ContextPrompt` appends.
`MaxOptionLength` stays 500.

**One page, two states — not two pages.** `AppShell` is deliberate about what is a tab and
what is a route; this warrants neither a new route nor a back-stack entry. The Ask tab swaps
between:

**State 1 — Describe.** One large editor, "What's going on?", with the existing **Speak**
button. Voice matters more here than on the option boxes: people say far more than they type,
and volume of detail is exactly what the model lacks. The placeholder models telling a story,
not naming options. Beneath it, a quiet escape hatch — **"I already know my two options"** —
jumping to state 2 with empty fields. That is also where framing failures land.

**State 2 — Confirm.** "Sounds like you're weighing:" over the two or three prefilled,
editable option fields the framing returned. Each can be deleted down to a floor of two;
there is no control to add a fourth. The description stays visible above them, collapsed but
expandable, so it is obvious where the options came from and that the words are still in
play. When `ContextThin`, the single question sits inline — optional and prominent. Weight
and persona are unchanged.

**Saved framings live on the device.** Every framing is written to local SQLite when it
returns, asked or not, so a paid-for framing is never lost and can be returned to later. The
Ask tab surfaces unredeemed ones — "you started this on Tuesday" — and redeeming one spends
no further coin.

This is deliberately **not** server-side. The README's second invariant is *"Nothing you type
is written down — the dilemma travels in the request, is used to build the prompt, and is
never persisted"*; it is enforced by a test that was broken and watched to fail, and it is
published in the privacy policy and on the website. Saved framings are dilemma text. On the
device they sit beside the decision history that already lives there and break nothing; on
the server they would break a promise made in public. The server's half of the arrangement is
the receipt from §1 — an id and a boolean.

**The tentative wording is load-bearing.** "Sounds like you're weighing" invites correction;
"Your options are" does not. If people rubber-stamp the draft, the form's invented binary has
merely been replaced by the model's invented binary — the same failure with better manners.

**So the analysis must know.** `GenerateDecisionRequest` carries whether the options were
model-drafted and left unedited. When they were, the analysis is told these are its own
suggested framings rather than the person's considered position, and that it should say so
when neither option is really the choice facing them. This turns the risk into the app's most
useful move: *"I don't think either of these is your actual decision."*

## 3. The domain-aware prompt

The worst case today is **Gut Check on a relationship question**. Its stance reads *"be brief
and decisive… Commit to one option. Do not hedge, do not say it depends."* Pointed at "should
I leave him", the reported output is not a model quirk — it is the persona doing exactly what
it was told, on the one class of question where committing hard is least defensible.

**Composition, not duplication.** `PromptAssembler` already substitutes `{{PROFILE}}` and
`{{WEIGHT}}`. Add `{{DOMAIN}}`, resolving to an empty string normally and to the interpersonal
clause when the flag is set. Six templates plus one clause, not twelve templates.

**The clause goes last and states its own precedence.** Placed after the persona stance,
saying explicitly that where the two conflict it governs. Without that, Gut Check's "do not
hedge" and the clause's "do not recommend ending it" are contradictory instructions and the
model picks whichever it prefers. Order and precedence are the fix.

> This decision affects someone who is not here and has not been heard. You have one person's
> account. Say so plainly, once, without apology. Do not recommend ending a relationship on
> this basis. Weigh every option honestly, then name the two or three things you would need to
> know — things only the other person or a conversation could supply — that would change your
> answer either way. Confidence must not exceed {{CONFIDENCE_CEILING}}.

### Confidence is earned, not toggled

The ceiling is not a constant and is deliberately **not** a user setting.

A setting labelled "just give me a straight answer" would be switched on by precisely the
person the ceiling exists for: someone at 1am who wants permission rather than analysis. The
research is specifically that people prefer the flattering model *even when its advice is
worse*, so shipping the safeguard alongside a documented way to disable it means shipping it
to nobody who needs it. "They chose it" does not make the advice better. A setting would also
imply the guardrail is a matter of taste; it is a fact about the input — the app heard one
side.

Instead the ceiling rises as the gap closes, computed server-side from what was actually
supplied:

| What the person has given | `{{CONFIDENCE_CEILING}}` |
|---|---|
| A one-sided account, nothing more | 50 |
| Answered the `ContextPrompt` | 65 |
| Described what the other person would say, or what happened when they last raised it | 80 |

The same agency, tied to the thing that justifies the answer — and it converts the cap from a
wall into a reason to say more, which is the behaviour the app wants anyway. The thresholds
are a starting point to be tuned against the eval suite, not a finding.

Any future tone setting — brevity, warmth, how much reasoning is shown — leaves this
untouched.

**Global hardening, every persona, interpersonal or not.** Do not adopt the person's framing of
anyone absent. Make the strongest case for the option they appear to resist. Lower confidence
when the account is thin or one-sided. Sycophancy is not relationship-specific; a model will
flatter someone about a job decision too.

**Two personas are rewritten on their own terms:**

- **Warm Support** — *"the hesitation usually says something"* tells the model that doubt is
  evidence, which in a relationship resolves to *your doubt means go*. It should name the
  feeling without treating it as a verdict.
- **The Long View** — *"regret is the currency of this persona"* is fine, but regret framing
  reliably favours action over inaction. It must weigh the regret of leaving against the
  regret of staying explicitly, or it has a thumb on the scale by construction.

**One new response field, following an existing pattern.** `DecisionResponse` has
`PersonalityNote` alongside a `Personalized` bool. Mirror it with **`LimitsNote`** — what the
analysis could not know — rendered like `PersonalityNote` in `AnswerView`. One nullable column
on `DecisionRow`. Preferred over folding the unknowns into `Reasoning`, where the UI cannot
distinguish "here is my thinking" from "here is what I could not see" — precisely the
distinction that makes the reframe trustworthy.

**Write `LimitsNote` as *what would change my answer*, not as a disclaimer.** A future sibling
app (two people connecting, each side heard) would use exactly this field as the prompt for
the second person. Written as boilerplate it is a dead end; written as open questions it is a
seam. See Out of scope.

**This is not a `ContentSeed.cs` edit.** That file runs only on an empty database. Per the
README, prompt changes are new `PromptTemplate` rows at version 2 with `IsActive` flipped,
because `UsageLog.PromptVersion` records which version answered each request and that record
is worthless if a version number can mean two prompts. Production needs the SQL from
`DEPLOY.md`. `ContentSeed.cs` is still updated so fresh databases match — the seed is not the
deployment.

**Accepted trade-off.** A ceilinged confidence and a declined recommendation will feel worse
to some users than today's confident answer. The research is blunt that people prefer the
flattering model even when it is wrong. Expect a satisfaction cost on exactly the questions
where being liked and being right diverge. That is the intended behaviour, not a regression —
and the earned ceiling above is the only escape hatch offered, because it is the only one
that improves the answer rather than merely the mood.

## 4. The onboarding shell

**Architecture.** `OnboardingShell` becomes the root page when `corechoice_onboarding_seen` is
unset; `AppShell` replaces it on finish or skip. Re-entry from the Profile menu pushes it
**modally** over `AppShell` rather than swapping the root back, so returning to it cannot
strand someone outside the tabs and dismissing always lands where they came from. One page
hosting a `CarouselView` of four screens, not four routes: the back-stack stays clean and the
background persists across them, which is what makes it feel continuous rather than sliced.

**The flag lives in `Preferences`, matching `ThemeService`.** That file establishes the split —
UI state in `Preferences.Default` under `corechoice_*` keys, domain data in SQLite. It cannot
be derived from "no answers yet", because that is equally true of someone who skipped, and
landing them on the test every launch is the nagging version of this feature.

**Four screens, then the test.**

1. **What this is.** The premise: you have read the reviews, made the list, asked three
   friends and had three answers. What is missing is not information.
2. **Why the profile.** Free, complete, permanently yours — and it changes *how* you are
   advised, not whether you are. This is where the ten-minute ask is earned.
3. **How to ask.** Teaches §2's input model: describe the situation, the app works out what
   you are weighing. First-timers and returning testers both need telling, because the form
   no longer demands two options up front.
4. **What it will not do.** It hears one side, and on decisions about another person it will
   not tell you to end things — it will tell you what you would need to find out. This
   pre-frames §3 so a capped-confidence answer reads as integrity rather than failure. Without
   this screen the safeguard looks like a bug.

Then the existing `TestIntroPage` content as the handoff. **Skip for now** is present
throughout as a persistent, low-contrast affordance — never hidden.

**Resumability already exists and is not rebuilt.** `IProfileRepository` persists per item
(`SaveAnswerAsync(itemNumber, response)`), and `TestIntroPage` already distinguishes a
first-timer from someone resuming or retaking, with the button reading *"Resume · 23 of 50"*.
This work makes it findable; it does not reimplement it.

**After a skip**, Ask carries a strip reflecting real state — "Take the test" or "Resume · 23
of 50" — read from the persisted answers. Dismissible, and the dismissal sticks.

**The quiet nudge does the long-term work.** Rather than re-nagging: when an analysis runs
with no profile, `Personalized` is already false, so `AnswerView` closes with *"This was
general advice — I don't know you yet."* It appears only when the person has just felt the
loss, which is the one moment the pitch is concrete rather than promised.

**Settings gains an explicit row** — "Personality test · Resume · 23 of 50" — so the entry
point exists where someone would look for it, not only via the Profile tab.

Coins are unchanged: finishing already calls the profile-grant endpoint for its 5, idempotent
per device.

### Motion

`design/Loading.dc.html` defines the vocabulary: a `breathe` keyframe, 2.6s ease-in-out,
opacity 0.2→0.85, staggered 0.45s, Lora for statements and DM Sans for labels. Slow and
settling. The product's job is calming an over-deciding mind; bouncy motion would contradict
it.

**MAUI primitives only — no Lottie.** The vocabulary is minimal; a JSON animation runtime adds
a dependency and a visual register that fights `breathe`.

- **Between screens:** never a hard slide. Outgoing fades to 0 drifting up ~16px; incoming
  fades in from 12px below. ~420ms, `Easing.CubicOut`.
- **Within a screen:** staggered entrance — micro-label, Lora statement, supporting line,
  ~90ms apart. The same gesture as `breathe`'s stagger, tightened for text.
- **Progress:** the breathing dots already designed. Current dot solid; the rest breathing.
  An existing idiom needs no explanation.
- **Continuity:** one accent hairline extending a quarter further with each advance. A single
  element persisting across all four is what stops it reading as four unrelated slides.
- **Reduced motion:** where the system animation scale is 0, every transition becomes an
  instant cut. Someone who turned motion off usually had a reason.

**Six theme combinations.** Composed, Considered and Still, each light and dark. Every colour
comes from the semantic tokens (`Bg`, `Ink`, `Accent`, `Faint`, …), never a literal. The
`#4FD1C5` in the mockup is Composed Dark's `Accent`; hardcoding it breaks the other five.

## 5. Localisation and the testability seam

The multi-language design establishes that `CoreChoice.App.Tests` reaches app code by linking
`.cs` files **by source**, and that a file touching `Microsoft.Maui.*` cannot be linked and
therefore cannot be tested at all.

The onboarding introduces roughly fifteen new user-facing strings, and §2 introduces several
more. All of them live in a **MAUI-free composer**, following the seven that already exist,
asserted on with exact expected text. Copy that lives inside a `ContentPage` code-behind is
untestable and will drift across seven languages.

The interpersonal clause and the persona rewrites are **server-side**, and so follow the
multi-language design's §7 (the analysis answers in the person's language) and §8 (personas —
resolving the server-wins conflict). The clause is prompt text sent to the model, not UI
chrome, and is localised by the same mechanism as the rest of the prompt.

## Testing strategy

Conventions are unchanged: xUnit, NSubstitute, FluentAssertions,
`Method_StateUnderTest_ExpectedBehavior`, strict AAA. These join the existing 398.

**Fast tests:**

- `PromptAssembler`: the clause is absent when `Interpersonal` is false; present when true;
  and appears **after** the persona stance with its precedence sentence intact. A refactor
  that reorders composition would silently restore the Gut Check problem and nothing else
  would catch it.
- **Every active persona composes with the clause.** Adding a persona is "two inserts and no
  redeploy", so a seventh persona could bypass the safeguard with no code change to review.
  Iterate every active template; assert the composed prompt carries the clause.
- **A forged flag does not disable the safeguard.** `Interpersonal: false` on a plainly
  interpersonal description still gets the clause, because the server re-derives.
- **Coins and the receipt:** framing spends one; framing failure refunds it; an analysis
  presenting an unredeemed `FramingId` spends nothing and marks it redeemed; presenting it a
  second time charges normally; presenting another device's id is refused. Analysis failure
  still refunds. The existing invariant tests must pass untouched — if this design requires
  changing them, the design is wrong.
- **Edits do not void the receipt:** options rewritten wholesale between framing and analysis
  still redeem free.
- **The confidence ceiling** is 50 on a bare account, and rises only on the evidence named in
  §3 — never on a client-supplied value.
- **Options list:** framing returns two or three, never one and never four; the confirm screen
  floors deletion at two; a round-trip through `OptionsJson` preserves order.
- **Saved framings** persist locally, survive a teardown and rebuild of the database (the
  existing history test's shape), and an unredeemed one is still redeemable afterwards.
- **Description → `Context`** mapping, the 1500 cap, and the appended `ContextPrompt` answer.
- **Onboarding:** unset flag routes to `OnboardingShell`; skip sets it; modal re-entry from
  Profile leaves it set.
- **Composers:** exact expected text for every new string, in every language.

**Eval suite — separate project, trait-filtered, run deliberately.** None of the above shows
whether the prompt *works*; only running it against a real model does.

- A set of relationship dilemmas written to bait the failure — a partner described
  unfavourably, asking whether to go. Assert no response recommends ending the relationship,
  every one names the missing side, and confidence never exceeds 50.
- A control set of job and money dilemmas asserting the app still gives a confident
  recommendation. A safeguard that makes everything timid has broken the product, not fixed
  it.

Run before any prompt version is promoted. This matches existing practice: the no-persistence
claim was verified against a live Gemini call, and the keying was checked against all 110
single-swap corruptions.

**Stated limitation.** An eval suite samples behaviour; it does not prove it. It will catch a
prompt regression that switches the safeguard off. It will not guarantee the model never says
something bad on an input nobody imagined. The reframe must not be described to users as a
guarantee.

## Out of scope

- **A fourth option, or a person-supplied one beyond three.** Three is a ceiling, not a
  starting point; see Governing decision.
- **A setting that lifts the confidence ceiling.** Rejected on the evidence, not deferred;
  see §3. A tone setting — brevity, warmth, reasoning shown — remains possible and would
  leave the ceiling alone.
- **Server-side storage of framings.** They live on the device. Moving them would break the
  "nothing you type is written down" invariant; see §2.
- **The two-person sibling app** — connecting both people, translating each into the other's
  profile, a communication-style instrument. It is a different product: it breaks the
  "nothing you type is written down" invariant, needs durable identity where there is
  deliberately none, and carries a safety problem this spec does not solve (a mediator that
  makes a controlling message more palatable, and lends it the app's authority). Its own spec,
  when it is taken on. `LimitsNote`'s wording is the only accommodation made for it here.
- **Replacing Big Five for communication style.** Big Five predicts some communication
  tendencies but was not built for it. The sibling app needs its own validated instrument.
- **A shortened first-pass test.** Ten items of IPIP-50 is not a valid Big Five measure; it
  would need a different instrument (Mini-IPIP, BFI-10), which sits badly against "the
  measurement is the real thing" and the keying tests.
- **Detecting abuse or crisis.** Named as a known gap, not addressed here.

## Sequencing

1. Multi-language (1.1.0/2) ships first. This spec assumes its composer seam exists.
2. Domain and contracts move from `OptionA`/`OptionB` to a list, everywhere at once. Doing
   this first means nothing below is written twice.
3. Server: `/api/dilemma/frame` with the coin charge and the `FramingId` receipt, the
   `Interpersonal` re-derivation, the earned ceiling, `{{DOMAIN}}` and
   `{{CONFIDENCE_CEILING}}` in `PromptAssembler`, `LimitsNote` through the contracts. Fast
   tests throughout.
4. Prompt templates at version 2 via `DEPLOY.md` SQL; `ContentSeed.cs` updated to match.
5. Eval suite, including the ceiling thresholds; promote the prompt version only once it
   passes.
6. App: describe-then-confirm on the Ask tab, the options list in `AnswerView`, `LimitsNote`,
   the raised context cap, locally saved framings and the unredeemed-framing prompt.
7. App: `OnboardingShell`, the four screens, motion, the Settings row, the skip strip.
8. Closed-testing round before promoting to production.

## Sources

- [Sweet Talkers: How Query Formulation Shapes Sycophancy in Romantic Relationship Advice](https://arxiv.org/html/2609.13841)
- [Sycophantic AI decreases prosocial intentions and promotes dependence (Science)](https://www.science.org/doi/10.1126/science.aec8352)
- [AI overly affirms users asking for personal advice (Stanford)](https://news.stanford.edu/stories/2026/03/ai-advice-sycophantic-models-research)
- [How Can You Avoid LLM Sycophancy? Keep it Professional (Northeastern)](https://news.northeastern.edu/2026/02/23/llm-sycophancy-ai-chatbots/)
- [Can AI Make Conflicts Worse? An Alignment Failure in LLM Deployment Across Conflict Contexts](https://arxiv.org/html/2605.22720v1)
- [AI chatbots are sucking up to you — with consequences for your relationships (Scientific American)](https://www.scientificamerican.com/article/ai-chatbots-are-sucking-up-to-you-with-consequences-for-your-relationships/)
- [App Onboarding Flow Benchmarks: Where Users Drop Off](https://semnexus.com/app-onboarding-flow-benchmarks-where-users-drop-off-2026)
