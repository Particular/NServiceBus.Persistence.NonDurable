namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Support;

    public class ConfigureEndpointNonDurablePersistence : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UsePersistence<NonDurablePersistence>();
            return Task.CompletedTask;
        }

        public Task Cleanup() =>
            // Nothing required for non-durable persistence
            Task.CompletedTask;
    }
}