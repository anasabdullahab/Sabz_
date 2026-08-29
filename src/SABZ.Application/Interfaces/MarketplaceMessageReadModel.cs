namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL projection for one private message. Sender display name and the
/// ownership flag are computed in SQL - no entities, no N+1.
/// </summary>
public record MarketplaceMessageReadModel(
    Guid MessageId,
    string SenderName,
    string Content,
    DateTime CreatedAt,
    bool IsOwnMessage);
