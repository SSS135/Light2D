# Migration and Upgrade Notes

This guide covers two moves:

- **Upgrading an existing Light2D install from 2.x to 3.0** — the breaking architecture change (below).
- **Migrating a legacy `Assets/Light2D` project into the UPM package** — the folder-to-package move (from [Legacy `Assets/Light2D` Migration](#legacy-assetslight2d-migration) onward).

## Upgrading from 2.x to 3.0

The upcoming 3.0 major release (version bump pending) is a breaking rewrite of the
render architecture. It removes the three-layer model and the Built-in helper
camera. There is no automatic upgrade — update existing scenes by hand:

1. The serialized `LightingSystem` fields `LightSourcesLayer`, `AmbientLightLayer`,
   and `LightObstaclesLayer` are gone. You no longer create Light2D layers or edit
   the source-camera culling mask; delete any leftover layer-only setup.
2. `LightingSystem.LightCamera`, the `Lighting Camera` prefab, and the
   `LightingSystemPrefabConfig` type are retired. Re-create the `LightingSystem`
   with **GameObject > Light2D > Lighting System** so it picks up the current
   default materials, or verify its material fields directly.
3. Objects that were ambient emitters "by layer" now need a `LightAmbientEmitter`
   component (**GameObject > Light2D > Ambient Emitter**).
4. Light sources and obstacles still use `LightSprite` /
   `LightObstacleSprite`/`Mesh`/`Generator`; they now register automatically when
   enabled, including on runtime-instantiated objects. Tilemap obstacles use the new
   `LightObstacleTilemap` (requires `com.unity.2d.tilemap`).

Because there is no auto-upgrade, re-add the components on existing scenes rather
than expecting serialized layer indices to carry over.

## Legacy `Assets/Light2D` Migration

The package retains the legacy GUIDs where assets were moved, so serialized references can resolve to their package locations. Unity must not import the legacy and package copies at the same time because duplicate GUIDs make the result ambiguous.

## Before Migrating

1. Commit or back up the complete project, including `.meta` files.
2. Record the active render pipeline and any modified Light2D materials or shaders. (3.0 has no Light2D layers to record.)
3. Move custom code and assets out of `Assets/Light2D`. The old folder is removed during migration.
4. Save every open scene and prefab.
5. Close Unity.

## Migration Sequence

1. Remove `Assets/Light2D` and `Assets/Light2D.meta` from the consuming project while Unity is closed. Preserve a backup outside the project.
2. Install the 2.0 package by Git URL or local disk.
3. Reopen the project in Unity 6000.5.4f1 and allow package import and script compilation to finish.
4. Resolve compilation errors before opening and saving production scenes.
5. Verify that `LightingSystem`, `LightSprite`, obstacle components, materials, and shader references resolve. Do not remove missing components until you have checked whether the package failed to import.
6. Re-add Light2D components where scenes previously relied on the legacy setup: a `LightSprite` for lights, an obstacle component for occluders, and a `LightAmbientEmitter` for ambient emission. The current package has no layers or helper camera to restore (see [Upgrading from 2.x to 3.0](#upgrading-from-2x-to-30)).
7. Follow the Built-in or URP setup in [INSTALLATION.md](INSTALLATION.md).
8. Import **Examples** through Package Manager if the project used the old example assets. Do not restore `Assets/Light2D/Examples` from backup.
9. Open and save migrated scenes and prefabs only after references and rendering have been verified.

## Removed Legacy Utility API

Version 2.0.0 removes the legacy public helper types that shipped alongside the old `Assets/Light2D` scripts:

- `Light2D.Util` (extension methods, XML serialization, input, and object helpers) has no replacement in the package. Migrate project code to `UnityEngine` equivalents such as `Vector2`/`Vector3` math, `Object.Destroy`, and `Mathf`.
- `Light2D.Point2` is replaced by `UnityEngine.Vector2Int` in project code. No package serialized assets referenced `Point2`, so scenes and prefabs are unaffected.
- `Light2D.ReadOnlyAttribute` and the `LIGHT2D_2DTK` (2D Toolkit) compile branches were removed without replacement.

Compilation errors naming these types come from project code, not package assets; update that code before continuing the sequence above.

## Custom Legacy Changes

UPM package updates can replace package files. Do not reapply custom modifications directly inside Package Cache. Choose one of these approaches:

- Keep custom shaders and materials under a project-owned folder with new GUIDs and names.
- Fork Light2D and install the fork by Git revision.
- Install a local clone from disk while developing package changes.

When copying a legacy shader, retain the property names consumed by `LightingSystem`, `LightSprite`, and existing materials. Add the URP subshader/pass contract if the custom shader must work in URP. See [ARCHITECTURE.md](ARCHITECTURE.md#material-and-shader-contract).

## Maintainer Reserialization Utility

`Light2D.LegacyProjectMigration.Run()` is an editor API used to resave package-owned materials, prefabs, scriptable objects, and the two package example scenes after their move. It searches `Packages/com.sss13594.light2d`; it does **not** migrate arbitrary assets in a consumer project and has no menu item. Do not present it as a substitute for the migration sequence above.

Maintainers can invoke it through editor automation `script.execute` or Unity’s `-executeMethod Light2D.LegacyProjectMigration.Run` after saving all scenes and making the local package writable. It reports missing scripts and fails as a group if any package asset cannot be saved or reserialized.

## Rollback

If references do not resolve, close Unity before changing package layout again. Restore the project backup and legacy `Assets/Light2D` tree together, including all `.meta` files. Do not copy only asset contents without their metas; that generates new GUIDs and permanently breaks the references the migration is intended to preserve.
