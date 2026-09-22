namespace CatAdoption.Api.DTOs;

public class ChatConversationResponse
{
    public int ConversationId { get; set; }

    public int OtherUserId { get; set; }

    public string OtherUserFirstName { get; set; } = string.Empty;

    public string OtherUserLastName { get; set; } = string.Empty;

    public string OtherUserEmail { get; set; } = string.Empty;

    public string OtherUserRole { get; set; } = string.Empty;

    public string? LastMessageContent { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public int UnreadCount { get; set; }
}

