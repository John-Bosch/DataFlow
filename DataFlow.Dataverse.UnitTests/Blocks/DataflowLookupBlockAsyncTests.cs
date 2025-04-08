
namespace DataFlow.Dataverse.UnitTests.Blocks
{
    using System.Threading.Tasks.Dataflow;
    using DataFlow.Dataverse.Blocks;
    using DataFlow.Dataverse.DataModel;
    using DataFlow.Dataverse.UnitTests.Models;
    using FluentAssertions;
    using Microsoft.PowerPlatform.Dataverse.Client;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Moq;

    public class DataflowLookupBlockAsyncTests
    {
        [Fact]
        public async Task DataflowLookupBlockAsyncTests_Succeeds()
        {
            var organizationServiceMock = new Mock<IOrganizationServiceAsync2>();

            var fetchXml = $"""
                <fetch>
                    <entity name='aar_concessiongroup'>
                        <attribute name='aar_concessioncode' />
                        <attribute name='aar_concessiongroupid' />
                    </entity>
                </fetch>
            """;

            organizationServiceMock.Setup(o => o.RetrieveMultipleAsync(It.IsAny<FetchExpression>()))
                .ReturnsAsync(new EntityCollection(
                [
                    new AAr_ConcessionGroup
                    {
                        AAr_ConcessionCode = "ASAC",
                        AAr_ConcessionGroupId = Guid.NewGuid(),
                    },
                    new AAr_ConcessionGroup
                    {
                        AAr_ConcessionCode = "DVA",
                        AAr_ConcessionGroupId = Guid.NewGuid(),
                    },
                ]));

            var concessions = new List<OngoingConcession>
            {
                new OngoingConcession
                {
                    RebateCode = "ASAC",
                    Consumer = 1,
                    EffectiveDate = DateTime.UtcNow,
                },
                new OngoingConcession
                {
                    RebateCode = "DVA",
                    Consumer = 2,
                    EffectiveDate = DateTime.UtcNow,
                },
                new OngoingConcession
                {
                    RebateCode = "NO-EXISTS",
                    Consumer = 3,
                    EffectiveDate = DateTime.UtcNow,
                },
            };

            var expectedResults = new List<List<AAr_ConcessionGroup>>
            {
                new List<AAr_ConcessionGroup>
                {
                    new AAr_ConcessionGroup
                    {
                        AAr_ConcessionCode = "ASAC",
                        AAr_ConcessionGroupId = Guid.NewGuid(),
                    },
                },
                new List<AAr_ConcessionGroup>
                {
                    new AAr_ConcessionGroup
                    {
                        AAr_ConcessionCode = "DVA",
                        AAr_ConcessionGroupId = Guid.NewGuid(),
                    },
                },
                new List<AAr_ConcessionGroup>(),
            };

            var results = new List<List<AAr_ConcessionGroup>>();

            var testBlock = new LazyDataflowLookupBlockAsync<OngoingConcession, AAr_ConcessionGroup>(
                organizationServiceMock.Object,
                fetchXml,
                (input, entity) => input.RebateCode == entity.AAr_ConcessionCode);

            var outputBlock = new ActionBlock<(OngoingConcession, List<AAr_ConcessionGroup>)>(
            (input) =>
            {
                results.Add(input.Item2);
            });

            testBlock.LinkTo(outputBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            // Act
            foreach (var concession in concessions)
            {
                await testBlock.SendAsync(concession);
            }

            testBlock.Complete();

            using var cts = new CancellationTokenSource();

            Task timerTask = Task.Delay(10000, cts.Token);
            var winner = await Task.WhenAny(timerTask, outputBlock.Completion);

            // Assert
            winner.Should().Be(outputBlock.Completion);

            organizationServiceMock.Verify(o => o.RetrieveMultipleAsync(It.IsAny<FetchExpression>()), Times.Exactly(1));

            results.Should().SatisfyRespectively(
                first =>
                {
                    first.Should().Equal(expectedResults[0], (r, e) => r.AAr_ConcessionCode == e.AAr_ConcessionCode);
                },
                second =>
                {
                    second.Should().Equal(expectedResults[1], (r, e) => r.AAr_ConcessionCode == e.AAr_ConcessionCode);
                },
                third =>
                {
                    third.Should().BeEmpty();
                });
        }
    }
}
