namespace DataFlow.Dataverse.IntegrationTests;

using DataFlow.Dataverse.Blocks;
using DataFlow.Dataverse.DataModel;
using DataFlow.Dataverse.IntegrationTests.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

public class DataflowLookupBlockAsyncTests : DataflowEntityIntergrationTestBase
{
    private readonly TimeKeeper timeKeeper = new();

    public DataflowLookupBlockAsyncTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task DataflowLookupBlockAsync_Succeeds()
    {
        // Arrange
        var organizationService = Services.GetRequiredService<IOrganizationServiceAsync2>();

        var fetchXml = $"""
            <fetch>
                <entity name='aar_concessiongroup'>
                    <attribute name='aar_concessioncode' />
                    <attribute name='aar_concessiongroupid' />
                </entity>
            </fetch>
        """;

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
                    AAr_ConcessionGroupId = new Guid("953183d1-47f2-ef11-9341-00224891cb02")
                },
            },
            new List<AAr_ConcessionGroup>
            {
                new AAr_ConcessionGroup
                {
                    AAr_ConcessionCode = "DVA",
                    AAr_ConcessionGroupId = new Guid("973183d1-47f2-ef11-9341-00224891cb02")
                },
            },
            new List<AAr_ConcessionGroup>(),
        };

        var results = new List<List<AAr_ConcessionGroup>>();

        var testBlock = new DataflowLookupBlockAsync<OngoingConcession, AAr_ConcessionGroup>(
            organizationService,
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

        timeKeeper.StartNew();

        bool complete = false;
        while (!complete)
        {
            using var cts = new CancellationTokenSource();

            Task timerTask = Task.Delay(1000, cts.Token);
            var waitTasks = new List<Task> { outputBlock.Completion, timerTask };

            var winner = await Task.WhenAny(waitTasks);

            if (winner == timerTask)
            {
                OutputHelper.WriteLine($"{timeKeeper.Elapsed} Waiting for completion ...");
            }
            else
            {
                await cts.CancelAsync();
                complete = true;
            }
        }

        OutputHelper.WriteLine($"{timeKeeper.Elapsed} Completed");

        // Assert
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
