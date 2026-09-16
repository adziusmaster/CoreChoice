# CoreChoice — backend

An API that turns a dilemma into decision advice shaped by the asker's personality.

A person takes a Big Five personality test on their phone, states two options, picks an
advisor persona, and gets back an analysis from Google Gemini that is written for *them* —
not a generic answer with their name on it.

This repository is the **backend**. The Android app is a separate piece of work.

---

## The three promises this codebase keeps

These are product invariants, not preferences. Each is enforced by a test that has been
deliberately broken and watched to fail.

**1. The personality test and its full result are free. Permanently.**
Coins buy AI analyses — the part that costs real money to serve. Completing the test costs
nothing and additionally *grants* five coins. Competing apps take ten minutes of honest
self-disclosure and then paywall the result; people sense the wall coming and stop answering
at item thirty. The free result is what earns the right to ask.

**2. Nothing you type is written down.**
The dilemma and the personality profile travel in the request, are used to build the prompt,
and are never persisted. What is stored per request: a salted device hash, the persona,
prompt version, weight, token counts, and whether it succeeded. Verified against a live
Gemini call — no fragment of the dilemma or profile appears anywhere in the row.

**3. A coin is never lost to our own failure.**
Every path that spends a coin refunds it if anything afterwards fails, including a caller
disconnecting mid-request. Two bugs of exactly this kind were found and fixed during the
build; both now have regression tests.

---

## Running it

```sh
# No API key needed — a fake advisor stands in and the whole service works end to end.
dotnet run --project src/CoreChoice.Server

# With a real Gemini key:
Gemini__ApiKey=<key> dotnet run --project src/CoreChoice.Server
```

In a container:

```sh
docker build -t corechoice-api .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Db="Data Source=/data/corechoice.server.db" \
  -e Gemini__ApiKey=<key> \
  corechoice-api
```

Try it:

```sh
DEV=$(uuidgen | tr 'A-Z' 'a-z')
curl -s -X POST localhost:8080/api/coins/ensure -H 'content-type: application/json' \
  -d "{\"deviceId\":\"$DEV\"}"

curl -s -X POST localhost:8080/api/decisions -H 'content-type: application/json' -d "{
  \"deviceId\":\"$DEV\",
  \"optionA\":\"Take the senior role at a bigger company\",
  \"optionB\":\"Stay and lead the small team I built\",
  \"context\":\"I have a mortgage and a young child.\",
  \"persona\":\"the-long-view\",
  \"weight\":5,
  \"profile\":{\"openness\":82,\"conscientiousness\":74,\"extraversion\":31,
               \"agreeableness\":66,\"neuroticism\":45}
}"
```

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/decisions` | Spend a coin, return an analysis |
| GET | `/api/coins/{deviceId}` | Balance |
| POST | `/api/coins/ensure` | First contact; seeds 5 free coins |
| POST | `/api/coins/profile-grant` | Once per device; 5 coins for finishing the test |
| GET | `/api/personas` | The advisor list for the picker |
| POST | `/api/dev/grant` | Shared-secret grant; the route does not exist without the secret |
| GET | `/health` | Liveness |

Failure behaviour: 400 malformed, 402 out of coins, 502 unusable model response,
503 advisor unavailable, 429 rate limited. Everything after the spend refunds.

## Layout

```
src/CoreChoice.Core/      Domain, application ports, Gemini client — shared with the app later
src/CoreChoice.Server/    Minimal APIs, SQLite, coin ledger, persona + prompt storage
tests/                    162 tests
```

`Core` has no dependency on the server. The Android app will reference it so the personality
scoring is byte-identical on both sides.

## The personality test

IPIP Big-Five Factor Markers, 50 items, public domain. The keying is deliberately uneven —
Openness 3 reverse-scored items, Conscientiousness 4, Extraversion 5, Agreeableness 4,
Neuroticism 2 — because the items are simply not written with balanced polarity.

**Do not "tidy" that to five per trait.** It was done once during development to make a faulty
test pass, and it silently inverted seven items: agreeing with *"Get upset easily"* began
*lowering* the neuroticism score. Scores stayed plausible and were entirely wrong. Two tests
now pin every reverse-keyed item by number, and a mutation sweep of all 110 possible
single-swap corruptions kills all 110.

## Personas and prompts are data

Six personas live in the database with their prompt templates. Adding one is two inserts and
no redeploy. Changing a prompt means inserting a new version and flipping `IsActive`; never
edit a row in place, because `UsageLog.PromptVersion` records which version answered each
request and that record is worthless if a version number can mean two prompts.

See `DEPLOY.md` for the SQL.

Templates carry `{{PROFILE}}` and `{{WEIGHT}}` placeholders that the assembler substitutes.
A round-trip test runs every seeded template through the real assembler and requires
profile-derived text in the output — deleting a placeholder leaves no braces behind, so
checking for leftover `{{` alone would not catch it.

## Costs

Measured against the live API, `gemini-flash-lite-latest`:

| Persona | Prompt | Output | Total |
|---|---|---|---|
| The Long View | 421 | 502 | 923 |
| The Gut Check | 424 | 302 | 726 |

Roughly 700–950 tokens per analysis. Every request records its own counts in `UsageLogs`, so
real cost per persona is queryable rather than estimated.

## Operations

Deployment, prompt editing, rollback: `DEPLOY.md`.

Two things that fail *silently* if you get them wrong:

- **`Security:IpHashSalt` must be stable and secret.** It is what lets the free-coin origin cap
  recognise a repeat origin across restarts. A rotating salt resets the cap on every deploy
  while appearing to work. Production refuses to boot without it, by design.
- **Forwarded headers must stay enabled.** Caddy terminates TLS; without them every request
  appears to come from the proxy, and the rate limiter becomes one global bucket for the
  entire internet while still returning plausible 429s.

## Design record

- `docs/superpowers/specs/` — what was decided and why
- `docs/superpowers/plans/` — how it was built, task by task

Both are worth reading before changing the coin ledger, the questionnaire, or the prompt
assembly. The reasoning behind the odd-looking parts is there.
