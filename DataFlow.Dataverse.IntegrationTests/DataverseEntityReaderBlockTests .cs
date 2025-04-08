namespace DataFlow.Dataverse.IntegrationTests;

using Azure.Core;
using Azure.Identity;
using DataFlow.Core.Blocks;
using DataFlow.Dataverse.Blocks;
using DataFlow.Dataverse.DataModel;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

public class DataverseEntityReaderBlockTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string[] scopes = [ "https://aar-dev.crm6.dynamics.com/" ];
    private readonly TimeKeeper timeKeeper = new();
    private int loadedAccountCount;
    private int loadedCustomerRoleCount;
    private int processedCount;

    [Fact]
    public async Task SimpleRead_Succeeds()
    {
        var connectionOptions = new ConnectionOptions
        {
            AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth,
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

        var readerBlock = new DataverseEntityReaderBlock<AAr_Account>(serviceClient);

        readerBlock.BeforePageRead = OnBeforeAccountPageRead;
        readerBlock.AfterPageRead = OnAfterAccountPageRead;

        var queryExpression = new QueryExpression(AAr_Account.EntityLogicalName);
        queryExpression.ColumnSet = new ColumnSet(AAr_Account.Fields.AAr_AccountId, AAr_Account.Fields.AAr_AccountNumber);

        queryExpression.Criteria.AddCondition(AAr_Account.Fields.StateCode, ConditionOperator.Equal, 0);

        var actionBlock = new ActionBlock<AAr_Account>(
            (contact) =>
            {
                var customerNumber = contact.AAr_AccountNumber;

                Task.Delay(100).Wait();

                Interlocked.Increment(ref processedCount);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 500,
                MaxDegreeOfParallelism = 52,
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
                testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedAccountCount} accounts, Processed: {processedCount}");
            }
            else
            {
                await cts.CancelAsync();
                complete = true;
            }
        }

        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedAccountCount} accounts, Processed: {processedCount}");

        Assert.Equal(processedCount, loadedAccountCount);
    }

    [Fact]
    public async Task SimpleRead_JoinPoc()
    {
        var connectionOptions = new ConnectionOptions
        {
            AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth,
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

        var accountReaderBlock = new DataverseEntityReaderBlock<AAr_Account>(serviceClient);

        accountReaderBlock.BeforePageRead = OnBeforeAccountPageRead;
        accountReaderBlock.AfterPageRead = OnAfterAccountPageRead;

        var accountQueryExpression = new QueryExpression(AAr_Account.EntityLogicalName);
        accountQueryExpression.ColumnSet = new ColumnSet(AAr_Account.Fields.AAr_AccountId, AAr_Account.Fields.AAr_VelocityLedgerId, AAr_Account.Fields.AAr_AccountName);

        accountQueryExpression.Criteria.AddCondition(AAr_Account.Fields.StateCode, ConditionOperator.Equal, 0);

        var customerRoleReaderBlock = new DataverseEntityReaderBlock<AAr_CustomerRoles>(serviceClient);

        customerRoleReaderBlock.BeforePageRead = OnBeforeCustomerRolePageRead;
        customerRoleReaderBlock.AfterPageRead = OnAfterCustomerRolePageRead;

        var customerRolesQueryExpression = new QueryExpression(AAr_CustomerRoles.EntityLogicalName);
        customerRolesQueryExpression.ColumnSet = new ColumnSet(AAr_CustomerRoles.Fields.AAr_CustomerRolesId, AAr_CustomerRoles.Fields.AAr_Name, AAr_CustomerRoles.Fields.AAr_Account, AAr_CustomerRoles.Fields.AAr_VelocityCRid);

        customerRolesQueryExpression.Criteria.AddCondition(AAr_CustomerRoles.Fields.StateCode, ConditionOperator.Equal, 0);

        var joinBlock = new QueryJoinBlock<AAr_CustomerRoles, AAr_Account, EntityReference>(r => r.AAr_Account, a => a.ToEntityReference(), JoinType.Left);

        var actionBlock = new ActionBlock<Tuple<AAr_CustomerRoles?, AAr_Account?>>(
            (Tuple<AAr_CustomerRoles?, AAr_Account?> result) =>
            {
                var customerRoleVelocityId = result.Item1?.AAr_VelocityCRid;
                var accountVelocityId = result.Item2?.AAr_VelocityLedgerId;

                Interlocked.Increment(ref processedCount);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 500,
                MaxDegreeOfParallelism = 52,
            });

        customerRoleReaderBlock.LinkTo(joinBlock.Left, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        accountReaderBlock.LinkTo(joinBlock.Right, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        joinBlock.LinkTo(actionBlock, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        timeKeeper.StartNew();
        accountReaderBlock.Post(accountQueryExpression);
        customerRoleReaderBlock.Post(customerRolesQueryExpression);

        bool complete = false;
        while (!complete)
        {
            using var cts = new CancellationTokenSource();

            Task timerTask = Task.Delay(10000, cts.Token);
            var waitTasks = new List<Task> { actionBlock.Completion, timerTask };

            var winner = await Task.WhenAny(waitTasks);

            if (winner == timerTask)
            {
                testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedAccountCount} accounts and {loadedCustomerRoleCount} customer roles, Processed: {processedCount}");
            }
            else
            {
                await cts.CancelAsync();
                complete = true;
            }
        }

        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Loaded: {loadedAccountCount} accounts and {loadedCustomerRoleCount} customer roles, Processed: {processedCount}");

        Assert.Equal(processedCount, loadedCustomerRoleCount);
    }

    protected void OnBeforeAccountPageRead(int pageNumber)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed}: Begin loading account page [{pageNumber}]");
    }

    protected void OnAfterAccountPageRead(int pageNumber, int recordCount, bool moreRecords)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Account page {pageNumber} loaded page {recordCount} records");

        Interlocked.Add(ref loadedAccountCount, recordCount);
    }

    protected void OnBeforeCustomerRolePageRead(int pageNumber)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed}: Begin loading customer role page [{pageNumber}]");
    }

    protected void OnAfterCustomerRolePageRead(int pageNumber, int recordCount, bool moreRecords)
    {
        testOutputHelper.WriteLine($"{timeKeeper.Elapsed} Customer role page {pageNumber} loaded page {recordCount} records");

        Interlocked.Add(ref loadedCustomerRoleCount, recordCount);
    }
}