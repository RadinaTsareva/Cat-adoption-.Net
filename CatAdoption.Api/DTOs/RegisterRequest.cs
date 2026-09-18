using CatAdoption.Api.Models;

namespace CatAdoption.Api.DTOs;

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.PetAdopter;
}