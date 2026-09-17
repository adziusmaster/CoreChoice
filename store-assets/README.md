# CoreChoice — Play Store listing assets

All images are generated from HTML in `_src/` via headless Chrome. To regenerate:

```bash
node store-assets/_src/build.mjs
```

The build re-flattens `icon-512.png` to 32-bit RGBA itself (needs Pillow) — Chrome
writes 24-bit RGB, which Play rejects for the icon. No manual follow-up step.

The screenshots draw from the same tokens the app ships (`_src/theme.js` — the
Considered palette, Lora + DM Sans), so the listing and the installed app match.

## Graphic assets (Play Console → Store listing → Main store listing)

| Asset | File | Size | Play requirement |
|-------|------|------|------------------|
| App icon | `icon-512.png` | 512×512, 32-bit PNG | Required |
| Feature graphic | `feature-1024x500.png` | 1024×500 | Required |
| Phone screenshots | `phone/phone-1..5-*.png` | 1080×1920 (9:16) | 2–8 required |
| 7-inch tablet | — | 1200×1920 | Not generated; only needed to list as tablet-optimised |
| 10-inch tablet | — | 1600×2560 | Not generated; only needed to list as tablet-optimised |

Screenshot rules met: min side ≥ 320 px, max side ≤ 3840 px, max ≤ 2× min.
The feature graphic is exactly 1024×500, which is its own fixed spec — the 2× rule
applies to screenshots, not to it.

The five screenshots tell the flow in order: the promise → the test → your profile
→ the dilemma → the answer.

## Suggested listing text

**App name (30 chars max):**
CoreChoice — Decide Clearly

**Short description (80 chars max):**
Take the Big Five test free. Get advice shaped by how you actually decide.

**Full description:**
CoreChoice is for the decision you have been turning over for days. Not because it
is hard, but because you have run out of ways to think about it alone.

It starts with the Big Five (IPIP-50) personality test — the real one, fifty
questions, about ten minutes. Then, when you bring it a decision, the advice is
written for how you actually decide: the traits that will pull you toward one
option, and the ones that will quietly talk you out of the other.

• Your full result is free, permanently. Every trait, no paywall at the end. We are
  not going to make you finish a ten-minute test and then ask for money to see it.
• Finishing your profile adds five analyses to your account, free.
• Six advisors — from a devil's advocate to the long view. One is suggested for your
  decision; the rest are one tap away.
• Weigh the decision. A five-point slider tells CoreChoice whether this is a small
  call or one you will live with for years.
• Say it out loud. Voice input, for when a dilemma is easier spoken than typed.
• Your answers stay on your phone. The test result is stored locally; nothing you
  type is kept on our servers after your answer comes back.
• Every question you ask and every answer you get is kept on your phone too,
  indefinitely, so you can look back on it later — and it is never sent anywhere.
• Three colour themes, dark and light, so the app can feel the way you need it to.

Analyses beyond the free five are paid for with coins, bought in-app. No
subscription, no account, no tracking, no ads.

Privacy policy: https://lechdigital.nl/projects/corechoice/privacy/

That page is live. The `corechoice.lechdigital.nl` subdomain does NOT resolve — only
`api.corechoice.lechdigital.nl` was ever pointed at the server — so the policy URL given
to Play must be the lechdigital.nl path above, which is the one that answers 200.

## Release notes

`release-notes/<version>.txt`, one file per release, in the same shape PurePrep uses: each
locale wrapped in its own `<xx-YY>` tag so the whole file can be pasted into Play's
release-notes field at once. **Play's limit is 500 characters per locale**, counted on the
text between the tags, not including them.

CoreChoice ships English only for now; add locales as whole tag blocks when there is a
translation worth shipping, never a machine-translated one.
