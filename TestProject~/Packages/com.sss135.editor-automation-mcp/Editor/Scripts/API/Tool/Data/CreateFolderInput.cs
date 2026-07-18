#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class CreateFolderInput
    {
        [Description("The parent folder path where the new folder will be created.")]
        public string ParentFolderPath { get; set; } = string.Empty;
        [Description("The name of the new folder to create.")]
        public string NewFolderName { get; set; } = string.Empty;
    }
}
