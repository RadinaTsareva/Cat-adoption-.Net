using System.Security.Claims;

namespace CatAdoption.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MessagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!IsChatParticipant(currentUser.Role))
        {
            return Forbid();
        }

        var targetRole = currentUser.Role == UserRoles.CareGiver
            ? UserRoles.PetAdopter
            : UserRoles.CareGiver;

        var contacts = await _context.Users
            .AsNoTracking()
            .Where(user => user.Role == targetRole)
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new ChatContactResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                City = user.City
            })
            .ToListAsync();

        return Ok(contacts);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!IsChatParticipant(currentUser.Role))
        {
            return Forbid();
        }

        var conversations = await _context.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.User1)
            .Include(conversation => conversation.User2)
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.User1Id == currentUser.Id || conversation.User2Id == currentUser.Id)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync();

        var result = conversations
            .Select(conversation => BuildConversationSummary(conversation, currentUser.Id))
            .ToList();

        return Ok(result);
    }

    [HttpGet("conversations/{conversationId:int}")]
    public async Task<IActionResult> GetConversation(int conversationId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!IsChatParticipant(currentUser.Role))
        {
            return Forbid();
        }

        var conversation = await _context.Conversations
            .Include(conversation => conversation.User1)
            .Include(conversation => conversation.User2)
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId);

        if (conversation == null)
        {
            return NotFound(new { message = "Conversation not found." });
        }

        if (conversation.User1Id != currentUser.Id && conversation.User2Id != currentUser.Id)
        {
            return Forbid();
        }

        var unreadMessages = await _context.Messages
            .Where(message => message.ConversationId == conversationId && message.ReceiverId == currentUser.Id && !message.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        if (unreadMessages.Count > 0)
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .Select(message => BuildMessageResponse(message))
            .ToListAsync();

        return Ok(new
        {
            conversationId = conversation.Id,
            otherUser = BuildOtherUser(conversation, currentUser.Id),
            messages
        });
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(CreateMessageRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!IsChatParticipant(currentUser.Role))
        {
            return Forbid();
        }

        var content = request.Content.Trim();
        if (request.ReceiverId <= 0 || string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { message = "Receiver and content are required." });
        }

        if (request.ReceiverId == currentUser.Id)
        {
            return BadRequest(new { message = "You cannot send a message to yourself." });
        }

        var receiver = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ReceiverId);
        if (receiver == null)
        {
            return NotFound(new { message = "Receiver not found." });
        }

        if (!IsChatParticipant(receiver.Role) || receiver.Role == currentUser.Role)
        {
            return BadRequest(new { message = "Chat is only available between care-givers and pet-adopters." });
        }

        var (user1Id, user2Id) = GetConversationKey(currentUser.Id, receiver.Id);
        var conversation = await _context.Conversations.FirstOrDefaultAsync(conversation =>
            conversation.User1Id == user1Id && conversation.User2Id == user2Id);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                User1Id = user1Id,
                User2Id = user2Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        }

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = currentUser.Id,
            ReceiverId = receiver.Id,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        conversation.UpdatedAt = message.SentAt;
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            conversationId = conversation.Id,
            otherUser = new ChatContactResponse
            {
                Id = receiver.Id,
                FirstName = receiver.FirstName,
                LastName = receiver.LastName,
                Email = receiver.Email,
                Role = receiver.Role,
                City = receiver.City
            },
            message = BuildMessageResponse(message, currentUser, receiver)
        });
    }

    [HttpPut("{messageId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int messageId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!IsChatParticipant(currentUser.Role))
        {
            return Forbid();
        }

        var message = await _context.Messages
            .FirstOrDefaultAsync(message => message.Id == messageId);

        if (message == null)
        {
            return NotFound(new { message = "Message not found." });
        }

        if (message.ReceiverId != currentUser.Id)
        {
            return Forbid();
        }

        if (!message.IsRead)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Message marked as read.", messageId = message.Id });
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var currentUserId))
        {
            return null;
        }

        return await _context.Users.FirstOrDefaultAsync(user => user.Id == currentUserId);
    }

    private static bool IsChatParticipant(string role)
    {
        return role == UserRoles.CareGiver || role == UserRoles.PetAdopter;
    }

    private static (int User1Id, int User2Id) GetConversationKey(int userId1, int userId2)
    {
        return userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);
    }

    private static ChatConversationResponse BuildConversationSummary(Conversation conversation, int currentUserId)
    {
        var otherUser = conversation.User1Id == currentUserId ? conversation.User2 : conversation.User1;
        var lastMessage = conversation.Messages
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .FirstOrDefault();

        return new ChatConversationResponse
        {
            ConversationId = conversation.Id,
            OtherUserId = otherUser.Id,
            OtherUserFirstName = otherUser.FirstName,
            OtherUserLastName = otherUser.LastName,
            OtherUserEmail = otherUser.Email,
            OtherUserRole = otherUser.Role,
            LastMessageContent = lastMessage?.Content,
            LastMessageAt = lastMessage?.SentAt,
            UnreadCount = conversation.Messages.Count(message => message.ReceiverId == currentUserId && !message.IsRead)
        };
    }

    private static ChatContactResponse BuildOtherUser(Conversation conversation, int currentUserId)
    {
        var otherUser = conversation.User1Id == currentUserId ? conversation.User2 : conversation.User1;

        return new ChatContactResponse
        {
            Id = otherUser.Id,
            FirstName = otherUser.FirstName,
            LastName = otherUser.LastName,
            Email = otherUser.Email,
            Role = otherUser.Role,
            City = otherUser.City
        };
    }

    private static ChatMessageResponse BuildMessageResponse(Message message)
    {
        return new ChatMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            IsRead = message.IsRead
        };
    }

    private static ChatMessageResponse BuildMessageResponse(Message message, User sender, User receiver)
    {
        return new ChatMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            SenderName = $"{sender.FirstName} {sender.LastName}",
            ReceiverName = $"{receiver.FirstName} {receiver.LastName}",
            Content = message.Content,
            SentAt = message.SentAt,
            IsRead = message.IsRead
        };
    }
}

