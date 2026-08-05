# Frequently Asked Questions

## Can a character (or any moving object) be a light?

Yes. Add a `LightSprite` to the character's GameObject (or a child of it) with
**GameObject > Light2D > Light Source**. `LightSprite` builds a sprite-shaped
point or line light whose `Sprite`, transform, and `Color` define its footprint;
because the light follows the transform, it moves with the character every frame
with no extra bookkeeping. Pick the shadow quality with the `LightSprite.Shadows`
popup to trade GPU cost for sharper obstacle shadowing (the `Light2D/Light Points`
shader ray-marches at the chosen `_PathTrackingSamples` count), or use
`Light2D/Light 1 Point` / `Light2D/Light Ignoring Obstacles` for cheap glows.
`NormalMapHeight` is the light height used by normal mapping. Lights and obstacles
need no colliders.

## How do I exclude an object from lighting? I remember setting up layers.

Light2D 2.0 removed the three-layer model. There are no "light source", "ambient",
or "light obstacle" layers to create, no `LightingSystem` layer fields to assign,
and no source-camera culling mask to edit. An object participates in lighting
**only if it carries a Light2D component**:

- Light source → `LightSprite`
- Ambient emitter → `LightAmbientEmitter`
- Obstacle → `LightObstacleSprite`, `LightObstacleMesh`, `LightObstacleGenerator`,
  or (with the Tilemap package) `LightObstacleTilemap`

To *exclude* an object, simply don't add (or remove/disable) the component. Each
component registers itself when enabled and is drawn directly into Light2D's
internal textures through a command buffer; it owns no scene renderer at all, so
lighting-input geometry never appears in the game image. That is why
the old "exclude these layers from the camera" step is gone — there is nothing to
exclude.

## What are the "roles" in Light2D 2.0?

Three roles, each expressed as a component that self-registers with the render core:

1. **Light sources** (`LightSprite`) emit direct light.
2. **Ambient emitters** (`LightAmbientEmitter`) feed the optional temporal
   ambient-propagation pass so surfaces bounce a soft fill light.
3. **Obstacles** (`LightObstacleSprite` / `LightObstacleMesh` /
   `LightObstacleGenerator` / `LightObstacleTilemap`) absorb and tint transmitted
   light per RGB channel.

A single `LightingSystem` on the receiving camera renders each role into a
low-resolution texture and composites the result over the camera image. Add a
`LightingSystem` with **GameObject > Light2D > Lighting System**. A fresh scene
composites to black until it has at least one light source or ambient emitter.

## How do I make it lit near the player and dark farther away?

Put a `LightSprite` on (or near) the player with a footprint sized to the lit
radius you want, and keep `LightingSystem.EnableAmbientLight` off or its emitters
dim so unlit areas fall to black. Surround the play area with obstacles
(`LightObstacleSprite`/`Mesh`/`Tilemap`) so walls absorb light and cast the dark.
Obstacle RGB controls transmission per channel — darker obstacle color blocks more
light, and colored obstacles tint what passes through. The bundled **Rocket** cave
sample is exactly this look: a bright central light in a dark, obstacle-walled
cave. Tune `LightPixelSize` (lower = sharper, costlier) and `LightCameraSizeAdd`
so off-screen lights still contribute at the edges.

## How do I make a bright world with a dark cave?

Flip the world's lighting baseline and re-enable Light2D only inside the cave. On
the camera's `LightingSystem`, set `DefaultCoverage = 0`: every pixel now shows the
raw game image at full brightness (Light2D is bypassed everywhere). Then confine
Light2D to the cave with a coverage volume:

1. **GameObject > Light2D > Light Volume** creates a `Light2DVolume`. Set
   `Shape = Box` and size it (`Size`) to cover the cave — or use `Circle` (`Radius`)
   or a `Sprite` outline if that fits the space better.
2. Set the volume's `Coverage = 1` so Light2D applies inside it. The cave goes dark
   until something lights it.
3. Put torches (`LightSprite`) and walls (`LightObstacleSprite` /
   `LightObstacleMesh` / `LightObstacleTilemap`) inside the volume. The cave now
   reads as dark with lit pools and cast shadows, while the world outside the volume
   stays full-bright.

