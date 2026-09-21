# Comic Toon UI

Generated with the built-in image generation tool. The supplied user mockups informed the art direction: thick black ink, emerald enamel, cream paper, halftone dots, bright highlights and chunky lettering. Text remains editable TextMeshPro content, never baked into a button.

## Generated assets and prompts

- `Assets/Art/UI/ComicToon/Button.png`: Single blank wide emerald/teal comic game button on transparent background. Thick near-black rounded outline and lower extrusion shadow, mint upper rim, cream upper-right shine, halftone dots concentrated along bottom corners. No lettering, symbols, icons, watermark or surrounding mockup. Reusable scalable UI art.
- `Assets/Art/UI/ComicToon/Card.png`: Empty portrait cream paper UI card with subtle halftone dots, rounded corners, thick near-black frame, transparent exterior. No content. A second image-generation edit replaced the original green rim with matte near-black while retaining the paper, dots and silhouette.
- `Assets/Art/UI/ComicToon/Coin.png`: Single gold coin comic UI icon, slightly tilted, bold black outline, warm gold face and darker lower edge, pale glossy highlights, transparent exterior, no text.
- `Assets/Art/UI/ComicToon/Sprout.png`: Single two-leaf lime-green sprout comic icon with bold black outline, simple glossy light accents and transparent exterior. No text.

Original generated files are preserved in the tool's generated_images directory. Unity sprite assets crop transparent margins without modifying the source PNGs. Nine-slice borders preserve corners. The UI shader creates green/red/blue/gold palettes and hover, pressed and disabled appearances from the same art.

## Fonts

Lilita One (headings/buttons) and Barlow SemiBold (body/fallback) from the Google Fonts repository, each under SIL Open Font License 1.1. License files are included with the fonts and must accompany redistribution. Font sources:

- https://github.com/google/fonts/tree/main/ofl/lilitaone
- https://github.com/google/fonts/tree/main/ofl/barlow

Atlas character set includes basic Latin and Turkish letters. Barlow is the fallback for any unavailable heading glyphs.

## Integration

`ComicUITheme` stores the reusable skin in Resources. `ComicButtonVisual` handles pointer, keyboard selection and disabled states. `ComicUIBuilder` applies the saved scene/prefab design; `ComicUIVerification.RunBatch` exercises actual GameScene interactions in an isolated Unity project. Shop animations use unscaled time and preserve the existing placement, half-cost cancellation refund and selling systems. High-resolution planter previews are rendered from the actual game prefabs with their toon materials; the UI framing and new button/icons were made with image generation. `RunMenuBatch` checks the menu rendering.

Before-change scene/script backups: `Backups/ComicToon-20260920`.

Planter shop follow-up: removed the decorative sixth card. Only the five real planters remain. Explicit unlock IDs connect 1x3/2x2/2x3 to their actual skill nodes; normalized Unity asset serialization preserves those gates. The BUY button uses Gold, Iron or Stone sprites. Iron and Stone previews were rendered from the project's KayKit resource models. Actual prerequisite purchases and reset behavior are covered by ComicUIVerification; see planter-unlock-validation.txt.

## Round map comic update
Grid Tile.png generated with built-in imagegen. Original source: exec-71fb3dbc-4a71-446c-a237-7ae0eaa440a9.png (preserved in generated_images).
Prompt: Single transparent square comic UI tile, neutral white/light gray surface, thick black rounded frame, inset gray bevel, glossy top-left highlight and subtle lower-corner halftone; no text or icons. Designed for arbitrary tinting in Unity.
Uses one shared sprite/texture with UI vertex color for locked charcoal, open white and the existing modifier colors. Simple sprite scaling preserves corners at small tile sizes. Grid objects and coordinate labels are pooled between rounds. Actual map dimensions and reversed Z mapping remain unchanged. TooltipTrigger/TileCellUI retain their existing behavior. ComicHoverMotion uses unscaled time and only runs a coroutine during transitions. Cart/star icons are small vector meshes.
ComicUIBuilder.UpdateRoundBatch applies the round screen; RoundMapComicVerification.RunBatch validates it in Play Mode. Backups: Backups/RoundMapComic-20260920.
