#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.IvanMurzak.ReflectorNet.Json;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace com.SSS135.EditorAutomation.JsonConverters
{
    public class Vector4Converter : JsonSchemaConverter<Vector4>, IJsonSchemaConverter
    {
        public override JsonNode GetSchema() => new JsonObject
        {
            [JsonSchema.Type] = JsonSchema.Object,
            [JsonSchema.Properties] = new JsonObject
            {
                ["x"] = new JsonObject { [JsonSchema.Type] = JsonSchema.Number },
                ["y"] = new JsonObject { [JsonSchema.Type] = JsonSchema.Number },
                ["z"] = new JsonObject { [JsonSchema.Type] = JsonSchema.Number },
                ["w"] = new JsonObject { [JsonSchema.Type] = JsonSchema.Number }
            },
            [JsonSchema.Required] = new JsonArray { "x", "y", "z", "w" },
            [JsonSchema.AdditionalProperties] = false
        };
        public override JsonNode GetSchemaRef() => new JsonObject
        {
            [JsonSchema.Ref] = JsonSchema.RefValue + Id
        };

        public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object token.");

            float x = 0, y = 0, z = 0, w = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Vector4(x, y, z, w);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "x":
                            x = JsonFloatHelper.ReadFloat(ref reader, options);
                            break;
                        case "y":
                            y = JsonFloatHelper.ReadFloat(ref reader, options);
                            break;
                        case "z":
                            z = JsonFloatHelper.ReadFloat(ref reader, options);
                            break;
                        case "w":
                            w = JsonFloatHelper.ReadFloat(ref reader, options);
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + "Expected 'x', 'y', 'z', or 'w'.");
                    }
                }
            }

            throw new JsonException("Expected end of object token.");
        }

        public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            JsonFloatHelper.WriteFloat(writer, "x", value.x, options);
            JsonFloatHelper.WriteFloat(writer, "y", value.y, options);
            JsonFloatHelper.WriteFloat(writer, "z", value.z, options);
            JsonFloatHelper.WriteFloat(writer, "w", value.w, options);
            writer.WriteEndObject();
        }
    }
}
