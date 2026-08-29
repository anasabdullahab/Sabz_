using SABZ.Application.DTOs.Community;
using SABZ.Application.DTOs.MarketplaceInbox;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Marketplace;

/// <summary>
/// Private farmer-to-farmer inbox for marketplace listings (Prompt 15).
///
/// Security model (critical): every conversation endpoint verifies that the
/// JWT user id is the BuyerUserId OR the SellerUserId - participants only.
/// There is no public message feed, and no request body ever accepts a
/// sender/buyer/seller id. A conversation is unique per
/// (ListingId, BuyerUserId, SellerUserId) trio, so "Message Seller" reuses
/// the existing thread instead of duplicating it.
///
/// Deleting a listing never deletes message history: conversations remain
/// readable for both participants with the listing title preserved.
/// This feature produces no notifications and no financial records.
/// </summary>
public class MarketplaceInboxService : IMarketplaceInboxService
{
    private const int MaxPageSize = 50;
    private const int MaxMessageLength = 2000;

    private const string BuyerRole = "Buyer";
    private const string SellerRole = "Seller";

    private readonly IMarketplaceListingRepository _listingRepository;
    private readonly IMarketplaceConversationRepository _conversationRepository;
    private readonly IMarketplaceMessageRepository _messageRepository;
    private readonly ISystemClock _clock;

