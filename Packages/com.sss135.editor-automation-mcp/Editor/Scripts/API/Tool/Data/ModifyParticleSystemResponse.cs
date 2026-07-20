#nullable enable
using System.ComponentModel;

namespace EA
{
    /// <summary>
    /// Response model for Modify ParticleSystem tool.
    /// </summary>
    [Description("Response containing the result of modifying a ParticleSystem.")]
    public class ModifyParticleSystemResponse
    {
        [Description("Whether the modification was successful.")]
        public bool success;

        [Description("Reference to the GameObject containing the ParticleSystem component.")]
        public GameObjectRef? gameObjectRef;

        [Description("Reference to the modified ParticleSystem component.")]
        public ComponentRef? componentRef;

        [Description("Index of the ParticleSystem component in the GameObject's component list.")]
        public int componentIndex = -1;

        [Description("Log of modifications made and any warnings/errors encountered.")]
        public string[]? logs;
    }
}
