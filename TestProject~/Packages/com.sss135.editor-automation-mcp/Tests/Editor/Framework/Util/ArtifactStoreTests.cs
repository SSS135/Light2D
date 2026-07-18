#nullable enable
using System;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using NUnit.Framework;

namespace com.SSS135.EditorAutomation.Editor.Tests
{
    /// <summary>
    /// Pure-helper coverage for <see cref="ArtifactStore"/>'s file-name sanitizer and description-header
    /// builder. The full Roslyn/persistence paths are verified live via MCP, not here.
    /// </summary>
    public sealed class ArtifactStoreTests
    {
        [Test]
        public void SanitizeFileName_AppendsExtension_WhenAbsent()
        {
            Assert.AreEqual("Foo.cs", ArtifactStore.SanitizeFileName("Foo", ".cs"));
        }

        [Test]
        public void SanitizeFileName_StripsAndReappendsExtension_CaseInsensitive()
        {
            Assert.AreEqual("Foo.cs", ArtifactStore.SanitizeFileName("Foo.cs", ".cs"));
            Assert.AreEqual("Foo.cs", ArtifactStore.SanitizeFileName("Foo.CS", ".cs"));
        }

        [Test]
        public void SanitizeFileName_PreservesInternalSpaces()
        {
            Assert.AreEqual("my file.cs", ArtifactStore.SanitizeFileName("  my file  ", ".cs"));
        }

        [Test]
        public void SanitizeFileName_Throws_OnPathTraversal()
        {
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName("../evil", ".cs"));
        }

        [Test]
        public void SanitizeFileName_Throws_OnDirectorySeparator()
        {
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName("a/b", ".cs"));
        }

        [Test]
        public void SanitizeFileName_Throws_OnDotNames()
        {
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName(".", ".cs"));
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName("..", ".cs"));
        }

        [Test]
        public void SanitizeFileName_Throws_OnEmpty()
        {
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName("", ".cs"));
        }

        [Test]
        public void SanitizeFileName_Throws_OnInvalidCharacter()
        {
            Assert.Throws<Exception>(() => ArtifactStore.SanitizeFileName("a<b", ".cs"));
        }

        [Test]
        public void BuildDescriptionHeader_SingleLine_CommentsAndTrailingBlankLine()
        {
            var nl = Environment.NewLine;
            Assert.AreEqual($"// hello{nl}{nl}", ArtifactStore.BuildDescriptionHeader("hello"));
        }

        [Test]
        public void BuildDescriptionHeader_MultiLine_OneCommentPerLine()
        {
            var nl = Environment.NewLine;
            Assert.AreEqual($"// a{nl}// b{nl}{nl}", ArtifactStore.BuildDescriptionHeader("a\nb"));
        }

        [Test]
        public void BuildDescriptionHeader_TrimsTrailingWhitespacePerLine()
        {
            var nl = Environment.NewLine;
            Assert.AreEqual($"// a{nl}// b{nl}{nl}", ArtifactStore.BuildDescriptionHeader("a   \nb\t"));
        }
    }
}
