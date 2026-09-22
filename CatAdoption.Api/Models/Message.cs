namespace CatAdoption.Api.Models;

public class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public User Sender { get; set; } = null!;

    public User Receiver { get; set; } = null!;
}

