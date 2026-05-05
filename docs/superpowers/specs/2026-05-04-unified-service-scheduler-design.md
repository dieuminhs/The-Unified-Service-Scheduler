# The Unified Service Scheduler — System Design

**Challenge:** Keyloop Technical Assessment — Scenario A
**Date:** 2026-05-04
**Author:** Phong Chung
**Service layer chosen:** Backend (REST API + persistent DB; client mocked via OpenAPI / Swagger UI / cURL)

---

## 1. Summary

Build an Appointment Scheduler that lets a customer request a service appointment for a specific vehicle, service type, and dealership at a desired time, checks in real time that both a qualified Technician and a Service Bay are free for the entire service duration, and creates a persistent Appointment record on success. The hard part is **race-free booking under concurrent requests**, so the design centres on it.

### Decisions at a glance

| # | Decision | Rationale |
|---|---|---|
| 1 | Backend-only implementation; client mocked via OpenAPI + Swagger UI + cURL | The scenario's interesting problems (concurrency, qualification matching, transactional integrity) live on the server. |
| 2 | .NET 8 + ASP.NET Core Web API + EF Core 8 + SQLite | Aligned with the Keyloop ecosystem; SQLite gives a zero-config run/test story; EF makes the persistence layer swap to SQL Server one config line away. |
| 3 | Skill-tag-based qualification matching (`required ⊆ technician.skills`) | Realistic for automotive service; cleanest to test. |
| 4 | Optimistic concurrency: serializable transaction + in-txn overlap check + filtered unique index + Polly retry + RowVersion | Standard production pattern; testable as "10 parallel POSTs → 1 win, 9 conflicts". |
| 5 | Lean scope + reschedule-as-atomic-cancel-then-book | Scenario's three core requirements are the booking flow itself; everything else risks dilution. |
| 6 | Serilog (stdout) + OpenTelemetry traces (console) + plain JSON report endpoint | Three observability surfaces: structured logs, request traces, and an aggregate report; the same setup works in dev and prod. |
| 7 | Layered architecture (`Domain` → `Application` → `Infrastructure` ← `Api`) with SOLID-disciplined splits | Mirrors the use-case shape; services are testable as a plain class library; no MediatR ceremony. |

### Goals

- Implement the three core requirements end-to-end with a persistent database.
- Demonstrate **concurrency correctness** under parallel booking attempts via tests that would fail without the layered defences.
- Provide a runnable service (`dotnet run`) with all three observability pillars wired and visible.
- Keep the design honest: every deliberate simplification is named with a one-line "how it would extend".

### Non-goals (named, not built)

Real auth (only `X-Dealership-Id` header stub), notifications, per-technician shifts, bay typing, pricing, multi-region sharding, GraphQL/HATEOAS/WebSocket, CI pipeline, container deployment infrastructure (Dockerfile is included but no compose stack), Grafana dashboards.

---

## 2. Architecture

### 2.1 High-level component view

```
┌────────────────────┐    ┌──────────────────────────────────────────────┐    ┌──────────────────┐
│ Clients (mocked)   │    │           ASP.NET Core 8 — Scheduler.Api    │    │ SQLite           │
│  - Swagger UI      │    │  ┌────────────────────────────────────────┐ │    │  scheduler.db    │
│  - cURL / .http    │───►│  │ Middleware:                            │ │───►│  EF migrations   │
│  - OpenAPI spec    │    │  │  CorrelationId · Serilog request log · │ │    │  seeded data     │
│  - Test harness    │    │  │  Idempotency · ProblemDetails ·        │ │    └──────────────────┘
│                    │    │  │  GlobalException · OTel ASP.NET        │ │
│ Mock auth:         │    │  └────────────────┬───────────────────────┘ │    ┌──────────────────┐
│  X-Dealership-Id   │    │                   ▼                         │    │ Telemetry        │
│  header stub       │    │  ┌────────────────────────────────────────┐ │    │ stdout (logs +   │
└────────────────────┘    │  │ Controllers (thin)                     │ │    │   traces)        │
                          │  └────────────────┬───────────────────────┘ │    │ /api/v1/report   │
                          │                   ▼                         │    │ (aggregate JSON) │
                          │  ┌────────────────────────────────────────┐ │    └──────────────────┘
                          │  │ Application — services (split per SRP) │ │
                          │  │  Booking · Cancellation · Reschedule · │ │    ┌──────────────────┐
                          │  │  Availability · Qualification ·        │ │    │ Health           │
                          │  │  ResourceSelector                      │ │    │  /health/live    │
                          │  └────────────────┬───────────────────────┘ │    │  /health/ready   │
                          │                   ▼                         │    └──────────────────┘
                          │  ┌────────────────────────────────────────┐ │
                          │  │ Infrastructure — repositories,         │ │
                          │  │ idempotency store, system clock,       │ │
                          │  │ EF DbContext + interceptors            │ │
                          │  └────────────────────────────────────────┘ │
                          │                                              │
                          │  Cross-cutting: Serilog · OTel SDK · DI      │
                          └──────────────────────────────────────────────┘
```

