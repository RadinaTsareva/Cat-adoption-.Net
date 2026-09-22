namespace CatAdoption.Api.DTOs;

public class CreateMessageRequest
{
    public int ReceiverId { get; set; }

    public string Content { get; set; } = string.Empty;
}

