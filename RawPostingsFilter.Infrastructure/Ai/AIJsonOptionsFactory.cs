using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AIServices.Models.Options;
using RawPostingsFilter.Infrastructure.Serialization;

namespace RawPostingsFilter.Infrastructure.Ai;

public static class AIJsonOptionsFactory
{
    public static void Configure(AIJsonOptions options)
    {
        var serializerOptions = JsonSerializerOptionsFactory.Create();
        serializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        serializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
        serializerOptions.WriteIndented = true;

        options.JsonSerializerOptions = serializerOptions;
        options.JsonSchemaExporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, schema) =>
            {
                var attributeProvider = context.PropertyInfo is not null
                    ? context.PropertyInfo.AttributeProvider
                    : context.TypeInfo.Type;

                var description = attributeProvider?
                    .GetCustomAttributes(typeof(DescriptionAttribute), inherit: true)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault()
                    ?.Description;

                if (description is null)
                {
                    return schema;
                }

                var schemaObject = schema as JsonObject ?? new JsonObject();
                schemaObject["description"] = description;
                return schemaObject;
            }
        };
    }
}
