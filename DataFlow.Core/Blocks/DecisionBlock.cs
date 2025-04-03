namespace DataFlow.Core.Blocks
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class DecisionBlock<T> : ITargetBlock<T>
    {
        private readonly ITargetBlock<T> input;
        private readonly ConcurrentBag<(Func<T, bool> Predicate, BufferBlock<T> Block)> outputs;

        public IEnumerable<ISourceBlock<T>> Outputs => outputs.Select(o => o.Block).Cast<ISourceBlock<T>>();

        public DecisionBlock(IEnumerable<Func<T, bool>> predicates)
        {
            input = new ActionBlock<T>(MakeDecision, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });

            outputs = new ConcurrentBag<(Func<T, bool>, BufferBlock<T>)>(predicates.Select(p => (p, new BufferBlock<T>())));

            WireCompletion();
        }

        public DecisionBlock(IEnumerable<Func<T, bool>> predicates, ExecutionDataflowBlockOptions dataflowBlockOptions)
        {
            input = new ActionBlock<T>(MakeDecision, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = dataflowBlockOptions.BoundedCapacity,
                CancellationToken = dataflowBlockOptions.CancellationToken,
                TaskScheduler = dataflowBlockOptions.TaskScheduler,
                MaxMessagesPerTask = dataflowBlockOptions.MaxMessagesPerTask,
                NameFormat = dataflowBlockOptions.NameFormat,
                EnsureOrdered = dataflowBlockOptions.EnsureOrdered,
                MaxDegreeOfParallelism = 1
            });

            outputs = new ConcurrentBag<(Func<T, bool>, BufferBlock<T>)>(predicates.Select(p => (p, new BufferBlock<T>(new DataflowBlockOptions
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

        public virtual void OnNonMatchingMessage(T message)
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

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T>? source, bool consumeToAccept)
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
                        ((IDataflowBlock)output.Block).Fault(t.Exception);
                    }
                }
                else
                {
                    foreach (var output in outputs)
                    {
                        output.Block.Complete();
                    }
                }
            });
        }

        private async Task MakeDecision(T message)
        {
            var match = outputs.FirstOrDefault(o => o.Predicate(message));

            if (match.Block != null)
            {
                await match.Block.SendAsync(message);
            }
            else
            {
                OnNonMatchingMessage(message);
            }
        }
    }
}