### 2.2 Components — what each does

- **Clients (mocked).** Swagger UI for interactive exploration; cURL examples and `.http` files in `/docs/api-examples` for scripted use; the OpenAPI spec is the contract. A `X-Dealership-Id` header substitutes for real auth.
- **Middleware pipeline.** Generates / propagates `X-Correlation-Id`, opens a Serilog log scope and an OpenTelemetry root activity, handles idempotency replay short-circuits, translates exceptions into RFC 7807 `application/problem+json` responses.
- **Controllers.** Thin: validate the request DTO via FluentValidation, delegate to a service, map the result. No business rules in controllers.
- **Application services.** One use-case per service (`BookingService`, `CancellationService`, `ReschedulingService`, `AvailabilityService`, `QualificationMatcher`, `ResourceSelector`). Each has a single responsibility and depends on abstractions only.
- **Repositories.** One per aggregate, defined as interfaces in `Application/Abstractions/Persistence/` and implemented in `Infrastructure/Repositories/`. Read-side and write-side segregated where useful (`IAppointmentReader` / `IAppointmentWriter`).
- **`SchedulerDbContext`.** EF Core context with global query filters for soft-delete, an audit/soft-delete `SaveChangesInterceptor`, and entity configurations that declare the filtered unique indexes used as concurrency backstops.
- **Cross-cutting infrastructure.** Serilog as the logging façade, OpenTelemetry SDK with ASP.NET Core / EF Core / SQLite auto-instrumentation, and the standard .NET DI container.

### 2.3 Data flow — happy-path booking

1. `POST /api/v1/appointments` arrives with body and `Idempotency-Key` header.
2. Middleware stamps `CorrelationId`, opens a root OTel activity, opens a Serilog log scope.
3. FluentValidation rejects malformed bodies with `400`.
4. Idempotency middleware checks the key; on a body-hash match it short-circuits with the original 201.
5. `BookingService.BookAsync` opens a serializable transaction, loads referenced entities, validates opening hours, calls `QualificationMatcher.FindQualified(...)`, then `AvailabilityService.FirstFreeTechnician(...)` and `FirstFreeBay(...)`, creates the `Appointment` (status `Confirmed`), saves and commits.
6. On `DbUpdateException` from the filtered unique index, Polly retries the whole transaction up to 3 times; exhausted retries raise `SlotTakenException` → 409.
7. On success the service persists the idempotency record, returns the entity, the controller maps to the response DTO, the client receives `201 Created` with `Location: /api/v1/appointments/{id}`.

---

## 3. Technology choices and justifications

| Concern | Choice | Why |
|---|---|---|
| Runtime | .NET 8 LTS | Aligned with Keyloop ecosystem; long-term support; AOT-friendly. |
| Web framework | ASP.NET Core 8 Web API | Built-in DI, configuration, health checks, problem details, OpenAPI generation. |
| ORM | EF Core 8 | Strongly typed; supports global query filters (essential for the soft-delete convention) and `SaveChanges` interceptors (essential for the audit convention). Avoids hand-rolled abstractions over EF — that's a known anti-pattern. |
| Persistence | SQLite (default), SQL Server-compatible via config swap | Zero-config for the demo; one connection-string change for production. SQLite WAL mode gives single-writer serialization for free. |
| Validation | FluentValidation | Declarative, testable, separates input-shape validation from business rules. |
| Logging | Serilog (`Serilog.AspNetCore` + `Serilog.Formatting.Compact`) | Structured-first; rich enrichers; supported by every observability backend. |
| Telemetry | OpenTelemetry .NET SDK + Prometheus exporter (`/metrics` endpoint) | Vendor-neutral instrumentation; metrics scrape-ready; logs and traces to stdout. |
| Resilience | Polly v8 (`Microsoft.Extensions.Resilience`) | Standard for transient-failure handling; clean retry policy DSL. |
| Testing | NUnit 4 + Moq + FluentAssertions + `WebApplicationFactory` + NetArchTest + Verify | NUnit 4 native parameterised tests; Moq for boundary mocks only; `WebApplicationFactory` for full HTTP integration; NetArchTest enforces architectural boundaries; Verify snapshots the OpenAPI contract. |

**Versioning:** package versions are pinned centrally via `Directory.Packages.props`.

---

## 4. Domain model

### 4.1 Convention — `EntityBase`

Every domain entity inherits an abstract `EntityBase` carrying the lifecycle + audit columns:

