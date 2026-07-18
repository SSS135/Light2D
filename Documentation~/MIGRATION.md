# Migration from `Assets/Light2D`

Light2D 2.0 is a UPM package, not a folder to copy under `Assets`. Package assets retain the legacy GUIDs where they were moved, so serialized references can resolve to their package locations. Unity must not import the legacy and package copies at the same time because duplicate GUIDs make the result ambiguous.

## Before Migrating

1. Commit or back up the complete project, including `.meta` files.
2. Record the three Light2D layer names and indices, source-camera culling masks, active render pipeline, and any modified Light2D materials or shaders.
3. Move custom code and assets out of `Assets/Light2D`. The old folder is removed during migration.
4. Save every open scene and prefab.
5. Close Unity.

## Migration Sequence

1. Remove `Assets/Light2D` and `Assets/Light2D.meta` from the consuming project while Unity is closed. Preserve a backup outside the project.
2. Install the 2.0 package by Git URL or local disk.
3. Reopen the project in Unity 6000.4.7f1 and allow package import and script compilation to finish.
4. Resolve compilation errors before opening and saving production scenes.
5. Verify that `LightingSystem`, `LightSprite`, obstacle components, materials, and shader references resolve. Do not remove missing components until you have checked whether the package failed to import.
6. Recreate the three layers if needed and restore their serialized indices on every `LightingSystem` and relevant child object.
7. Exclude those layers from each source camera culling mask.
8. Follow the Built-in or URP setup in [INSTALLATION.md](INSTALLATION.md).
9. Import **Examples** through Package Manager if the project used the old example assets. Do not restore `Assets/Light2D/Examples` from backup.
10. Open and save migrated scenes and prefabs only after references and rendering have been verified.

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
