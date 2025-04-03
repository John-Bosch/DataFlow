namespace DataFlow.Core.Blocks
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class DecisionBlock<TIndex, TMessage> : ITargetBlock<TMessage>
        where TIndex : notnull
    {
        private readonly ITargetBlock<TMessage> input;
        private readonly ConcurrentDictionary<TIndex, (Func<TMessage, bool> Predicate, BufferBlock<TMessage> Block)> outputs;

        public IReadOnlyDictionary<TIndex, ISourceBlock<TMessage>> Outputs => outputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Block as ISourceBlock<TMessage>);

        public DecisionBlock(IReadOnlyDictionary<TIndex, Func<TMessage, bool>> predicates)
        {
            input = new ActionBlock<TMessage>(MakeDecision, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });

            outputs = new ConcurrentDictionary<TIndex, (Func<TMessage, bool> Predicate, BufferBlock<TMessage> Block)>(predicates.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, new BufferBlock<TMessage>())));

            WireCompletion();
        }

        public DecisionBlock(IReadOnlyDictionary<TIndex, Func<TMessage, bool>> predicates, ExecutionDataflowBlockOptions dataflowBlockOptions)
        {
            input = new ActionBlock<TMessage>(MakeDecision, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = dataflowBlockOptions.BoundedCapacity,
                CancellationToken = dataflowBlockOptions.CancellationToken,
                TaskScheduler = dataflowBlockOptions.TaskScheduler,
                MaxMessagesPerTask = dataflowBlockOptions.MaxMessagesPerTask,
                NameFormat = dataflowBlockOptions.NameFormat,
                EnsureOrdered = dataflowBlockOptions.EnsureOrdered,
                MaxDegreeOfParallelism = 1
            });

            outputs = new ConcurrentDictionary<TIndex, (Func<TMessage, bool> Predicate, BufferBlock<TMessage> Block)>(predicates.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, new BufferBlock<TMessage>(new DataflowBlockOptions
            {
                BoundedCapacity = 1,
                CancellationToken = dataflowBlockOptions.CancellationToken,
                TaskScheduler = dataflowBlockOptions.TaskScheduler,
                MaxMessagesPerTask = dataflowBlockOptions.MaxMessagesPerTask,
                NameFormat = dataflowBlockOptions.NameFormat,
                EnsureOrdered = dataflowBlockOptions.EnsureOrdered
            }))));

            WireCompletion();
        }

        public virtual void OnNonMatchingMessage(TMessage message)
        {
        }

        public Task Completion => input.Completion;

        public void Complete()
        {
            input.Complete();
        }

        public void Fault(Exception exception)
        {
            input.Fault(exception);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TMessage messageValue, ISourceBlock<TMessage>? source, bool consumeToAccept)
        {
            return input.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        private void WireCompletion()
        {
            input.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    foreach (var output in outputs)
                    {
                        ((IDataflowBlock)output.Value.Block).Fault(t.Exception);
                    }
                }
                else
                {
                    foreach (var output in outputs)
                    {
                        output.Value.Block.Complete();
                    }
                }
            });
        }

        private async Task MakeDecision(TMessage message)
        {
            var match = outputs.OrderBy(kvp => kvp.Key).FirstOrDefault(o => o.Value.Predicate(message));

            if (match.Value.Block != null)
            {
                await match.Value.Block.SendAsync(message);
            }
            else
            {
                OnNonMatchingMessage(message);
            }
        }
    }
}
