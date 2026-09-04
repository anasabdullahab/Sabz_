using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Read-only access to the CropCatalog reference table. Consumers resolve
/// canonical crop names/ids (e.g. crop-price filtering); nothing here is
/// ever written.
/// </summary>
public interface ICropCatalogRepository
{
    /// <summary>All catalog crops ordered by id, read-only (AsNoTracking).</summary>
    Task<List<CropCatalog>> GetCatalogAsync(CancellationToken ct = default);
}
