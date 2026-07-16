using System.Security.Claims;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// The reflection rules the audit writer applies to an arbitrary entity, in isolation (no DB): which
/// property becomes ActionDesc, how the Update change-list is computed, where PrimaryKeyValues comes
/// from, the "system" UserName fallback, and the 1000-char ActionDesc cap. The DB write itself is
/// covered by the retrofitted repositories' integration tests.
/// </summary>
public class RowAuditWriterTests
{
    /// <summary>
    /// A stand-in business entity: pkid is NOT first, and there are two int properties before the first
    /// string, so "first string in declaration order" (Name) is a real choice, not just "property #2".
    /// </summary>
    private sealed class Sample
    {
        public int Order { get; set; }
        public int Pkid { get; set; }
        public string Name { get; set; } = string.Empty;   // first string property → ActionDesc
        public string? Code { get; set; }
        public bool IsActive { get; set; }
        public List<int> Tags { get; set; } = [];
    }

    private static RowAuditWriter WriterWithUser(string? userName)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();

        if (userName is not null)
        {
            var identity = new ClaimsIdentity([new Claim(JwtTokenGenerator.UserNameClaim, userName)], "test");
            accessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
        }
        // userName == null → leave HttpContext null (the unauthenticated case).

        return new RowAuditWriter(accessor);
    }

    private static RowAuditWriter Writer() => WriterWithUser("Miles");

    // ---------- ActionDesc: Insert / Delete use the first string property ----------

    [Fact]
    public void BuildInsertEntry_UsesFirstStringProperty_AsActionDesc()
    {
        var entry = Writer().BuildInsertEntry("Sample", new Sample { Pkid = 7, Name = "微軟", Code = "MS" });

        Assert.Equal("Sample", entry.TableName);
        Assert.Equal("Insert", entry.ActionType);
        Assert.Equal("微軟", entry.ActionDesc);        // Name, not Code — first string in declaration order
        Assert.Equal("7", entry.PrimaryKeyValues);
    }

    [Fact]
    public void BuildDeleteEntry_UsesFirstStringProperty_AsActionDesc()
    {
        var entry = Writer().BuildDeleteEntry("Sample", new Sample { Pkid = 42, Name = "Cisco" });

        Assert.Equal("Delete", entry.ActionType);
        Assert.Equal("Cisco", entry.ActionDesc);
        Assert.Equal("42", entry.PrimaryKeyValues);
    }

    // ---------- ActionDesc: Update lists exactly the changed property names ----------

    [Fact]
    public void BuildUpdateEntry_ListsOnlyTheChangedProperties_InDeclarationOrder()
    {
        var before = new Sample { Pkid = 1, Order = 10, Name = "Old", Code = "A", IsActive = false };
        var after = new Sample { Pkid = 1, Order = 20, Name = "New", Code = "A", IsActive = true };

        var entry = Writer().BuildUpdateEntry("Sample", before, after);

        Assert.Equal("Update", entry.ActionType);
        // Order and Name and IsActive changed; Code and Pkid did not. Declaration order: Order, Name, IsActive.
        Assert.Equal("Order,Name,IsActive", entry.ActionDesc);
        Assert.Equal("1", entry.PrimaryKeyValues);
    }

    [Fact]
    public void BuildUpdateEntry_ComparesCollectionsByValue_NotReference()
    {
        // Two distinct list instances with equal contents must NOT count as a change...
        var before = new Sample { Name = "x", Tags = [1, 2, 3] };
        var afterSame = new Sample { Name = "x", Tags = [1, 2, 3] };
        Assert.Equal(string.Empty, Writer().BuildUpdateEntry("Sample", before, afterSame).ActionDesc);

        // ...but different contents must.
        var afterChanged = new Sample { Name = "x", Tags = [1, 2, 4] };
        Assert.Equal("Tags", Writer().BuildUpdateEntry("Sample", before, afterChanged).ActionDesc);
    }

    [Fact]
    public void BuildUpdateEntry_ActionDescIsEmpty_WhenNothingChanged()
    {
        var before = new Sample { Pkid = 5, Name = "Same", Code = "C", IsActive = true };
        var after = new Sample { Pkid = 5, Name = "Same", Code = "C", IsActive = true };

        Assert.Equal(string.Empty, Writer().BuildUpdateEntry("Sample", before, after).ActionDesc);
    }

    // ---------- PrimaryKeyValues reads pkid regardless of its position ----------

    [Fact]
    public void PkidValue_ReadsThePkidProperty()
        => Assert.Equal("99", RowAuditWriter.PkidValue(new Sample { Pkid = 99, Name = "n" }));

    // ---------- UserName: JWT claim, else "system" ----------

    [Fact]
    public void UserName_IsTheUserNameClaim_WhenAuthenticated()
    {
        var entry = WriterWithUser("Miles").BuildInsertEntry("Sample", new Sample { Name = "n" });
        Assert.Equal("Miles", entry.UserName);
    }

    [Fact]
    public void UserName_FallsBackToSystem_WhenNoAuthenticatedUser()
    {
        var entry = WriterWithUser(null).BuildInsertEntry("Sample", new Sample { Name = "n" });
        Assert.Equal(RowAuditWriter.SystemUser, entry.UserName);
        Assert.Equal("system", entry.UserName);
    }

    // ---------- ActionDesc truncates at 1000 characters ----------

    [Fact]
    public void ActionDesc_TruncatesAt1000Characters()
    {
        var longName = new string('x', 1500);
        var entry = Writer().BuildInsertEntry("Sample", new Sample { Name = longName });

        Assert.Equal(RowAuditWriter.MaxActionDescLength, entry.ActionDesc!.Length);
        Assert.Equal(1000, entry.ActionDesc.Length);
    }
}
