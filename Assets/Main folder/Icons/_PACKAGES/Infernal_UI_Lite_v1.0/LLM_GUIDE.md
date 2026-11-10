# Infernal UI Lite: guide for AI coding assistants

This short guide lets an AI coding assistant use the Lite kit without opening the images. The
full pack ships a longer guide, with every inset table, screen recipes and a `catalog.json`
index. When in doubt, `manifest.json` wins.

Kit: 127 sprites in 48 families, scales 1x, 2x.

## Rules

1. Work in 1x pixels on a 960x540 canvas, then scale by an integer (2x for 1080p) with
   nearest-neighbour filtering. The 2x files are exact pixel-doublings.
2. Files live at `{scale}/{category}/{file}`, for example `1x/buttons/btn_stone_md_idle.png`.
   Find a family's folder with `manifest.assets[family].category`.
3. Resize only sprites that have `nine_slice`, using its insets and fill mode. Everything else
   is fixed size.
4. Grid: 8 px grid, 4 px padding and gap, 1 px hairlines. A 24 px icon goes in a 32 px slot or
   button.
5. Text is drawn by your game, on palette colours. Roles (`conventions.text_roles`): `title`
   Jersey 10 Infernal 40, `ui` and `ui_bold` Pixel Operator 16, `caption` and `caption_bold`
   Pixel Operator 8. In a browser, use the glyph atlases in `fonts/bitmap/`: each JSON gives
   per-character cells, offsets and advances. Canvas `fillText` is always smoothed and leaves
   off-palette pixels. "y + N" in a usage note is the cap top of the text.
6. Serve the page over HTTP (`py -m http.server 8000` on Windows, `python3 -m http.server 8000`
   elsewhere), and size the 960x540 canvas's CSS box in device pixels: "2x" means 2 device
   pixels per art pixel, so `width = 960*2/dpr px` and `height = 540*2/dpr px`, with
   `image-rendering: pixelated`.

## Naming

`{category}_{variant}[_{size}][_{state}][_{frameNN}].png`. The states are `idle hover pressed
disabled focus selected selected_focus locked`, plus `drag_over empty` on the slot. Icons are
`icon_{name}_24.png` and `icon_{name}_24_disabled.png`.

## manifest.json

`assets` is keyed by family. Each entry has `category`, `canvas` {w, h}, `states`, `repeat`,
`nine_slice` {l, t, r, b, repeat}, `files` (each with `file`, `state`, and `pieces` and
`presized` where they exist) and `usage` (exact placement notes). `full_pack` lists what the
full pack adds.

**Inset order:** always **left, top, right, bottom** (manifest keys `l`, `t`, `r`, `b`), in 1x
pixels; multiply by the scale. CSS `border-image-slice` takes them in a different order (top,
right, bottom, left). Fill modes: `stretch` (stretch edges and centre), `repeat` (tile them),
`stretch-x` (width only; the height is the canvas height) and `fixed`.

