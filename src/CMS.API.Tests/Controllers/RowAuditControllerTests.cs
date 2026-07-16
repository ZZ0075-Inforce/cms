using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests for GET /api/rowaudit. The repository is mocked, so these never touch SQL —
/// they pin that the controller forwards tableName + pkid to the repository, returns the rows it gets,
/// and rejects a missing tableName with 400.
/// </summary>
public class RowAuditControllerTests
{
    private readonly IRowAuditRepository _repository = Substitute.For<IRowAuditRepository>();
    private readonly RowAuditController _controller;

    public RowAuditControllerTests() => _controller = new RowAuditController(_repository);

    private static RowAuditEntry Entry(string action, string user, DateTime when) => new()
    {
        DateTime = when,
        UserName = user,
        ActionType = action,
        ActionDesc = "Title"
    };

    [Fact]
    public async Task GetForRecord_ForwardsTableNameAndPkid_AndReturnsRows()
    {
        var rows = new[]
        {
            Entry("Update", "alice", new DateTime(2026, 6, 4, 14, 30, 0)),
            Entry("Insert", "bob", new DateTime(2026, 6, 1, 9, 0, 0))
        };
        _repository.GetForRecordAsync("Course", "123", Arg.Any<CancellationToken>()).Returns(rows);

        var result = await _controller.GetForRecord("Course", 123, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<RowAuditEntry>>(ok.Value);
        Assert.Equal(2, body.Count);
        Assert.Equal("Update", body[0].ActionType);           // repository order preserved
        await _repository.Received(1).GetForRecordAsync("Course", "123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForRecord_ReturnsEmptyList_WhenRecordHasNoHistory()
    {
        _repository.GetForRecordAsync("Partner", "9", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<RowAuditEntry>)Array.Empty<RowAuditEntry>());

        var result = await _controller.GetForRecord("Partner", 9, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<RowAuditEntry>>(ok.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetForRecord_Returns400_WhenTableNameMissing(string? tableName)
    {
        var result = await _controller.GetForRecord(tableName, 1, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        await _repository.DidNotReceive().GetForRecordAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
