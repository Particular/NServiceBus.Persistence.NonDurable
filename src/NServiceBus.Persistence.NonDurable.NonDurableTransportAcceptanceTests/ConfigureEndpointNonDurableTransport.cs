namespace NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;

public class ConfigureEndpointNonDurableTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UseTransport(new NonDurableTransport());
        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;
}