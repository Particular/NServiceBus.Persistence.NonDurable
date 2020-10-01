using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.AcceptanceTests
{
    public class ConfigureEndpointNonDurablePersistence : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UsePersistence<InMemoryPersistence>();
            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            // Nothing required for non-durable persistence
            return Task.FromResult(0);
        }
    }
}