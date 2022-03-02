using NServiceBus;
using NUnit.Framework;

[SetUpFixture]
public class NonDurableTestSetup
{
    [OneTimeSetUp]
    public void SetupFixture()
    {
        typeof(NonDurablePersistence).ToString();
    }
}