namespace NServiceBus.Persistence.NonDurable.Tests
{
    using NServiceBus.Sagas;

    class SagaMetadataHelper
    {
        public static SagaCorrelationProperty GetMetadata<T>(IContainSagaData entity)
        {
            var metadata = SagaMetadata.Create(typeof(T));

            if (!metadata.TryGetCorrelationProperty(out var correlatedProp))
            {
                return SagaCorrelationProperty.None;
            }
            var prop = entity.GetType().GetProperty(correlatedProp.Name);

            var value = prop.GetValue(entity);

            return new SagaCorrelationProperty(correlatedProp.Name, value);
        }
    }
}