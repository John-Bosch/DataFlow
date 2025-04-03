namespace DataFlow.Core.UnitTests.Blocks;

using DataFlow.Core.Blocks;
using FluentAssertions;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

public class BatchedJoinBlockTests
{
    private ITestOutputHelper outputHelper { get; }

    public BatchedJoinBlockTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Fact]
    public void BatchedJoinBlock_InnerJoin()
    {
        var joinBlock = new QueryJoinBlock<int, int, int>(o => o, i => i, JoinType.Inner);

        var leftList = new List<int> { 1, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10 };
        var rightList = new List<int> { 2, 4, 6, 8, 10, 10, 12, 14 };

        var expected = leftList.Join(rightList, o => o, i => i, (o, i) => new Tuple<int, int>(o, i)).ToList();

        foreach (var item in leftList)
        {
            joinBlock.Left.Post(item);
        }

        foreach (var item in rightList)
        {
            joinBlock.Right.Post(item);
        }

        joinBlock.Right.Complete();
        joinBlock.Left.Complete();

        var results = joinBlock.ReceiveAllAsync().ToBlockingEnumerable().ToList();

        results.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void BatchedJoinBlock_LeftJoin()
    {
        var joinBlock = new QueryJoinBlock<int, int, int>(o => o, i => i, JoinType.Left);

        var leftList = new List<int> { 1, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10 };
        var rightList = new List<int> { 2, 4, 6, 8, 10, 10, 12, 14 };

        var expected = leftList.GroupJoin(rightList, o => o, i => i, (o, i) => new Tuple<int, IEnumerable<int?>>(o, i.Cast<int?>()))
                                .SelectMany(t => t.Item2.DefaultIfEmpty(), (t, i) => new Tuple<int?, int?>(t.Item1, i))
                                .ToList();

        foreach (var item in leftList)
        {
            joinBlock.Left.Post(item);
        }

        foreach (var item in rightList)
        {
            joinBlock.Right.Post(item);
        }

        joinBlock.Right.Complete();
        joinBlock.Left.Complete();

        var results = joinBlock.ReceiveAllAsync().ToBlockingEnumerable().ToList();

        results.Should().BeEquivalentTo(expected);
    }
}
