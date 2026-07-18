#nullable enable
using System;
using System.Reflection;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;
using NUnit.Framework;

namespace com.SSS135.EditorAutomation.Editor.Tests
{
    /// <summary>
    /// Regression guard for B-wiki-index-omits-tools. Asserts that the partial-type-load classifier
    /// surfaces a non-zero failure signal (so a thin registry snapshot is no longer silent) while
    /// still preserving the types that loaded successfully.
    /// </summary>
    public sealed class AttributeScannerPartialLoadTests
    {
        // Stands in as a successfully-loaded [McpToolType] type alongside a failed (null) slot.
        [McpToolType]
        sealed class DummyLoadedToolType
        {
        }

        [Test]
        public void ClassifyLoadedTypes_PartialLoad_SignalsFailureAndPreservesLoadedTypes()
        {
            var rtle = new ReflectionTypeLoadException(
                new Type?[] { typeof(DummyLoadedToolType), null },
                new Exception?[] { new TypeLoadException("simulated") });

            var loaded = AttributeScanner.ClassifyLoadedTypes(rtle.Types, rtle, out var failedTypeCount);

            // A type failed to load — the scan must report it rather than silently keeping rtle.Types.
            Assert.GreaterOrEqual(failedTypeCount, 1, "partial-load failure count must be reported");
            // The type that did load is preserved, so successful tools still register.
            Assert.Contains(typeof(DummyLoadedToolType), (System.Collections.ICollection)loaded,
                "successfully-loaded [McpToolType] type must be preserved");
        }
    }
}
