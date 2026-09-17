# Big Five and Decision Making: What the Evidence Actually Supports

**Purpose.** Establish the evidence base for CoreChoice's per-trait decision advice, so that user-facing copy can be written *from* research rather than from plausible-sounding trait description.

**Date of review:** 2026-09-17
**Status:** Research only. No copy written here. Nothing in this file is user-facing text.

---

## 0. How to read this document

### 0.1 Verification policy

Every citation below carries a verification tag:

- **[VERIFIED]** — I located the paper (publisher page, PubMed, PMC, or journal archive) in this review and confirmed authors, year, title and journal. Where I also confirmed volume/pages, they are given.
- **[PARTIAL]** — The paper exists and I confirmed authors/title/journal/year, but I could **not** access the full text or the results table; the numbers quoted come from abstracts or reputable secondary summaries and should be re-checked against the original before any number is used in copy.
- **[UNVERIFIED]** — I saw the claim cited somewhere but could not confirm the source. **Do not use in copy.** Listed only so nobody re-discovers it and assumes it is solid.

Several paywalls (ScienceDirect, Taylor & Francis, the Steel 2007 PDF) blocked full-text access. Where that happened I have said so rather than filling the gap from memory. **If a number below is tagged [PARTIAL], treat it as a lead, not a fact.**

### 0.2 How to read an effect size

This matters more than anything else in this document, because it is where the app is most likely to overclaim.

A correlation describes how two *distributions* line up across a population. It does not describe an individual. Translating r into "how often does the high-trait person actually differ from the low-trait person in the expected direction" (common-language effect size, CLES):

| r | approx. d | CLES: chance a randomly chosen high-trait person exceeds a randomly chosen low-trait person | Variance explained |
|---|---|---|---|
| .10 | 0.20 | ~56% | 1% |
| .20 | 0.41 | ~61% | 4% |
| .30 | 0.63 | ~67% | 9% |
| .40 | 0.87 | ~73% | 16% |
| .60 | 1.50 | ~86% | 36% |

**A coin flip is 50%.** So an r = .20 finding — which is a perfectly respectable, publishable, real population effect — means that if you pick one high scorer and one low scorer at random, the high scorer is on the predicted side of the comparison 61% of the time. Forty percent of the time, they are not.

**Practical rule adopted in this document:**

- r ≥ .45 — strong enough to say something in the second person ("you tend to…"), with hedging.
- r = .25–.45 — say it as a tendency the user can check against themselves ("people who score like you often…, does that fit?"). Never as fact.
- r < .25 — **do not personalise.** It is real in aggregate and useless for one person. Either omit, or state it as a population fact about people in general, not about the reader.

Almost everything in this literature falls in the third band. That is the single most important finding of this review.

### 0.3 Cross-cutting caveats that apply to every claim below

1. **Self-report on both sides.** Most trait↔decision-style findings correlate one questionnaire with another questionnaire, both completed by the same person in the same sitting. Shared method variance inflates these correlations, and some of the overlap is semantic rather than behavioural: the GDMS avoidant item "I avoid making important decisions until the pressure is on" and a conscientiousness item about procrastination are close to paraphrases. When the criterion is a *behavioural task* instead of a questionnaire, trait correlations shrink sharply (see §3 Conscientiousness, and Bruine de Bruin et al. 2007).
2. **WEIRD samples.** Henrich, Heine & Norenzayan (2010), *Behavioral and Brain Sciences* [VERIFIED] — the overwhelming majority of this literature is Western, educated, and heavily undergraduate. Sample sizes of 200–500 psychology students are the norm in the decision-styles literature specifically. The maximisation, indecisiveness and GDMS literatures are almost entirely WEIRD.
3. **Replication.** Soto (2019), *Psychological Science*, 30(5), 711–727 [VERIFIED] — the Life Outcomes of Personality Replication Project pre-registered high-powered replications (median N = 1,504) of 78 published trait–outcome associations. **87% replicated in the expected direction, but replication effects were typically only 77% as strong as the originals.** So: the direction of Big Five findings generally holds up; the magnitude in the original papers is generally overstated by about a quarter. Every effect size in this document should be mentally discounted accordingly.
4. **Baseline expectation for trait effects.** Zell & Lesick (2022), *Journal of Personality*, 90(4), 559–573 [VERIFIED] — synthesis of 54 meta-analyses (k = 2,028, N = 554,778) found Big Five↔performance associations that the authors themselves characterise as **small**. This is the realistic ceiling for what a trait score buys you.
5. **The middle of a trait is under-studied by construction.** Nearly all of this evidence is linear correlation across a continuum, or extreme-group contrasts. Almost no study in this literature tests whether mid-scorers differ from either end in a way that is distinct rather than merely intermediate. **There is essentially no research basis for saying anything specific about "moderate" on any trait.** See §9.
6. **The Barnum problem.** Forer (1949); reviewed in Furnham & Schofield (1987), *Current Psychology*, "Accepting personality test feedback: A review of the Barnum effect" [VERIFIED]. People rate vague, generally-favourable personality feedback as highly accurate about themselves *regardless of whether it was derived from their actual scores*. **A user telling us the passage "really fits" is not evidence the passage is right.** Any in-app validation of copy quality by user agreement is confounded by this and should not be used as proof the rebuild worked.

---

## 1. Openness to Experience

### High Openness

