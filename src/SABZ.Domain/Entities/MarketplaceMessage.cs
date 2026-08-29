namespace SABZ.Domain.Entities;

/// <summary>
/// One private message inside a marketplace conversation (Prompt 15).
/// SenderUserId always comes from the JWT identity - no request body ever
/// supplies a sender, buyer or seller id. Messages are only readable by the
/// two conversation participants and survive listing soft-deletion.
/// </summary>
public class MarketplaceMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderUserId { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public MarketplaceConversation Conversation { get; set; } = null!;
    public User SenderUser { get; set; } = null!;
}
