// Generates every Play Store graphic from HTML via headless Chrome.
//   node store-assets/_src/build.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { doc, T } from './theme.js';
import { caption, panel, accentPanel, verdict, trait, tick, mark, markTight } from './parts.js';

const __dir = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dir, '..');
const HTML = join(__dir, 'html');
mkdirSync(HTML, { recursive: true });
mkdirSync(join(ROOT, 'phone'), { recursive: true });
const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

const PW = 1080, PH = 1920;                       // 9:16, inside Play's size rules
const body = `padding:0 78px;`;

const screen = (cap, content) => doc(PW, PH, `
<div style="position:absolute;inset:0;display:flex;flex-direction:column;justify-content:center;">
  ${cap}
  <div style="margin-top:56px;">${content}</div>
</div>`);

// ---------- 1. the promise ----------
const p1 = screen(caption('Decide without', 'deciding alone.'), `
<div style="${body}">
  <div style="font-size:31px;line-height:1.55;color:${T.muted};margin-bottom:64px;">
    A ten-minute personality test, then advice written for how <em style="color:${T.inkSoft};font-style:normal;">you</em> decide — not advice in general.
  </div>
  ${panel(`
    ${tick('<b style="font-weight:600;">Your full result is free.</b> Every trait, permanently. No paywall at the end.')}
    ${tick('Finishing adds <b style="font-weight:600;">five analyses</b> to your account.')}
  `, 38)}
  <div style="margin-top:56px;font-size:27px;line-height:1.6;color:${T.faint};">
    Nothing you type is stored on our servers. Your answers stay on your phone.
  </div>
</div>`);

// ---------- 2. the test ----------
const p2 = screen(caption('Fifty questions.', 'No right answers.'), `
<div style="${body}">
  <div style="font-size:29px;color:${T.muted};margin-bottom:52px;">Stop anywhere. It remembers where you were.</div>
  ${panel(`
    <div class="serif" style="font-size:38px;line-height:1.4;margin-bottom:30px;">I get stressed out easily.</div>
    <div style="display:flex;gap:14px;">
      ${[1,2,3,4,5].map(n => `<div style="flex:1;height:92px;border-radius:14px;${n===3
        ? `background:${T.accent};color:${T.accentInk};font-weight:600;`
        : `border:2px solid ${T.line};color:${T.faint};`}display:flex;align-items:center;justify-content:center;font-size:27px;">${n}</div>`).join('')}
    </div>
    <div style="display:flex;justify-content:space-between;margin-top:20px;font-size:21px;color:${T.faint};">
      <div>Very inaccurate</div><div>Very accurate</div>
    </div>`, 38)}
  <div style="margin-top:44px;font-size:27px;color:${T.faint};">Answer on instinct. It is more accurate than deliberating.</div>
</div>`);

// ---------- 3. the profile ----------
const p3 = screen(caption('This is how', 'you decide.'), `
<div style="${body}">
  <div style="font-size:29px;color:${T.muted};margin-bottom:50px;">Yours, free, permanently.</div>
  ${panel(`
    ${trait('Openness', 88, 'Very high')}
    ${trait('Conscientiousness', 45, 'Moderate')}
    ${trait('Extraversion', 62, 'Moderate')}
    ${trait('Agreeableness', 70, 'High')}
    ${trait('Neuroticism', 58, 'Moderate')}`, 38)}
  <div class="serif" style="margin-top:46px;font-size:31px;line-height:1.55;color:${T.inkSoft};">
    You are drawn to what is new and possible, and less to the systems that make new things survive.
  </div>
</div>`);

// ---------- 4. asking ----------
const p4 = screen(caption('Two options.', 'One honest answer.'), `
<div style="${body}">
  <div style="font-size:29px;color:${T.muted};margin-bottom:50px;">Type it, or say it out loud.</div>
  ${panel(`<div style="font-size:21px;letter-spacing:.09em;text-transform:uppercase;color:${T.accent};font-weight:600;margin-bottom:16px;">One way</div>
    <div class="serif" style="font-size:33px;line-height:1.45;">Quit my job to go full-time on my own app</div>`, 34)}
  <div style="display:flex;align-items:center;gap:26px;margin:28px 0;">
    <div style="flex:1;height:2px;background:${T.lineSoft};"></div>
    <div class="serif" style="font-size:31px;font-style:italic;color:${T.faint};">or</div>
    <div style="flex:1;height:2px;background:${T.lineSoft};"></div>
  </div>
  ${panel(`<div style="font-size:21px;letter-spacing:.09em;text-transform:uppercase;color:${T.accent};font-weight:600;margin-bottom:16px;">The other</div>
    <div class="serif" style="font-size:33px;line-height:1.45;">Keep the job and build it on evenings and weekends</div>`, 34)}
  <div style="margin-top:50px;font-size:27px;line-height:1.6;color:${T.faint};">
    Six advisors, from a devil's advocate to a long view. One is suggested; the rest are a tap away.
  </div>
</div>`);

