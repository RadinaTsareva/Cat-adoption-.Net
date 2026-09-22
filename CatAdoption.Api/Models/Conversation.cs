namespace CatAdoption.Api.Models;

public class Conversation
{
    public int Id { get; set; }

    public int User1Id { get; set; }

    public int User2Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User1 { get; set; } = null!;

    public User User2 { get; set; } = null!;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

