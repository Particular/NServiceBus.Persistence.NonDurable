namespace NServiceBus.Persistence.NonDurable.Tests
{
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        public void ApproveNonDurablePersistence()
        {
            var publicApi = typeof(NonDurablePersistence).Assembly.GeneratePublicApi(new ApiGeneratorOptions { ExcludeAttributes = new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" } });
            Approver.Verify(publicApi);
        }
    }
}