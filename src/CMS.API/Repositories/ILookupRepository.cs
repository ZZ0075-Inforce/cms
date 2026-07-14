using CMS.API.Models.Lookups;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AppRoleLookup>> GetAppRolesAsync(CancellationToken ct = default);
}
