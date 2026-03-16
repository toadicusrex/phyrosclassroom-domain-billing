# PhyrosClassroom Billing Domain Service

`phyrosclassroom-domain-billing` is the first downstream domain-service repository for PhyrosClassroom.

This repository is intentionally a reference implementation for:

- the numbered project-folder structure
- command-side and query-side hosts in one repository
- hexagonalish architecture with explicit ports and adapters
- strict avoidance of service location outside the composition root

## Current Shape

- `PhyrosClassroom.Billing.Host.CommandApi` exposes command-side endpoints
- `PhyrosClassroom.Billing.Host.QueryApi` exposes query-side endpoints
- `PhyrosClassroom.Billing.Composition` is the composition root
- local persistence uses file-backed infrastructure for early development only

## Local Development Notes

- command-side routes are under `/command/billing`
- query-side routes are under `/query/billing`
- local persistence writes under `App_Data`

## Architectural Note

This service is described as hexagonalish rather than strict hexagonal architecture.

The main intentional deviations are:

- explicit `Orchestration`, `Engines`, `Composition`, and `Host` projects
- one repository containing multiple deployable hosts
- practical CQRS deployment boundaries reflected directly in the project layout
