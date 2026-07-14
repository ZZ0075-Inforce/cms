using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;

namespace CMS.API.Tests.Repositories;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class LookupRepositoryIntegrationTests(DatabaseFixture fixture)
{
    private readonly LookupRepository _repository = new(fixture.ConnectionFactory);

    [IntegrationFact]
    public async Task GetAppUsersAsync_ReturnsUsers_OrderedByUserName()
    {
        var users = await _repository.GetAppUsersAsync();

        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.False(string.IsNullOrWhiteSpace(u.UserId)));

        var names = users.Select(u => u.UserName).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    }

    [IntegrationFact]
    public async Task GetAppRolesAsync_ReturnsRoles_OrderedByRoleId()
    {
        var roles = await _repository.GetAppRolesAsync();

        Assert.Contains(roles, r => r.RoleId == "Admin");

        var roleIds = roles.Select(r => r.RoleId).ToList();
        Assert.Equal(roleIds.OrderBy(r => r, StringComparer.OrdinalIgnoreCase), roleIds);
    }
}
