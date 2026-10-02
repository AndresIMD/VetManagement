using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Clients;

/// <summary>
/// Pet owner. Domain entity — no validation or UI attributes.
/// </summary>
public class Client : Entity<int>
{
    public Client() : base(0) { }

    public Client(string name, string lastName, string taxId, string address, int phoneNumber, string email) : base(0)
    {
        Name = name;
        LastName = lastName;
        TaxId = taxId;
        Address = address;
        PhoneNumber = phoneNumber;
        Email = email;
    }

    public string Name { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public int PhoneNumber { get; set; }

    public string Email { get; set; } = string.Empty;

    public List<Pet> Pets { get; set; } = [];
}
