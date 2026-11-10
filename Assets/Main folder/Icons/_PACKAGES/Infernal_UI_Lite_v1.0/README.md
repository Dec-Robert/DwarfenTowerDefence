# Infernal UI Lite v1.0 (free)

A complete small kit taken from Infernal UI: obsidian and stone with lava seams, brimstone and
ember accents and demonic sigils (no gore). There is enough here to finish a small real screen:
a menu, a settings window, a small inventory or a HUD. It has 127 sprites in
48 families at 1x, 2x, under the **same licence as the full pack**, so commercial
use is allowed.

## What is inside

| Folder or file | What it holds |
|---|---|
| `1x/`, `2x/` | The sprites (1x is native on a 960x540 screen; 2x fills 1080p). |
| `…/pieces/`, `…/presized/` | 9-slice pieces and ready-made sizes of the window, panels, buttons and bars. |
| `manifest.json` | Every sprite: size, states, 9-slice insets, fill mode and usage notes. |
| `LLM_GUIDE.md` | A short guide for AI coding assistants: naming, insets and how to read the manifest. |
| `fonts/`, `palette/` | The two free font families with glyph atlases, and the 75-colour palette. |
| `previews/lite_contents.png` | Every sprite in this kit on one sheet, at 2x. |
| `previews/full_pack_teaser.png` | A picture of what the full pack adds (not usable as sprites). |

The kit:
- **Containers:** a window with title bar and sigil corners (`window_clean_titlebar_9s`), the
  plain card (`panel_card_plain_regular_9s`, 6 states), an inset panel (`well`), a tooltip box with its
  tail, and a divider.
- **Buttons:** the stone button at `md` and `sm` size, all 7 states, plus close and settings
  icon buttons (7 states each).
- **Controls:** a checkbox and a toggle switch (on and off), a slider with track, fill and knob.
- **Bars and pips:** a bar track with red, blue and green fills, plus lit and empty pips.
- **Slots:** the 32 px item slot in all 9 states.
- **24 icons at 24 px**, each with a disabled version: chest closed, coin, exit, flame, gem red, heart, info, key, left, map, menu, pause, play, potion blue, potion red, right, shield, skull, soul, sound off, sound on, star, sword, trophy.

Use nearest-neighbour filtering. Resize only the sprites that have `nine_slice` in
`manifest.json`, using its insets and fill mode ("stretch" means stretch, "repeat" means tile).
Insets are always in the order left, top, right, bottom (keys `l t r b`).

## What the full pack adds

The full Infernal UI has **1,099 sprites in 502 families** at
1x, 2x, 3x, 4x:

- 481 icons (122 subjects at 24 and 48 px, the 48s with more detail), most with a disabled version
- every button style: brimstone, hero with end caps, danger, ghost, text, round and square icon buttons, steppers, tabs
- more windows and panels: ready-made settings, pause and modal windows, a dialog window, thin and inset panels, list rows
- radios, text fields, search and multiline fields, dropdowns, scroll bars, key-binding cells
- boss, XP and thin bars, sigil bar caps, more pip colours, health and mana orbs, radial meters, a cooldown overlay
- the hotbar slot, rarity frames and equipment silhouettes
- dialogue boxes, name plates, portrait frames, popups, toasts, header and footer strips, HUD frames
- banners, ribbons, badges, sigil corners, flourishes and outlined numbers
- a deckbuilder module: card frames by rarity and type, cost gems, energy orbs, enemy intents, run-map nodes
- 12 cursors with hotspots and 3 animations
- 7 example screens with layout files, spritesheets with Aseprite-style JSON, contact sheets
- 3x and 4x exports (1440p and 4K)
- the full LLM_GUIDE.md with every inset table and screen recipes, plus catalog.json

See the full pack: https://brunkagames.itch.io/infernal-ui ($4.99). The picture
`previews/full_pack_teaser.png` shows some of it.

## Licence in short

- Use the sprites in any number of personal and commercial projects, with no royalties. Credit
  is appreciated, not required.
- Modify, recolour, crop, animate and combine them freely.
- Do not resell or redistribute the sprites themselves, even modified, and do not upload them to
  public repositories or asset stores.
- Do not use them to train or fine-tune generative AI models.

The full text is in `LICENSE.txt`.

Made with an AI-assisted pipeline: AI image generation for the base art, then code for the
pixel clean-up, states, 9-slice geometry and manifest; the code and docs are AI-written too.
Every sprite was checked at 1x and zoomed in, and is 100 % on the palette.
This is v1.0. Requests are welcome: leave a comment on the itch.io page.
