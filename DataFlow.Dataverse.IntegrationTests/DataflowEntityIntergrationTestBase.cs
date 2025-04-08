namespace DataFlow.Dataverse.IntegrationTests;

using DataFlow.Dataverse.IntegrationTests.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;

public class DataflowEntityIntergrationTestBase
{
    public DataflowEntityIntergrationTestBase(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;

        var host = new HostBuilder()
                .ConfigureAppConfiguration(
                    builder =>
                    {
                        builder.AddJsonFile("appsettings.json", true);
                        builder.AddUserSecrets<DataflowEntityIntergrationTestBase>();
                    })
                .ConfigureLogging(builder =>
                {
                    builder.AddFilter("AAR.CRM", LogLevel.Debug);

                    if (OutputHelper != null)
                    {
                        builder.AddXunit(OutputHelper);
                    }
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    services
                        .AddLogging()
                        .AddMemoryCache()
                        .AddDataverseService(hostBuilderContext);
                })
                .Build();

        Services = host.Services;
    }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    protected IServiceProvider Services { get; }

    /// <summary>
    /// Gets the test output helper.
    /// </summary>
    protected ITestOutputHelper OutputHelper { get; }
}
