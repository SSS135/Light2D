#nullable enable
#if !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Runtime.Utils
{
    public static partial class ShaderUtils
    {
        public static IEnumerable<Shader> GetAllShaders()
            => Enumerable.Empty<Shader>();
    }
}
#endif