**Claim 1.1 — High openness goes with higher risk propensity, and this is the strongest Big Five↔risk link there is.**
Highhouse, Wang & Zhang (2022), "Is risk propensity unique from the big five factors of personality? A meta-analytic investigation", *Journal of Research in Personality* [VERIFIED]. 6,644 correlations, 133 independent samples, **N = 69,125**. Openness showed the strongest relation to risk propensity of any Big Five trait, **ρ = .30**.
*Evidence quality: WELL REPLICATED (very large meta-analysis, direction consistent across the literature).*
*But:* ρ = .30 → ~67% CLES. And the same meta-analysis found **the whole Big Five together account for only 22% of the variance in risk propensity**, and that risk propensity predicts risky outcomes over and above the Big Five. So the honest reading is: *openness is the trait most related to risk appetite, and it still tells you rather little about any given person's risk appetite.* This is the cleanest illustration in the whole review of a robust finding that is nonetheless weak for personalisation.

**Claim 1.2 — Openness is associated with preferring to think things through (need for cognition).**
Fleischhauer et al. (2010), "Same or different? Clarifying the relationship of need for cognition to personality and intelligence", *Personality and Social Psychology Bulletin* [VERIFIED — exists; full text not accessed]. A meta-analysis of need for cognition across 76 independent samples found its strongest relationships with general cognitive ability, the openness facet of the Big Five, and conscientiousness — presented at Academy of Management Proceedings (2013) [PARTIAL: conference proceedings, lower evidentiary tier]. Individual studies report openness↔NFC around r ≈ .40–.50 [PARTIAL].
*Evidence quality: WELL REPLICATED in direction, but note the construct-overlap problem — need for cognition and the "ideas" facet of openness are arguably measuring overlapping things, so a chunk of this correlation is definitional, not empirical.*

**Claim 1.3 — Openness and decision-making styles.**
Multiple studies report openness predicting higher rational *and* higher intuitive style scores, and contributing to spontaneous style [PARTIAL — these come from small single-sample studies, typically N = 200–500 students, with inconsistent signs across studies]. I could **not** find a meta-analysis of Big Five↔GDMS associations. See §7.
*Evidence quality: THIN / MIXED. The pattern "openness predicts both rational and intuitive" is itself a warning sign that the styles instrument is picking up general engagement with decisions rather than a distinct style.*

**Claim 1.4 — Openness and delay discounting.**
A large-sample analysis (N ≈ 5,888) reported openness r = −.05 with discounting [PARTIAL — sourced from a secondary summary in Frontiers in Psychology (2021), "Individual Differences in Intertemporal Choice"; original not accessed].
*Evidence quality: THIN, and the effect is far too small to mention at all.*

### Low Openness

There is no separate literature on low openness and decision-making. Every finding above is a linear correlation; "low openness" means only "the other end of the same line". The defensible statements are the mirror images of 1.1 and 1.2, with the same weak effect sizes, and nothing more.
*Evidence quality: N/A — inferred by reflection, not directly studied.*

### Moderate Openness

**Nothing.** No study located tests mid-range openness as a distinct group.

---

## 2. Conscientiousness

This is the strongest trait in the whole review, and essentially for one reason: procrastination.

### High Conscientiousness

**Claim 2.1 — Conscientiousness is strongly (inversely) related to procrastination. This is the single largest effect in this document.**
Steel, P. (2007), "The nature of procrastination: A meta-analytic and theoretical review of quintessential self-regulatory failure", *Psychological Bulletin*, 133(1), 65–94 [VERIFIED for citation; **[PARTIAL]** for the numbers — the full-text tables were behind a 406/paywall in this session]. Based on **691 correlations**. Conscientiousness showed **the largest average effect size, r ≈ .63** (negative direction: higher conscientiousness, less procrastination). The strong and consistent predictors were task aversiveness, task delay, self-efficacy, impulsiveness, and conscientiousness with its facets of self-control, distractibility, organisation and achievement motivation. **Neuroticism, rebelliousness and sensation seeking showed only a weak connection to procrastination** — which contradicts a common intuition and should be flagged in copy.
*Evidence quality: WELL REPLICATED.* r = .63 → ~86% CLES → variance explained ~36%. **This is the only finding in this review that clears the bar for second-person statement.**
*Caveat that must survive into copy:* both sides are self-report, and the overlap is partly semantic (conscientiousness items and procrastination items share content). The honest framing is "these are near-synonyms measured two ways", not "your trait causes your delay".
⚠️ **Action item: re-verify the exact r and facet values against the published tables before any number appears in copy or docs.**

**Claim 2.2 — Conscientiousness relates to a rational/deliberative decision style.**
Reported repeatedly: rational style positively associated with conscientiousness; avoidant and spontaneous styles negatively associated with conscientiousness and emotional stability [PARTIAL — consistent direction across several small studies; no meta-analysis found].
*Evidence quality: MIXED-to-REPLICATED in direction, effect sizes generally r ≈ .20–.35, i.e. below the personalisation bar. Same self-report/semantic-overlap caveat as 2.1.*

**Claim 2.3 — Deliberation only weakly improves decision *performance*.**
Phillips, Fletcher, Marks & Hine (2016), "Thinking styles and decision making: A meta-analysis", *Psychological Bulletin* [VERIFIED — Psych Bulletin, 2016; volume/pages [PARTIAL]]. Pooled **N = 17,704 across 89 samples**. Reflective thinking style → decision performance **r = .11**; → decision experience (speed, enjoyment) **r = .14**. Intuitive style → performance **r = −.09**; → experience **r = .06**.
*Evidence quality: WELL REPLICATED, and the news is deflationary.* **Being a deliberative thinker buys you r = .11 on decision quality.** That is a 3% edge in CLES terms over a coin flip. Any app copy implying "your conscientiousness makes you a better decider" is not supported. The authors' own conclusion is that the style↔outcome link is context-dependent.

