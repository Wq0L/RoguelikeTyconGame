# Skill Tree Pop Up — implemented

Updated existing `Assets/Prefabs/UI/Skill Tree Pop Up.prefab` in Unity 6000.3.14f1. Prefab GUID and TooltipContent references are preserved; Skill Node.prefab already points to this prefab.

- Root size 448 × 320; intentional right-center pivot preserved for the existing hover positioning.
- Separate Popup_Background, Popup_Frame and Popup_Shadow sprites, Sliced, white tint, pixels-per-unit multiplier 0.5.
- 32-unit content margins, 64 × 64 icon, header-anchored title and level, stretch-anchored stat area, bottom-anchored cost.
- All graphics are on the UI layer and do not block pointer raycasts.
- Dark text and dark teal upgrades replace white/bright-green stat values on the pale panel.

The preview uses example content; runtime values still come from SkillNodeUI.FillTooltip. Existing TMP fonts are preserved. Dynamic font atlas changes made during validation were reverted because they are not part of this prefab change.

`SkillTreePopup_Editor.png` is a crop of `Unity_Editor_Screenshot.png`, captured directly in Unity Prefab Mode. Grid lines in the screenshot belong to the Scene view, not the popup artwork.

Validation: Unity compilation, prefab loading, five serialized content references, sample text overflow check, raycast check and visual inspection. Full in-game hover positioning at screen edges and all screen sizes was not tested.

The temporary editor setup source is archived as `.cs.txt`; it is not included in project compilation. Its experimental offscreen renderer is not the delivered screenshot.
