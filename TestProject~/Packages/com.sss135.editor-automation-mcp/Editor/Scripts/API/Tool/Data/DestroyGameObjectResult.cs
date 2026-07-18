#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class DestroyGameObjectResult
    {
        [Description("Name of the destroyed GameObject.")]
        public string? DestroyedName { get; set; }

        [Description("Hierarchy path of the destroyed GameObject.")]
        public string? DestroyedPath { get; set; }

        [Description("Instance ID of the destroyed GameObject.")]
        public UnityEngine.EntityId DestroyedInstanceId { get; set; }
    }
}