| Field | Type | Purpose |
|---|---|---|
| `IsActive` | `bool` | Operational toggle (e.g., technician on leave). Visible to queries; callers filter explicitly. |
| `IsDeleted` | `bool` | Soft-delete tombstone. **Hidden by an EF Core global query filter** on every entity. |
| `CreatedAtUtc` | `DateTime` | Audit. Stamped by the interceptor. |
| `UpdatedAtUtc` | `DateTime?` | Audit. Stamped by the interceptor. |
| `DeletedAtUtc` | `DateTime?` | Set when `IsDeleted` flips true. |
| `RowVersion` | `byte[]` | EF Core optimistic concurrency token. |

`SchedulerDbContext` overrides `SaveChangesAsync` to:

- Convert `EntityState.Deleted` to a soft-delete (set `IsDeleted = true`, `DeletedAtUtc = utcNow`, then revert state to `Modified`).
- Stamp `CreatedAtUtc` for added rows and `UpdatedAtUtc` for modified rows.

A model-building convention applies `HasQueryFilter(e => !e.IsDeleted)` to every `EntityBase` descendant. `IgnoreQueryFilters()` is available for admin/audit views. Hard-delete remains available via `ExecuteDelete()` for retention jobs.

### 4.2 Entities

**Reference and resource entities** (all inherit `EntityBase`):

- **`Dealership`** — `Id`, `Name`, `TimeZone` (IANA), `OpeningHours[]` per day-of-week.
- **`Customer`** — `Id`, `FirstName`, `LastName`, `Email` (unique, filtered), `Phone`.
- **`Vehicle`** — `Id`, `CustomerId` (FK), `Vin` (unique, filtered), `Make`, `Model`, `Year`.
- **`ServiceType`** — `Id`, `Name`, `DurationMinutes`, `Description`.
- **`Technician`** — `Id`, `FullName`. *Independent entity; assigned to one or more dealerships via `TechnicianDealership`.* This models multi-site dealership groups where a technician may rotate between locations.
- **`ServiceBay`** — `Id`, `Name`. *Independent entity; assigned to one or more dealerships via `ServiceBayDealership`.* For symmetry with technicians and to support shared-resource arrangements between sites.
- **`Skill`** — `Id`, `Code` (unique, filtered, e.g. `EV_CERT`), `Name`, `Description`, `Category`.

**Junction entities** (also inherit `EntityBase`):

- **`TechnicianDealership`** — composite PK `(TechnicianId, DealershipId)`. Skills are personal certifications and remain on `TechnicianSkill`, *not* duplicated per assignment.
- **`ServiceBayDealership`** — composite PK `(ServiceBayId, DealershipId)`.
- **`TechnicianSkill`** — composite PK `(TechnicianId, SkillId)`.
- **`ServiceTypeRequiredSkill`** — composite PK `(ServiceTypeId, SkillId)`.

**Aggregate root**:

- **`Appointment`** — `Id` (Guid), `DealershipId` (partition key), `CustomerId`, `VehicleId`, `ServiceTypeId`, `TechnicianId`, `ServiceBayId`, `StartsAtUtc`, `EndsAtUtc` (= Start + ServiceType.Duration), `Status` (`Confirmed | Cancelled`).

`Status` is **business state** and remains distinct from `IsDeleted` (data lifecycle). Cancelled appointments are kept; the global filter only hides deleted ones. Availability checks query `WHERE IsDeleted = 0 AND Status = 'Confirmed'`.

### 4.3 Indexes (EF migrations)

| Index | Columns | Filter | Purpose |
|---|---|---|---|
| `UX_Appointment_Technician_Confirmed_Start` | `(TechnicianId, StartsAtUtc)` UNIQUE | `Status = 'Confirmed' AND IsDeleted = 0` | Cheap backstop for exact-time double-booking on a technician. |
| `UX_Appointment_Bay_Confirmed_Start` | `(ServiceBayId, StartsAtUtc)` UNIQUE | same | Same, for bay. |
| `IX_Appointment_Tech_Window` | `(TechnicianId, StartsAtUtc, EndsAtUtc)` covering | same | Fast overlap queries inside the booking transaction. |
| `IX_Appointment_Bay_Window` | `(ServiceBayId, StartsAtUtc, EndsAtUtc)` covering | same | Same, for bay. |
| `IX_Appointment_Dealership_Start` | `(DealershipId, StartsAtUtc)` | none | List-by-dealership reads. |
| `UX_Skill_Code` | `(Code)` UNIQUE | `IsDeleted = 0` | Skill code uniqueness, allows re-use after deletion. |
| `UX_Customer_Email` | `(Email)` UNIQUE | `IsDeleted = 0` | Same, for customer email. |
| `UX_Vehicle_Vin` | `(Vin)` UNIQUE | `IsDeleted = 0` | Same, for VIN. |
| `IX_TechnicianDealership_Dealership` | `(DealershipId, TechnicianId)` | `IsDeleted = 0` | Fast lookup of technicians assigned to a dealership during availability filtering. |
| `IX_ServiceBayDealership_Dealership` | `(DealershipId, ServiceBayId)` | `IsDeleted = 0` | Same, for bays. |

