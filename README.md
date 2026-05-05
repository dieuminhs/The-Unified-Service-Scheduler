# The Unified Service Scheduler

Backend implementation of the Keyloop Technical Assessment **Scenario A — The Unified Service Scheduler**.

A .NET 8 / ASP.NET Core / EF Core / SQLite REST API that books service appointments while guaranteeing a qualified Technician and a Service Bay are free for the full service duration.

## What's in here

| Area | Implementation |
|---|---|
| Booking | Resource-constrained booking with skill-based qualification matching |
| Concurrency | Serializable transaction + filtered unique index + Polly retry + RowVersion |
| Soft-delete | `EntityBase` convention + EF global query filter + `SaveChanges` interceptor |
| API | REST + OpenAPI + RFC 7807 problem details + Idempotency-Key |
| Observability | Serilog (stdout JSON) + OpenTelemetry traces (console) + Prometheus `/metrics` |
| Tests | NUnit 4 unit + integration + concurrency + architecture tests |

## Quick start

```bash
dotnet restore
dotnet build
dotnet run --project src/Scheduler.Api
```

Then:

- Swagger UI: `http://localhost:5000/swagger`
- Metrics:    `http://localhost:5000/metrics`
- Health:     `http://localhost:5000/health/ready`

The first run creates `scheduler.db` and seeds demo data when running in `Development`.

## Tests

```bash
dotnet test
dotnet test --filter Category=Concurrency
```

The concurrency suite proves race-free booking by issuing 10 parallel POSTs against the same time slot and asserting that exactly the resource capacity succeeds with the rest receiving clean `409 SLOT_TAKEN` responses.

## Project layout

```
src/
  Scheduler.Domain/         entities + invariants. No EF, no ASP.
  Scheduler.Application/    services (Booking, Cancellation, ...) + abstractions. No EF, no ASP.
  Scheduler.Infrastructure/ EF Core + repositories + idempotency store + system clock.
  Scheduler.Api/            ASP.NET Core composition root.
tests/
  Scheduler.Domain.UnitTests/
  Scheduler.Application.UnitTests/
  Scheduler.Api.IntegrationTests/      (WebApplicationFactory + SQLite per test)
  Scheduler.ArchitectureTests/         (NetArchTest layer enforcement)
docs/
  superpowers/specs/        the System Design Document (the long-form spec)
  superpowers/plans/        the Implementation Plan
  api-examples/             .http files for manual exploration
```

Dependencies flow: `Api → Application → Domain` and `Infrastructure → Application → Domain`. Architecture tests enforce these directions.

## API at a glance

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/v1/appointments` | Book — `201` on success, `409 SLOT_TAKEN` / `422 *` on failure |
| `GET`  | `/api/v1/appointments/{id}` | Read one |
| `GET`  | `/api/v1/appointments` | List (filters: dealershipId, customerId, technicianId, fromUtc, toUtc) |
| `POST` | `/api/v1/appointments/{id}/cancel` | Cancel |
| `POST` | `/api/v1/appointments/{id}/reschedule` | Atomic cancel-then-book |
| `GET`  | `/api/v1/availability/slots` | List bookable windows |
| `GET`  | `/api/v1/dealerships` etc. | Reference-data reads |

All POSTs accept an `Idempotency-Key` header; replays with the same body return the original response, replays with a different body return `409 IDEMPOTENCY_REPLAY_MISMATCH`.

## AI Collaboration Narrative

This solution was built in a deliberately disciplined human-in-the-loop loop with Claude (Anthropic). Both the System Design Document and the Implementation Plan were produced through one-question-at-a-time conversation, then executed task-by-task with explicit review and pushback at every fork.

### Strategy

1. **Start with intent, not code.** Before writing a single line, the brief was distilled into six clarifying questions covering layer choice (backend), tech stack (.NET 8 + EF Core + SQLite), qualification model (skill-tag superset), concurrency strategy (optimistic with filtered unique indexes and Polly retry), scope (lean + reschedule), and observability (one simple setup for dev and prod). Each was a multi-choice question with explicit trade-offs and a recommendation; corrections were applied whenever the recommendation didn't match my judgment.
2. **Design before plan, plan before code.** A full system design document was written and committed to source control *before* any implementation tasks were drafted. The plan was then derived directly from the spec, with every requirement traceable to a task.
3. **Section-by-section approval.** The design was presented in seven sections (architecture, domain model, API surface, booking flow, testing strategy, observability, project structure). Each was reviewed and signed off before the next was drafted. This produced corrections that reshape the design, not just polish:
   - Adding `IsActive` and `IsDeleted` to every entity, with the soft-delete convention enforced via an EF interceptor and a global query filter.
   - Promoting `Skill` from a string code to a first-class entity for referential integrity and lifecycle independence.
   - Splitting the project layout so `Application` and `Api` no longer share concerns.
   - Removing CI scope from the deliverables to keep focus on what the brief asks for.
   - Lifting `Technician` and `ServiceBay` out of a 1:N relationship with `Dealership` into M:N junctions, prompting a corresponding revision of the booking and availability flows.
   - Simplifying the telemetry story to one consistent setup for dev and prod (no exporter-swap configuration).

### Verification process

Every AI-generated artifact was verified before acceptance:

- **Tests are first-class.** Domain invariants, application services, and the booking concurrency invariant all have explicit tests. The 10-parallel-bookings test would have caught a missing transaction or a missing unique index immediately.
- **Architecture tests enforce structural decisions.** NetArchTest assertions ensure `Domain` references nothing, `Application` references no EF or ASP.NET Core, and entities all inherit `EntityBase`.
- **Real bugs caught and fixed.** During integration testing the AI implementer flagged a nested-transaction bug in the reschedule path (`ReschedulingService.RescheduleAsync` opens a transaction, then calls `BookingService.BookAsync` which tries to open another). The fix joined inner saves to the outer transaction — a clean, correct pattern surfaced by an integration test, not by code review.
- **Subagent-driven execution.** Each task was implemented by a fresh subagent dispatched with the full task spec. Implementers reported `DONE` / `DONE_WITH_CONCERNS` / `BLOCKED` / `NEEDS_CONTEXT` — concerns were explicit and frequently led to plan refinements (e.g., switching `IsRowVersion()` to `IsConcurrencyToken()` for SQLite portability).

### How quality was ensured

- **Layered enforcement, not convention only.** EF global query filters, the `SaveChanges` interceptor, and the architecture tests together prevent layer violations and soft-delete escapes at compile- or run-time, not just at code-review time.
- **Focused exception types with stable codes.** Domain exceptions carry a stable `Code` (`SLOT_TAKEN`, `TECHNICIAN_UNQUALIFIED`, ...) so client error-handling is deterministic and tests can assert on codes, not strings.
- **Determinism in tests.** `IClock` is injected everywhere a service touches time; `IResourceSelector` orders selection deterministically. Tests can predict which technician gets assigned and which slot wins a race.
- **YAGNI, ruthlessly.** Auth, notifications, GraphQL, dashboards, OTLP exporters — every one named explicitly as out-of-scope with a one-line extension path, so the design's *non*-goals are as visible as its goals.

The full per-decision history lives in `docs/superpowers/specs/2026-05-04-unified-service-scheduler-design.md` (Section 11, "AI collaboration in the design phase"). The 39-task implementation plan is at `docs/superpowers/plans/2026-05-04-unified-service-scheduler-implementation.md`.

## License

MIT.
