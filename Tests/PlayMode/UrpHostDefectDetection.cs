using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#endif

namespace Light2D.Tests
{
    /// <summary>
    /// Positive detection of the known Unity 6000.4.7f1 / URP 17.4.0 host defect that breaks URP
    /// initialization in the pinned test host. The canonical sibling implementation is the private
    /// helper in <c>Tests/EditMode/EditorWorkflowTests.cs</c> (private by design there). Keep the
    /// four detection conditions in sync between both files.
    /// </summary>
    internal static class UrpHostDefectDetection
    {
#if UNITY_EDITOR
        private const string KnownDefectUnityVersion = "6000.4.7f1";
        private const string KnownDefectUrpVersion = "17.4.0";
        private const string KnownDefectShaderGraphPath =
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph";

        // Skips only when ALL four conditions positively identify the defective host:
        // 1. the editor version is exactly the known-defect version,
        // 2. the URP package version is exactly the known-defect version,
        // 3. the required URP ShaderGraph imported as a non-Shader main asset,
        // 4. the UniversalRendererData MonoScript is unresolvable.
        internal static void IgnoreIfKnownUrpHostDefect()
        {
            if (Application.unityVersion != KnownDefectUnityVersion)
                return;

            var urpPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(UniversalRenderPipeline).Assembly);
            var shaderGraphAsset = AssetDatabase.LoadMainAssetAtPath(KnownDefectShaderGraphPath);
            var probe = ScriptableObject.CreateInstance<UniversalRendererData>();
            bool rendererScriptUnresolvable;
            try
            {
                rendererScriptUnresolvable = MonoScript.FromScriptableObject(probe) == null;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            if (urpPackage?.version != KnownDefectUrpVersion || shaderGraphAsset == null ||
                shaderGraphAsset is Shader || !rendererScriptUnresolvable)
                return;

            Assert.Ignore($"Unity {KnownDefectUnityVersion} with URP {KnownDefectUrpVersion} imported " +
                $"'{KnownDefectShaderGraphPath}' as '{shaderGraphAsset.GetType().FullName}' instead of Shader and " +
                "cannot resolve the renderer-data MonoScript; URP initialization on this host is untrustworthy.");
        }
#else
        // Player builds cannot query editor import state; the detected defect is editor-host-only.
        internal static void IgnoreIfKnownUrpHostDefect()
        {
        }
#endif
    }
}
