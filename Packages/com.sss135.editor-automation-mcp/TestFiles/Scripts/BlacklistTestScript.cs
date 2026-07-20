#nullable enable
using UnityEngine;

namespace com.SSS135.EditorAutomation.TestFiles
{
    /// <summary>
    /// A type that will be blacklisted during serialization tests.
    /// </summary>
    [System.Serializable]
    public class BlacklistedType
    {
        public int value;
        public string? name;
    }

    /// <summary>
    /// Another type that will be blacklisted during serialization tests.
    /// </summary>
    [System.Serializable]
    public struct BlacklistedStruct
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// Test component containing fields and properties of blacklisted types.
    /// Used to verify that blacklisted types are excluded from serialization.
    /// </summary>
    public class BlacklistTestScript : MonoBehaviour
    {
        // Non-blacklisted fields (should be serialized)
        public int normalIntField;
        public string? normalStringField;
        public Vector3 normalVectorField;

        // Fields of types that will be blacklisted (should NOT be serialized when blacklisted)
        public BlacklistedType blacklistedTypeField = null!;
        public BlacklistedStruct blacklistedStructField;

        // Property of blacklisted type (should NOT be serialized when blacklisted)
        public BlacklistedType? BlacklistedTypeProperty { get; set; }
    }
}