> **Note:** Filtered (partial) unique indexes catch *identical-start* collisions cheaply. They do **not** catch overlapping ranges with different starts. Full overlap prevention is enforced inside the booking transaction via the `NOT EXISTS` overlap query (Section 6).
>
> **Cross-dealership overlap:** Because a technician (or bay) may be assigned to multiple dealerships, the overlap-prevention indexes are intentionally scoped by `TechnicianId` / `ServiceBayId` *across all dealerships* — a technician can only be in one place at a time, regardless of which dealership the appointment is for. The same applies to bays under any shared-resource arrangement.

### 4.4 Deliberate simplifications

Each is documented here rather than built; each has a clear extension path.

- **Bay typing skipped for v1.** Any bay can host any service. Adding `BayType` and a `ServiceTypeAllowedBayType` junction mirrors the technician-skill pattern.
- **Per-technician working hours skipped.** Dealership opening hours apply uniformly. Easy extension: a `TechnicianShift` entity and a small change in `AvailabilityService`.
- **Cancelled appointments retained.** Visible for history; status filter excludes them from availability checks.

---

## 5. API surface

REST over JSON, versioned at the URL prefix (`/api/v1/...`), problem details (RFC 7807) for errors, OpenAPI 3 published at `/swagger`. All booking-mutating endpoints accept an `Idempotency-Key` header.

### 5.1 Endpoints — booking and lifecycle

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| `POST` | `/api/v1/appointments` | Book | `201 Created` + `Location` | `400` validation · `404` ref-data · `409 SLOT_TAKEN` · `422 TECHNICIAN_UNQUALIFIED` · `422 OUTSIDE_OPENING_HOURS` · `422 START_IN_PAST` |
| `GET` | `/api/v1/appointments/{id}` | Read one | `200 OK` | `404` |
| `GET` | `/api/v1/appointments` | List with filters: `dealershipId`, `customerId`, `technicianId`, `from`, `to`, `status` | `200 OK` | `400` |
| `POST` | `/api/v1/appointments/{id}/cancel` | Cancel | `204 No Content` | `404` · `409` already cancelled · `409` past start |
| `POST` | `/api/v1/appointments/{id}/reschedule` | Atomic cancel-then-book in one transaction | `200 OK` | as POST + `409` if new slot taken |

### 5.2 Endpoints — availability and reference data

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/v1/availability/slots?dealershipId=&serviceTypeId=&from=&to=&granularityMinutes=15` | Returns the list of `[start, end)` windows where ≥1 qualified technician AND ≥1 bay are free for the full service duration. |
| `GET` | `/api/v1/dealerships`, `/api/v1/dealerships/{id}` | Read dealerships. |
| `GET` | `/api/v1/dealerships/{id}/technicians`, `/api/v1/dealerships/{id}/bays` | Read technicians / bays *assigned to* the given dealership (via `TechnicianDealership` / `ServiceBayDealership`). |
| `GET` | `/api/v1/technicians`, `/api/v1/technicians/{id}` | Read technicians as independent entities; the response includes assigned dealerships and skills. |
| `GET` | `/api/v1/bays`, `/api/v1/bays/{id}` | Read bays as independent entities; the response includes assigned dealerships. |
| `GET` | `/api/v1/service-types`, `/api/v1/skills` | Read catalogs. |
| `GET` | `/api/v1/customers/{id}/vehicles` | Read vehicles by customer. |

### 5.3 System endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health/live` | Liveness — never depends on DB. |
| `GET` | `/health/ready` | Readiness — `SELECT 1` against SQLite. |
| `GET` | `/metrics` | Prometheus exposition. |
| `GET` | `/swagger` | Interactive OpenAPI UI. |

### 5.4 Booking request / response

```http
POST /api/v1/appointments
Idempotency-Key: 2f7b…
X-Dealership-Id: …
Content-Type: application/json

{
  "dealershipId": "…",
  "customerId":  "…",
  "vehicleId":   "…",
  "serviceTypeId": "…",
  "startsAtUtc": "2026-05-12T09:30:00Z"
}
```

Response:

```http
201 Created
Location: /api/v1/appointments/8f1e…

{
  "id": "8f1e…",
  "dealershipId": "…", "customerId": "…", "vehicleId": "…",
  "serviceTypeId": "…", "technicianId": "…", "serviceBayId": "…",
  "startsAtUtc": "2026-05-12T09:30:00Z",
  "endsAtUtc":   "2026-05-12T10:30:00Z",
  "status": "Confirmed",
  "createdAtUtc": "2026-05-04T14:22:11Z"
}
```

### 5.5 Error envelope

All non-2xx responses use `application/problem+json` and include a stable `code` plus the `correlationId`:

