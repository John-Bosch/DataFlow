namespace DataFlow.Core.Blocks
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public abstract class InjectableTransformManyBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>, ITargetBlock<TInput>, ISourceBlock<TOutput>
    {
        private readonly Lazy<TransformManyBlock<TInput, TOutput>> transformBlock;

        private ITargetBlock<TInput> TargetBlock => transformBlock.Value;

        private ISourceBlock<TOutput> SourceBlock => transformBlock.Value;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput>? source, bool consumeToAccept)
        {
            return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void Complete()
        {
            TargetBlock.Complete();
        }

        protected InjectableTransformManyBlock()
        {
            transformBlock = new Lazy<TransformManyBlock<TInput, TOutput>>(() => new TransformManyBlock<TInput, TOutput>(Transform));
        }

        protected InjectableTransformManyBlock(ExecutionDataflowBlockOptions blockOptions)
        {
            transformBlock = blockOptions != null
                        ? new Lazy<TransformManyBlock<TInput, TOutput>>(() => new TransformManyBlock<TInput, TOutput>(Transform, blockOptions))
                        : new Lazy<TransformManyBlock<TInput, TOutput>>(() => new TransformManyBlock<TInput, TOutput>(Transform));
        }

        protected abstract IEnumerable<TOutput> Transform(TInput message);

        public Task Completion => TargetBlock.Completion;

        public void Fault(Exception exception)
        {
            TargetBlock.Fault(exception);
        }

        public virtual TOutput? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed)
        {
            return SourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public virtual IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions)
        {
            return SourceBlock.LinkTo(target, linkOptions);
        }

        public virtual void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            SourceBlock.ReleaseReservation(messageHeader, target);
        }

        public virtual bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            return SourceBlock.ReserveMessage(messageHeader, target);
        }

        public int InputCount => transformBlock.Value.InputCount;

        public int OutputCount => transformBlock.Value.OutputCount;

        public bool TryReceive(Predicate<TOutput> filter, out TOutput? item)
        {
            return transformBlock.Value.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<TOutput>? items)
        {
            return transformBlock.Value.TryReceiveAll(out items);
        }
    }
}
