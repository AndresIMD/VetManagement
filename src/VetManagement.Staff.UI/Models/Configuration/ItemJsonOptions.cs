using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using VetManagement.Staff.UI.Models.Core;

namespace VetManagement.Staff.UI.Models.Configuration;

public static class ItemJsonOptions
{
    /// <summary>
    /// Gets JsonSerializerOptions configured for polymorphic deserialization of Item types.
    /// This allows the system to correctly instantiate Drug objects when a JSON payload
    /// indicates the type is 'Drug'.
    /// </summary>
    /// <returns>A JsonSerializerOptions object with polymorphic settings.</returns>
    public static JsonSerializerOptions GetPolymorphicOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type == typeof(Item))
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = false,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                    DerivedTypes =
                    {
                        new JsonDerivedType(typeof(Drug), "Drug")
                        // Future derived types of Item can be added here.
                    }
                };
            }
        });

        options.TypeInfoResolver = resolver;
        return options;
    }
}
