using FluentAssertions;
using VetManagement.Domain.Exams;

namespace VetManagement.Tests.Application;

/// <summary>Every bundled report font ships with its files and licence (the report would fall back to Arial without a word).</summary>
public class BundledFontsTests
{
    private static readonly string Fonts = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "VetManagement.Staff.UI", "wwwroot", "fonts");

    public static IEnumerable<object[]> BundledFonts() => ExamReportSettings.BundledFonts.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(BundledFonts))]
    public void BundledFont_HasItsFilesAndLicence_AndCanBeChosen(string font)
    {
        var folder = Path.Combine(Fonts, font.ToLowerInvariant());
        foreach (var subset in new[] { "latin", "latin-ext" })
            File.Exists(Path.Combine(folder, $"{font.ToLowerInvariant()}-{subset}-wght-normal.woff2")).Should().BeTrue($"{font} {subset} ships with the app");
        File.ReadAllText(Path.Combine(folder, "LICENSE")).Should().Contain("SIL Open Font License");

        new ExamReportSettings { FontFamily = font }.Validate().Should().BeEmpty();
    }
}
