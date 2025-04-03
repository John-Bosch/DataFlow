namespace DataFlow.Core.Blocks
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class QueryJoinBlock<TLeft, TRight, TKey> : ISourceBlock<Tuple<TLeft?, TRight?>>, IReceivableSourceBlock<Tuple<TLeft?, TRight?>>
        where TKey : notnull
    {
        public ITargetBlock<TLeft> Left => batchBlock.Target1;

        public ITargetBlock<TRight> Right => batchBlock.Target2;

        public Task Completion => joinBlock.Completion;

        private BatchedJoinBlock<TLeft, TRight> batchBlock;
        private TransformManyBlock<Tuple<IList<TLeft>, IList<TRight>>, Tuple<TLeft?, TRight?>> joinBlock;

        private Func<TLeft, TKey?> leftSelector;

        private Func<TRight, TKey?> rightSelector;

        public QueryJoinBlock(Func<TLeft, TKey?> leftSelector, Func<TRight, TKey?> rightSelector, JoinType joinType)
        {
            this.leftSelector = leftSelector;
            this.rightSelector = rightSelector;

            batchBlock = new(int.MaxValue);

            joinBlock = joinType switch
            {
                JoinType.Inner => new(InnerJoin),
                JoinType.Left => new(LeftJoin),
                JoinType.Right => new(RightJoin),
                JoinType.Full => new(FullJoin),
                _ => throw new ArgumentException("Invalid join type")
            };

            batchBlock.LinkTo(joinBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
        }

        public QueryJoinBlock(Func<TLeft, TKey?> leftSelector, Func<TRight, TKey?> rightSelector, JoinType joinType, ExecutionDataflowBlockOptions dataflowBlockOptions)
        {
            this.leftSelector = leftSelector;
            this.rightSelector = rightSelector;

            var groupingOptions = new GroupingDataflowBlockOptions
            {
                BoundedCapacity = dataflowBlockOptions.BoundedCapacity,
                CancellationToken = dataflowBlockOptions.CancellationToken,
                TaskScheduler = dataflowBlockOptions.TaskScheduler,
                MaxMessagesPerTask = dataflowBlockOptions.MaxMessagesPerTask,
                NameFormat = dataflowBlockOptions.NameFormat,
                EnsureOrdered = dataflowBlockOptions.EnsureOrdered
            };

            batchBlock = new(int.MaxValue, groupingOptions);

            joinBlock = joinType switch
            {
                JoinType.Inner => new(InnerJoin, dataflowBlockOptions),
                JoinType.Left => new(LeftJoin, dataflowBlockOptions),
                JoinType.Right => new(RightJoin, dataflowBlockOptions),
                JoinType.Full => new(FullJoin, dataflowBlockOptions),
                _ => throw new ArgumentException("Invalid join type")
            };

            batchBlock.LinkTo(joinBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
        }

        public void Complete()
        {
            batchBlock.Complete();
        }

        public Tuple<TLeft?, TRight?>? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Tuple<TLeft?, TRight?>> target, out bool messageConsumed)
        {
            return ((ISourceBlock<Tuple<TLeft?, TRight?>>)joinBlock).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            ((ISourceBlock<Tuple<TLeft?, TRight?>>)joinBlock).Fault(exception);
        }

        public IDisposable LinkTo(ITargetBlock<Tuple<TLeft?, TRight?>> target, DataflowLinkOptions linkOptions)
        {
            return joinBlock.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Tuple<TLeft?, TRight?>> target)
        {
            ((ISourceBlock<Tuple<TLeft?, TRight?>>)joinBlock).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Tuple<TLeft?, TRight?>> target)
        {
            return ((ISourceBlock<Tuple<TLeft?, TRight?>>)joinBlock).ReserveMessage(messageHeader, target);
        }

        public bool TryReceive(Predicate<Tuple<TLeft?, TRight?>>? filter, [MaybeNullWhen(false)] out Tuple<TLeft?, TRight?> item)
        {
            return joinBlock.TryReceive(filter, out item);
        }

        public bool TryReceiveAll([NotNullWhen(true)] out IList<Tuple<TLeft?, TRight?>>? items)
        {
            return joinBlock.TryReceiveAll(out items);
        }

        private IEnumerable<Tuple<TLeft?, TRight?>> InnerJoin(Tuple<IList<TLeft>, IList<TRight>> input)
        {
            return input.Item1.Join(input.Item2, leftSelector, rightSelector, (l, r) => new Tuple<TLeft?, TRight?>(l, r));
        }

        private IEnumerable<Tuple<TLeft?, TRight?>> LeftJoin(Tuple<IList<TLeft>, IList<TRight>> input)
        {
            return input.Item1
                    .GroupJoin(input.Item2, leftSelector, rightSelector, (l, r) => new Tuple<TLeft, IEnumerable<TRight?>>(l, r))
                    .SelectMany(t => t.Item2.DefaultIfEmpty(), (t, r) => new Tuple<TLeft?, TRight?>(t.Item1, r));
        }

        private IEnumerable<Tuple<TLeft?, TRight?>> RightJoin(Tuple<IList<TLeft>, IList<TRight>> input)
        {
            return input.Item2
                    .GroupJoin(input.Item1, rightSelector, leftSelector, (r, l) => new Tuple<IEnumerable<TLeft?>, TRight>(l, r))
                    .SelectMany(t => t.Item1.DefaultIfEmpty(), (t, l) => new Tuple<TLeft?, TRight?>(l, t.Item2));
        }

        private IEnumerable<Tuple<TLeft?, TRight?>> FullJoin(Tuple<IList<TLeft>, IList<TRight>> input)
        {
            var left = input.Item1.Where(v => !Equals(leftSelector(v), default(TKey))).ToDictionary(v => leftSelector(v)!, v => v);
            var right = input.Item2.Where(v => !Equals(rightSelector(v), default(TKey))).ToDictionary(v => rightSelector(v)!, v => v);
            var keys = left.Keys.Union(right.Keys);

            return keys.Select(k => new Tuple<TLeft?, TRight?>(left.TryGetValue(k, out var l) ? l : default, right.TryGetValue(k, out var r) ? r : default));
        }
    }
}