```json
{
  "type":   "https://scheduler.example.com/errors/slot-taken",
  "title":  "Slot no longer available",
  "status": 409,
  "detail": "All qualified technicians are booked for the requested window.",
  "code":   "SLOT_TAKEN",
  "correlationId": "01HVK…"
}
```

Distinct codes: `SLOT_TAKEN`, `TECHNICIAN_UNQUALIFIED`, `OUTSIDE_OPENING_HOURS`, `START_IN_PAST`, `RESOURCE_NOT_FOUND`, `IDEMPOTENCY_REPLAY_MISMATCH`, `ALREADY_CANCELLED`, `VALIDATION_FAILED`.

### 5.6 Idempotency

`Idempotency-Key` is stored alongside the request body hash and the resulting appointment id. Replays with the same key and same body return the original `201 Created`. Replays with the same key and a different body return `409 IDEMPOTENCY_REPLAY_MISMATCH`. Keys expire after 24 hours (a background `IHostedService` runs `ExecuteDelete()` against expired rows; not implemented in v1, documented as a one-class addition).

### 5.7 Out of scope

Auth (only `X-Dealership-Id`), pagination cursors (offset+limit, capped at 100), HATEOAS, batch endpoints, GraphQL.

---

## 6. Booking flow and concurrency — the centrepiece

### 6.1 Sequence (happy path)

1. `POST /api/v1/appointments` — middleware sets correlation/trace, opens Serilog scope.
2. FluentValidation rejects malformed input (`400 VALIDATION_FAILED`).
3. Idempotency middleware: on `(key, bodyHash)` match, return cached 201.
4. `BookingService.BookAsync` — begin serializable transaction (SQLite: `BEGIN IMMEDIATE` in WAL mode; SQL Server / Postgres: true SERIALIZABLE).
5. Load `Dealership`, `ServiceType`, `Customer`, `Vehicle` — verify they exist, are active, and belong to the right dealership scope.
6. Compute `EndsAtUtc = StartsAtUtc + ServiceType.DurationMinutes`. Validate against `Dealership.OpeningHours` for that day-of-week in dealership timezone.
7. `QualificationMatcher.FindQualified(serviceTypeId, dealershipId)` — joins `TechnicianDealership` to scope candidates to the booking dealership, then applies the SQL `NOT EXISTS / NOT EXISTS` skill-superset pattern. Returns the ordered technician list.
8. `AvailabilityService.FirstFreeTechnician([Start, End))` — among the qualified, dealership-assigned set, find the first technician with no overlapping `Confirmed` appointment *across all dealerships* (a technician can only be in one place at a time).
9. `AvailabilityService.FirstFreeBay([Start, End))` — joins `ServiceBayDealership` to scope candidates to the booking dealership, then finds the first bay with no overlapping `Confirmed` appointment across all dealerships.
10. Construct the `Appointment` (status `Confirmed`), `SaveChangesAsync()` — filtered unique indexes and FK constraints fire.
11. Persist the idempotency record (`(key, bodyHash, appointmentId)`).
12. `COMMIT`. Return `201 Created` with `Location`.

### 6.2 Conflict path

- Step 8 / 9 finds no free resource → `SlotTakenException`. Polly retries the transaction (max 3 attempts, jittered backoff, base 50 ms). Most races resolve on retry. Exhaustion → `409 SLOT_TAKEN`.
- Step 10 raises `DbUpdateException` from a filtered unique index hit by a parallel writer → same retry envelope.
- No qualified technician exists at all (skills missing) → never retry → `422 TECHNICIAN_UNQUALIFIED`.
- Outside opening hours → never retry → `422 OUTSIDE_OPENING_HOURS`.
- Start in the past → `422 START_IN_PAST`.

### 6.3 Layered concurrency defences

| Layer | Mechanism | What it catches |
|---|---|---|
| 1 | Serializable transaction (SQLite `BEGIN IMMEDIATE`; serializable on SQL Server / Postgres) | Establishes the consistent read snapshot. |
| 2 | In-transaction overlap query (`NOT EXISTS`) backed by a covering index | The real check — full range overlap, not just identical starts. |
| 3 | Filtered unique index on `(TechnicianId, StartsAtUtc) WHERE Confirmed AND !Deleted` | Cheap backstop for the most common race (two clients hitting "book 09:30 with Bob"). |
| 4 | Polly retry policy (3 attempts, jittered backoff) | Resolves transient conflicts from layer 2/3. |
| 5 | EF `RowVersion` token on `Appointment` | Protects cancel/reschedule against lost updates. |

### 6.4 Reschedule = atomic cancel-then-book

The reschedule handler runs both the cancel of the old appointment and the booking of the new one inside a single serializable transaction with the same retry envelope. Without atomicity, you'd briefly free a slot another booker could grab.

### 6.5 SQLite caveat — named honestly

SQLite serializes writes globally per database file in WAL mode. That makes the demo trivially correct but masks the harder distributed problem. The integration tests fire 10 parallel bookings against SQLite to exercise the codepath, **not** to prove distributed correctness. In production on SQL Server / Postgres, layers 1–4 are what catch real concurrent writers across multiple app instances.

