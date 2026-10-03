namespace VetManagement.Contracts.TestData;

/// <summary>A kind of sample data, how many records exist and which prerequisites are still missing.</summary>
public sealed record TestDataStatusDto(string Key, string Name, string Description, List<string> DependsOn, int Existing, List<string> Missing)
{
    public bool CanCreate => Missing.Count == 0;
}

public sealed record TestDataCreatedDto(string Key, int Created);
