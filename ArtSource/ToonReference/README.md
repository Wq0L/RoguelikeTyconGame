# Simple Toon URP reference

Open `Assets/ToonReference/SimpleToon_URP_Test.unity` in Unity 6000.3.14f1.
Alternatively use **Tools > Simple Toon > Create and Open URP Test**.
The scene uses the supplied Bear, four spheres, and DemoMat5 from DemoScene2.
Its dedicated renderer excludes the game's screen-space outline and SSAO so the
asset's own outlines and lighting can be compared independently.

The three Simple Toon shader files have been ported in place, retaining their
names, GUIDs, and material property names. Existing demo materials therefore
resolve without reassignment. The imported version was Built-in, not HDRP.
The shared implementation is `STUniversal.hlsl`.

Opaque shaders include URP shadow casting, depth and depth-normal passes.
Outline uses a `SimpleToonOutline` Render Objects pass installed on PC, Mobile,
and the reference renderer. Existing renderer features remain enabled.
Shading uses the main directional light; additional point/spot illumination,
baked GI, fog, and PBR reflections are intentionally not part of this reference.
Transparent surfaces alpha-blend without writing depth or casting shadows.
The color bands are evaluated in display space to retain the dark gold tones
in the project's Linear color space. Shine retains the original light-facing
calculation. Object-space outline width retains the original scale dependence.

Validation: separate Unity 6000.3.14f1 / URP 17.3 editor, GPU render at 2560x1440,
all three shaders supported, no shader errors reported, zero magenta pixels.
`simple-toon-urp.png` is the actual reference render. `validation.txt` is its check result.
The opaque reference is visually checked; transparent material behavior and
mobile-device performance have not been separately visually/device tested.
GameScene and gameplay materials were not replaced.

Use **Tools > Simple Toon > Capture Active Test Camera** to save a fresh render
under `Logs/ToonReference`. The setup menu reopens/configures the reference scene,
so save any custom reference-scene experiments separately before rebuilding it.
Vendor reimports can overwrite the ported shader files.
