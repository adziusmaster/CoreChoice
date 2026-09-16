import { T } from './theme.js';

/** The headline every phone screenshot carries above the device content. */
export const caption = (line1, line2 = '') => `
<div style="padding:0 78px;">
  <div class="serif" style="font-size:66px;line-height:1.14;letter-spacing:-.02em;color:${T.ink};">
    ${line1}${line2 ? `<br><span style="color:${T.accent};">${line2}</span>` : ''}
  </div>
</div>`;

/** A rounded panel in the app's surface colour. */
export const panel = (inner, pad = 34) => `
<div style="background:${T.surface};border:2px solid ${T.line};border-radius:22px;padding:${pad}px;">${inner}</div>`;

/** The accent-washed panel the app uses for the personality note. */
export const accentPanel = (label, body) => `
<div style="background:${T.accentWash};border:2px solid ${T.accentLine};border-radius:22px;padding:34px;">
  <div style="font-size:19px;letter-spacing:.1em;text-transform:uppercase;color:${T.accent};font-weight:600;margin-bottom:18px;">${label}</div>
  <div class="serif" style="font-size:31px;line-height:1.55;color:${T.inkSoft};">${body}</div>
</div>`;

/** The verdict badge — a word, never a percentage. */
export const verdict = (text) => `
<div style="display:inline-flex;align-items:center;gap:14px;background:${T.accentWash};
     border:2px solid ${T.accentLine};border-radius:12px;padding:14px 22px;">
  <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="${T.accent}" stroke-width="2.4"
       stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>
  <div style="font-size:25px;font-weight:600;color:${T.accent};">${text}</div>
</div>`;

/** A trait as a marker on a line — a position, never a filled bar. */
export const trait = (name, pct, band) => `
<div style="margin-bottom:30px;">
  <div style="display:flex;justify-content:space-between;align-items:baseline;margin-bottom:14px;">
    <div style="font-size:27px;color:${T.ink};">${name}</div>
    <div style="font-size:24px;color:${T.muted};">${band}</div>
  </div>
  <div style="position:relative;height:22px;display:flex;align-items:center;">
    <div style="width:100%;height:2px;background:${T.line};"></div>
    <div style="position:absolute;left:calc(${pct}% - 10px);width:20px;height:20px;border-radius:50%;background:${T.accent};"></div>
  </div>
</div>`;

/** The tick used for the two promises on the first screenshot. */
export const tick = (html) => `
<div style="display:flex;gap:20px;align-items:flex-start;margin-bottom:22px;">
  <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="${T.accent}" stroke-width="2.4"
       stroke-linecap="round" stroke-linejoin="round" style="flex-shrink:0;margin-top:4px;"><path d="M20 6 9 17l-5-5"/></svg>
  <div style="font-size:29px;line-height:1.5;color:${T.ink};">${html}</div>
</div>`;

/** The app mark, for the feature graphic. */
export const mark = (size = 132) => `
<svg width="${size}" height="${size}" viewBox="0 0 456 456" fill="none">
  <g stroke-linecap="round" stroke-linejoin="round" stroke-width="26">
    <path d="M228 340 L228 238" stroke="${T.accent}"/>
    <path d="M228 238 L150 160" stroke="#2F6F69"/>
    <path d="M228 238 L306 160" stroke="${T.accent}"/>
  </g>
  <circle cx="306" cy="152" r="21" fill="${T.accent}"/>
</svg>`;

/** The mark cropped to its own ink, for the store icon where it must fill the tile. */
export const markTight = (size) => `
<svg width="${size}" height="${size}" viewBox="58.5 68.5 347 347" fill="none">
  <g stroke-linecap="round" stroke-linejoin="round" stroke-width="26">
    <path d="M228 340 L228 238" stroke="${T.accent}"/>
    <path d="M228 238 L150 160" stroke="#2F6F69"/>
    <path d="M228 238 L306 160" stroke="${T.accent}"/>
  </g>
  <circle cx="306" cy="152" r="21" fill="${T.accent}"/>
</svg>`;
