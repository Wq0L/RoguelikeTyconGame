# UI art direction

The user's “Temel güç / LEGENDARY” card is the visual reference for the game's UI.

- Thick near-black plum ink, angular cut-paper silhouettes, offset hard shadows.
- Thin imperfect paper inset, short triangular corner strokes, sparse corner halftone.
- Broad printed colour washes; avoid glossy bevels and soft plastic-looking buttons.
- Shared visual vocabulary does not mean identical shapes: vary corner cuts, line weight,
  tilt and print details deliberately per element. Preserve readability and stable identities.
- Avoid evenly spaced puzzle tabs, uniformly distressed surfaces and arbitrary noisy edges.
- Coordinate plates retain blue normal fill and actual gold highlight artwork. Multiplying
  the blue artwork by yellow produces an unwanted green highlight.
- Mascot uses the approved sleepy plant in an intact glass dome and hand-inked sprite frames.
  Keep expressive asymmetry; do not replace it with a polished, perfectly symmetrical mascot.

Apply this direction when editing each UI component; do not automatically recolour or
rebuild every screen. The coordinate plates are shared by the world and round map.

## 2026-11-06 update: comic paper kit

The user asked for the preparation screen to follow the new kit:
- the HAZIRLIK / round summary card;
- the Planter Shop, Next Round and Skill Shop buttons;
- Preview, which moves to purple;
- the round map cells: open cells are white and tinted by the tile, locked cells are charcoal.

This supersedes the 2026-10-08 exception for those elements. The new rules are in `Docs/UI-Dili.md` and the inventory is in `Docs/UI-Envanter.md`.

The same day the user also asked for the coordinate boxes to change. They now use the kit's blue square (`slot_blue`) on both the map and the field. The field highlight is the real gold square (`slot_gold`), cross-faded in; blue is never multiplied by yellow.

The planter shop follows the kit too.

Headings, buttons and numbers use Bungee 400.

The 2026-10-08 exception below is fully superseded for the preparation screen, the map, the coordinates and the planter shop.

## Current user-approved exceptions (2026-10-08)

The user reverted the round-screen action buttons to the original rounded toon
sprite and palette materials. Coordinate letters/numbers also use the original
plain blue rounded plates and original sizes. The preparation/round-summary panel
uses its original straight rounded frame and level amber heading, not QuotaPanel.
Do not reapply the angular banner or coordinate artwork to these elements unless
requested again. World column labels remain on the far/top edge of the grid.
