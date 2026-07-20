#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using EA;

namespace EA
{
    public class CopyAssetsResponse
    {
        [Description("List of copied assets.")]
        public List<AssetObjectRef>? CopiedAssets { get; set; }
        [Description("List of errors encountered during copy operations.")]
        public List<string>? Errors { get; set; }
    }
}
