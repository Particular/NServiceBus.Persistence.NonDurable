namespace NServiceBus.Persistence.NonDurable.Tests;

using Sagas;

class SagaMetadataHelper
{
    public static SagaCorrelationProperty GetMetadata<TSaga>(IContainSagaData entity) where TSaga : Saga
    {
        var metadata = SagaMetadata.Create<TSaga>();

        if (!metadata.TryGetCorrelationProperty(out var correlatedProp))
        {
            return SagaCorrelationProperty.None;
        }
        var prop = entity.GetType().GetProperty(correlatedProp.Name);

        var value = prop.GetValue(entity);

        return new SagaCorrelationProperty(correlatedProp.Name, value);
    }
}