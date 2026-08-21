using SABZ.Application.DTOs.Financial;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Farm profit & loss financial ledger (Prompt 9). All operations are
/// JWT-user-scoped through farm ownership; the client never supplies a
/// UserId/OwnerId. Summaries are computed dynamically from raw transactions -
/// no derived financial state is persisted.
/// </summary>
public interface IFinancialService
{
    Task<FinancialTransactionResponseDto> CreateTransactionAsync(
        Guid userId, Guid farmId, CreateFinancialTransactionDto dto, CancellationToken ct = default);

    Task<List<FinancialTransactionResponseDto>> GetTransactionsAsync(
        Guid userId, Guid farmId, string? type, string? category, Guid? cropId,
        DateTime? fromDate, DateTime? toDate, int? take, CancellationToken ct = default);

    Task<FinancialTransactionResponseDto> GetTransactionByIdAsync(
        Guid userId, Guid transactionId, CancellationToken ct = default);

    Task<FinancialTransactionResponseDto> UpdateTransactionAsync(
        Guid userId, Guid transactionId, UpdateFinancialTransactionDto dto, CancellationToken ct = default);

    Task DeleteTransactionAsync(Guid userId, Guid transactionId, CancellationToken ct = default);

    Task<FinancialSummaryResponseDto> GetSummaryAsync(
        Guid userId, Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
}
