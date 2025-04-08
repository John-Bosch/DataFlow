namespace DataFlow.Dataverse.Blocks;

using DataFlow.Dataverse.Caching;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Threading.Tasks.Dataflow;

public class DataflowLookupBlockAsync<TInput, TEntity> : IPropagatorBlock<TInput, (TInput, List<TEntity>)>, ITargetBlock<TInput>, ISourceBlock<(TInput, List<TEntity>)>
    where TEntity : Entity
{
    private readonly TransformBlock<TInput, (TInput, List<TEntity>)> transformBlock;
    private readonly AsyncLazy<List<TEntity>> entityCache;
    private readonly Func<TInput, TEntity, bool> selector;

    public Task Completion => TargetBlock.Completion;

    private ITargetBlock<TInput> TargetBlock => transformBlock;

    private ISourceBlock<(TInput, List<TEntity>)> SourceBlock => transformBlock;

    public DataflowLookupBlockAsync(IOrganizationServiceAsync2 organisationService, string fetchXml, Func<TInput, TEntity, bool> selector)
    {
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        transformBlock = new TransformBlock<TInput, (TInput, List<TEntity>)>(TransformAsync);

        entityCache = new AsyncLazy<List<TEntity>>(() => LookupEntitiesAsync(organisationService, fetchXml));
    }

    public (TInput, List<TEntity>) ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<(TInput, List<TEntity>)> target, out bool messageConsumed)
    {
        return SourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
    }

    public IDisposable LinkTo(ITargetBlock<(TInput, List<TEntity>)> target, DataflowLinkOptions linkOptions)
    {
        return SourceBlock.LinkTo(target, linkOptions);
    }

    public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<(TInput, List<TEntity>)> target)
    {
        SourceBlock.ReleaseReservation(messageHeader, target);
    }

    public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<(TInput, List<TEntity>)> target)
    {
        return SourceBlock.ReserveMessage(messageHeader, target);
    }

    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput>? source, bool consumeToAccept)
    {
        return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
    }

    public void Complete()
    {
        TargetBlock.Complete();
    }

    public void Fault(Exception exception)
    {
        TargetBlock.Fault(exception);
    }

    private static async Task<List<TEntity>> LookupEntitiesAsync(IOrganizationServiceAsync2 organisationService, string fetchXml)
    {
        var fetchXmlQuery = new FetchExpression(fetchXml);
        var entities = await organisationService.RetrieveMultipleAsync(fetchXmlQuery);

        return [.. entities.Entities.Select(e => e.ToEntity<TEntity>())];
    }

    private async Task<(TInput, List<TEntity>)> TransformAsync(TInput input)
    {
        var entities = await entityCache.Value;
        return (input, entities.Where(e => selector(input, e)).ToList());
    }
}
