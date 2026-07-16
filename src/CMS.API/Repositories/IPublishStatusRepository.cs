using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPublishStatusRepository
{
    Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken ct = default);

    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken ct = default);

    /// <summary>True when a row with this pkid already exists — the pre-check for the create 409.</summary>
    Task<bool> ExistsAsync(byte pkid, CancellationToken ct = default);

    /// <summary>
    /// Inserts the status. The pkid is client-supplied (tinyint, not IDENTITY), so it is returned
    /// unchanged. Throws SqlException 2627 when the pkid already exists — the caller maps that to 409.
    /// </summary>
    Task<byte> InsertAsync(PublishStatusRequest request, CancellationToken ct = default);

    /// <summary>Updates every writable column. pkid is the immutable key. Returns false when it is missing.</summary>
    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the status. Returns false when the pkid does not exist.
    /// Throws SqlException 547 when Course rows still reference it (FK_Course_PublishStatus does not
    /// cascade), so the caller must translate that into 409.
    /// </summary>
    Task<bool> DeleteAsync(byte pkid, CancellationToken ct = default);
}
