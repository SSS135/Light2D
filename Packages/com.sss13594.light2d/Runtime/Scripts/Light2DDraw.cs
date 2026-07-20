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
        /// one pass, so callers MUST resolve the index once per material and hand it to <see cref="Draw"/>
        /// rather than assuming pass 0 (design risk R1). <see cref="Material.FindPass"/> matches by pass
        /// name; a name-scan fallback covers shaders whose pass name differs from the LightMode tag.
        /// Returns 0 when nothing matches so a mis-tagged shader still draws its first pass.
        /// </summary>
        internal static int ResolvePass(Material material, string lightMode)
        {
            if (material == null || string.IsNullOrEmpty(lightMode))
                return 0;

            var pass = material.FindPass(lightMode);
            if (pass >= 0)
                return pass;

            var passCount = material.passCount;
            for (var i = 0; i < passCount; i++)
            {
                if (string.Equals(material.GetPassName(i), lightMode, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }
    }
}