### 6.6 Determinism for tests

- Technician selection: first qualified by `Id` ascending.
- Bay selection: first free by `Id` ascending.

Tests can predict which resource gets assigned. Selection lives behind `IResourceSelector`, so a future round-robin / load-balancing strategy is a one-class change (Open/Closed Principle).

---

## 7. Testing strategy

The brief weighs Technical Execution as one of four evaluation dimensions; the strategy below is shaped to make the booking invariants unfalsifiable, not to chase coverage.

### 7.1 Toolchain

| Tool | Purpose |
|---|---|
| **NUnit 4** | Test runner; native `[TestCase]` / `[TestCaseSource]` parameterisation |
| **Moq 4** | Mocking — only at boundaries (`IClock`, `IIdempotencyStore`, repository fakes for unit tests) |
| **FluentAssertions** | Readable assertions |
| **`WebApplicationFactory<Program>`** | Full HTTP stack integration tests |
| **SQLite in-memory** (`Data Source=:memory:` with shared connection) | Per-test DB isolation |
| **NetArchTest.Rules** | Architectural invariants enforced as tests |
| **Verify.NUnit** | OpenAPI snapshot — fails the build if the contract changes silently |
| **Coverlet** | Coverage reporting (`dotnet test`) |

### 7.2 Pyramid

- **Architecture tests** (~5) — boundary invariants.
- **Contract tests** (~2) — OpenAPI snapshot.
- **Concurrency tests** (~5) — *the centrepiece*.
- **Integration tests** (~30) — real EF + SQLite + `WebApplicationFactory`.
- **Unit tests** (~40) — pure logic; `Application` services test as a plain class library against Moq fakes.

### 7.3 Unit tests (selection)

- `QualificationMatcher_Should_*` — empty required, exact match, superset, missing skill, soft-deleted skill ignored.
- `TimeWindow_Should_DetectOverlap` — adjacency (half-open: 10–11 vs 11–12 = no overlap), strict overlap, equal, containment.
- `OpeningHoursValidator_Should_*` — within hours, on the boundary, across DST transitions, dealership-closed days.
- `ResourceSelector_Should_BeDeterministic` — same input set → same selection.
- Domain invariants on `Appointment` constructors (`End > Start`, FK Guids non-empty, status transitions valid).

### 7.4 Integration tests (selection)

- `Book_HappyPath_ReturnsConfirmedAppointment`
- `Book_OutsideOpeningHours_Returns422`
- `Book_NoQualifiedTechnician_Returns422_WithCode_TECHNICIAN_UNQUALIFIED`
- `Book_AllTechniciansBusyForWindow_Returns409_WithCode_SLOT_TAKEN`
- `Book_AllBaysBusyForWindow_Returns409_WithCode_SLOT_TAKEN`
- `Book_TechnicianNotAssignedToDealership_NotConsidered_FallsBackToOthers`
- `Book_BayNotAssignedToDealership_NotConsidered_FallsBackToOthers`
- `Book_TechnicianBookedAtAnotherDealershipForOverlappingWindow_NotConsidered` — proves cross-dealership overlap blocking.
- `Book_StartInPast_Returns422`
- `Book_VehicleBelongsToDifferentCustomer_Returns422`
- `Cancel_FreesSlotForRebooking`
- `Cancel_AlreadyCancelled_Returns409`
- `Reschedule_HappyPath_ReturnsNewAppointment_OldIsCancelled_Atomically`
- `Reschedule_NewSlotTaken_LeavesOldAppointmentIntact`
- `Idempotency_SameKeyAndBody_ReturnsOriginal201`
- `Idempotency_SameKeyDifferentBody_Returns409_WithCode_IDEMPOTENCY_REPLAY_MISMATCH`
- `SoftDelete_DeletedTechnician_NotConsideredForBooking`
- `GlobalQueryFilter_DeletedAppointment_NotInListResults`

### 7.5 Concurrency tests — the centrepiece

- `Concurrent_TenBookingsForSameSlot_ExactlyOneSucceeds_NineGet409` — `Parallel.ForEachAsync` of 10 booking POSTs against the same `(dealership, serviceType, start)`. Asserts `Count(success) == 1 && Count(409) == 9` and exactly 1 confirmed appointment in the DB.
- `Concurrent_TwoReschedulesOfSameAppointmentToSameTarget_ExactlyOneSucceeds`.
- `Concurrent_BookAndCancel_NoLostUpdate` — `RowVersion` behaviour.
- `Retry_TransientConflict_ResolvedWithinPolicy` — injected `ITestConflictInjector` forces one collision; the booking succeeds on the second attempt.
- `RetryExhausted_ReturnsCleanProblemDetails_NotEFException`.

### 7.6 Architecture tests (NetArchTest)

