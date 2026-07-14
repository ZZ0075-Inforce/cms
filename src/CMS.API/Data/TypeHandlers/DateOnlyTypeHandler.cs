using System.Data;
using Dapper;

namespace CMS.API.Data.TypeHandlers;

/// <summary>
/// Microsoft.Data.SqlClient cannot bind <see cref="DateOnly"/> parameters natively, so any
/// <c>date</c> column would throw without this. Dapper routes <c>DateOnly?</c> through the same
/// handler, so no separate nullable handler is needed.
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}
