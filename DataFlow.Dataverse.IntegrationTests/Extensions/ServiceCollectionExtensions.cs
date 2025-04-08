namespace DataFlow.Dataverse.IntegrationTests.Extensions;

using Azure.Core;
using Azure.Identity;
using DataFlow.Dataverse.DataModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;

/// <summary>
/// Extension methods for <see cref="ServiceCollection"/>.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataverseService(
        this IServiceCollection services,
        HostBuilderContext hostBuilderContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(hostBuilderContext);

        services.AddScoped<IOrganizationServiceAsync2>(services =>
        {
            var connectionOptions = new ConnectionOptions
            {
                AuthenticationType = AuthenticationType.OAuth,
                ServiceUri = new Uri("https://aar-dev.crm6.dynamics.com/"),
                AccessTokenProviderFunctionAsync = async (string instanceUri) =>
                {
                    var credentials = new DefaultAzureCredential();
                    var tokenRequest = new TokenRequestContext(["https://aar-dev.crm6.dynamics.com/"]);
                    var tokenResult = await credentials.GetTokenAsync(tokenRequest);

                    return tokenResult.Token;
                }
            };

            var serviceClient = new ServiceClient(connectionOptions);
            return serviceClient;
        });

        services.AddScoped<DataverseService>();

        return services;
    }
}
