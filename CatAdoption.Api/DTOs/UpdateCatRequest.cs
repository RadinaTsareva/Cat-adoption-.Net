using Microsoft.AspNetCore.Http;

namespace CatAdoption.Api.DTOs;

public class UpdateCatRequest
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Sex { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public IFormFile? Image { get; set; }
}
