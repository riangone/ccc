using System.ComponentModel.DataAnnotations;

namespace DynamicCrudSample.Models.Auth;

public class UserEditViewModel
{
    public int? Id { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string PreferredLanguage { get; set; } = "en-US";

    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}
