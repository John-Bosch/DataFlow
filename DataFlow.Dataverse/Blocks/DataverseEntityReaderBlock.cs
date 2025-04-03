namespace DataFlow.Dataverse.Blocks;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

public class DataverseEntityReaderBlock<TEntity> : IPropagatorBlock<QueryExpression, TEntity>
    where TEntity : Entity
{
    public Task Completion => transformBlock.Completion;

    public Action<int>? BeforePageRead { get; set; }

    public Action<int, int, bool>? AfterPageRead { get; set; }

    private TransformManyBlock<QueryExpression, TEntity> transformBlock;
    private IOrganizationService organizationService;

    private ITargetBlock<QueryExpression> TargetBlock => transformBlock;

    private ISourceBlock<TEntity> SourceBlock => transformBlock;

    private int pageSize;

    public DataverseEntityReaderBlock(IOrganizationService organizationService, int pageSize = 5000)
    {
        this.organizationService = organizationService;
        this.pageSize = pageSize;
        transformBlock = new TransformManyBlock<QueryExpression, TEntity>(TransformAsync);
    }

    public DataverseEntityReaderBlock(IOrganizationService organizationService, ExecutionDataflowBlockOptions blockOptions, int pageSize = 5000)
    {
        this.organizationService = organizationService;
        this.pageSize = pageSize;
        transformBlock = new TransformManyBlock<QueryExpression, TEntity>(TransformAsync, blockOptions);
    }

    public void Complete()
    {
        TargetBlock.Complete();
    }

    public TEntity? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target, out bool messageConsumed)
    {
        return SourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
    }

    public IDisposable LinkTo(ITargetBlock<TEntity> target, DataflowLinkOptions linkOptions)
    {
        return SourceBlock.LinkTo(target, linkOptions);
    }

    public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target)
    {
        SourceBlock.ReleaseReservation(messageHeader, target);
    }

    public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TEntity> target)
    {
        return SourceBlock.ReserveMessage(messageHeader, target);
    }

    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, QueryExpression messageValue, ISourceBlock<QueryExpression>? source, bool consumeToAccept)
    {
        return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
    }

    public void Fault(Exception exception)
    {
        TargetBlock.Fault(exception);
    }

    protected IEnumerable<TEntity> TransformAsync(QueryExpression message)
    {
        message.PageInfo = new PagingInfo
        {
            PageNumber = 1,
            Count = pageSize,
            ReturnTotalRecordCount = true
        };

        while (true)
        {
            BeforePageRead?.Invoke(message.PageInfo.PageNumber);

            var results = organizationService.RetrieveMultiple(message);

            AfterPageRead?.Invoke(message.PageInfo.PageNumber, results.Entities.Count(), results.MoreRecords);

            foreach (var result in results.Entities)
            {
                yield return result.ToEntity<TEntity>();
            }

            if (!results.MoreRecords)
            {
                TargetBlock.Complete();
                break;
            }

            message.PageInfo.PageNumber++;
            message.PageInfo.PagingCookie = results.PagingCookie;
        }
    }
}
