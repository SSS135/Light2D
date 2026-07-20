#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;
using System.Linq;

namespace EA
{
    public class ModifyObjectResponse
    {
        [Description("Whether the modification was successful.")]
        public bool Success { get; set; } = false;

        [Description("Reference to the modified object.")]
        public ObjectRef? Reference { get; set; }

        [Description("Updated object data after modification.")]
        public SerializedMember? Data { get; set; }

        [Description("Log of modifications made and any warnings/errors encountered.")]
        public string[]? Logs { get; set; }

        public ModifyObjectResponse(bool success, Logs logs)
        {
            Success = success;
            Logs = logs
                .Select(log => log.ToString())
                .ToArray();
        }
    }
}
