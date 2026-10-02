using System.ComponentModel.DataAnnotations;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Models.Core;

public class Client(string name, string lastName, string taxId, string address, int phoneNumber, string email)
{
    public int Id { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Name { get; set; } = name;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string LastName { get; set; } = lastName;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string TaxId { get; set; } = taxId;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Address { get; set; } = address;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public int PhoneNumber { get; set; } = phoneNumber;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = email;

    public List<Pet> Pets { get; set; } = [];

    public Client() : this(string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty) { }
}