Use `BlendDistance` for a soft transition at the cave mouth and `Priority` to
resolve overlapping volumes (higher wins). If you would rather keep the baseline in
the scene as an object than as a field on the `LightingSystem`, leave
`DefaultCoverage = 1` and add a low-priority `Shape = Global, Coverage = 0` volume
instead — the local `Coverage = 1` cave volume paints over it the same way. Leaving
`DefaultCoverage` at its default 1 with no volumes lights the whole world exactly as
before, at no cost.

If you instead want a region that stays *lit* but stylized — a soft ambient floor
rather than the raw art showing through — use a `LightAmbientEmitter` in `Fill` mode
(`Box` or `Circle`) to emit a flat, shaped ambient region there, rather than a
coverage volume that bypasses Light2D.

## Can I use Light2D for fog-of-war or gameplay visibility?

Only for the *look*, not as a gameplay query. Light2D is a rendering system: it
darkens areas that receive no light and that obstacles occlude, which reads
visually like fog-of-war or a vision cone. It does **not** expose line-of-sight or
"is this object visible" queries, a revealed/explored map, or per-unit visibility
state — there is no gameplay API to read back. If you need authoritative
visibility for gameplay (AI perception, hiding units, explored-tile memory),
compute that separately (raycasts, a visibility grid, or Unity's 2D shadow
casters) and, if you like, drive Light2D's lights/obstacles from it to match the
art. (This is the substance of
[issue #7](https://github.com/SSS135/Light2D/issues/7).)

## Do runtime-instantiated lights and obstacles work?

Yes. Instantiate a prefab that carries a `LightSprite`, an obstacle component, or a
`LightAmbientEmitter`, and it registers itself in `OnEnable` — no layer assignment,
no camera reconfiguration, no manual registration call. This is a direct
consequence of the 2.0 register-and-draw model and specifically fixes the old
failure where a spawned object produced no light because its layer was never set
([issue #2](https://github.com/SSS135/Light2D/issues/2)). For obstacles, make
sure the source is wired (a `SpriteRenderer` for `LightObstacleSprite`, a parent
mesh for `LightObstacleMesh`, or use `LightObstacleGenerator` to pick the right
one at `Start`). The component must be enabled to render.

## What is the status of this project? Should I use it?

Light2D is an MIT-licensed, actively maintained 2D lighting package. It was
modernized to Unity 6 and rebuilt as 2.0 with a clean register-and-draw
architecture (no layers, no helper cameras), Tilemap obstacle support, and a
settings panel. Bug reports and feature requests go to the
[issue tracker](https://github.com/SSS135/Light2D/issues). Light2D is not
affiliated with Unity Technologies.

Reach for Light2D when you want its specific look and constraints:

- **Colored, per-channel light transmission** through obstacles (tinted shadows).
- **Soft ambient bounce** via temporal propagation.
- **Zero-authoring, collider-free occlusion** — obstacles are sprites/meshes/tiles,
  not hand-placed shadow-caster shapes.
- **Cross-pipeline** support: both the Built-in Render Pipeline and URP from one
  package.

Light2D and **URP's built-in 2D lights** aim at different goals. Unity's 2D lights
are the fit when you want Unity-maintained `ShadowCaster2D` shadows and standard
light types inside the URP 2D Renderer. Light2D is the fit when you want the traits
above, or Built-in Render Pipeline support, from one package. Compare the two in
[When to use Unity's built-in 2D lights instead](../README.md#when-to-use-unitys-built-in-2d-lights-instead).

## Do you support Tilemaps? How do obstacles work with tiles?

Yes, via `LightObstacleTilemap`
([issue #8](https://github.com/SSS135/Light2D/issues/8)). Add it with
**GameObject > Light2D > Light Obstacle** on a GameObject that has a `Tilemap` —
the menu detects the Tilemap and adds the component in place; the visible
`TilemapRenderer` stays on. It is **texture-aware**: it builds obstacle geometry
per source tile texture and maps each cell's quad to that tile's sprite UVs, so
the obstacle shader samples the tile's alpha and **transparent tile pixels pass
light** instead of casting a solid block. Only collidable tiles occlude (tiles
whose `Tile.ColliderType` is `None` are skipped). The mesh rebuilds when tiles
change, not every frame.

`LightObstacleTilemap` requires the **`com.unity.2d.tilemap`** package; it is
compiled only when that package is present (the `LIGHT2D_TILEMAP` define). Without
the package the component simply doesn't exist and the rest of Light2D is
unaffected. The bundled **Rocket Tilemap** sample scene demonstrates it.
