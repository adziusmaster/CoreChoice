# CoreChoice Multi-Language Support — Design

**Date:** 2026-09-17
**Status:** Accepted
**Ships as:** 1.1.0 (`ApplicationDisplayVersion` 1.1.0, `ApplicationVersion` **2** — 1.0.0/1 is on Play)

## Goal

CoreChoice speaks the person's language everywhere it speaks at all: the interface, the
personality test, the profile summary, the advisor names, and the analysis the advisor
returns. Voice dictation listens in that language too.

## Language set

The same seven as PurePrep, plus "System":

| Code | Native name |
|---|---|
| *(empty)* | System default — **listed first, and selectable** |
| `en` | English |
| `de` | Deutsch |
| `fr` | Français |
| `es` | Español |
| `it` | Italiano |
| `pl` | Polski |
| `nl` | Nederlands |

"System" follows the OS when the OS language is one of the seven, and falls back to English
otherwise. It is a real, selectable entry — not merely the absence of a choice — so a person
who has picked Polish can get back to "whatever my phone says" without guessing which entry
that is.

`en-GB`/`en-US` exist only as Play listing locales. The app has one English.

## Governing principle: the testability seam

CoreChoice's user-facing text lives in two structurally different places, and the split is
not cosmetic.

`src/CoreChoice` targets `net10.0-android`. `CoreChoice.App.Tests` is a plain `net10.0`
project that reaches app code by **linking `.cs` files by source** (see the `<Compile
Include=... Link=...>` block in its csproj). A file that touches `Microsoft.Maui.*` cannot be
linked, and therefore cannot be tested at all. Conversely, seven MAUI-free composers
currently hold ~200 user-facing strings and six of them are asserted on with **exact expected
text**:

| Composer | Strings | Exact-text tests |
|---|---|---|
| `ProfileTraitSummaryComposer` | 85 | yes |
| `ProfileNoteComposer` | 38 | yes |
| `WaitingLineComposer` | 29 | yes |
| `PersonaDisplayNames` | 21 | indirectly, via `DilemmaViewModelTests` |
| `TraitDisplayBand` | 10 | yes |
| `VerdictLabel` | 9 | yes — `VerdictLabelTests` |
| `DecisionWeightLabel` | 8 | yes |

Those assertions exist because of a lesson this project already paid for: *invariant sweeps
prove output is never broken; they say nothing about whether it is true.* Meaning needs
exact-text expectations.

Moving those strings into `.resx` would put them behind MAUI resource lookup, which a
source-linked `net10.0` test project cannot resolve — deleting every one of those
assertions. So:

> **If a test asserts the text, the text stays in MAUI-free C#. Otherwise it goes in `.resx`.**

This yields two mechanisms, with a principled seam between them:

- **`.resx`** — the ~125 literal strings in 23 XAML files. Untested today, and untestable
  either way.
- **MAUI-free language tables in C#** — the 200 composer strings and the 50 test items.
  Fully testable, in all seven languages.

## 1. Language selection and resolution

Three pieces, split so the decision logic is testable:

**`CoreChoice.Core/Application/LanguageChoice.cs`** (new, MAUI-free, pure)

```csharp
public sealed record AppLanguage(string Code, string NativeName);

public static class LanguageChoice
{
    public static IReadOnlyList<AppLanguage> Supported { get; }   // system first, then the seven
    public static bool IsSupported(string? twoLetterCode);
    public static string Resolve(string? storedCode, string? osTwoLetterCode);  // -> "en".."nl"
    public static string ToSpeechTag(string twoLetterCode);       // "pl" -> "pl-PL"
}
```

`Resolve` is the whole rule in one function: an empty stored code means follow the OS, an OS
language we do not ship falls back to `en`, and an explicit stored code wins outright. It is
a pure function of two strings, so every branch is a unit test — the same shape as
`BackNavigation.Decide`, which exists for exactly this reason.

**`src/CoreChoice/Services/LanguageService.cs`** (new, MAUI, thin)

