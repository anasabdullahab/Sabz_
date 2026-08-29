namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>One private message. Sender display name only - never a user id.</summary>
public class MarketplaceMessageDto
{
    public Guid MessageId { get; set; }

    /// <summary>Sender display name only - never email, phone or id.</summary>
    public string SenderName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>True when the authenticated farmer wrote this message.</summary>
    public bool IsOwnMessage { get; set; }
}