**Claim 2.4 — Decision-making competence is better predicted by cognitive ability than by personality.**
Bruine de Bruin, Parker & Fischhoff (2007), "Individual differences in adult decision-making competence", *Journal of Personality and Social Psychology*, 92(5), 938–956 [VERIFIED]; see also Bruine de Bruin, Parker & Fischhoff (2020), "Decision-making competence: More than intelligence?", *Current Directions in Psychological Science* [VERIFIED]. A-DMC correlates with socioeconomic status, cognitive ability, and decision styles, and predicts real-world decision outcomes over and above demographics and cognitive ability.
Dewberry, Juanchich & Narendran (2013), "Decision-making competence in everyday life: The roles of general cognitive styles, decision-making styles and personality", *Personality and Individual Differences*, 55(7), 783–788 [VERIFIED citation; full text paywalled [PARTIAL]] — personality showed incremental validity over decision-making styles in predicting decision outcomes, and general cognitive styles did **not** add over decision styles.
*Evidence quality: REPLICATED. Implication for the app: personality is a secondary predictor of actually deciding well. Do not imply the trait profile explains decision quality.*

### Low Conscientiousness

**Claim 2.5 — Low conscientiousness is the best-evidenced trait-level statement in this review: it goes with delaying decisions.**
This is Claim 2.1 read from the other end, and it is the one place where an extreme-group reading is genuinely supported by the size of the effect.
*Evidence quality: WELL REPLICATED.*

**Claim 2.6 — Low conscientiousness and sunk cost.**
Frequently asserted; I found **no** adequately powered study supporting it. The literature I located consists of small fMRI and finance-professional samples.
*Evidence quality: THIN / ESSENTIALLY ABSENT. Do not say this.*

### Moderate Conscientiousness

**Nothing specific.** Procrastination research treats conscientiousness continuously; there is no evidence that mid-scorers have a characteristic decision pattern distinct from "in between".

---

## 3. Extraversion

### High Extraversion

**Claim 3.1 — Extraversion relates to risk-taking and reward sensitivity.**
Highhouse, Wang & Zhang (2022) [VERIFIED] — extraversion relates to risk propensity but **less strongly than openness** (openness was the strongest at ρ = .30; extraversion weaker). Nicholson, Soane, Fenton-O'Creevy & Willman (2005), "Personality and domain-specific risk taking", *Journal of Risk Research*, 8(2), 157–176 [VERIFIED], **N = 2,041**, NEO-PI-R: overall risk propensity was characterised by **high extraversion and openness with low neuroticism, agreeableness and conscientiousness**.
The theoretical account is Gray's reinforcement sensitivity theory (extraversion ↔ behavioural approach system ↔ reward sensitivity), which is well established as a theory but whose behavioural predictions are inconsistently supported.
*Evidence quality: REPLICATED in direction; effect sizes modest (ρ well under .30 for extraversion specifically).* **Below the personalisation bar.**

⚠️ **A claim I could not verify and which should not be used:** a 2025 Frontiers paper (He & Lei, 2025, *Frontiers in Psychology*, 16:1537658 [VERIFIED as a paper]) cites "Wang et al. (2022)" as a meta-analysis showing extraversion↔risk-taking r = 0.31. **I could not locate that meta-analysis.** [UNVERIFIED] The He & Lei study itself is **N = 110 undergraduates**, cross-sectional, with a lab dice task — far too small and too narrow to support anything.