Owns only what the pure function cannot: reading and writing `Preferences`, reading the OS
culture, and assigning `CultureInfo.CurrentUICulture` / `CurrentCulture` /
`DefaultThreadCurrent*`. No decisions. Not linked into tests, and nothing is lost by that.

**Applying a change mid-session.** Localized XAML resolves `CurrentUICulture` at page load,
so a language change only reaches pages that are rebuilt. PurePrep rebuilds its root page.
CoreChoice's root is a five-tab `Shell` constructed from five DI-injected pages, so applying
a language change means rebuilding `AppShell` from the container and reassigning
`Application.Current.MainPage`. The person lands back on the Ask tab. This is acceptable and
expected — it is what changing an app's language does — but it must be deliberate: any
in-progress test answers live in the view model, so **the language picker is disabled while a
test is part-finished**, with a one-line explanation, rather than silently discarding
answers.

**Settings UI.** `SettingsPage` already has `MODE` and `PALETTE` headed sections; a
`LANGUAGE` section joins them, in the shape the headed-sections decision anticipated. Each
entry shows its native name (`Polski`, not `Polish`) because a person looking for their own
language is looking for the word they call it.

## 2. UI chrome — `.resx`

Mirrors PurePrep exactly: `Resources/Localization/AppResources.resx` plus six satellite
files (`.de`, `.fr`, `.es`, `.it`, `.pl`, `.nl`), and an `AppResources` accessor class.
XAML literals become `{x:Static}` lookups.

**Inline format strings must move too.** XAML currently carries English grammar inside
`StringFormat`:

- `StringFormat='{0} analyses'` (three occurrences on `CoinsPage`)
- `StringFormat='{0} left'` (`AnalysisPage`)
- `StringFormat='Ask {0}'` (`PersonaPage`)

These become resx format strings. `{0} analyses` is the dangerous one: Polish needs
*1 analiza / 2 analizy / 5 analiz*, and Polish's rule is not "one vs many" — it depends on the
last two digits. So counted nouns do not go through `StringFormat` at all. They go through a
MAUI-free `CountedNoun` helper in `Core` with a per-language plural rule (English: 2 forms;
Polish: 3 forms; the rest: 2 forms), unit-tested on the boundary values Polish actually
breaks on — 1, 2, 5, 12, 22, 25, 101, 102.

## 3. Composed text — language tables in C#

Each composer gains a language parameter and an internal table keyed by language code. The
shape, using `TraitDisplayBand` as the smallest example:

```csharp
private static readonly IReadOnlyDictionary<string, string[]> BandLabels =
    new Dictionary<string, string[]>
    {
        ["en"] = ["Low", "Moderate", "High"],
        ["pl"] = ["Niskie", "Umiarkowane", "Wysokie"],
        // ... all seven
    };
```

Callers pass the resolved language code down from the view model. No composer reads ambient
`CultureInfo` — the language is an argument, because an argument can be varied by a test and
ambient culture cannot be varied reliably inside a test run.

**Tests.** Two layers, and both are required:

1. *Completeness*, mechanically: every table has an entry for all seven codes; no entry is
   empty; every language's array has the same arity as English's. One test per composer,
   driven by `LanguageChoice.Supported`, so adding an eighth language fails loudly
   everywhere it is missing rather than falling back silently.
2. *Meaning*, by exact text: the existing English assertions stay, and each gains a
   counterpart in at least Polish and German — the two most structurally distant from English
   in this set. This is the layer that catches a table whose rows are shifted by one, which
   completeness checks cannot see.

## 4. The 50 IPIP test items

The items are a validated instrument, not UI copy. Reverse-keying, scoring, and the trait
norms all assume specific wording; a loose translation still produces numbers, but they stop
measuring what the app claims.

**Policy (as decided):** use the official IPIP translation where one exists. Where none
exists, write ours — and say so in the app.

**Provenance must be verified, not assumed.** Two fetches of the same IPIP translations page
in the course of this design **disagreed about Polish**: one reported no 50-item Polish entry,
while the site in fact carries Pachalska's Polish translation of the 50-item markers. Page
summaries are lossy. Therefore, at implementation time, each language's source is confirmed
by reading the IPIP page directly, and the verified URL plus translator attribution is
recorded in a comment beside that language's items.

