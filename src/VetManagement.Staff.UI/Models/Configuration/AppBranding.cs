namespace VetManagement.Staff.UI.Models.Configuration;

/// <summary>
/// What a deployment says about its clinic (config.json). The staff app shows the logo in its header, or the product
/// name as text when there is none.
/// </summary>
public record AppBranding(string? LogoUrl)
{
    public bool HasLogo => Uri.IsWellFormedUriString(LogoUrl, UriKind.RelativeOrAbsolute) && !string.IsNullOrWhiteSpace(LogoUrl);
}
