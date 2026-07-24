namespace NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting.Support;

public class ConfigureEndpointNonDurablePersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UseNonDurablePersistence(new NonDurablePersistenceOptions
        {
            Saga = new NonDurableSagaOptions
            {
                ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic
            }
        });
        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;
}