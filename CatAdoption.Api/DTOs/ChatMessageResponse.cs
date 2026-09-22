namespace CatAdoption.Api.DTOs;

public class ChatMessageResponse
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string ReceiverName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }
}

