namespace NServiceBus.Persistence.NonDurable.Tests;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[TestFixture]
public class When_registering_nondurable_storage
{
    [Test]
    public void Should_use_dependency_injection_then_explicit_configuration_then_shared_default()
    {
        var serviceCollection = new ServiceCollection();
        var serviceProviderStorage = new NonDurableStorage();
        serviceCollection.AddSingleton(serviceProviderStorage);

        NonDurableStorageRuntime.Configure(serviceCollection, new NonDurablePersistenceOptions { Storage = new NonDurableStorage() });

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<NonDurableStorage>(), Is.SameAs(serviceProviderStorage));

        var configuredServices = new ServiceCollection();
        var configuredStorage = new NonDurableStorage();

        NonDurableStorageRuntime.Configure(configuredServices, new NonDurablePersistenceOptions { Storage = configuredStorage });

        using var configuredProvider = configuredServices.BuildServiceProvider();
        Assert.That(configuredProvider.GetRequiredService<NonDurableStorage>(), Is.SameAs(configuredStorage));

        var defaultServices = new ServiceCollection();

        NonDurableStorageRuntime.Configure(defaultServices, new NonDurablePersistenceOptions());

        using var defaultProvider = defaultServices.BuildServiceProvider();
        Assert.That(defaultProvider.GetRequiredService<NonDurableStorage>(), Is.SameAs(NonDurableStorageRuntime.SharedStorage));
    }
}