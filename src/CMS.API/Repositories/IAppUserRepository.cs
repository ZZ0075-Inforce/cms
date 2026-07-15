using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken ct = default);

    /// <summary>Returns the user with its assigned <see cref="AppUser.RoleIds"/>, or null if not found.</summary>
    Task<AppUser?> GetByIdAsync(string userId, CancellationToken ct = default);

    Task<bool> ExistsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the user and its AppUserRole rows in one transaction. PasswordHash is set from the
    /// SysConfig default password (SHA-256). Returns the new IDENTITY pkid.
    /// </summary>
    Task<int> InsertAsync(AppUserRequest request, CancellationToken ct = default);

    /// <summary>Updates scalars and re-syncs AppUserRole. Never touches PasswordHash. Returns false when the user does not exist.</summary>
    Task<bool> UpdateAsync(AppUserRequest request, CancellationToken ct = default);

    /// <summary>Deletes the user and its AppUserRole rows. Returns false when the user does not exist.</summary>
    Task<bool> DeleteAsync(string userId, CancellationToken ct = default);

    /// <summary>Re-hashes the SysConfig default password into PasswordHash and re-stamps PasswordUpdatedTime. Returns false when the user does not exist.</summary>
    Task<bool> ResetPasswordAsync(string userId, CancellationToken ct = default);
}
