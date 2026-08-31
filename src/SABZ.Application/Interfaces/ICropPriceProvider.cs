using SABZ.Application.DTOs.CropPrice;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Abstraction over the crop price data source (Prompt 17). The current
/// implementation is a clearly-labelled reference dataset; a real AMIS
/// Punjab provider can replace it in DI later without touching the
/// Application or API layers.
///
/// Providers return only records they actually have - never invented,
/// placeholder or random prices - and always stamp each record with a
/// factual <c>Source</c>, <c>PriceDate</c> and <c>DataStatus</c>.
/// Failures must surface as <see cref="SABZ.Domain.Exceptions.CropPriceProviderException"/>.
/// </summary>
public interface ICropPriceProvider
{
    /// <summary>Stable identifier of this data source, e.g. "SABZ Reference Dataset".</summary>
    string SourceName { get; }

    /// <summary>Whether this provider returns genuinely current data.</summary>
    bool IsLive { get; }

    /// <summary>All dated price records currently available from the source.</summary>
    Task<IReadOnlyList<CropPriceRecordDto>> GetRecordsAsync(CancellationToken ct = default);
}
