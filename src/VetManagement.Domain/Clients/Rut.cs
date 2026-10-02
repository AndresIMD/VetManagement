namespace VetManagement.Domain.Clients;

/// <summary>
/// Chilean tax id (RUT/RUN). Accepts "12.345.678-5", "12345678-5" or "123456785" (K in either case) and
/// stores the normalized form "12345678-5" used to match clients.
/// </summary>
public readonly record struct Rut
{
    public int Number { get; }
    public char CheckDigit { get; }

    private Rut(int number, char checkDigit) => (Number, CheckDigit) = (number, checkDigit);

    public override string ToString() => $"{Number}-{CheckDigit}";

    public static bool TryParse(string? input, out Rut rut)
    {
        rut = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var compact = input.Replace(".", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (compact.Length is < 2 or > 9)
            return false;

        var body = compact[..^1];
        var digit = compact[^1];
        if (!body.All(char.IsAsciiDigit) || !int.TryParse(body, out var number) || number < 1_000_000)
            return false;
        if (digit != ComputeCheckDigit(number))
            return false;

        rut = new Rut(number, digit);
        return true;
    }

    /// <summary>Modulo 11 with weights 2..7 from the right.</summary>
    public static char ComputeCheckDigit(int number)
    {
        var sum = 0;
        var weight = 2;
        for (var n = number; n > 0; n /= 10)
        {
            sum += n % 10 * weight;
            weight = weight == 7 ? 2 : weight + 1;
        }
        return (11 - sum % 11) switch
        {
            11 => '0',
            10 => 'K',
            var d => (char)('0' + d)
        };
    }
}
