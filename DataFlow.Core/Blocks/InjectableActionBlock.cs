namespace DataFlow.Core.Blocks
{
    using System.Threading.Tasks.Dataflow;

    public abstract class InjectableActionBlock<TInput> : ITargetBlock<TInput>
    {
        private readonly Lazy<ActionBlock<TInput>> actionBlock;

        private ITargetBlock<TInput> TargetBlock => actionBlock.Value;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput>? source, bool consumeToAccept)
        {
            return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void Complete()
        {
            TargetBlock.Complete();
        }

        protected InjectableActionBlock()
        {
            actionBlock = new Lazy<ActionBlock<TInput>>(() => new ActionBlock<TInput>(Action));
        }

        protected InjectableActionBlock(ExecutionDataflowBlockOptions blockOptions)
        {
            actionBlock = blockOptions != null
                                ? new Lazy<ActionBlock<TInput>>(() => new ActionBlock<TInput>(Action, blockOptions))
                                : new Lazy<ActionBlock<TInput>>(() => new ActionBlock<TInput>(Action));
        }

        protected abstract void Action(TInput message);

        public Task Completion => TargetBlock.Completion;

        public void Fault(Exception exception)
        {
            TargetBlock.Fault(exception);
        }

        public int InputCount => actionBlock.Value.InputCount;
    }
}
