# NServiceBus.Persistence.NonDurable

NServiceBus.Persistence.NonDurable adds support for NServiceBus to persist in-memory, in a non-durable fashion. Any data is **lost** when the process ends, making this persistence option suitable for a very small range of scenario's in which the system can afford to lose data.

It is part of the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems.

See the [Non-durable persistence documentation](https://docs.particular.net/persistence/non-durable/) for more details on how to use it.
