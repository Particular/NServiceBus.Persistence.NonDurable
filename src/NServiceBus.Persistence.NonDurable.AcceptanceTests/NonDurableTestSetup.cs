using NServiceBus;
using NUnit.Framework;

// HINT: Workaround for https://github.com/Particular/NServiceBus/pull/6291
// Ensures that the NonDurablePersistence assembly has been loaded into the AppDomain
[SetUpFixture]
public class NonDurableTestSetup
{
    [OneTimeSetUp]
    public void SetupFixture()
    {
        typeof(NonDurablePersistence).ToString();
    }
}