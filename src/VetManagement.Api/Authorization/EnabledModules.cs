using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Authorization;

/// <summary>
/// Modules switched on for this clinic (configuration Modules:Enabled; absent or empty = all). A module that is off
/// grants no permissions, so its menus, pages and endpoints disappear for everyone, Admin included.
/// </summary>
public sealed class EnabledModules
{
    /// <summary>Modules that need patients: they can't be on without Clients.</summary>
    private static readonly AppModule[] NeedClients = [AppModule.Exams, AppModule.Clinical, AppModule.Scheduling, AppModule.Billing];

    private readonly HashSet<AppModule> _enabled;

    public EnabledModules(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Modules:Enabled").Get<AppModule[]>();
        _enabled = configured is { Length: > 0 } ? [.. configured] : [.. Enum.GetValues<AppModule>()];

        var withoutClients = _enabled.Contains(AppModule.Clients) ? [] : NeedClients.Where(_enabled.Contains).ToList();
        if (withoutClients.Count > 0)
            throw new InvalidOperationException($"Modules:Enabled has {string.Join(", ", withoutClients)} without Clients; add Clients.");
    }

    public bool IsEnabled(AppModule module) => _enabled.Contains(module);

    /// <summary>Drops the permissions of modules that are off.</summary>
    public HashSet<string> Filter(IEnumerable<string> permissions)
        => permissions.Where(p => Permissions.ModuleOf(p) is not { } module || IsEnabled(module)).ToHashSet();
}

/// <summary>Answers 404 when the module is off; for anonymous endpoints, which permissions don't cover.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresModuleAttribute(AppModule module) : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!context.HttpContext.RequestServices.GetRequiredService<EnabledModules>().IsEnabled(module))
            context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
