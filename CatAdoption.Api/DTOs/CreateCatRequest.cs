namespace CatAdoption.Api.DTOs;

public class CreateCatRequest
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Sex { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
}