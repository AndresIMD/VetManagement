using FluentAssertions;
using VetManagement.Domain.Clients;

namespace VetManagement.Tests.Scheduling;

public class RutTests
{
    [Theory]
    [InlineData("12.345.678-5", "12345678-5")]
    [InlineData("12345678-5", "12345678-5")]
    [InlineData("123456785", "12345678-5")]
    [InlineData("11.111.111-1", "11111111-1")]
    [InlineData("7.654.321-6", "7654321-6")]
    [InlineData("10.000.013-k", "10000013-K")]  // K check digit, lower case accepted
    [InlineData("14.000.000-0", "14000000-0")]  // 0 check digit (11 - sum % 11 == 11)
    public void ValidRuts_AreNormalized(string input, string expected)
    {
        Rut.TryParse(input, out var rut).Should().BeTrue();
        rut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("12.345.678-4")]   // wrong check digit
    [InlineData("12.345.678-K")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1-9")]           // too short to be a real RUT
    [InlineData("123.456.789-0")] // too long
    public void InvalidRuts_AreRejected(string? input)
        => Rut.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void CheckDigit_MatchesTheModulo11Rule_ForEveryOutcome()
    {
        // Brute-force a range so every remainder (incl. K and 0) is exercised against an independent formula.
        for (var n = 1_000_000; n < 1_000_300; n++)
        {
            var digits = n.ToString().Reverse().Select(c => c - '0').ToArray();
            var sum = digits.Select((d, i) => d * (2 + i % 6)).Sum();
            var expected = (11 - sum % 11) switch { 11 => '0', 10 => 'K', var d => (char)('0' + d) };
            Rut.ComputeCheckDigit(n).Should().Be(expected);
        }
    }
}
