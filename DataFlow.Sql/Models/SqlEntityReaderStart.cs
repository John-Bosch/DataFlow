namespace DataFlow.Sql.Models;
using System;
using System.Linq.Expressions;

public class SqlEntityReaderStart<TEntity>
    where TEntity : class
{
    public FormattableString SqlQuery { get; set; }

    public Expression<Func<TEntity, long>>? VersionStampColumnSelector { get; set; }

    public long? LastVersionStamp { get; set; }
}