// ---------- 5. the answer ----------
const p5 = screen(caption('Advice that knows', 'who is asking.'), `
<div style="${body}">
  <div class="serif" style="font-size:44px;line-height:1.28;letter-spacing:-.02em;margin-bottom:28px;">
    Keep the job. Build it on evenings and weekends.
  </div>
  <div style="margin-bottom:40px;">${verdict('Strong case')}</div>
  ${accentPanel('Because it is you asking',
    'Your soaring openness will convince you that leaping without a net is an inspiring adventure. It will also blind you to the grinding, unglamorous friction that kills most solo apps.')}
  <div class="serif" style="margin-top:46px;font-size:29px;line-height:1.55;font-style:italic;color:${T.faint};">
    You can sit with this. It will be here in the morning.
  </div>
</div>`);

// ---------- feature graphic ----------
const feature = doc(1024, 500, `
<div style="position:absolute;inset:0;background:radial-gradient(82% 130% at 76% 18%, #12211f 0%, ${T.bg} 60%);"></div>
<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:space-between;padding:0 68px;">
  <div style="max-width:560px;">
    <div class="eyebrow">CoreChoice</div>
    <div class="serif" style="font-size:66px;line-height:1.06;letter-spacing:-.025em;margin:18px 0 20px;">Decide without<br>deciding alone.</div>
    <div style="color:${T.muted};font-size:20px;line-height:1.55;max-width:470px;">
      Take the Big Five test once. Then get advice shaped by how you actually decide — free result, always.
    </div>
  </div>
  <div style="opacity:.96;">${mark(190)}</div>
</div>`);

// ---------- store icon ----------
const icon = doc(512, 512, `
<div style="position:absolute;inset:0;background:${T.bg};display:flex;align-items:center;justify-content:center;">
  ${markTight(318)}
</div>`);

const shots = [
  ['feature', feature, 1024, 500, 'feature-1024x500.png'],
  ['icon', icon, 512, 512, 'icon-512.png'],
  ['p1', p1, PW, PH, 'phone/phone-1-promise.png'],
  ['p2', p2, PW, PH, 'phone/phone-2-test.png'],
  ['p3', p3, PW, PH, 'phone/phone-3-profile.png'],
  ['p4', p4, PW, PH, 'phone/phone-4-ask.png'],
  ['p5', p5, PW, PH, 'phone/phone-5-answer.png'],
];

for (const [name, html, w, h, out] of shots) {
  const file = join(HTML, `${name}.html`);
  writeFileSync(file, html);
  const r = spawnSync(CHROME, [
    '--headless', '--disable-gpu', '--hide-scrollbars',
    `--screenshot=${join(ROOT, out)}`,
    `--window-size=${w},${h}`,
    '--virtual-time-budget=4000',
    `file://${file}`,
  ], { encoding: 'utf8' });
  if (r.status !== 0) { console.error(`FAILED ${out}`, r.stderr?.slice(0, 400)); process.exitCode = 1; }
  else console.log(`wrote ${out}  (${w}x${h})`);
}

// Chrome writes 24-bit RGB; Play requires the app icon as a 32-bit PNG. Re-flatten it
// here rather than as a manual follow-up step, so a rebuild can never ship the wrong depth.
const ICON = join(ROOT, 'icon-512.png');
const conv = spawnSync('python3', ['-c',
  `from PIL import Image; Image.open(${JSON.stringify(ICON)}).convert('RGBA').save(${JSON.stringify(ICON)})`,
], { encoding: 'utf8' });
if (conv.status !== 0) {
  console.error('FAILED to re-flatten icon-512.png to 32-bit RGBA (needs Pillow):', conv.stderr?.slice(0, 300));
  process.exitCode = 1;
} else console.log('icon-512.png re-flattened to 32-bit RGBA');
