#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class ShaderMessageData
    {
        [Description("The error or warning message text.")]
        public string? Message { get; set; }

        [Description("The line number in the shader source where the issue occurs.")]
        public int Line { get; set; }

        [Description("Severity level (e.g. 'Error', 'Warning').")]
        public string? Severity { get; set; }

        [Description("The platform on which the error occurs (e.g. 'OpenGLCore', 'D3D11').")]
        public string? Platform { get; set; }
    }
}
