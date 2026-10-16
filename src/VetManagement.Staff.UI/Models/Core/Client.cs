using System.ComponentModel.DataAnnotations;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Models.Core;

public class Client(string name, string lastName, string taxId, string address, int phoneNumber, string email)
{
    public int Id { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Name { get; set; } = name;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string LastName { get; set; } = lastName;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string TaxId { get; set; } = taxId;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Address { get; set; } = address;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public int PhoneNumber { get; set; } = phoneNumber;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    [EmailAddress(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.InvalidEmail))]
    public string Email { get; set; } = email;

    public List<Pet> Pets { get; set; } = [];

    public Client() : this(string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty) { }
}