- Controllers must not reference `Microsoft.EntityFrameworkCore`.
- Every type ending in `Entity` (or in the `Domain` namespace) must inherit `EntityBase`.
- `Application` must not reference `Microsoft.EntityFrameworkCore` or `Microsoft.AspNetCore`.
- `Domain` must reference no other project or third-party dependency beyond the BCL.
- Services in the `Application/Services` namespace must depend on interfaces, not concrete repositories.
- No type may reference `System.Console` outside the composition root (forces structured logging).

### 7.7 Contract tests

- `OpenApi_Snapshot_HasNotChanged` — Verify-based; a contract diff fails the test.
- `OpenApi_AllEndpointsHaveProblemDetailsResponse_For_4xx_5xx`.

### 7.8 Coverage target

- 90%+ on `Domain/` and `Application/Services/`.
- Migrations and DTOs excluded.
- Coverlet runs as part of `dotnet test`; threshold enforced via MSBuild property.

### 7.9 Out of test scope

ASP.NET Core middleware itself, Serilog/OTel wiring (smoke-tested via `curl /metrics`), the SQLite engine.

---

## 8. Observability

All three pillars are wired with one consistent setup that works the same in **dev** and **prod**:

- **Logs** — Serilog structured JSON to stdout (where any container runtime, journald, or log shipper picks them up).
- **Metrics** — Prometheus exposition at `/metrics` (any monitoring stack that scrapes Prometheus reads it).
- **Traces** — OpenTelemetry `Activity` spans wired throughout the booking codepath; rendered to the console for the demo.

The application's job is to **emit** clean, correlated telemetry. *How* it gets stored, queried, or visualised is a deployment concern, not an application concern. There is no exporter-swap code in the app.

### 8.1 Logging — Serilog

Compact JSON formatter to stdout. Log scope fields stamped on every line:

| Field | Source |
|---|---|
| `CorrelationId` | `CorrelationIdMiddleware` (`X-Correlation-Id` header or generated ULID) |
| `TraceId` / `SpanId` | OTel `Activity.Current` |
| `DealershipId` | scoped via `LogContext` once a request resolves a dealership |
| `AppointmentId` | scoped inside `BookingService` once the entity exists |
| `IdempotencyKey` | from header |
| `ClientIp`, `UserAgent` | request log middleware |

Request logs use `UseSerilogRequestLogging()` with method, status, elapsed-ms, route. **PII is never logged** — services use IDs only; a `SensitiveAttribute` + custom `IDestructuringPolicy` masks any field that slips through.

### 8.2 Operational report

A read-only `GET /api/v1/report` endpoint returns aggregate counts: total appointments, breakdown by status (Confirmed/Cancelled), per-dealership totals, and per-service-type totals. Anyone (operator, demo viewer, monitoring tool) can hit it for a quick view of what the system has been doing — no scraping, no Prometheus, no auth.

### 8.3 Tracing — OpenTelemetry

`ActivitySource` named `Scheduler`. Span hierarchy on a typical booking:

```
HTTP POST /api/v1/appointments         (auto from AspNetCore instrumentation)
└─ booking.attempt                     (BookingService.BookAsync)
   ├─ booking.find_qualified_techs     (QualificationMatcher)
   ├─ booking.find_free_resources      (AvailabilityService)
   │  ├─ db.query  technicians         (auto from EF instrumentation)
   │  └─ db.query  bays
   ├─ db.command   INSERT appointment  (auto)
   └─ idempotency.persist
```

Span attributes: `dealership.id`, `service_type.id`, `appointment.id`, `outcome`, `retry.attempt`, `correlation.id`. Errors set `Status = Error` and `exception.*` attributes via `RecordException`.

`AddSqliteInstrumentation()` and `AddEntityFrameworkCoreInstrumentation()` give DB spans for free.

### 8.4 Health checks

- `/health/live` — constant `Healthy`, never depends on DB.
- `/health/ready` — `AddDbContextCheck<SchedulerDbContext>(tags: ["ready"])`.

### 8.5 What proves it works

- Boot the API → `curl http://localhost:5000/metrics` shows `bookings_total{outcome="confirmed"}` ticking up after one POST.
- Run the concurrency test → `bookings_total{outcome="conflict"}` jumps by 9, `booking_retries_total{outcome="resolved"}` climbs.
- Force an error → stdout JSON log line with full context (correlation id, dealership id, exception, stacktrace).
- The console-formatted trace shows the booking-attempt span hierarchy on each request.

---

## 9. Project structure and run/deploy

### 9.1 Solution layout (4 projects)

