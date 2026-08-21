using SABZ.Application.DTOs.Financial;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Financial;

/// <summary>
/// Farm profit & loss financial ledger (Prompt 9).
///
/// Design decisions:
/// - Ownership is derived JWT user -> farm -> transaction; clients never
///   submit a UserId/OwnerId. An optional crop must belong to that farm.
/// - Money is decimal only (never float/double), positive and capped.
/// - The farmer's TransactionDate (when the event occurred) is kept separate
///   from CreatedAt (when SABZ stored the record); future dates are rejected.
/// - P&L summaries are always computed dynamically from raw transactions;
///   no totals are persisted. The system never invents financial data.
/// </summary>
public class FinancialService : IFinancialService
{
    public const decimal MaxAmount = 1_000_000_000m;

    private const int DefaultTake = 50;
    private const int MaxTake = 100;
    private const int MaxNotesLength = 1000;

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly ISystemClock _clock;

    public FinancialService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IFinancialTransactionRepository transactionRepository,
        ISystemClock clock)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _transactionRepository = transactionRepository;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    //  Create
    // ------------------------------------------------------------------

    public async Task<FinancialTransactionResponseDto> CreateTransactionAsync(
        Guid userId, Guid farmId, CreateFinancialTransactionDto dto, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);

        var type = ParseType(dto.TransactionType);
        ValidateCategory(dto.Category, type);
        ValidateAmount(dto.Amount);
        var transactionDate = ValidateTransactionDate(dto.TransactionDate);
        ValidateNotes(dto.Notes);
        var crop = await ValidateCropAsync(farmId, dto.CropId, ct);

        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            CropId = crop?.Id,
            TransactionType = type,
            Category = dto.Category.Trim(),
            Amount = dto.Amount,
            TransactionDate = transactionDate,
            Notes = dto.Notes,
            CreatedAt = _clock.UtcNow
        };

        await _transactionRepository.AddAsync(transaction, ct);
        await _transactionRepository.SaveChangesAsync(ct);

        transaction.Farm = farm;
        transaction.Crop = crop;
        return MapToDto(transaction);
    }

    // ------------------------------------------------------------------
    //  Read
    // ------------------------------------------------------------------

    public async Task<List<FinancialTransactionResponseDto>> GetTransactionsAsync(
        Guid userId, Guid farmId, string? type, string? category, Guid? cropId,
        DateTime? fromDate, DateTime? toDate, int? take, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);

        FinancialTransactionType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type))
            typeFilter = ParseType(type);

        if (!string.IsNullOrWhiteSpace(category) && !TransactionCategories.All.Contains(category.Trim()))
            throw new ValidationException("Category filter is not a known category.");

        if (take is <= 0)
            throw new ValidationException("take must be a positive number.");
        var limit = Math.Min(take ?? DefaultTake, MaxTake);

        var (from, to) = ValidateDateRange(fromDate, toDate);
        Crop? crop = null;
        if (cropId is not null)
            crop = await ValidateCropAsync(farm.Id, cropId, ct);

        var transactions = await _transactionRepository.GetByFarmIdAsync(
            farm.Id, typeFilter, category?.Trim(), crop?.Id, from, to, limit, ct);

        return transactions.Select(MapToDto).ToList();
    }

    public async Task<FinancialTransactionResponseDto> GetTransactionByIdAsync(
        Guid userId, Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await GetOwnedTransactionAsync(userId, transactionId, ct);
        return MapToDto(transaction);
    }

    public async Task<FinancialSummaryResponseDto> GetSummaryAsync(
        Guid userId, Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        Crop? crop = null;
        if (cropId is not null)
            crop = await ValidateCropAsync(farm.Id, cropId, ct);

        var (totalIncome, totalExpenses, count) =
            await _transactionRepository.GetTotalsAsync(farm.Id, crop?.Id, from, to, ct);

        return new FinancialSummaryResponseDto
        {
            FarmId = farm.Id,
            CropId = crop?.Id,
            FromDate = from,
            ToDate = to,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetProfitLoss = totalIncome - totalExpenses,
            TransactionCount = count
        };
    }

    // ------------------------------------------------------------------
    //  Update / delete (full replacement with ownership re-validation)
    // ------------------------------------------------------------------

    public async Task<FinancialTransactionResponseDto> UpdateTransactionAsync(
        Guid userId, Guid transactionId, UpdateFinancialTransactionDto dto, CancellationToken ct = default)
    {
        var transaction = await GetOwnedTransactionAsync(userId, transactionId, ct);

        var type = ParseType(dto.TransactionType);
        ValidateCategory(dto.Category, type);
        ValidateAmount(dto.Amount);
        var transactionDate = ValidateTransactionDate(dto.TransactionDate);
        ValidateNotes(dto.Notes);
        var crop = await ValidateCropAsync(transaction.FarmId, dto.CropId, ct);

        transaction.TransactionType = type;
        transaction.Category = dto.Category.Trim();
        transaction.Amount = dto.Amount;
        transaction.TransactionDate = transactionDate;
        transaction.CropId = crop?.Id;
        transaction.Notes = dto.Notes;
        transaction.UpdatedAt = _clock.UtcNow;

        _transactionRepository.Update(transaction);
        await _transactionRepository.SaveChangesAsync(ct);

        transaction.Crop = crop;
        return MapToDto(transaction);
    }

    public async Task DeleteTransactionAsync(Guid userId, Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await GetOwnedTransactionAsync(userId, transactionId, ct);
        _transactionRepository.Remove(transaction);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    private async Task<FinancialTransaction> GetOwnedTransactionAsync(Guid userId, Guid transactionId, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId, ct)
            ?? throw new NotFoundException("Transaction not found.");

        if (transaction.Farm is null || transaction.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this transaction.");

        return transaction;
    }

    private async Task<Crop?> ValidateCropAsync(Guid farmId, Guid? cropId, CancellationToken ct)
    {
        if (cropId is null)
            return null;

        var crop = await _cropRepository.GetByIdAsync(cropId.Value)
            ?? throw new NotFoundException("Crop not found.");

        if (crop.FarmId != farmId)
            throw new ValidationException("Selected crop does not belong to the selected farm.");

        return crop;
    }

    // ------------------------------------------------------------------
    //  Field validation
    // ------------------------------------------------------------------

    private static FinancialTransactionType ParseType(string? transactionType)
    {
        if (!Enum.TryParse<FinancialTransactionType>(transactionType, ignoreCase: true, out var type))
            throw new ValidationException("Transaction type must be exactly one of: Income, Expense.");

        return type;
    }

    private static void ValidateCategory(string? category, FinancialTransactionType type)
    {
        var trimmed = category?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || !TransactionCategories.IsValidFor(trimmed, type))
            throw new ValidationException(
                $"Category is not valid for transaction type '{type}'. " +
                $"Income categories: {string.Join(", ", TransactionCategories.IncomeCategories)}. " +
                $"Expense categories: {string.Join(", ", TransactionCategories.ExpenseCategories)}.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ValidationException("Amount must be greater than zero.");

        if (amount > MaxAmount)
            throw new ValidationException("Amount must not exceed 1,000,000,000.");
    }

    private DateTime ValidateTransactionDate(DateTime? transactionDate)
    {
        if (transactionDate is null)
            throw new ValidationException("Transaction date is required.");

        var date = transactionDate.Value.Date;
        if (date > _clock.UtcNow.Date)
            throw new ValidationException("Transaction date cannot be in the future.");

        // Store as UTC midnight - the date the financial event occurred.
        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static void ValidateNotes(string? notes)
    {
        if (notes is not null && notes.Length > MaxNotesLength)
            throw new ValidationException($"Notes must not exceed {MaxNotesLength} characters.");
    }

    private static (DateTime? From, DateTime? To) ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        DateTime? from = fromDate?.Date;
        DateTime? to = toDate?.Date;

        if (from is not null && to is not null && from > to)
            throw new ValidationException("fromDate must be on or before toDate.");

        // Normalise to UTC midnight so range filters compare like stored dates.
        from = from is null ? null : new DateTime(from.Value.Year, from.Value.Month, from.Value.Day, 0, 0, 0, DateTimeKind.Utc);
        to = to is null ? null : new DateTime(to.Value.Year, to.Value.Month, to.Value.Day, 0, 0, 0, DateTimeKind.Utc);

        return (from, to);
    }

    private static FinancialTransactionResponseDto MapToDto(FinancialTransaction transaction) => new()
    {
        Id = transaction.Id,
        FarmId = transaction.FarmId,
        CropId = transaction.CropId,
        CropName = transaction.Crop?.CropName,
        TransactionType = transaction.TransactionType.ToString(),
        Category = transaction.Category,
        Amount = transaction.Amount,
        TransactionDate = transaction.TransactionDate,
        Notes = transaction.Notes,
        CreatedAt = transaction.CreatedAt,
        UpdatedAt = transaction.UpdatedAt
    };
}
