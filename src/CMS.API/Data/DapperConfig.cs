using CMS.API.Data.TypeHandlers;
using Dapper;

namespace CMS.API.Data;

public static class DapperConfig
{
    private static bool _registered;

    /// <summary>
    /// Must run before the first query. Idempotent so tests can call it freely.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        // MatchNamesWithUnderscores stays at Dapper's default (false). The coding convention
        // mandates explicit aliasing instead (`c.Partner_pkid AS PartnerPkid`), which is
        // self-documenting; do not flip this without auditing every SELECT in the project.

        // AppRole uses neither of these, but the next table with a date column (Course.ScheduleOn)
        // would fail at runtime without them.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
    }
}