    public MarketplaceInboxService(
        IMarketplaceListingRepository listingRepository,
        IMarketplaceConversationRepository conversationRepository,
        IMarketplaceMessageRepository messageRepository,
        ISystemClock clock)
    {
        _listingRepository = listingRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    //  Inbox
    // ------------------------------------------------------------------

    public async Task<MarketplaceInboxPagedResultDto> GetInboxAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        ValidatePagination(page, pageSize);

        var (items, totalCount) = await _conversationRepository.GetInboxPageAsync(userId, page, pageSize, ct);
        return new MarketplaceInboxPagedResultDto
        {
            Items = items.Select(c => MapSummary(c, userId)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<MarketplaceConversationDto> GetConversationAsync(
        Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        ValidatePagination(page, pageSize);

        var detail = await _conversationRepository.GetDetailAsync(conversationId, ct)
            ?? throw new NotFoundException("Conversation not found.");

        EnsureParticipant(detail, userId);

        return await BuildConversationAsync(detail, userId, page, pageSize, ct);
    }

    // ------------------------------------------------------------------
    //  Contact + messaging
    // ------------------------------------------------------------------

    public async Task<MarketplaceConversationDto> ContactSellerAsync(
        Guid userId, Guid listingId, StartMarketplaceConversationDto dto, CancellationToken ct = default)
    {
        var content = ValidateMessage(dto.Message);

        var listing = await _listingRepository.FindTrackedByIdAsync(listingId, ct)
            ?? throw new NotFoundException("Marketplace listing not found.");

        if (listing.UserId == userId)
            throw new ValidationException("You cannot contact the seller about your own listing.");

        var now = _clock.UtcNow;
        var sellerId = listing.UserId;

        // Unique (ListingId, BuyerUserId, SellerUserId) identity: reuse the
        // existing conversation, never create a duplicate.
        var conversation = await _conversationRepository.FindByParticipantsAsync(listingId, userId, sellerId, ct);
        var isNewConversation = conversation is null;
        if (conversation is null)
        {
            conversation = new MarketplaceConversation
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                BuyerUserId = userId,
                SellerUserId = sellerId,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _conversationRepository.AddAsync(conversation, ct);
        }

        var message = new MarketplaceMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = userId,
            Content = content,
            CreatedAt = now
        };
        await _messageRepository.AddAsync(message, ct);

        // A brand-new conversation is already tracked as Added; calling
        // Update() would flip it to Modified and EF would emit an UPDATE
        // for a row that does not exist yet (DbUpdateConcurrencyException).
        conversation.UpdatedAt = now;
        if (!isNewConversation)
            _conversationRepository.Update(conversation);
        await _conversationRepository.SaveChangesAsync(ct);

        var detail = await _conversationRepository.GetDetailAsync(conversation.Id, ct)
            ?? throw new NotFoundException("Conversation not found.");

        return await BuildConversationAsync(detail, userId, page: 1, pageSize: MaxPageSize, ct);
    }

    public async Task<MarketplaceMessageDto> SendMessageAsync(
        Guid userId, Guid conversationId, SendMarketplaceMessageDto dto, CancellationToken ct = default)
    {
        var content = ValidateMessage(dto.Message);

        var conversation = await _conversationRepository.FindTrackedByIdAsync(conversationId, ct)
            ?? throw new NotFoundException("Conversation not found.");

        if (conversation.BuyerUserId != userId && conversation.SellerUserId != userId)
            throw new ForbiddenException("You do not have access to this conversation.");

        var now = _clock.UtcNow;
        var message = new MarketplaceMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = userId,
            Content = content,
            CreatedAt = now
        };
        await _messageRepository.AddAsync(message, ct);

        conversation.UpdatedAt = now;
        _conversationRepository.Update(conversation);
        await _conversationRepository.SaveChangesAsync(ct);

        return new MarketplaceMessageDto
        {
            MessageId = message.Id,
            SenderName = await GetDisplayNameAsync(userId, ct),
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            IsOwnMessage = true
        };
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private async Task<MarketplaceConversationDto> BuildConversationAsync(
        MarketplaceConversationDetailReadModel detail, Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var (messages, totalCount) = await _messageRepository.GetPageAsync(detail.ConversationId, userId, page, pageSize, ct);

        return new MarketplaceConversationDto
        {
            ConversationId = detail.ConversationId,
            ListingId = detail.ListingId,
            ListingTitle = detail.ListingTitle,
            ListingType = detail.ListingType,
            ListingPrice = detail.ListingPrice,
            ListingPriceUnit = detail.ListingPriceUnit,
            BuyerName = detail.BuyerName,
            SellerName = detail.SellerName,
            CurrentUserRole = detail.BuyerUserId == userId ? BuyerRole : SellerRole,
            Messages = new PagedResult<MarketplaceMessageDto>
            {
                Items = messages.Select(m => new MarketplaceMessageDto
                {
                    MessageId = m.MessageId,
                    SenderName = m.SenderName,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    IsOwnMessage = m.IsOwnMessage
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };
    }

    /// <summary>Participants only - the core marketplace inbox security rule.</summary>
    private static void EnsureParticipant(MarketplaceConversationDetailReadModel detail, Guid userId)
    {
        if (detail.BuyerUserId != userId && detail.SellerUserId != userId)
            throw new ForbiddenException("You do not have access to this conversation.");
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException("page must be 1 or greater.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");
    }

    private static string ValidateMessage(string? message)
    {
        var trimmed = message?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException("Message is required.");
        if (trimmed.Length > MaxMessageLength)
            throw new ValidationException($"Message must be at most {MaxMessageLength} characters.");
        return trimmed;
    }

    /// <summary>Display name only - never email, phone or password hash.</summary>
    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var name = await _messageRepository.GetSenderNameAsync(userId, ct);
        return name ?? "Farmer";
    }

    private static MarketplaceConversationSummaryDto MapSummary(
        MarketplaceConversationSummaryReadModel conversation, Guid currentUserId)
    {
        var isBuyer = conversation.BuyerUserId == currentUserId;
        return new MarketplaceConversationSummaryDto
        {
            ConversationId = conversation.ConversationId,
            ListingId = conversation.ListingId,
            ListingTitle = conversation.ListingTitle,
            OtherParticipantName = isBuyer ? conversation.SellerName : conversation.BuyerName,
            LatestMessagePreview = conversation.LatestMessagePreview,
            LatestMessageAt = conversation.LatestMessageAt,
            Role = isBuyer ? BuyerRole : SellerRole
        };
    }
}
