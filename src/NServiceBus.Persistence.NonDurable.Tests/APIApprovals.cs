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
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(NonDurablePersistence).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" });
            Approver.Verify(publicApi);
        }
    }
}