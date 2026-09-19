# GameScene toon integration

GameScene now uses the verified toon renderer (camera renderer index 1), hard
directional shadows, and no postprocessing. Its playable camera position,
orthographic projection and size are preserved.

Dedicated materials in `Assets/Materials/GameToon` replace the surface materials
on all 12 plant prefabs, five planter prefabs, Ground, and the scene walls.
Original material assets and textures remain available. Ground locked/unlocked
material arrays are converted too, so runtime swaps retain toon shading.

The shader supports `_BaseColor` property blocks for tile colors, configurable
surface culling for double-sided crops, and a world-space outline width that
does not grow with model import scale. VFXManager uses a separate toon flash
overlay so white hit flashes remain visible over colored texture atlases.
Existing placement-valid/invalid ghost materials are preserved.

Validation used Unity 6000.3.14f1 / URP 17.3 in an isolated project copy:
- Game scripts compile, material/shader checks pass, and the game camera renders.
- Play Mode: ground unlock/lock, tile bonus color/reset, placement ghost material
  restoration, white hit flash activation and timed reset all pass.
- Screenshots show unsaved sample plants/planters in GameScene. These samples
  were not saved into the gameplay layout. `gamescene-toon.png` uses the actual
  game camera framing; `gamescene-toon-detail.png` is a separate close view.

The geometry is unchanged: angular crops remain angular. This pass supplies the
reference's lighting, shine and outlines, while retaining the game's palette.
No full gameplay session or mobile-device performance run was performed.

The previous scene/prefabs are in `Backups/GameToon-20260919`; do not restore
those blindly over later edits. `after-hashes.json` identifies this revision.