Best evidence at design time, to be confirmed:

| Language | Official 50-item translation |
|---|---|
| German | Yes — `German50-itemBigFiveFactorMarkers.htm` (Angleitner, Hempel, Langert, Spinath) |
| Spanish | Yes — `SpanishBig-FiveFactorMarkers.htm` |
| French | Yes — `French50-itemBigFiveFactorMarkers&Conservatism.htm`, **Canadian French** |
| Polish | Yes — Pachalska, University of Gdańsk |
| Italian | Unconfirmed; IPIP's Italian entries may cover NEO instruments only |
| Dutch | **No** — Dutch entries cover Mini-IPIP and IPIP-NEO-120, not the 50-item markers |

**Structure.** `IpipItemBank` keeps what it owns today — id, trait, reverse-keyed flag — and
gains per-language text tables plus a provenance flag:

```csharp
public enum ItemSource { OfficialIpipTranslation, CoreChoiceTranslation }

public static IReadOnlyList<LocalizedIpipItem> Items(string languageCode);
public static ItemSource SourceFor(string languageCode);
```

**"View original in English".** On any language whose items are `CoreChoiceTranslation`, the
test screen shows a persistent, tappable *View original in English* affordance that reveals
the English wording of the item on screen. This is the general rule, not a Dutch special
case: it appears for **every** language we translated ourselves, and never for one where the
official translation is used. If Italian turns out to lack an official 50-item version, it
gets the affordance too, with no code change — the flag drives it.

A test asserts the pairing directly: for every language, `SourceFor` is
`OfficialIpipTranslation` **if and only if** a verified source URL is recorded for it. That
keeps the badge honest even if someone later adds items without provenance.

## 5. Voice dictation

`AndroidVoiceDictation.ListenOnMainThreadAsync` currently does:

```csharp
intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToLanguageTag());
```

Two defects for a multilingual app: it uses the **device** language rather than the app's,
and it omits `ExtraLanguagePreference`.

The fix, taking PurePrep's hard-won knowledge:

- Pass the app's resolved language in, region-qualified via `LanguageChoice.ToSpeechTag`
  (`pl` → `pl-PL`). A bare two-letter code matches an installed recognition model far less
  reliably.
- Set **both** `ExtraLanguage` and `ExtraLanguagePreference`.
- Keep them as **BCP-47 strings**. Handing `EXTRA_LANGUAGE` a `Locale` object stores a
  Serializable extra that the engine reads back as null and silently transcribes in the device
  language instead — the exact bug that made every non-English PurePrep recipe "wonky".

`ToSpeechTag` is pure and lives in `Core`, so the mapping table is unit-tested even though
the recogniser itself can only be verified on a device. On-device verification is explicit:
dictate into both dilemma fields in at least German and Polish.

## 6. Culture-sensitive formatting

Dates are currently pinned to `InvariantCulture` with a US pattern in two source-linked,
tested places:

- `AnswerDetailViewModel.cs:65`
- `PastDecisionRow.cs:28`

Both read `"MMM d, yyyy 'at' h:mm tt"`. That renders *Sep 17, 2026 at 1:46 PM* to a Polish
reader, which is wrong in pattern, month name, and clock convention. Both move to the
resolved language's `CultureInfo` with a culture-appropriate short date/time, and the
connecting word ("at") becomes a resx/table entry rather than a literal inside a format
string. Their existing exact-text tests are updated and extended per language — passing the
culture in as an argument, never reading ambient culture, so the tests are deterministic on
any build agent.

## 7. Server — the analysis answers in the person's language

`GenerateDecisionRequest` gains one nullable field:

```csharp
internal sealed record GenerateDecisionRequest(
    Guid DeviceId, string OptionA, string OptionB, string? Context,
    string Persona, int Weight, OceanProfileDto? Profile,
    string? Language);          // two-letter code; null/unknown => English
```

Nullable, and last, so an existing 1.0.0 client keeps working unchanged against the deployed
server — which matters, because 1.0.0 will be live in closed testing while this is built.

