using System.Reflection;

namespace VetManagement.Shared.Helpers;

/// <summary>
/// Extension methods for object manipulation and reflection-based operations.
/// </summary>
public static class ObjectExtensions
{
    /// <summary>
    /// Copies matching public properties from source to target object.
    /// </summary>
    /// <typeparam name="TSource">Source object type.</typeparam>
    /// <typeparam name="TTarget">Target object type.</typeparam>
    /// <param name="source">Source object to copy from.</param>
    /// <param name="target">Target object to copy to.</param>
    public static void CopyPropertiesTo<TSource, TTarget>(this TSource source, TTarget target)
    {
        if (source is null || target is null)
            return;

        var sourceType = source.GetType();
        var targetType = target.GetType();

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProp in sourceProps)
        {
            var targetProp = targetProps.FirstOrDefault(p =>
              p.Name == sourceProp.Name &&
               p.CanWrite &&
                   p.PropertyType.IsAssignableFrom(sourceProp.PropertyType));

            if (targetProp != null)
            {
                var value = sourceProp.GetValue(source);
                targetProp.SetValue(target, value);
            }
        }
    }
}
