using System.Data;
using Dapper;

namespace CMS.API.Data.TypeHandlers;

/// <summary>
/// See <see cref="DateOnlyTypeHandler"/> — same rationale for <c>time(7)</c> columns.
/// </summary>
public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }

    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);
}
