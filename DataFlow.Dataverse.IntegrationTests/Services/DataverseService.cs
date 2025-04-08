namespace DataFlow.Dataverse;

using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class DataverseService
{
    private readonly IOrganizationServiceAsync2 organizationService;

    public DataverseService(IOrganizationServiceAsync2 organizationService)
    {
        this.organizationService = organizationService;
    }

    public async Task<IEnumerable<TEntity>> RetrieveMultipleAsync<TEntity>(string fetchXml)
        where TEntity : Entity
    {
        var fetchXmlQuery = new FetchExpression(fetchXml);
        var entities = await organizationService.RetrieveMultipleAsync(fetchXmlQuery);

        return entities.Entities.Select(e => e.ToEntity<TEntity>());
    }
}
