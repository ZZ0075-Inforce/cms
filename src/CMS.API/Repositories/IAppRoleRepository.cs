using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppRoleRepository
{
    Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken ct = default);

    /// <summary>Returns the role with its assigned <see cref="AppRole.UserIds"/>, or null if not found.</summary>
    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken ct = default);

    Task<bool> ExistsAsync(string roleId, CancellationToken ct = default);

    /// <summary>Inserts the role and its AppUserRole rows in one transaction. Returns the new IDENTITY pkid.</summary>
    Task<int> InsertAsync(AppRoleRequest request, CancellationToken ct = default);

    /// <summary>Updates scalars and re-syncs AppUserRole. Returns false when the role does not exist.</summary>
    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken ct = default);

    /// <summary>Deletes the role and its AppUserRole rows. Returns false when the role does not exist.</summary>
    Task<bool> DeleteAsync(string roleId, CancellationToken ct = default);
}
