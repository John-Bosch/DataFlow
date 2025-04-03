namespace DataFlow.Sql.Blocks
{
    using DataFlow.Sql.Models;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class SqlEntityReaderBlock<TEntity> : IPropagatorBlock<SqlEntityReaderStart<TEntity>, TEntity>
        where TEntity : class
    {
        private readonly DbContext dbContext;

        public SqlEntityReaderBlock(DbContext dbContext)
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

        protected IEnumerable<TEntity> TransformAsync(SqlEntityReaderStart<TEntity> message)
        {
            dbContext.Database.SqlQuery<TEntity>(message.SqlQuery);
        }
    }
}