```
Scheduler.sln
├── src/
│   ├── Scheduler.Domain/             # entities, value objects, rules. No EF, no ASP, no DI.
│   ├── Scheduler.Application/        # services, contracts, validators, abstractions. No EF, no ASP.
│   ├── Scheduler.Infrastructure/     # EF, repositories, idempotency store, system clock.
│   └── Scheduler.Api/                # ASP.NET Core composition root — controllers, middleware, telemetry, Program.cs, seed.
└── tests/
    ├── Scheduler.Domain.UnitTests/
    ├── Scheduler.Application.UnitTests/
    ├── Scheduler.Api.IntegrationTests/
    └── Scheduler.ArchitectureTests/
```

### 9.2 Dependency rule

```
Api ──────> Application ──────> Domain
 │             ▲
 └──> Infrastructure ──┘
                 │
                 └──> Domain
```

- `Domain` references nothing.
- `Application` references only `Domain`. **Most service tests run as plain class-library unit tests against Moq fakes** — no `WebApplicationFactory` overhead.
- `Infrastructure` references `Application` (to implement persistence abstractions) and `Domain`.
- `Api` references `Application` and `Infrastructure` for composition.

NetArchTest enforces these directions.

### 9.3 Run

```bash
dotnet restore
dotnet build
dotnet ef database update --project src/Scheduler.Infrastructure --startup-project src/Scheduler.Api
dotnet run --project src/Scheduler.Api
# → http://localhost:5000
# → http://localhost:5000/swagger
# → http://localhost:5000/metrics
# → http://localhost:5000/health/ready
```

`Program.cs` calls `db.Database.MigrateAsync()` at startup; a fresh clone with no DB just works. Seed data loads from `SeedData.cs` when `Seeding:Enabled = true` (default in Development).

### 9.4 Test

```bash
dotnet test
dotnet test --filter Category=Concurrency
```

### 9.5 Container

`Dockerfile` (multi-stage, `mcr.microsoft.com/dotnet/aspnet:8.0` runtime). Not required by the brief but a cheap demonstration of production-thinking.

### 9.6 Configuration surface

| Section | Keys |
|---|---|
| `ConnectionStrings:Default` | SQLite path (default `Data Source=scheduler.db`) |
| `Booking:MaxRetries` | Polly attempts (default 3) |
| `Booking:RetryBaseDelayMs` | Jittered backoff base (default 50) |
| `Booking:DefaultGranularityMinutes` | Availability slot step (default 15) |
| `Seeding:Enabled` | Seed demo data on startup (default `true` in Development) |

---

## 10. Risks and mitigations

| Risk | Mitigation |
|---|---|
| SQLite single-writer model hides distributed concurrency bugs | Tests exercise the codepath; the design names this honestly; arch tests + retry envelope are the real defence under SQL Server. |
| OTel SDK version churn | Versions pinned in `Directory.Packages.props`; span creation behind a thin helper. |
| EF migrations diverge from production DB | Migrations in source control; `dotnet ef migrations script` for review in PRs. |
| Idempotency store becomes a hotspot | Currently a regular table; future cache (Redis) is a one-interface change. |
| Seeded clock vs `DateTime.UtcNow` drift in tests | All time-aware code uses `IClock`; tests inject a controllable clock. |

---

## 11. AI collaboration in the design phase

This document was produced through an iterative dialogue with Claude (Anthropic) playing the role of a pair-thinking systems engineer. The collaboration shape:

- **One-question-at-a-time refinement.** Six clarifying questions narrowed the choice set: layer (backend), stack (.NET 8 / EF Core / SQLite), qualification model (skill-tag), concurrency strategy (optimistic + filtered uniques + retry), scope (lean + reschedule), observability scope (console-mode OTel pillars).
- **2–3-options-with-tradeoffs at every fork.** Architecture style (vertical slice / layered / clean), concurrency strategy (optimistic / pessimistic / queue), test stack (NUnit vs xUnit, Moq vs NSubstitute) — each presented with explicit pros/cons before a choice was made.
- **Per-section approval before advancing.** Sections 1–7 (architecture, domain model, API surface, booking flow, testing, observability, project structure) were each reviewed and signed off before the next was drafted.
- **Visual companion for spatial reasoning.** Architecture diagrams, ERDs, and the booking sequence were shown as rendered HTML mockups to make boundary and flow choices unambiguous; text was used for everything that read better as prose or tables.
- **Pushback explicit.** Several reviewer corrections reshaped the design rather than accepting the first cut: soft-delete + `IsActive` were added to every entity via a shared `EntityBase`; `Skill` was promoted from a string code to a first-class entity; the project layout was split so `Application` and `Api` no longer share concerns; CI scope was removed from the deliverables; and `Technician` / `ServiceBay` were lifted out of a 1:N relationship with `Dealership` into M:N junctions, prompting a corresponding revision of the booking and availability flows.

This produced a design where every decision is traceable to a question and an option set, every simplification is named, and every "out of scope" item has an extension path. The implementation phase will use a separate written plan, derived from this spec, to direct the AI through scaffolding, test-first slice implementation, and verification — with the same one-question-at-a-time discipline applied at the code level.
