using System.Reflection;

namespace VetManagement.Shared.Enums;

/// <summary>
/// An attribute to provide a user-friendly display string for an enum value.
/// This is useful for UI elements where the enum's programmatic name is not ideal.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class DisplayStringAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

/// <summary>
/// Extension methods for enum values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Gets the display string for an enum value.
    /// Falls back to the enum's member name if the attribute is not present.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The display string.</returns>
    public static string GetDisplayString<TEnum>(this TEnum value) where TEnum : Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var attr = member?.GetCustomAttribute<DisplayStringAttribute>();
        return attr?.Value ?? value.ToString();
    }
}
