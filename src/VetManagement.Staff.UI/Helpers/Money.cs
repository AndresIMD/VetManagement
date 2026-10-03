using System.Globalization;

namespace VetManagement.Staff.UI.Helpers;

/// <summary>Chilean pesos as "$25.000". Built by hand: the web client runs with invariant globalization.</summary>
public static class Money
{
    public static string Clp(int amount)
        => (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
}
