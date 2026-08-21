using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface IDiseaseInformationRepository
{
    /// <summary>
    /// Active curated disease guidance applicable to the given crop catalog id
    /// (includes general entries with no crop association).
    /// </summary>
    Task<List<DiseaseInformation>> GetActiveForCropAsync(int? cropCatalogId, CancellationToken ct = default);
}
