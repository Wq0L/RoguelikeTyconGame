# Sci-fi metal buttons

Original pixel art drawn with Aseprite Lua. Editable source:
`Assets/Art/UI/SciFiButtons/SciFi_Button.aseprite`.

Four tagged frames: Normal, Hover, Pressed, Disabled. Exported PNGs are
128 x 32, transparent, with no baked-in text. Preview order is normal / hover
on the top row, pressed / disabled on the bottom row.

Unity import: Sprite Single, Point filtering, no compression or mipmaps,
100 pixels per unit. Nine-slice border: left 20, bottom 12, right 20, top 10.
Use Image Type Sliced, white tint, pixels-per-unit multiplier 1.
Recommended minimum control size: 80 x 32 UI units.

GameScene's Planter Shop Buy Button and Back Button use SpriteSwap with these
states; keyboard selected state shares Hover. Labels stretch with 20 units
horizontal and 8 units vertical padding. Existing button handlers and purchase
availability logic are preserved. The press effect is drawn into the metal
surface; label position remains stable.

Verified PNG dimensions, unique asset GUIDs, slice borders and the six intended
scene component changes. In-game interaction has not been tested in Play Mode.

Regenerate using Aseprite --batch with create_buttons.lua, passing output and
preview directory parameters. Preserve existing PNG .meta files when exporting.
