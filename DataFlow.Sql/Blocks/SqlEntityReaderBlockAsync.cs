namespace DataFlow.Sql.Blocks
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using DataFlow.Sql.Models;
    using Microsoft.EntityFrameworkCore;

    public class SqlEntityReaderBlockASync<TEntity> : IPropagatorBlock<SqlEntityReaderStart<TEntity>, TEntity>
        where TEntity : class
    {
        private readonly DbContext dbContext;

        public SqlEntityReaderBlockASync(DbContext dbContext)
        {
            this.dbContext = dbContext;
            transformBlock = new TransformManyBlock<SqlEntityReaderStart<TEntity>, TEntity>(TransformAsync);
        }

        public Task Completion => transformBlock.Completion;

        public Action<int>? BeforePageRead { get; set; }

        public Action<int, int, bool>? AfterPageRead { get; set; }

        private TransformManyBlock<SqlEntityReaderStart<TEntity>, TEntity> transformBlock;

        private ITargetBlock<SqlEntityReaderStart<TEntity>> TargetBlock => transformBlock;

        private ISourceBlock<TEntity> SourceBlock => transformBlock;

        public void Complete()
        {
            TargetBlock.Complete();
        }

        public TEntity? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target, out bool messageConsumed)
        {
            return SourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            TargetBlock.Fault(exception);
        }

        public IDisposable LinkTo(ITargetBlock<TEntity> target, DataflowLinkOptions linkOptions)
        {
            return SourceBlock.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, SqlEntityReaderStart<TEntity> messageValue, ISourceBlock<SqlEntityReaderStart<TEntity>>? source, bool consumeToAccept)
        {
            return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target)
        {
            SourceBlock.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target)
        {
            return SourceBlock.ReserveMessage(messageHeader, target);
        }

        protected async IAsyncEnumerable<TEntity> TransformAsync(SqlEntityReaderStart<TEntity> message)
        {
            var resultSet = dbContext.Database.SqlQuery<TEntity>(message.SqlQuery);

            if (message.VersionStampColumnSelector != null && message.LastVersionStamp.HasValue)
            {
                var propertyExpression = message.VersionStampColumnSelector.Body;
                var greaterThanExpression = Expression.GreaterThan(propertyExpression, Expression.Constant(message.LastVersionStamp));

                var lambda = Expression.Lambda<Func<TEntity, bool>>(greaterThanExpression, message.VersionStampColumnSelector.Parameters);
                resultSet = resultSet.Where(lambda);
            }

            await foreach (var entity in resultSet.AsAsyncEnumerable())
            {
                yield return entity;
            }

            TargetBlock.Complete();
        }
    }
}