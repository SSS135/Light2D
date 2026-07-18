#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.IvanMurzak.ReflectorNet.Json;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace com.SSS135.EditorAutomation.JsonConverters
{
    public class GradientColorKeyConverter : JsonSchemaConverter<GradientColorKey>, IJsonSchemaConverter
    {
        public override JsonNode GetSchema() => new JsonObject
        {
            [JsonSchema.Type] = JsonSchema.Object,
            [JsonSchema.Properties] = new JsonObject
            {
                ["color"] = new JsonObject
                {
                    [JsonSchema.Ref] = JsonSchema.RefValue + ColorConverter.StaticId
                },
                ["time"] = new JsonObject
                {
                    [JsonSchema.Type] = JsonSchema.Number,
                    [JsonSchema.Minimum] = 0,
                    [JsonSchema.Maximum] = 1
                }
            },
            [JsonSchema.Required] = new JsonArray { "color", "time" },
            [JsonSchema.AdditionalProperties] = false
        };
        public override JsonNode GetSchemaRef() => new JsonObject
        {
            [JsonSchema.Ref] = JsonSchema.RefValue + Id
        };

        public override GradientColorKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object token.");

            var color = new Color(1, 1, 1, 1);
            float time = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new GradientColorKey(color, time);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "color":
                            color = System.Text.Json.JsonSerializer.Deserialize<Color>(ref reader, options);
                            break;
                        case "time":
                            time = JsonFloatHelper.ReadFloat(ref reader, options);
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + "Expected 'color' or 'time'.");
                    }
                }
            }

            throw new JsonException("Expected end of object token.");
        }

        public override void Write(Utf8JsonWriter writer, GradientColorKey value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("color");
            System.Text.Json.JsonSerializer.Serialize(writer, value.color, options);
            JsonFloatHelper.WriteFloat(writer, "time", value.time, options);
            writer.WriteEndObject();
        }
    }
}
