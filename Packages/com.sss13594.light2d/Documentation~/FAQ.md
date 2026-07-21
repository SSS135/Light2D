# Frequently Asked Questions

## Can a character (or any moving object) be a light?

Yes. Add a `LightSprite` to the character's GameObject (or a child of it) with
**GameObject > Light2D > Light Source**. `LightSprite` builds a sprite-shaped
point or line light whose `Sprite`, transform, and `Color` define its footprint;
because the light follows the transform, it moves with the character every frame
with no extra bookkeeping. Pick a ray-marching material
(`Light2D/Light 20 Points` … `Light2D/Light 90 Points`, or
`Light2D/Light Auto Points`) to trade GPU cost for sharper obstacle shadowing, or
`Light2D/Light 1 Point` / `Light2D/Light Ignoring Obstacles` for cheap glows.
`LightOrigin.z` is the light height used by normal mapping. Lights and obstacles
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
internal textures through a command buffer; its own `MeshRenderer` is kept
disabled, so lighting-input geometry never appears in the game image. That is why
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

## Can I use Light2D for fog-of-war or gameplay visibility?

Only for the *look*, not as a gameplay query. Light2D is a rendering system: it
darkens areas that receive no light and that obstacles occlude, which reads
visually like fog-of-war or a vision cone. It does **not** expose line-of-sight or
"is this object visible" queries, a revealed/explored map, or per-unit visibility
state — there is no gameplay API to read back. If you need authoritative
visibility for gameplay (AI perception, hiding units, explored-tile memory),
compute that separately (raycasts, a visibility grid, or Unity's 2D shadow
casters) and, if you like, drive Light2D's lights/obstacles from it to match the
art. (This is the substance of issue #7.)

## Do runtime-instantiated lights and obstacles work?

Yes. Instantiate a prefab that carries a `LightSprite`, an obstacle component, or a
`LightAmbientEmitter`, and it registers itself in `OnEnable` — no layer assignment,
no camera reconfiguration, no manual registration call. This is a direct
consequence of the 2.0 register-and-draw model and specifically fixes the old
failure where a spawned object produced no light because its layer was never set
(issue #2). For obstacles, make sure the source is wired (a `SpriteRenderer`/
`CustomSprite` for `LightObstacleSprite`, a parent mesh for `LightObstacleMesh`, or
use `LightObstacleGenerator` to pick the right one at `Start`). The component must
be enabled to render.

## What is the status of this project? Should I use it?

Light2D is an MIT-licensed, community-maintained 2D lighting package. The original
repository was archived in 2018; it was later modernized to Unity 6 and rebuilt as
2.0 with a clean register-and-draw architecture (no layers, no helper cameras),
Tilemap obstacle support, and a settings panel. It is not an officially supported
Unity product and carries no support guarantee.

Reach for Light2D when you want its specific look and constraints:

- **Colored, per-channel light transmission** through obstacles (tinted shadows).
- **Soft ambient bounce** via temporal propagation.
- **Zero-authoring, collider-free occlusion** — obstacles are sprites/meshes/tiles,
  not hand-placed shadow-caster shapes.
- **Cross-pipeline** support: both the Built-in Render Pipeline and URP from one
  package.

If you're on URP and want mainstream, Unity-supported 2D lighting with shadow
casters and light types maintained by Unity, prefer **URP's built-in 2D lights**.
See the README's "When to use Unity's built-in 2D lights instead" section.

## Do you support Tilemaps? How do obstacles work with tiles?

Yes, via `LightObstacleTilemap` (issue #8). Add it with **GameObject > Light2D >
Light Obstacle** on a GameObject that has a `Tilemap` — the menu detects the
Tilemap and adds the component in place; the visible `TilemapRenderer` stays on.
It is **texture-aware**: it builds obstacle geometry per source tile texture and
maps each cell's quad to that tile's sprite UVs, so the obstacle shader samples the
tile's alpha and **transparent tile pixels pass light** instead of casting a solid
block. Only collidable tiles occlude (tiles whose `Tile.ColliderType` is `None` are
skipped). The mesh rebuilds when tiles change, not every frame.

`LightObstacleTilemap` requires the **`com.unity.2d.tilemap`** package; it is
compiled only when that package is present (the `LIGHT2D_TILEMAP` define). Without
the package the component simply doesn't exist and the rest of Light2D is
unaffected. The bundled **Rocket Tilemap** sample scene demonstrates it.
