namespace DataFlow.Dataverse.Blocks;

using System.Threading.Tasks.Dataflow;

public class DataverseLookupBlock<TInput, TLookup> : IPropagatorBlock<TInput, (TInput, TLookup)>, ITargetBlock<TInput>, ISourceBlock<(TInput, TLookup)>
{

}