The server validates the code against the same seven-language list and appends one
instruction to the existing shared prompt. The six persona prompt templates are **not**
duplicated per language; they stay exactly as they are, and one appended line carries the
requirement:

> Write every part of your response in {language}. This includes the recommendation, the
> reasoning, the strengths and risks, and the personality note. The two options are quoted
> from the person in their own words — reproduce them as written, do not translate them.

That last sentence matters: the verdict echoes one of the two options the person typed, and
re-translating a person's own phrasing back at them reads as a misquote.

An unknown or absent code means English, decided server-side, so a future client language
cannot produce an untranslated-but-confidently-labelled answer.

**Validation.** Server tests assert the language instruction is present for each of the seven
codes, absent for null, and that an unrecognised code falls back to English rather than being
interpolated into the prompt — a prompt-injection surface if the field were passed through
unchecked.

## 8. Personas — resolving the server-wins conflict

`PersonaDisplayNames` is documented today as a *fallback*: `DilemmaViewModel` prefers the
server catalog's spelling the moment `/api/personas` answers, so that a wording change on the
server reaches the app without a client release.

That behaviour is wrong once the app is multilingual: an online Polish device would show the
local Polish name for a moment and then have it replaced by the server's English one.

**Rule:** the server catalog remains authoritative for **English only**. When the resolved
language is anything else, the local table wins and the catalog supplies nothing but ids and
ordering. The "change wording without a client release" property is preserved where it can be
(English), and the app never overwrites a translation with an untranslated string. Persona
names and descriptions for the other six languages live in the `PersonaDisplayNames` table,
which is MAUI-free and therefore gains the same completeness and exact-text tests as every
other composer.

## 9. History and already-generated text

A stored analysis is text that was generated in whatever language was active when it was
asked. It stays exactly as stored — it is a record of what the advisor said, and rewriting it
would be a lie about the past. Switching language changes the *chrome* around the history
list (headings, the date format, the verdict label) but never the stored recommendation,
reasoning, or the person's own options.

Re-asking a past decision generates a fresh analysis, which naturally comes back in the
language that is current at that moment.

## 10. Store assets

Out of scope for the code work, tracked as follow-up: Play listing and release notes in the
seven languages. The `store-assets/release-notes/<version>.txt` format already takes one
`<xx-YY>` block per locale, with a 500-character-per-locale limit, so 1.1.0's notes are
written in all seven at release time.

## Testing strategy

| Layer | What it proves |
|---|---|
| `LanguageChoice` unit tests | Resolution rules: empty code, unsupported OS language, explicit override, speech-tag qualification |
| Completeness tests | Every table has all seven languages, no empty values, matching arity — driven by `Supported`, so a new language fails loudly |
| Exact-text tests | Meaning, in English plus Polish and German, for every composer |
| `CountedNoun` tests | Polish plural boundaries: 1, 2, 5, 12, 22, 25, 101, 102 |
| Provenance test | `SourceFor` says "official" iff a verified source URL is recorded |
| Server tests | Language instruction present per code, absent for null, unknown code falls back to English and is never interpolated |
| On-device | Voice dictation in German and Polish; a full analysis round trip in a non-English language; language switch rebuilds the shell without losing stored data |

Mutation testing is the acceptance bar, as elsewhere in this project: a completeness sweep
that passes when a table's rows are shifted by one is not a test.

## Out of scope

- An eighth language, or per-region variants beyond what the speech tag table needs.
- Translating the stored history of analyses already generated.
- Right-to-left layout — none of the seven languages needs it.
- Machine translation at runtime. Every string ships in the build.

## Sequencing

Three units, each independently shippable and reviewable:

1. **Shell** — `LanguageChoice`, `LanguageService`, `.resx` for XAML chrome, the Settings
   section, voice tag plumbing, culture-aware dates. The app is fully translated except for
   composed text and the test.
2. **Content** — composer language tables, `CountedNoun`, the 50 items with provenance and
   the "View original in English" affordance.
3. **Server** — the `Language` field, the prompt instruction, validation, deploy.

Unit 3 is backward compatible by construction, so it can deploy before the client that uses
it.
