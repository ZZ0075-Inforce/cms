using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPartnerRepository
{
    Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken ct = default);

    Task<Partner?> GetByIdAsync(short pkid, CancellationToken ct = default);

    /// <summary>Inserts the partner. Returns the new IDENTITY pkid.</summary>
    Task<short> InsertAsync(PartnerRequest request, CancellationToken ct = default);

    /// <summary>Updates every writable column. Returns false when the pkid does not exist.</summary>
    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the partner. Returns false when the pkid does not exist.
    /// Throws SqlException 547 when child rows (Course, Certification, …) still reference it —
    /// none of those FKs cascade, so the caller must translate that into 409.
    /// </summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken ct = default);
}
