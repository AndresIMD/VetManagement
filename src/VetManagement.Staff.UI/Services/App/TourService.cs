using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Services.App;

/// <summary>
/// Guided tours of screens that need explaining (driver.js, see js/tour.js). The first time a person opens such a screen
/// the app offers its tour once; the screen's "Tutorial" button shows it again, and the profile can turn the offers off.
/// What each person has seen is kept in this browser, per user name (clinic computers are shared).
/// </summary>
public class TourService(IJSRuntime js, AuthenticationStateProvider auth, IStringLocalizer<Strings> L)
{
    private const string SEEN_KEY = "tours.seen";
    private const string OFF_KEY = "tours.off";

    public async Task StartAsync(string tourId, IEnumerable<TourStep> steps)
    {
        await MarkSeenAsync(tourId);
        var labels = new { next = L["Next"].Value, previous = L["Previous"].Value, done = L["Done"].Value, progress = L["{{current}} of {{total}}"].Value };
        await js.InvokeVoidAsync("appTour.start", steps, labels);
    }

    /// <summary>True the first time this person opens the screen, unless they turned the offers off.</summary>
    public async Task<bool> ShouldOfferAsync(string tourId)
        => await OffersOnAsync() && !(await SeenAsync()).Contains(tourId);

    public async Task MarkSeenAsync(string tourId)
    {
        var seen = await SeenAsync();
        if (seen.Add(tourId))
            await js.InvokeVoidAsync("localStorage.setItem", await KeyAsync(SEEN_KEY), string.Join(',', seen));
    }

    public async Task<bool> OffersOnAsync()
        => await js.InvokeAsync<string?>("localStorage.getItem", await KeyAsync(OFF_KEY)) is null;

    public async Task SetOffersOnAsync(bool on)
    {
        var key = await KeyAsync(OFF_KEY);
        if (on)
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        else
            await js.InvokeVoidAsync("localStorage.setItem", key, "1");
    }

    private async Task<HashSet<string>> SeenAsync()
        => [.. (await js.InvokeAsync<string?>("localStorage.getItem", await KeyAsync(SEEN_KEY)) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)];

    private async Task<string> KeyAsync(string key)
        => $"{key}.{(await auth.GetAuthenticationStateAsync()).User.Identity?.Name?.ToLowerInvariant()}";
}

/// <summary>One step of a tour: the element marked data-tour="<see cref="Anchor"/>" is highlighted (no anchor = a message in the middle).</summary>
public sealed record TourStep(string? Anchor, string Title, string Text);
