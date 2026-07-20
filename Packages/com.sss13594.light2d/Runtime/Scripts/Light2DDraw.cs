using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Light2D
{
    /// <summary>
    /// The single geometry-draw routine both backends share. It only issues DrawMesh, so it behaves
    /// identically whether fed a Built-in plain CommandBuffer wrapped via
    /// <see cref="CommandBufferHelpers.GetRasterCommandBuffer"/> or a URP RenderGraph raster command
    /// buffer. Target binding, clear, view/projection and global/keyword state are the caller's job.
    /// </summary>
    internal static class Light2DDraw
    {
        private static readonly ShaderTagId LightModeTag = new ShaderTagId("LightMode");

        internal static void Draw(IRasterCommandBuffer cmd, List<Light2DDrawEntry> snapshot, int shaderPass)
        {
            if (cmd == null || snapshot == null)
                return;

            for (var i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (entry.Mesh == null || entry.Material == null)
                    continue;

                var submeshCount = entry.SubmeshCount > 0 ? entry.SubmeshCount : 1;
                for (var submesh = 0; submesh < submeshCount; submesh++)
                    cmd.DrawMesh(entry.Mesh, entry.Matrix, entry.Material, submesh, shaderPass);
            }
        }

        /// <summary>
        /// Resolves the DrawMesh pass index for a LightMode (e.g. "SRPDefaultUnlit", "Light2DNormals").
        /// DrawMesh selects a pass by index, not by LightMode, and the obstacle shaders carry more than
        /// one pass (an SRPDefaultUnlit colour pass and a Light2DNormals pass), so callers MUST resolve
        /// the index and hand it to <see cref="Draw"/> rather than assuming pass 0 (design risk R1).
        ///
        /// The authoritative match is on the pass's LightMode TAG, read from the material's active
        /// SubShader via <see cref="Shader.FindPassTagValue(int, ShaderTagId)"/> — the same SubShader
        /// DrawMesh's pass index and <see cref="Material.passCount"/> index into, so a tag hit maps
        /// straight to a valid DrawMesh pass index. <see cref="Material.FindPass"/> only matches the pass
        /// NAME (e.g. "Obstacle"), which differs from the LightMode tag, so it is used only as a fallback
        /// for shaders whose pass Name happens to equal the requested LightMode (e.g. a pass literally
        /// named "Light2DNormals"). Returns 0 when nothing matches so a single-pass Built-in SubShader
        /// (whose only pass carries no LightMode tag) still draws its colour pass.
        /// </summary>
        internal static int ResolvePass(Material material, string lightMode)
        {
            if (material == null || string.IsNullOrEmpty(lightMode))
                return 0;

            var shader = material.shader;
            if (shader != null)
            {
                var passCount = material.passCount;
                for (var i = 0; i < passCount; i++)
                {
                    var tag = shader.FindPassTagValue(i, LightModeTag);
                    if (string.Equals(tag.name, lightMode, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            var byName = material.FindPass(lightMode);
            return byName >= 0 ? byName : 0;
        }
    }
}
