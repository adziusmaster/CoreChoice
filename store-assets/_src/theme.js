// CoreChoice store-asset tokens — the Considered palette, same values the app ships.
export const T = {
  bg:        '#0A0D0D',
  surface:   '#121818',
  sunken:    '#0E1313',
  line:      '#1F2A29',
  lineSoft:  '#161E1D',
  accent:    '#4FD1C5',
  accentDim: '#7FB8B2',
  accentLine:'#2A514C',
  accentWash:'#132422',
  accentInk: '#04100F',
  ink:       '#EAF2F1',
  inkSoft:   '#C6D5D3',
  muted:     '#879795',
  faint:     '#5A6968',
};

const FONTS = 'https://fonts.googleapis.com/css2?family=Lora:ital,wght@0,400;0,500;0,600;1,400&family=DM+Sans:opsz,wght@9..40,400;9..40,500;9..40,600;9..40,700&display=swap';

/** A full HTML document at an exact pixel size, ready for a headless screenshot. */
export const doc = (w, h, body, extraCss = '') => `<!doctype html>
<html><head><meta charset="utf-8">
<link rel="stylesheet" href="${FONTS}">
<style>
  *{box-sizing:border-box;margin:0;padding:0;}
  html,body{width:${w}px;height:${h}px;overflow:hidden;}
  body{background:${T.bg};color:${T.ink};font-family:'DM Sans',system-ui,sans-serif;
       -webkit-font-smoothing:antialiased;position:relative;}
  .serif{font-family:Lora,Georgia,serif;}
  .eyebrow{font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:${T.faint};font-weight:600;}
  ${extraCss}
</style></head><body>${body}</body></html>`;