**Claim 3.2 — Extraversion predicts overconfidence.**
Schaefer, Williams, Goodie & Campbell (2004), "Overconfidence and the Big Five", *Journal of Research in Personality*, 38, 473–480 [VERIFIED]. With the other four traits controlled, **extraversion significantly predicted overconfidence** (confidence minus accuracy) on a cognitive task; openness predicted confidence and accuracy but *not* overconfidence.
*Evidence quality: MIXED / THIN.* Single study, undergraduate sample, one task. The broader overconfidence literature (e.g. Moore's "three faces of overconfidence") holds that overconfidence is not a stable unitary individual difference, which undercuts trait explanations of it. **Interesting, not solid. Do not personalise.**

**Claim 3.3 — Extraversion and decision speed.**
I found no adequately powered evidence that extraverts decide faster. The claim is intuitive and widely repeated; I could not source it.
*Evidence quality: ABSENT. Do not say this.*

### Low Extraversion

Nothing studied directly. Mirror-image of the above, with the same weak effects.

### Moderate Extraversion

**Nothing.**

---

## 4. Agreeableness

**This is the weakest trait in the review by a wide margin, and that is the honest headline.**

### High Agreeableness

**Claim 4.1 — Agreeableness and taking advice / deferring to others.** This is the claim the app would most want to make, and it is **not supported**.
Bailey, Leon, Ebner, Moustafa & Weidemann (2022/2023), "A meta-analysis of the weight of advice in decision-making", *Current Psychology*, 42(28), 24516–24541 [VERIFIED — full text read]. **346 effect sizes, 129 independent datasets, N = 17,296.** Pooled weight of advice = **0.39** [95% CI 0.37–0.42] — people move about 39% of the way toward advice. The only unique predictor of advice-taking was **information about the advisor's quality** (adjustments of 32% / 37% / 48% for low / neutral / high implied advisor quality). **Sample characteristics — age, gender, individualism — had no effect.**
Critically: **this meta-analysis did not find, and does not report, personality moderators of advice-taking.** The field's largest synthesis of advice-taking has essentially nothing to say about traits.
*Evidence quality on "agreeable people take more advice": ABSENT at meta-analytic level.* Scattered small studies on AI-advice uptake report agreeableness and neuroticism associated with more use of AI advice and openness with less [PARTIAL, small samples, AI-specific, not generalisable].

**Claim 4.2 — Agreeableness and conformity.**
Directly contested. Some sources describe high agreeableness as the most consistent predictor of normative conformity; others state there is no evidence that agreeable people are more conforming, compliant or submissive than others.
*Evidence quality: MIXED / CONTESTED. Do not say this.*

**Claim 4.3 — Agreeableness and risk.**
Nicholson et al. (2005) [VERIFIED]: low agreeableness formed part of the risk-propensity profile in an N = 2,041 NEO-PI-R sample. Highhouse et al. (2022) [VERIFIED]: agreeableness among the weaker Big Five correlates of risk propensity.
*Evidence quality: REPLICATED in direction, effect very small. Below the personalisation bar.*

**Claim 4.4 — Agreeableness and decision styles.**
Reported as a predictor of dependent style and (inconsistently) intuitive style [PARTIAL, small studies, inconsistent signs].
*Evidence quality: THIN.*

### Low Agreeableness

Nothing studied directly.

### Moderate Agreeableness

**Nothing.**

**Summary for agreeableness: there is no well-evidenced, effect-size-adequate finding linking agreeableness to any decision behaviour. This trait should say very little or nothing in the app.**

---

## 5. Neuroticism

### High Neuroticism

**Claim 5.1 — Neuroticism is the strongest personality correlate of indecisiveness.**
Germeijs & Verschueren (2011), "Indecisiveness and Big Five personality factors: Relationship and specificity", *Personality and Individual Differences*, 50, 1023–1028 [VERIFIED]. **N = 543 adolescents**, longitudinal across Grade 12. Neuroticism was the strongest correlate of indecisiveness (strong positive); extraversion and conscientiousness negatively associated; openness small negative; **agreeableness not significant**. Indecisiveness predicted decisional problems *even after controlling for the Big Five* — i.e. it is not reducible to trait neuroticism.
*Evidence quality: REPLICATED in direction across the indecisiveness literature; but this specific study is adolescents in one country, and exact r values were not accessible [PARTIAL].* The "specificity" result matters for the app: **measuring neuroticism is a worse way to know whether someone is indecisive than asking them whether they are indecisive.**

**Claim 5.2 — Neuroticism, maximising, and regret.**
Schwartz, Ward, Monterosso, Lyubomirsky, White & Lehman (2002), "Maximizing versus satisficing: Happiness is a matter of choice", *Journal of Personality and Social Psychology* [VERIFIED as a paper; volume/pages [PARTIAL]]. Maximising correlates positively with regret, depression and perfectionism, and negatively with life satisfaction, optimism and self-esteem.
**But the construct is a mess, and this is the key honesty flag.** Cheek & Schwartz (2016), "On the meaning and measurement of maximization", *Judgment and Decision Making*, 11(2), 126–146 [VERIFIED — full text read]. Schwartz's own co-author documents **11 different maximisation scales measuring incompatible constructs**, and notes "much of the theorizing about maximization has followed, rather than preceded, scale development and analysis". They argue decision difficulty and regret should be treated as *outcomes or moderators*, not components of maximising — which means the famous "maximisers are miserable" finding is partly an artefact of building misery into the scale. **Scales emphasising high standards alone (MTS) show adaptive or neutral outcomes; scales including alternative search and decision difficulty show maladaptive ones.**
Downstream: the original Maximization Scale correlated with neuroticism and indecision; the high-standards-only MTS correlated only with regret and was unrelated to life satisfaction [PARTIAL]. Nenkov, Morrin, Ward, Schwartz & Hulland (2008), *Judgment and Decision Making* [VERIFIED as a paper] established the three-factor structure (alternative search / decision difficulty / high standards).
*Evidence quality: MIXED, and actively disputed by the originating authors.* At least one published dataset reports that maximisers "are about as happy as satisficers, and don't have a greater likelihood of being indecisive, avoidant or neurotic".
**Implication: the app must not tell anyone that their neuroticism makes them a miserable maximiser. The construct does not currently support it.**

**Claim 5.3 — Neuroticism and decision avoidance / avoidant style.**
Avoidant style is negatively associated with emotional stability (i.e. positively with neuroticism) and is the style most consistently linked to poor outcomes. Bavoľár & Orosová (2015), "Decision-making styles and their associations with decision-making competencies and mental health", *Judgment and Decision Making*, 10(1), 115–122 [VERIFIED — full text read]. **N = 427 Slovak students.** Avoidant style ↔ decision-making competence r = −.29 to −.32; avoidant style predicted worse well-being (β = −.29) and higher depression (β = .35); intuitive style predicted better well-being (β = .25) and lower depression (β = −.26).
⚠️ **Important correction to a natural assumption: this study did *not* measure the Big Five at all.** It links *styles* to competence and mental health, not *traits* to anything. It cannot be cited as trait evidence.
*Evidence quality: the style↔outcome links are REPLICATED in direction; the trait↔style bridge is THIN.*

**Claim 5.4 — Neuroticism and procrastination is WEAKER than people assume.**
Steel (2007) [VERIFIED citation / [PARTIAL] numbers]: **neuroticism showed only a weak connection to procrastination**, in contrast to conscientiousness's r ≈ .63. The apparent neuroticism link runs largely through impulsiveness and self-efficacy.
*Evidence quality: WELL REPLICATED. This is a good candidate for a "counter-intuitive but true" line — anxiety is not the main engine of delay; low self-discipline is.*

**Claim 5.5 — Neuroticism and career indecision.**
A recent meta-analysis (65 studies, 82 samples, N = 33,968, 1977–2023) reports neuroticism as a consistent direct predictor of higher career indecision, with openness, agreeableness and conscientiousness negatively associated but **inconsistent across studies** [PARTIAL — published in *Cogent Business & Management* (2026), a lower-tier open-access journal; I could not access the full text or the effect sizes]. Treat as suggestive only.
*Evidence quality: PROMISING BUT UNVERIFIED IN DETAIL. Do not quote numbers.*

### Low Neuroticism

Nicholson et al. (2005) [VERIFIED]: low neuroticism forms part of the risk-propensity profile. Otherwise nothing studied directly; the mirror of the above.

### Moderate Neuroticism

**Nothing.**

---

## 6. Cross-trait findings worth knowing

**Behavioural tasks vs questionnaires.** When decision behaviour is measured by actual task performance rather than by self-report style inventories, Big Five correlations get much smaller and much less consistent. I was unable to access the full text of the most directly relevant paper — "Relationships between the big five personality characteristics and performance on behavioral decision making tasks", *Personality and Individual Differences* (2020) [VERIFIED as existing; paywalled, [PARTIAL]] — and flag it as the highest-value item for a follow-up with library access, because it is the direct test of whether traits predict decision *behaviour* as opposed to decision *self-description*.

**Risk propensity is largely its own thing.** Highhouse et al. (2022) [VERIFIED] found the Big Five collectively explain only **22%** of risk-propensity variance, and risk propensity predicts risky outcomes over and above the Big Five. If the app wants to say something about a user's risk appetite, **the Big Five is the wrong instrument; ask about risk directly.**

**Delay discounting.** Reported correlations in a large sample (N ≈ 5,888): extraversion r = .10, neuroticism r = .09, conscientiousness r = −.09, openness r = −.05 [PARTIAL, secondary source]. Also: "Delay discounting, cognitive ability, and personality: What matters?", *Psychonomic Bulletin & Review* (2021) [VERIFIED as existing]. **All effects negligible. Say nothing.**

**Framing effects and sunk cost.** I searched specifically for Big Five moderators of framing susceptibility and of the sunk cost fallacy and found **no adequately powered, replicated evidence**. What exists is small investor samples, small fMRI samples, and blog-level assertion.
*Evidence quality: ABSENT. The app must say nothing about a user's susceptibility to framing or sunk cost based on their trait profile.*

---

## 7. A significant gap: there is no meta-analysis of Big Five ↔ decision-making styles

I searched specifically and repeatedly for a meta-analysis or systematic review synthesising Big Five correlations with the Scott & Bruce GDMS (Scott & Bruce, 1995, *Educational and Psychological Measurement*, 55(5), 818–831 [VERIFIED]). **I did not find one.**

What exists is a scatter of single-sample studies (typically N = 200–600, often students, often non-WEIRD-adjacent convenience samples in single countries) reporting inconsistent patterns. The most commonly repeated pattern — conscientiousness→rational, neuroticism→avoidant/dependent, low conscientiousness→spontaneous — is directionally consistent but the specific coefficients vary widely between studies and several reported "predictions" come from regression models where the trait sets explain roughly a quarter of style variance at best.

**Consequence for the app: any copy built on "your trait implies your decision style" rests on an unsynthesised, heterogeneous, self-report literature.** This should be the single biggest brake on confident phrasing.

There is also a live psychometric concern: a psychometric evaluation of the GDMS itself exists in *Personality and Individual Differences* [VERIFIED as existing, not accessed], and the five-style structure does not always replicate cleanly.

---

## 8. Summary table of effect sizes

Only findings where I could attach a number are listed. Sorted by usefulness for personalisation.

| # | Trait | Finding | Effect size | Source | Quality | Personalisable? |
|---|---|---|---|---|---|---|
| 2.1 | Conscientiousness | ↓ procrastination | r ≈ .63 (691 correlations) | Steel 2007, Psych Bull | WELL REPLICATED (numbers [PARTIAL]) | **Yes, with hedging** |
| 1.1 | Openness | ↑ risk propensity | ρ = .30 (N = 69,125) | Highhouse et al. 2022, JRP | WELL REPLICATED | Borderline — tendency only |
| 1.2 | Openness | ↑ need for cognition | r ≈ .40–.50 | Fleischhauer et al. 2010 | REPLICATED, construct overlap | Borderline — tendency only |
| 5.1 | Neuroticism | ↑ indecisiveness | "strongest correlate", exact r not accessed | Germeijs & Verschueren 2011, PAID | REPLICATED in direction | Tendency only |
| — | All | Big Five → risk propensity, combined | R² = .22 | Highhouse et al. 2022 | WELL REPLICATED | *Deflationary* |
| 2.3 | (styles) | Reflective style → decision performance | r = .11 (N = 17,704) | Phillips et al. 2016, Psych Bull | WELL REPLICATED | **No** |
| 2.3 | (styles) | Intuitive style → decision performance | r = −.09 | Phillips et al. 2016 | WELL REPLICATED | **No** |
| 5.3 | (styles) | Avoidant style → decision competence | r = −.29 to −.32 (N = 427) | Bavoľár & Orosová 2015, JDM | Single study; **no Big Five measured** | **No** |
| 5.3 | (styles) | Avoidant style → depression | β = .35 | Bavoľár & Orosová 2015 | Single study, cross-sectional | **No** |
| 4.1 | — | Weight of advice, everyone | 0.39 [.37–.42] (N = 17,296) | Bailey et al. 2022, Curr Psych | WELL REPLICATED | *Population fact, no trait moderators found* |
| 4.1 | — | Advisor quality → advice taken | 32% / 37% / 48% | Bailey et al. 2022 | WELL REPLICATED | *Situational, not dispositional* |
| 3.1 | Extraversion | ↑ risk propensity | weaker than openness's .30 | Highhouse et al. 2022; Nicholson et al. 2005 | REPLICATED, small | **No** |
| 3.2 | Extraversion | ↑ overconfidence | significant β, single study | Schaefer et al. 2004, JRP | THIN | **No** |
| — | Various | Delay discounting | \|r\| ≤ .10 | secondary source | THIN | **No** |
| 5.4 | Neuroticism | procrastination | "weak" | Steel 2007 | WELL REPLICATED (as a *null-ish* result) | **No — and correct a myth** |
| — | Meta | Replication shrinkage | replications ≈ 77% of original effect | Soto 2019, Psych Science | WELL REPLICATED | *Apply to everything above* |

---

## 9. The "moderate" level: a direct answer

**There is essentially no research basis for telling a mid-scorer anything about their decision-making.**

Reasons, in order of importance:

1. **Design.** This literature models traits as continuous linear predictors. A linear model *cannot* say anything about the middle that is not simply "halfway between the ends". No study I located tested non-linear or quadratic trait effects on decision behaviour, or treated mid-scorers as a group with distinct properties.
2. **Where studies do use groups, they contrast extremes.** Extreme-group designs by construction discard the middle.
3. **Measurement error is proportionally worst in the middle.** With typical IPIP-50 scale reliabilities, a mid-range score is the least diagnostic score a person can get — the confidence interval around it spans a large part of the distribution, and retest movement can flip a mid-scorer's rank.
4. **The one exception I found is not an exception.** Germeijs & Verschueren's cluster analysis produced overcontrolled / undercontrolled / resilient clusters, but these are *profile* types across several traits, not "moderate on one trait". They are not a basis for per-trait moderate copy.

**Recommendation: the app should have no "moderate" passages at all, for any trait.** If a level system is required by the UI, the moderate state should say something true about the measurement rather than about the person — e.g. that this score does not distinguish them, and that the research has little to offer someone in the middle. That is honest, it is defensible, and it is more informative than a fabricated middle description.

---

## 10. The personalisation premise: does knowing someone's Big Five allow better-targeted decision advice?

**Short answer: the trait–outcome links are established; the personalisation benefit is largely untested. The app should assume it has no evidence for the premise and speak accordingly.**

In more detail:

**What is established.** Big Five traits correlate with self-reported decision styles, procrastination, indecisiveness, and risk propensity, at effect sizes mostly in the r = .10–.30 band, with one exception (conscientiousness↔procrastination). Soto (2019) shows these links generally replicate at about three-quarters of original magnitude. Zell & Lesick (2022) show that across 54 meta-analyses the trait↔performance associations are small.

**What is not established: that tailoring advice to those traits works better than not tailoring it.**

The closest supporting evidence is from *persuasion*, not advice:

- Hirsh, Kang & Bodenhausen (2012), "Personalized persuasion: Tailoring persuasive appeals to recipients' personality traits", *Psychological Science*, 23(6), 578–581 [VERIFIED]. **N = 324**, a single product, five ads each targeting one trait. Ads were rated more positively the more they matched the recipient's dispositional motives. This is an ad-rating study, not a decision-quality study, and I found **no direct replication** of it.
- Matz, Kosinski, Nave & Stillwell (2017), "Psychological targeting as an effective approach to digital mass persuasion", *PNAS*, 114(48), 12714–12719 [VERIFIED]. Three field experiments, >3.5 million people; trait-matched ads increased clicks and purchases. **This paper is contested in print.** Eckles, Gordon & Johnson published a PNAS letter arguing the field studies face threats to internal validity; Sharp, Danenberg & Bellman published a PNAS letter arguing the results actually refute the claim that psychological targeting beats normal advertising. The authors replied. [All VERIFIED as existing exchanges.] *Evidence quality: CONTESTED.*

And note what both of these are: **evidence that trait-matched framing shifts click-through and ad liking.** Neither shows that trait-matched *advice* helps someone make a better decision, or feel better about a decision, or act on it.

**The tailoring literature that does show benefits is not personality tailoring.** Meta-analyses of computer-tailored health interventions find real but small effects (fixed effect g = 0.17 across 88 studies; web-delivered d ≈ .14–.16) [VERIFIED as meta-analyses]. But the tailoring variable in almost all of these is *behaviour, stage of change, barriers, and self-efficacy* — not Big Five traits. **These findings cannot be borrowed to support Big Five personalisation, and it would be a misrepresentation to do so.**

**Three further problems specific to this app:**

1. **The Barnum confound.** Users will rate trait-derived passages as accurate whether or not they are derived from their actual scores (Forer 1949; Furnham & Schofield 1987 [VERIFIED]). Perceived fit is not evidence of validity. If the app ever A/B tests copy, the control arm must be *the same passages assigned at random*, not no passage.
2. **Measurement noise.** IPIP-50 scores carry enough error that a substantial fraction of users near a level boundary would receive different copy on retest. Copy that speaks confidently about "you" is therefore confidently wrong for those users some of the time.
3. **Risk propensity, arguably the most decision-relevant construct here, is 78% *not* explained by the Big Five** (Highhouse et al. 2022). For several of the things a decision app most wants to know, asking directly would outperform inferring from traits.

**Conclusion for product tone.** The app may honestly claim: *"the research links some of these traits to some decision tendencies, and we'll show you what it says."* It may **not** claim: *"knowing your personality lets us give you better advice."* That second claim is, as of this review, untested for personality-based decision advice specifically, and the nearest adjacent evidence is contested.

---

## 11. What the app may honestly say

One line per trait/level: the strongest statement the evidence will actually bear. Phrasing here is deliberately plain; it is not copy, it is a ceiling on what copy may assert.

### Openness

- **High** — *May say:* "Research links higher openness to a somewhat greater appetite for risk and to enjoying thinking a problem through. The link is real across large samples but modest — it's a tendency, not a description of you." (Highhouse et al. 2022, ρ = .30.)
- **Moderate** — **SAY NOTHING.** No evidence exists about mid-range openness and deciding.
- **Low** — *May say, minimally:* "Lower openness is, on average, associated with less appetite for risk." Same weak effect. Nothing else is supported.

### Conscientiousness

- **High** — *May say:* "This is the best-evidenced link in the whole research base: higher conscientiousness goes strongly with not putting decisions off." (Steel 2007, r ≈ .63.) **This is the only place the app may use second-person phrasing.** *May also say, as a corrective:* "It does not mean you decide better — deliberation only weakly predicts decision quality" (Phillips et al. 2016, r = .11).
- **Moderate** — **SAY NOTHING** about the person. May state the measurement fact: a mid-range score doesn't distinguish you, and the research has little to say about the middle.
- **Low** — *May say:* "Lower conscientiousness is strongly associated with putting decisions off — this is one of the largest and most consistent findings in the field." *May not say* anything about sunk cost, impulsive choices, or decision quality.

### Extraversion

- **High** — *May say, weakly or not at all:* "Extraversion shows a small association with greater risk appetite." Everything else — decision speed, overconfidence, sociable decision styles — is thin or absent. **Recommend saying nothing.**
- **Moderate** — **SAY NOTHING.**
- **Low** — **SAY NOTHING.** No direct evidence.

### Agreeableness

- **High** — **SAY NOTHING.** The single largest advice-taking meta-analysis (N = 17,296) found no personality moderators and no sample-characteristic moderators at all. The conformity claim is actively contested. There is no adequately-sized finding to report.
- **Moderate** — **SAY NOTHING.**
- **Low** — **SAY NOTHING.** (The risk-profile association from Nicholson et al. 2005 is real but too small to personalise, and is better carried by the openness line.)

*If the app must show something on this screen, the honest content is a population fact with no trait attached: "People move about 39% of the way toward advice they're given, and what predicts that is how good they think the advisor is — not their personality." (Bailey et al. 2022.)*

### Neuroticism

- **High** — *May say:* "Higher neuroticism is the strongest personality correlate of finding decisions hard to settle — though how indecisive you feel predicts that better than your trait score does." (Germeijs & Verschueren 2011, including their specificity finding.) *May also say, as a corrective:* "It is **not** a strong predictor of putting things off — that's conscientiousness." (Steel 2007.)
- **Moderate** — **SAY NOTHING.**
- **Low** — **SAY NOTHING** beyond the mirror of the above; no direct evidence.

### Explicit list of trait/level combinations where the honest answer is silence

| Trait | High | Moderate | Low |
|---|---|---|---|
| Openness | speak, hedged | **SILENT** | speak, minimal |
| Conscientiousness | **speak (strongest claim available)** | **SILENT** | speak |
| Extraversion | **SILENT (recommended)** | **SILENT** | **SILENT** |
| Agreeableness | **SILENT** | **SILENT** | **SILENT** |
| Neuroticism | speak, hedged | **SILENT** | **SILENT** |

**That is 4 speaking slots out of 15.** Plus one non-personalised population fact for the agreeableness screen if the UI demands content.

### Things the app must never say, on current evidence

- That the profile predicts how *well* the user decides. (Phillips et al. 2016: r = .11 for the best style predictor. Bruine de Bruin et al. 2007: ability beats personality.)
- That the user is more or less susceptible to framing, anchoring or sunk cost. (No evidence located at all.)
- That the user is a "maximiser" who will regret decisions. (Construct disputed by its own originators — Cheek & Schwartz 2016.)
- That the user decides faster or slower than others. (No evidence located.)
- That the user takes more or less advice than others. (Largest meta-analysis found no trait moderators.)
- That knowing their personality lets the app give better advice. (Untested for this use case.)
- Anything at all about someone in the middle of a trait, as a statement about that person.

---

## 12. Reference list with verification status

**[VERIFIED] — citation confirmed in this review**

1. Bailey, P. E., Leon, T., Ebner, N. C., Moustafa, A. A., & Weidemann, G. (2022/2023). A meta-analysis of the weight of advice in decision-making. *Current Psychology*, 42(28), 24516–24541.
2. Bavoľár, J., & Orosová, O. (2015). Decision-making styles and their associations with decision-making competencies and mental health. *Judgment and Decision Making*, 10(1), 115–122. *(Note: does NOT measure Big Five.)*
3. Bruine de Bruin, W., Parker, A. M., & Fischhoff, B. (2007). Individual differences in adult decision-making competence. *Journal of Personality and Social Psychology*, 92(5), 938–956. doi:10.1037/0022-3514.92.5.938
4. Bruine de Bruin, W., Parker, A. M., & Fischhoff, B. (2020). Decision-making competence: More than intelligence? *Current Directions in Psychological Science*.
5. Cheek, N. N., & Schwartz, B. (2016). On the meaning and measurement of maximization. *Judgment and Decision Making*, 11(2), 126–146.
6. Dewberry, C., Juanchich, M., & Narendran, S. (2013). Decision-making competence in everyday life: The roles of general cognitive styles, decision-making styles and personality. *Personality and Individual Differences*, 55(7), 783–788.
7. Fleischhauer, M., et al. (2010). Same or different? Clarifying the relationship of need for cognition to personality and intelligence. *Personality and Social Psychology Bulletin*.
8. Germeijs, V., & Verschueren, K. (2011). Indecisiveness and Big Five personality factors: Relationship and specificity. *Personality and Individual Differences*, 50, 1023–1028. doi:10.1016/j.paid.2011.01.017
9. He, Y., & Lei, P. (2025). Differential pathways from personality to risk-taking. *Frontiers in Psychology*, 16, 1537658. *(N = 110 — too small to use.)*
10. Henrich, J., Heine, S. J., & Norenzayan, A. (2010). The weirdest people in the world? *Behavioral and Brain Sciences*, 33(2–3).
11. Highhouse, S., Wang, Y., & Zhang, D. C. (2022). Is risk propensity unique from the big five factors of personality? A meta-analytic investigation. *Journal of Research in Personality*.
12. Hirsh, J. B., Kang, S. K., & Bodenhausen, G. V. (2012). Personalized persuasion. *Psychological Science*, 23(6), 578–581.
13. Juanchich, M., Dewberry, C., Sirota, M., & Narendran, S. (2016). Cognitive reflection predicts real-life decision outcomes, but not over and above personality and decision-making styles. *Journal of Behavioral Decision Making*.
14. Matz, S. C., Kosinski, M., Nave, G., & Stillwell, D. J. (2017). Psychological targeting as an effective approach to digital mass persuasion. *PNAS*, 114(48), 12714–12719. *(Contested — see Eckles, Gordon & Johnson; Sharp, Danenberg & Bellman, PNAS letters.)*
15. Nenkov, G. Y., Morrin, M., Ward, A., Schwartz, B., & Hulland, J. (2008). A short form of the Maximization Scale. *Judgment and Decision Making*.
16. Nicholson, N., Soane, E., Fenton-O'Creevy, M., & Willman, P. (2005). Personality and domain-specific risk taking. *Journal of Risk Research*, 8(2), 157–176. *(N = 2,041, NEO-PI-R.)*
17. Phillips, W. J., Fletcher, J. M., Marks, A. D. G., & Hine, D. W. (2016). Thinking styles and decision making: A meta-analysis. *Psychological Bulletin*. *(N = 17,704, 89 samples.)*
18. Schaefer, P. S., Williams, C. C., Goodie, A. S., & Campbell, W. K. (2004). Overconfidence and the Big Five. *Journal of Research in Personality*, 38, 473–480.
19. Schwartz, B., Ward, A., Monterosso, J., Lyubomirsky, S., White, K., & Lehman, D. R. (2002). Maximizing versus satisficing: Happiness is a matter of choice. *Journal of Personality and Social Psychology*.
20. Scott, S. G., & Bruce, R. A. (1995). Decision-making style: The development and assessment of a new measure. *Educational and Psychological Measurement*, 55(5), 818–831.
21. Soto, C. J. (2019). How replicable are links between personality traits and consequential life outcomes? The Life Outcomes of Personality Replication Project. *Psychological Science*, 30(5), 711–727.
22. Steel, P. (2007). The nature of procrastination: A meta-analytic and theoretical review of quintessential self-regulatory failure. *Psychological Bulletin*, 133(1), 65–94.
23. Zell, E., & Lesick, T. L. (2022). Big five personality traits and performance: A quantitative synthesis of 50+ meta-analyses. *Journal of Personality*, 90(4), 559–573.
24. Furnham, A., & Schofield, S. (1987). Accepting personality test feedback: A review of the Barnum effect. *Current Psychology*.
25. Forer, B. R. (1949). The fallacy of personal validation. *Journal of Abnormal and Social Psychology*. *(Classic; confirmed as the origin of the Barnum/Forer demonstration, original not accessed.)*

**[PARTIAL] — exists, numbers not confirmed against full text**

26. "Relationships between the big five personality characteristics and performance on behavioral decision making tasks" (2020), *Personality and Individual Differences*. **Highest-priority follow-up — this is the direct trait↔behaviour test.**
27. Career indecision meta-analysis (2026), *Cogent Business & Management*, 65 studies / 82 samples / N = 33,968. Low-tier journal; effect sizes not accessed.
28. "Delay discounting, cognitive ability, and personality: What matters?" (2021), *Psychonomic Bulletin & Review*.
29. "The Need For Cognition: A Meta-Analysis Clarifying the Link to Intelligence and Personality" — Academy of Management Proceedings (2013), 76 samples. Conference proceeding.
30. Steel (2007) facet-level correlation tables — **the r ≈ .63 headline is reported consistently by multiple secondary sources but I could not open the tables. Re-verify before use.**

**[UNVERIFIED] — do not use**

31. "Wang et al. (2022)" meta-analysis reporting extraversion↔risk-taking r = .31, cited in He & Lei (2025). **I could not find this paper. Do not cite it.**
32. Any claim that low conscientiousness predicts sunk-cost susceptibility.
33. Any claim that extraversion predicts decision speed.
34. Any claim that agreeableness predicts conformity or advice-taking.

---

## 13. Recommended follow-up

1. Get library access to Steel (2007) and confirm the facet table before any number is used.
2. Get the PAID (2020) behavioural-tasks paper — it is the single most decision-relevant unread item.
3. Decide product-side whether risk appetite and indecisiveness should be **asked directly** rather than inferred from the IPIP-50. The evidence says direct measurement would be substantially more accurate for both.
4. If copy is A/B tested, the control must be randomly-assigned trait passages, not absence of passages, or the Barnum effect will make any copy look validated.
