namespace DataFlow.Dataverse.IntegrationTests;

using Azure.Core;
using Azure.Identity;
using DataFlow.Dataverse.Blocks;
using DataFlow.Dataverse.DataModel;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.Xrm.Sdk.Query;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

public class DataverseEntityReaderBlockAsyncTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string[] scopes = ["https://aar-dev.crm6.dynamics.com/"];
    private readonly TimeKeeper timeKeeper = new();
    private int loadedCount;
    private int processedCount;

    [Fact]
    public async Task SimpleRead_Succeeds()
    {
        var connectionOptions = new ConnectionOptions
        {
            AuthenticationType = AuthenticationType.OAuth,
            ServiceUri = new Uri("https://aar-dev.crm6.dynamics.com/"),
            AccessTokenProviderFunctionAsync = async (string instanceUri) =>
            {
                var credentials = new DefaultAzureCredential();
                var tokenRequest = new TokenRequestContext(scopes);
                var tokenResult = await credentials.GetTokenAsync(tokenRequest);

                return tokenResult.Token;
            }
        };

        var serviceClient = new ServiceClient(connectionOptions);
        var organizationService = (IOrganizationServiceAsync2)serviceClient;

        var readerBlock = new DataverseEntityReaderBlockAsync<Contact>(organizationService);

        readerBlock.BeforePageRead = OnBeforePageRead;
        readerBlock.AfterPageRead = OnAfterPageRead;

        var queryExpression = new QueryExpression(Contact.EntityLogicalName);
        queryExpression.ColumnSet = new ColumnSet(Contact.Fields.ContactId, Contact.Fields.AAr_VelocityCustomerId);

        queryExpression.Criteria.AddCondition(Contact.Fields.StateCode, ConditionOperator.Equal, 0);

        var actionBlock = new ActionBlock<Contact>(
            async (contact) =>
            {
                var velocityId = contact.AAr_VelocityCustomerId;

                await Task.Delay(1);

                Interlocked.Increment(ref processedCount);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 500,
                MaxDegreeOfParallelism = 52
            });

        readerBlock.LinkTo(actionBlock, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        timeKeeper.StartNew();
        readerBlock.Post(queryExpression);

        bool complete = false;
        while (!complete)
        {
            using var cts = new CancellationTokenSource();

            Task timerTask = Task.Delay(10000, cts.Token);
            var waitTasks = new List<Task> { actionBlock.Completion, timerTask };

            var winner = await Task.WhenAny(waitTasks);

            if (winner == timerTask)
            {
                testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedCount}, Processed: {processedCount}");
            }
            else
            {
                await cts.CancelAsync();
                complete = true;
            }
        }

        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedCount}, Processed: {processedCount}");

        Assert.True(loadedCount == processedCount);
    }

    protected Task OnBeforePageRead(int pageNumber)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed}: Begin loading page [{pageNumber}]");
        return Task.CompletedTask;
    }

    protected Task OnAfterPageRead(int pageNumber, int recordCount, bool moreRecords)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Page {pageNumber} loaded page {recordCount} records");

        Interlocked.Add(ref loadedCount, recordCount);

        return Task.CompletedTask;
    }
}