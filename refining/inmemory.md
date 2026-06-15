# Complete and ship InMemory transport support for Octopus

## Job to be done

Enable Octopus to run NServiceBus endpoints using an in-memory transport so that endpoint behavior can be exercised without requiring external infrastructure.

Related:
- [Vision#807](https://github.com/Particular/Vision/issues/807)
- [NServiceBus#7819](https://github.com/Particular/NServiceBus/pull/7819)

## Context

Work towards an `InMemoryTransport` was started in [NServiceBus#7819](https://github.com/Particular/NServiceBus/pull/7819) but was never completed.

Octopus has expressed a need for an in-memory transport. The work already completed in [NServiceBus#7819](https://github.com/Particular/NServiceBus/pull/7819) should be used as the foundation for delivering a supported solution.

The PR also contains an in-memory persistence implementation. While persistence is not currently an explicit Octopus requirement, the existing work should be evaluated to determine whether it should be shipped alongside the transport or remain separate.

The existing [`NServiceBus.Persistence.NonDurable`](https://github.com/Particular/NServiceBus.Persistence.NonDurable) package should also be considered as part of that evaluation.

## Scope

- Resume the work started in [NServiceBus#7819](https://github.com/Particular/NServiceBus/pull/7819)
- Complete the `InMemoryTransport` implementation required to support Octopus scenarios
- Identify and address any gaps preventing the implementation from being shipped
- Finalize the required tests and documentation
- Decide whether the in-memory persistence implementation should be shipped as part of the initial offering or deferred to follow-up work

## Acceptance criteria

- An `InMemoryTransport` package is released and available for Octopus consumption
- The package supports the scenarios required by Octopus
- Automated tests validate the supported behavior
- Documentation describes the intended use cases and limitations of the transport
- A recommendation is documented regarding the future of the accompanying in-memory persistence implementation, including whether to:
  - ship it together with the transport,
  - evolve [`NServiceBus.Persistence.NonDurable`](https://github.com/Particular/NServiceBus.Persistence.NonDurable), or
  - pursue it as separate follow-up work