| Family | Category | Canvas | Insets l/t/r/b | Fill | States |
|---|---|---|---|---|---|
| `btn_stone_md` | buttons | 98x42 | 12/0/12/0 | `stretch-x` | disabled, focus, hover, idle, pressed, selected, selected_focus |
| `btn_stone_sm` | buttons | 22x22 | 5/0/5/0 | `stretch-x` | disabled, focus, hover, idle, pressed, selected, selected_focus |
| `ibtn_marked_close_32` | buttons | 32x32 | - | `fixed` | disabled, focus, hover, idle, pressed, selected, selected_focus |
| `ibtn_marked_settings_gear_32` | buttons | 32x32 | - | `fixed` | disabled, focus, hover, idle, pressed, selected, selected_focus |
| `bar_fill_blue` | controls | 32x7 | 3/0/1/0 | `stretch-x` | - |
| `bar_fill_green` | controls | 32x7 | 3/0/1/0 | `stretch-x` | - |
| `bar_fill_red` | controls | 32x7 | 3/0/1/0 | `stretch-x` | - |
| `bar_track` | controls | 32x9 | 3/0/3/0 | `stretch-x` | - |
| `checkbox_off` | controls | 20x20 | - | `fixed` | disabled, focus, hover, idle |
| `checkbox_on` | controls | 20x20 | - | `fixed` | disabled, focus, hover, idle |
| `pip_off` | controls | 10x10 | - | `fixed` | - |
| `pip_on` | controls | 10x10 | - | `fixed` | - |
| `slider_fill` | controls | 32x10 | 3/0/1/0 | `stretch-x` | - |
| `slider_knob` | controls | 22x26 | - | `fixed` | disabled, focus, hover, idle, pressed |
| `slider_track` | controls | 32x13 | 4/0/4/0 | `stretch-x` | - |
| `slot_item_32` | controls | 32x32 | - | `fixed` | disabled, drag_over, empty, focus, hover, idle, locked, selected, selected_focus |
| `toggle_off` | controls | 50x30 | - | `fixed` | disabled, focus, hover, idle, pressed |
| `toggle_on` | controls | 50x30 | - | `fixed` | disabled, focus, hover, idle, pressed |
| `divider_plain_h` | panels | 8x2 | - | `fixed` | - |
| `panel_card_plain_regular_9s` | panels | 30x30 | 13/13/13/13 | `stretch` | disabled, focus, hover, idle, selected, selected_focus |
| `tip_box_9s` | panels | 12x12 | 4/4/4/4 | `stretch` | - |
| `tip_tail_up` | panels | 13x9 | - | `fixed` | - |
| `well` | panels | 16x16 | 3/3/3/3 | `stretch` | - |
| `window_clean_titlebar_9s` | panels | 352x258 | 103/81/57/81 | `repeat` | - |

Icons (24 px, with `_disabled`): chest closed, coin, exit, flame, gem red, heart, info, key, left, map, menu, pause, play, potion blue, potion red, right, shield, skull, soul, sound off, sound on, star, sword, trophy.

## Useful facts

- `window_clean_titlebar_9s`: 352x258, insets 103/81/57/81, repeat. It tiles from each region's
  top-left (`tile_origin`), not from the centre as CSS does: draw it on a canvas, or use the
  480x270 file in `presized/`. Minimum 160x162, practical 200x210. Content goes inside the face:
  x from 38 to w - 38, y from 49 to h - 43. Title: UPPERCASE `title` font in `brim-2`, with an
  `ember-1` copy 1 row lower behind it, ink centred on the window width, cap top at y + 59.
- `btn_stone_md`: 98x42, insets 12/0/12/0, stretch-x. Label: `ui_bold`, caps, `ink-1`
  (`ink-4` when disabled), ink centred on the width, cap top at y + 16 (17 when pressed).
  `btn_stone_sm`: 22x22, insets 5/0/5/0; label `caption_bold`, cap top at y + 7.
- `bar_track`: 32x9, insets 3/0/3/0, stretch-x. Put `bar_fill_red`, `bar_fill_blue` or `bar_fill_green` at (+1, +1),
  `round((track_w - 2) * value)` wide; any value above 0 draws at least 4 px.
- `slider_track` with `slider_fill` at (+1, +1), and `slider_knob` at
  x = track_x + 1 + fill_w - 11, y = track_y - 7.
- `toggle_on` and `toggle_off`: 50x30; the row label goes at x + 62, cap top y + 11.
- `slot_item_32`: put the 24 px icon at (4, 4).
- `tip_box_9s`: text pen at x + 6, first cap top at y + 6, then 11 px per line.
  `tip_tail_up` goes on the box top: x = tail centre + (-6), y = box top - 6.

## Example prompt

"Using only this folder, build a 960x540 HTML canvas settings window scaled 2x: a
window_clean_titlebar_9s window 480x270 titled SETTINGS, with two toggles, a volume slider and
two checkbox rows inside the face, and Cancel and Apply stone buttons 98x42, 4 px apart, at the
bottom right of the face."
