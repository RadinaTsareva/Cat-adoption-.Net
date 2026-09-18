namespace CatAdoption.Api.Models;

public class Cat
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Sex { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Status { get; set; } = "Available";

    public string? ImageUrl { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}