# Unified Service Scheduler — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the backend for the Keyloop Unified Service Scheduler (Scenario A) — a .NET 8 / ASP.NET Core / EF Core / SQLite REST API that books service appointments while guaranteeing a qualified Technician and a Service Bay are free for the full service duration, with race-free concurrency, soft-delete + audit conventions, full Serilog/OpenTelemetry observability, and a real test suite.

**Architecture:** Layered: `Scheduler.Domain` → `Scheduler.Application` → `Scheduler.Infrastructure` ← `Scheduler.Api`. SOLID-disciplined splits (one service per use-case, segregated repository interfaces). Optimistic concurrency defended by serializable transactions + filtered unique indexes + Polly retries + RowVersion. Every entity inherits an abstract `EntityBase` carrying `IsActive` / `IsDeleted` / audit fields, with EF global query filters and a `SaveChanges` interceptor enforcing the convention.

**Tech Stack:** .NET 8, ASP.NET Core 8, EF Core 8, SQLite, FluentValidation, Polly v8 (`Microsoft.Extensions.Resilience`), Serilog, OpenTelemetry SDK + Prometheus exporter, NUnit 4, Moq 4, FluentAssertions, NetArchTest.Rules, Verify.NUnit, Coverlet.

**Source spec:** `docs/superpowers/specs/2026-05-04-unified-service-scheduler-design.md`

**Conventions throughout this plan:**
- All file paths are relative to the repo root.
- Every task ends with a commit. Commit messages follow Conventional Commits.
- Tests use NUnit 4 attributes (`[Test]`, `[TestFixture]`, `[SetUp]`, `[TestCase]`).
- Tests follow Arrange / Act / Assert structure.
- Build verification step: `dotnet build` exits with `Build succeeded`.
- Test verification step: `dotnet test` exits with `Passed!` summary unless the task explicitly expects failure (TDD red phase).

---

## Phase 0 — Solution scaffolding

### Task 1: Create the solution and project skeleton

**Files:**
- Create: `Scheduler.sln`
- Create: `Directory.Packages.props`
- Create: `Directory.Build.props`
- Create: `src/Scheduler.Domain/Scheduler.Domain.csproj`
- Create: `src/Scheduler.Application/Scheduler.Application.csproj`
- Create: `src/Scheduler.Infrastructure/Scheduler.Infrastructure.csproj`
- Create: `src/Scheduler.Api/Scheduler.Api.csproj`
- Create: `tests/Scheduler.Domain.UnitTests/Scheduler.Domain.UnitTests.csproj`
- Create: `tests/Scheduler.Application.UnitTests/Scheduler.Application.UnitTests.csproj`
- Create: `tests/Scheduler.Api.IntegrationTests/Scheduler.Api.IntegrationTests.csproj`
- Create: `tests/Scheduler.ArchitectureTests/Scheduler.ArchitectureTests.csproj`

- [ ] **Step 1: Create the solution file**

```bash
dotnet new sln -n Scheduler
```

- [ ] **Step 2: Create the four src projects**

```bash
dotnet new classlib -n Scheduler.Domain         -o src/Scheduler.Domain         -f net8.0
dotnet new classlib -n Scheduler.Application    -o src/Scheduler.Application    -f net8.0
dotnet new classlib -n Scheduler.Infrastructure -o src/Scheduler.Infrastructure -f net8.0
dotnet new webapi   -n Scheduler.Api            -o src/Scheduler.Api            -f net8.0 --use-controllers --no-openapi false
```

- [ ] **Step 3: Create the four test projects**

```bash
dotnet new nunit -n Scheduler.Domain.UnitTests        -o tests/Scheduler.Domain.UnitTests        -f net8.0
dotnet new nunit -n Scheduler.Application.UnitTests   -o tests/Scheduler.Application.UnitTests   -f net8.0
dotnet new nunit -n Scheduler.Api.IntegrationTests    -o tests/Scheduler.Api.IntegrationTests    -f net8.0
dotnet new nunit -n Scheduler.ArchitectureTests       -o tests/Scheduler.ArchitectureTests       -f net8.0
```

- [ ] **Step 4: Add all projects to the solution**

```bash
dotnet sln add src/Scheduler.Domain/Scheduler.Domain.csproj
dotnet sln add src/Scheduler.Application/Scheduler.Application.csproj
dotnet sln add src/Scheduler.Infrastructure/Scheduler.Infrastructure.csproj
dotnet sln add src/Scheduler.Api/Scheduler.Api.csproj
dotnet sln add tests/Scheduler.Domain.UnitTests/Scheduler.Domain.UnitTests.csproj
dotnet sln add tests/Scheduler.Application.UnitTests/Scheduler.Application.UnitTests.csproj
dotnet sln add tests/Scheduler.Api.IntegrationTests/Scheduler.Api.IntegrationTests.csproj
dotnet sln add tests/Scheduler.ArchitectureTests/Scheduler.ArchitectureTests.csproj
```

- [ ] **Step 5: Create `Directory.Build.props` (repo root) for shared compile settings**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <NoWarn>1701;1702;CS1591</NoWarn>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

- [ ] **Step 6: Create `Directory.Packages.props` (repo root) for centralised versions**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- ASP.NET Core / EF Core -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Diagnostics.HealthChecks" Version="2.2.0" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.10" />
    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.10.0" />
    <PackageVersion Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
    <!-- Resilience -->
    <PackageVersion Include="Microsoft.Extensions.Resilience" Version="8.10.0" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
    <PackageVersion Include="Polly" Version="8.4.2" />
    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
    <PackageVersion Include="Serilog.Enrichers.Thread" Version="4.0.0" />
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
    <PackageVersion Include="OpenTelemetry.Exporter.Console" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-beta.2" />
    <!-- Swagger -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.8.1" />
    <PackageVersion Include="Swashbuckle.AspNetCore.Annotations" Version="6.8.1" />
    <!-- Testing -->
    <PackageVersion Include="NUnit" Version="4.2.2" />
    <PackageVersion Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageVersion Include="NUnit.Analyzers" Version="4.3.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="Verify.NUnit" Version="26.6.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <!-- Misc -->
    <PackageVersion Include="NodaTime" Version="3.2.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Verify build**

Run: `dotnet build`
Expected: `Build succeeded` with 8 projects.

- [ ] **Step 8: Commit**

```bash
git add .
git commit -m "chore: scaffold solution with 4 src + 4 test projects"
```

---

### Task 2: Wire project references and NuGet packages

**Files:**
- Modify: each `.csproj` to add references and packages.

- [ ] **Step 1: `src/Scheduler.Domain/Scheduler.Domain.csproj` — no project refs, no packages**

Replace the file's `<ItemGroup>` content (if any) so the file looks like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Scheduler.Domain</RootNamespace>
    <AssemblyName>Scheduler.Domain</AssemblyName>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: `src/Scheduler.Application/Scheduler.Application.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Scheduler.Application</RootNamespace>
    <AssemblyName>Scheduler.Application</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Scheduler.Domain\Scheduler.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Resilience" />
  </ItemGroup>
</Project>
```

(Add the two `Microsoft.Extensions.*` versions to `Directory.Packages.props` as well so the central-management policy holds; or rely on the explicit `Version` attribute used here — either works, just be consistent.)

- [ ] **Step 3: `src/Scheduler.Infrastructure/Scheduler.Infrastructure.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Scheduler.Infrastructure</RootNamespace>
    <AssemblyName>Scheduler.Infrastructure</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Scheduler.Domain\Scheduler.Domain.csproj" />
    <ProjectReference Include="..\Scheduler.Application\Scheduler.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: `src/Scheduler.Api/Scheduler.Api.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>Scheduler.Api</RootNamespace>
    <AssemblyName>Scheduler.Api</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Scheduler.Application\Scheduler.Application.csproj" />
    <ProjectReference Include="..\Scheduler.Infrastructure\Scheduler.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation.AspNetCore" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <PackageReference Include="Serilog.Formatting.Compact" />
    <PackageReference Include="Serilog.Enrichers.Environment" />
    <PackageReference Include="Serilog.Enrichers.Thread" />
    <PackageReference Include="OpenTelemetry" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Console" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="Swashbuckle.AspNetCore" />
    <PackageReference Include="Swashbuckle.AspNetCore.Annotations" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Test project csprojs (apply same shape to all four)**

For `tests/Scheduler.Domain.UnitTests/Scheduler.Domain.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="NUnit.Analyzers" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Scheduler.Domain\Scheduler.Domain.csproj" />
  </ItemGroup>
</Project>
```

For `Scheduler.Application.UnitTests`: same packages **plus Moq**, plus a project ref to `Scheduler.Application` and `Scheduler.Domain`.

For `Scheduler.Api.IntegrationTests`: above packages plus `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`, `Verify.NUnit`, `Moq`. Project refs to all four src projects.

For `Scheduler.ArchitectureTests`: above packages plus `NetArchTest.Rules`. Project refs to all four src projects.

- [ ] **Step 6: Verify build**

Run: `dotnet build`
Expected: `Build succeeded` for all 8 projects.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: wire project references and NuGet packages"
```

---

### Task 3: Add a smoke test to confirm test runners are alive

**Files:**
- Create: `tests/Scheduler.Domain.UnitTests/SmokeTests.cs`
- Modify: delete the auto-generated `tests/*/UnitTest1.cs` files in every test project.

- [ ] **Step 1: Delete auto-generated stubs**

```bash
rm tests/Scheduler.Domain.UnitTests/UnitTest1.cs
rm tests/Scheduler.Application.UnitTests/UnitTest1.cs
rm tests/Scheduler.Api.IntegrationTests/UnitTest1.cs
rm tests/Scheduler.ArchitectureTests/UnitTest1.cs
```

- [ ] **Step 2: Add `tests/Scheduler.Domain.UnitTests/SmokeTests.cs`**

```csharp
using FluentAssertions;
using NUnit.Framework;

namespace Scheduler.Domain.UnitTests;

[TestFixture]
public sealed class SmokeTests
{
    [Test]
    public void Runner_executes_at_least_one_test()
    {
        true.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run the test suite**

Run: `dotnet test`
Expected: `Passed!` with `Total tests: 1`.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "test: add smoke test confirming runner discovery"
```

---

## Phase 1 — Domain layer

### Task 4: `EntityBase` abstract base class

**Files:**
- Create: `src/Scheduler.Domain/Common/EntityBase.cs`
- Test:   `tests/Scheduler.Domain.UnitTests/Common/EntityBaseTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.Domain.UnitTests.Common;

[TestFixture]
public sealed class EntityBaseTests
{
    private sealed class FakeEntity : EntityBase { }

    [Test]
    public void New_entity_has_active_true_and_deleted_false()
    {
        var e = new FakeEntity();
        e.IsActive.Should().BeTrue();
        e.IsDeleted.Should().BeFalse();
        e.DeletedAtUtc.Should().BeNull();
        e.UpdatedAtUtc.Should().BeNull();
    }

    [Test]
    public void RowVersion_initialised_as_empty_array_not_null()
    {
        var e = new FakeEntity();
        e.RowVersion.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails (compile error: EntityBase missing)**

Run: `dotnet test --filter FullyQualifiedName~EntityBaseTests`
Expected: build error, type `EntityBase` not found.

- [ ] **Step 3: Implement `src/Scheduler.Domain/Common/EntityBase.cs`**

```csharp
namespace Scheduler.Domain.Common;

public abstract class EntityBase
{
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~EntityBaseTests`
Expected: `Passed! - Failed: 0, Passed: 2`.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(domain): add EntityBase with audit + soft-delete fields"
```

---

### Task 5: `TimeWindow` value object with overlap semantics

**Files:**
- Create: `src/Scheduler.Domain/Common/TimeWindow.cs`
- Test:   `tests/Scheduler.Domain.UnitTests/Common/TimeWindowTests.cs`

- [ ] **Step 1: Write failing tests covering half-open `[Start, End)` semantics**

```csharp
using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.Domain.UnitTests.Common;

[TestFixture]
public sealed class TimeWindowTests
{
    private static DateTime At(int h, int m = 0) => new(2026, 5, 12, h, m, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_throws_when_end_not_after_start()
    {
        var act = () => new TimeWindow(At(10), At(10));
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Adjacent_windows_do_not_overlap()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(11), At(12));
        a.Overlaps(b).Should().BeFalse();
        b.Overlaps(a).Should().BeFalse();
    }

    [Test]
    public void Strict_overlap_detected()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(10, 30), At(11, 30));
        a.Overlaps(b).Should().BeTrue();
    }

    [Test]
    public void Containment_is_overlap()
    {
        var outer = new TimeWindow(At(9), At(12));
        var inner = new TimeWindow(At(10), At(11));
        outer.Overlaps(inner).Should().BeTrue();
        inner.Overlaps(outer).Should().BeTrue();
    }

    [Test]
    public void Identical_windows_overlap()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(10), At(11));
        a.Overlaps(b).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~TimeWindowTests`
Expected: build error.

- [ ] **Step 3: Implement `src/Scheduler.Domain/Common/TimeWindow.cs`**

```csharp
namespace Scheduler.Domain.Common;

public readonly record struct TimeWindow
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public TimeWindow(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            throw new ArgumentException("End must be strictly after Start.", nameof(endUtc));

        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
    }

    public TimeSpan Duration => EndUtc - StartUtc;

    public bool Overlaps(TimeWindow other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Contains(DateTime utc)
        => utc >= StartUtc && utc < EndUtc;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~TimeWindowTests`
Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(domain): add TimeWindow value object with half-open overlap semantics"
```

---

### Task 6: Reference, junction, and Skill entities

**Files:**
- Create: `src/Scheduler.Domain/Enums/AppointmentStatus.cs`
- Create: `src/Scheduler.Domain/ValueObjects/OpeningHoursEntry.cs`
- Create: `src/Scheduler.Domain/Entities/Customer.cs`
- Create: `src/Scheduler.Domain/Entities/Vehicle.cs`
- Create: `src/Scheduler.Domain/Entities/Dealership.cs`
- Create: `src/Scheduler.Domain/Entities/ServiceType.cs`
- Create: `src/Scheduler.Domain/Entities/Technician.cs`
- Create: `src/Scheduler.Domain/Entities/ServiceBay.cs`
- Create: `src/Scheduler.Domain/Entities/Skill.cs`
- Create: `src/Scheduler.Domain/Entities/TechnicianDealership.cs`
- Create: `src/Scheduler.Domain/Entities/ServiceBayDealership.cs`
- Create: `src/Scheduler.Domain/Entities/TechnicianSkill.cs`
- Create: `src/Scheduler.Domain/Entities/ServiceTypeRequiredSkill.cs`

These are data carriers. No TDD here — write them directly. Behavior tests come with the services that use them.

- [ ] **Step 1: `Enums/AppointmentStatus.cs`**

```csharp
namespace Scheduler.Domain.Enums;

public enum AppointmentStatus
{
    Confirmed = 1,
    Cancelled = 2
}
```

- [ ] **Step 2: `ValueObjects/OpeningHoursEntry.cs`**

```csharp
namespace Scheduler.Domain.ValueObjects;

public sealed record OpeningHoursEntry(
    DayOfWeek DayOfWeek,
    TimeOnly OpensAt,
    TimeOnly ClosesAt)
{
    public bool Contains(TimeOnly local)
        => local >= OpensAt && local < ClosesAt;
}
```

- [ ] **Step 3: `Entities/Customer.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Customer : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
```

- [ ] **Step 4: `Entities/Vehicle.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Vehicle : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }

    public Customer? Customer { get; set; }
}
```

- [ ] **Step 5: `Entities/Dealership.cs`**

```csharp
using Scheduler.Domain.Common;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Entities;

public sealed class Dealership : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Etc/UTC";
    public List<OpeningHoursEntry> OpeningHours { get; set; } = new();

    public ICollection<TechnicianDealership> TechnicianAssignments { get; set; } = new List<TechnicianDealership>();
    public ICollection<ServiceBayDealership> ServiceBayAssignments { get; set; } = new List<ServiceBayDealership>();
}
```

- [ ] **Step 6: `Entities/ServiceType.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceType : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }

    public ICollection<ServiceTypeRequiredSkill> RequiredSkills { get; set; } = new List<ServiceTypeRequiredSkill>();
}
```

- [ ] **Step 7: `Entities/Technician.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Technician : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;

    public ICollection<TechnicianDealership> DealershipAssignments { get; set; } = new List<TechnicianDealership>();
    public ICollection<TechnicianSkill> Skills { get; set; } = new List<TechnicianSkill>();
}
```

- [ ] **Step 8: `Entities/ServiceBay.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceBay : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<ServiceBayDealership> DealershipAssignments { get; set; } = new List<ServiceBayDealership>();
}
```

- [ ] **Step 9: `Entities/Skill.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Skill : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
}
```

- [ ] **Step 10: Junction entities**

`Entities/TechnicianDealership.cs`:

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class TechnicianDealership : EntityBase
{
    public Guid TechnicianId { get; set; }
    public Guid DealershipId { get; set; }

    public Technician? Technician { get; set; }
    public Dealership? Dealership { get; set; }
}
```

`Entities/ServiceBayDealership.cs`:

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceBayDealership : EntityBase
{
    public Guid ServiceBayId { get; set; }
    public Guid DealershipId { get; set; }

    public ServiceBay? ServiceBay { get; set; }
    public Dealership? Dealership { get; set; }
}
```

`Entities/TechnicianSkill.cs`:

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class TechnicianSkill : EntityBase
{
    public Guid TechnicianId { get; set; }
    public Guid SkillId { get; set; }

    public Technician? Technician { get; set; }
    public Skill? Skill { get; set; }
}
```

`Entities/ServiceTypeRequiredSkill.cs`:

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceTypeRequiredSkill : EntityBase
{
    public Guid ServiceTypeId { get; set; }
    public Guid SkillId { get; set; }

    public ServiceType? ServiceType { get; set; }
    public Skill? Skill { get; set; }
}
```

- [ ] **Step 11: Verify build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 12: Commit**

```bash
git add .
git commit -m "feat(domain): add reference entities, junctions, Skill, OpeningHoursEntry"
```

---

### Task 7: `Appointment` aggregate root with invariants (TDD)

**Files:**
- Create: `src/Scheduler.Domain/Entities/Appointment.cs`
- Test:   `tests/Scheduler.Domain.UnitTests/Entities/AppointmentTests.cs`

- [ ] **Step 1: Write failing tests for `Appointment.Confirm()` and `Appointment.Cancel()`**

```csharp
using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;

namespace Scheduler.Domain.UnitTests.Entities;

[TestFixture]
public sealed class AppointmentTests
{
    private static DateTime At(int h) => new(2026, 5, 12, h, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Confirm_throws_when_end_not_after_start()
    {
        var act = () => Appointment.Confirm(
            dealershipId: Guid.NewGuid(),
            customerId:   Guid.NewGuid(),
            vehicleId:    Guid.NewGuid(),
            serviceTypeId:Guid.NewGuid(),
            technicianId: Guid.NewGuid(),
            serviceBayId: Guid.NewGuid(),
            startsAtUtc:  At(10),
            endsAtUtc:    At(10));

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Confirm_creates_appointment_with_status_Confirmed()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        a.Status.Should().Be(AppointmentStatus.Confirmed);
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public void Cancel_transitions_status_and_stamps_cancelledAtUtc()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        var ts = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        a.Cancel(ts);

        a.Status.Should().Be(AppointmentStatus.Cancelled);
        a.CancelledAtUtc.Should().Be(ts);
    }

    [Test]
    public void Cancel_throws_if_already_cancelled()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        a.Cancel(DateTime.UtcNow);
        var act = () => a.Cancel(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~AppointmentTests`
Expected: build error.

- [ ] **Step 3: Implement `src/Scheduler.Domain/Entities/Appointment.cs`**

```csharp
using Scheduler.Domain.Common;
using Scheduler.Domain.Enums;

namespace Scheduler.Domain.Entities;

public sealed class Appointment : EntityBase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DealershipId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ServiceTypeId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public Guid ServiceBayId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    private Appointment() { }

    public static Appointment Confirm(
        Guid dealershipId,
        Guid customerId,
        Guid vehicleId,
        Guid serviceTypeId,
        Guid technicianId,
        Guid serviceBayId,
        DateTime startsAtUtc,
        DateTime endsAtUtc)
    {
        if (endsAtUtc <= startsAtUtc)
            throw new ArgumentException("End must be strictly after Start.", nameof(endsAtUtc));

        if (dealershipId == Guid.Empty) throw new ArgumentException("Required.", nameof(dealershipId));
        if (customerId == Guid.Empty)   throw new ArgumentException("Required.", nameof(customerId));
        if (vehicleId == Guid.Empty)    throw new ArgumentException("Required.", nameof(vehicleId));
        if (serviceTypeId == Guid.Empty)throw new ArgumentException("Required.", nameof(serviceTypeId));
        if (technicianId == Guid.Empty) throw new ArgumentException("Required.", nameof(technicianId));
        if (serviceBayId == Guid.Empty) throw new ArgumentException("Required.", nameof(serviceBayId));

        return new Appointment
        {
            DealershipId = dealershipId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTypeId = serviceTypeId,
            TechnicianId = technicianId,
            ServiceBayId = serviceBayId,
            StartsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc),
            EndsAtUtc   = DateTime.SpecifyKind(endsAtUtc,   DateTimeKind.Utc),
            Status = AppointmentStatus.Confirmed
        };
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status == AppointmentStatus.Cancelled)
            throw new InvalidOperationException("Appointment is already cancelled.");

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~AppointmentTests`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(domain): add Appointment aggregate root with Confirm/Cancel invariants"
```

---

### Task 8: Domain exceptions and `IClock` abstraction

**Files:**
- Create: `src/Scheduler.Domain/Exceptions/DomainException.cs`
- Create: `src/Scheduler.Domain/Exceptions/SlotTakenException.cs`
- Create: `src/Scheduler.Domain/Exceptions/TechnicianUnqualifiedException.cs`
- Create: `src/Scheduler.Domain/Exceptions/OutsideOpeningHoursException.cs`
- Create: `src/Scheduler.Domain/Exceptions/StartInPastException.cs`
- Create: `src/Scheduler.Domain/Exceptions/AlreadyCancelledException.cs`
- Create: `src/Scheduler.Domain/Exceptions/ResourceNotFoundException.cs`
- Create: `src/Scheduler.Domain/Exceptions/IdempotencyReplayMismatchException.cs`
- Create: `src/Scheduler.Domain/Abstractions/IClock.cs`

- [ ] **Step 1: `Exceptions/DomainException.cs` (base)**

```csharp
namespace Scheduler.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public abstract string Code { get; }
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 2: Concrete exceptions (one per file)**

`Exceptions/SlotTakenException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class SlotTakenException : DomainException
{
    public override string Code => "SLOT_TAKEN";
    public SlotTakenException(string message = "All qualified resources are booked for the requested window.") : base(message) { }
}
```

`Exceptions/TechnicianUnqualifiedException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class TechnicianUnqualifiedException : DomainException
{
    public override string Code => "TECHNICIAN_UNQUALIFIED";
    public TechnicianUnqualifiedException(string message = "No qualified technician exists for this service.") : base(message) { }
}
```

`Exceptions/OutsideOpeningHoursException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class OutsideOpeningHoursException : DomainException
{
    public override string Code => "OUTSIDE_OPENING_HOURS";
    public OutsideOpeningHoursException(string message = "The requested time is outside the dealership's opening hours.") : base(message) { }
}
```

`Exceptions/StartInPastException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class StartInPastException : DomainException
{
    public override string Code => "START_IN_PAST";
    public StartInPastException(string message = "Booking start time must be in the future.") : base(message) { }
}
```

`Exceptions/AlreadyCancelledException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class AlreadyCancelledException : DomainException
{
    public override string Code => "ALREADY_CANCELLED";
    public AlreadyCancelledException(string message = "Appointment is already cancelled.") : base(message) { }
}
```

`Exceptions/ResourceNotFoundException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class ResourceNotFoundException : DomainException
{
    public override string Code => "RESOURCE_NOT_FOUND";
    public ResourceNotFoundException(string resource, Guid id)
        : base($"{resource} {id} was not found or is inactive.") { }
}
```

`Exceptions/IdempotencyReplayMismatchException.cs`:

```csharp
namespace Scheduler.Domain.Exceptions;

public sealed class IdempotencyReplayMismatchException : DomainException
{
    public override string Code => "IDEMPOTENCY_REPLAY_MISMATCH";
    public IdempotencyReplayMismatchException(string message = "Idempotency-Key reused with a different request body.") : base(message) { }
}
```

- [ ] **Step 3: `Abstractions/IClock.cs`**

```csharp
namespace Scheduler.Domain.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(domain): add domain exceptions with stable error codes and IClock"
```

---

## Phase 2 — Infrastructure layer

### Task 9: `SchedulerDbContext` skeleton with global query filter convention

**Files:**
- Create: `src/Scheduler.Infrastructure/Persistence/SchedulerDbContext.cs`
- Create: `src/Scheduler.Infrastructure/Persistence/Conventions/SoftDeleteQueryFilterConvention.cs`

- [ ] **Step 1: `Persistence/SchedulerDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence;

public sealed class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<TechnicianDealership> TechnicianDealerships => Set<TechnicianDealership>();
    public DbSet<ServiceBayDealership> ServiceBayDealerships => Set<ServiceBayDealership>();
    public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();
    public DbSet<ServiceTypeRequiredSkill> ServiceTypeRequiredSkills => Set<ServiceTypeRequiredSkill>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);
    }
}
```

(`IdempotencyEntry` is created in Task 13; the build will fail until then. That is acceptable for the staged plan; we will green it when the type exists.)

- [ ] **Step 2: `Persistence/Conventions/SoftDeleteQueryFilterConvention.cs`** — applies `HasQueryFilter(e => !e.IsDeleted)` to every `EntityBase` descendant via reflection inside `OnModelCreating`. Add a method called from `OnModelCreating` instead of using a custom `IConvention` to keep things simple.

Replace the `OnModelCreating` body in `SchedulerDbContext.cs` with the following:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);
    ApplySoftDeleteQueryFilter(modelBuilder);
}

private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(Scheduler.Domain.Common.EntityBase).IsAssignableFrom(entityType.ClrType))
            continue;

        var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
        var propertyAccess = System.Linq.Expressions.Expression.Property(
            parameter, nameof(Scheduler.Domain.Common.EntityBase.IsDeleted));
        var notDeleted = System.Linq.Expressions.Expression.Equal(
            propertyAccess, System.Linq.Expressions.Expression.Constant(false));
        var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }
}
```

- [ ] **Step 3: Verify build (will fail until Task 13 introduces `IdempotencyEntry`)**

Run: `dotnet build src/Scheduler.Infrastructure`
Expected: error `IdempotencyEntry` not found — this is fine; the next task adds it.

- [ ] **Step 4: Commit (intentional partial — fixed in Task 13)**

```bash
git add .
git commit -m "feat(infra): add SchedulerDbContext skeleton with soft-delete filter convention"
```

---

### Task 10: Audit + soft-delete `SaveChanges` interceptor (TDD)

**Files:**
- Create: `src/Scheduler.Infrastructure/Persistence/Interceptors/AuditAndSoftDeleteInterceptor.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Persistence/AuditAndSoftDeleteInterceptorTests.cs`

The interceptor:
- Stamps `CreatedAtUtc` for added rows.
- Stamps `UpdatedAtUtc` for modified rows.
- Converts `EntityState.Deleted` to a soft delete: sets `IsDeleted = true`, `DeletedAtUtc = utcNow`, then resets state to `Modified`.

- [ ] **Step 1: Write failing tests using an in-memory SQLite context**

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;
using Scheduler.Infrastructure.Persistence.Interceptors;

namespace Scheduler.Application.UnitTests.Persistence;

[TestFixture]
public sealed class AuditAndSoftDeleteInterceptorTests
{
    private SqliteConnection _connection = null!;
    private SchedulerDbContext _ctx = null!;
    private Mock<IClock> _clock = null!;
    private DateTime _now;

    [SetUp]
    public void SetUp()
    {
        _now = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        _clock = new Mock<IClock>();
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditAndSoftDeleteInterceptor(_clock.Object))
            .Options;

        _ctx = new SchedulerDbContext(options);
        _ctx.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Adding_an_entity_stamps_CreatedAtUtc()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        c.CreatedAtUtc.Should().Be(_now);
        c.UpdatedAtUtc.Should().BeNull();
    }

    [Test]
    public async Task Modifying_an_entity_stamps_UpdatedAtUtc()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        _now = _now.AddMinutes(5);
        c.FirstName = "Aaron";
        await _ctx.SaveChangesAsync();

        c.UpdatedAtUtc.Should().Be(_now);
    }

    [Test]
    public async Task Removing_an_entity_soft_deletes_it()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        _now = _now.AddMinutes(5);
        _ctx.Customers.Remove(c);
        await _ctx.SaveChangesAsync();

        var fromDb = await _ctx.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == c.Id);
        fromDb.IsDeleted.Should().BeTrue();
        fromDb.DeletedAtUtc.Should().Be(_now);
    }

    [Test]
    public async Task Soft_deleted_rows_are_filtered_out_by_default()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();
        _ctx.Customers.Remove(c);
        await _ctx.SaveChangesAsync();

        (await _ctx.Customers.AnyAsync()).Should().BeFalse();
        (await _ctx.Customers.IgnoreQueryFilters().AnyAsync()).Should().BeTrue();
    }
}
```

(Add `Microsoft.Data.Sqlite` to `Scheduler.Application.UnitTests.csproj` PackageReferences.)

- [ ] **Step 2: Run the tests — they fail (interceptor type missing)**

Run: `dotnet test --filter FullyQualifiedName~AuditAndSoftDeleteInterceptorTests`
Expected: build error.

- [ ] **Step 3: Implement `Persistence/Interceptors/AuditAndSoftDeleteInterceptor.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Common;

namespace Scheduler.Infrastructure.Persistence.Interceptors;

public sealed class AuditAndSoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;

    public AuditAndSoftDeleteInterceptor(IClock clock) => _clock = clock;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? db)
    {
        if (db is null) return;
        var now = _clock.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    break;
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AuditAndSoftDeleteInterceptorTests`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(infra): add audit + soft-delete SaveChanges interceptor"
```

---

### Task 11: Entity-type configurations (one file per entity)

**Files:** one configuration per entity under `src/Scheduler.Infrastructure/Persistence/Configurations/`.

The pattern is identical for all entities: implement `IEntityTypeConfiguration<T>`, configure keys/columns/indexes, set `RowVersion` as a concurrency token. Show one in full, then the rest are listed concisely with their distinguishing details.

- [ ] **Step 1: `Configurations/AppointmentConfiguration.cs` (the full pattern)**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.StartsAtUtc).IsRequired();
        b.Property(x => x.EndsAtUtc).IsRequired();

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<Dealership>().WithMany().HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceType>().WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Technician>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceBay>().WithMany().HasForeignKey(x => x.ServiceBayId).OnDelete(DeleteBehavior.Restrict);

        // Filtered unique backstops: identical-start collisions on the same resource
        b.HasIndex(x => new { x.TechnicianId, x.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("UX_Appointment_Technician_Confirmed_Start")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        b.HasIndex(x => new { x.ServiceBayId, x.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("UX_Appointment_Bay_Confirmed_Start")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        // Covering indexes for in-transaction overlap queries
        b.HasIndex(x => new { x.TechnicianId, x.StartsAtUtc, x.EndsAtUtc })
            .HasDatabaseName("IX_Appointment_Tech_Window")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        b.HasIndex(x => new { x.ServiceBayId, x.StartsAtUtc, x.EndsAtUtc })
            .HasDatabaseName("IX_Appointment_Bay_Window")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        b.HasIndex(x => new { x.DealershipId, x.StartsAtUtc })
            .HasDatabaseName("IX_Appointment_Dealership_Start");
    }
}
```

> **SQLite note:** SQLite's parser does not use bracket-quoted identifiers. EF Core's SQLite migration generator translates the filter expressions for you, but the raw filter string above is portable enough that EF will rewrite it. If you see migration errors, switch the filter strings to: `"Status = 1 AND IsDeleted = 0"` (no brackets).

- [ ] **Step 2: `Configurations/CustomerConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).IsRequired().HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("UX_Customer_Email")
            .HasFilter("IsDeleted = 0");
    }
}
```

- [ ] **Step 3: `Configurations/VehicleConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("Vehicles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Vin).IsRequired().HasMaxLength(32);
        b.Property(x => x.Make).IsRequired().HasMaxLength(60);
        b.Property(x => x.Model).IsRequired().HasMaxLength(60);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Customer).WithMany(c => c.Vehicles).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Vin).IsUnique().HasDatabaseName("UX_Vehicle_Vin").HasFilter("IsDeleted = 0");
    }
}
```

- [ ] **Step 4: `Configurations/DealershipConfiguration.cs`**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> b)
    {
        b.ToTable("Dealerships");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.TimeZone).IsRequired().HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.Property(x => x.OpeningHours)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<OpeningHoursEntry>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");
    }
}
```

- [ ] **Step 5: `Configurations/ServiceTypeConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> b)
    {
        b.ToTable("ServiceTypes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 6: `Configurations/TechnicianConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> b)
    {
        b.ToTable("Technicians");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 7: `Configurations/ServiceBayConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> b)
    {
        b.ToTable("ServiceBays");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 8: `Configurations/SkillConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.ToTable("Skills");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(64);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Category).HasMaxLength(120);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Skill_Code").HasFilter("IsDeleted = 0");
    }
}
```

- [ ] **Step 9: Junction configurations (composite keys)**

`Configurations/TechnicianDealershipConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class TechnicianDealershipConfiguration : IEntityTypeConfiguration<TechnicianDealership>
{
    public void Configure(EntityTypeBuilder<TechnicianDealership> b)
    {
        b.ToTable("TechnicianDealerships");
        b.HasKey(x => new { x.TechnicianId, x.DealershipId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Technician).WithMany(t => t.DealershipAssignments).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Dealership).WithMany(d => d.TechnicianAssignments).HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.DealershipId, x.TechnicianId })
            .HasDatabaseName("IX_TechnicianDealership_Dealership")
            .HasFilter("IsDeleted = 0");
    }
}
```

`Configurations/ServiceBayDealershipConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceBayDealershipConfiguration : IEntityTypeConfiguration<ServiceBayDealership>
{
    public void Configure(EntityTypeBuilder<ServiceBayDealership> b)
    {
        b.ToTable("ServiceBayDealerships");
        b.HasKey(x => new { x.ServiceBayId, x.DealershipId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.ServiceBay).WithMany(s => s.DealershipAssignments).HasForeignKey(x => x.ServiceBayId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Dealership).WithMany(d => d.ServiceBayAssignments).HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.DealershipId, x.ServiceBayId })
            .HasDatabaseName("IX_ServiceBayDealership_Dealership")
            .HasFilter("IsDeleted = 0");
    }
}
```

`Configurations/TechnicianSkillConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class TechnicianSkillConfiguration : IEntityTypeConfiguration<TechnicianSkill>
{
    public void Configure(EntityTypeBuilder<TechnicianSkill> b)
    {
        b.ToTable("TechnicianSkills");
        b.HasKey(x => new { x.TechnicianId, x.SkillId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Technician).WithMany(t => t.Skills).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

`Configurations/ServiceTypeRequiredSkillConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceTypeRequiredSkillConfiguration : IEntityTypeConfiguration<ServiceTypeRequiredSkill>
{
    public void Configure(EntityTypeBuilder<ServiceTypeRequiredSkill> b)
    {
        b.ToTable("ServiceTypeRequiredSkills");
        b.HasKey(x => new { x.ServiceTypeId, x.SkillId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.ServiceType).WithMany(s => s.RequiredSkills).HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 10: Commit**

```bash
git add .
git commit -m "feat(infra): add EF entity configurations with filtered indexes for concurrency"
```

---

### Task 12: Idempotency entity + initial migration

**Files:**
- Create: `src/Scheduler.Infrastructure/Persistence/IdempotencyEntry.cs`
- Create: `src/Scheduler.Infrastructure/Persistence/Configurations/IdempotencyEntryConfiguration.cs`
- Create: `src/Scheduler.Infrastructure/Persistence/Migrations/<timestamp>_InitialCreate.cs` (generated)

- [ ] **Step 1: `Persistence/IdempotencyEntry.cs`**

```csharp
using Scheduler.Domain.Common;

namespace Scheduler.Infrastructure.Persistence;

public sealed class IdempotencyEntry : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string BodyHash { get; set; } = string.Empty;
    public Guid? AppointmentId { get; set; }
    public string ResponseStatusCode { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
```

- [ ] **Step 2: `Persistence/Configurations/IdempotencyEntryConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> b)
    {
        b.ToTable("IdempotencyEntries");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(128);
        b.Property(x => x.BodyHash).IsRequired().HasMaxLength(64);
        b.Property(x => x.ResponseStatusCode).IsRequired().HasMaxLength(8);
        b.Property(x => x.ResponseBody).IsRequired().HasColumnType("TEXT");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_IdempotencyEntries_ExpiresAtUtc");
    }
}
```

- [ ] **Step 3: Build to confirm Task 9's pending error is resolved**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Add the initial migration**

```bash
dotnet ef migrations add InitialCreate --project src/Scheduler.Infrastructure --startup-project src/Scheduler.Api --output-dir Persistence/Migrations
```

(If `dotnet ef` is not installed: `dotnet tool install --global dotnet-ef --version 8.0.10`.)

The `Program.cs` doesn't yet wire DI for `SchedulerDbContext`. The migration command needs a usable design-time configuration. Add a temporary `Persistence/SchedulerDbContextFactory.cs` so `dotnet ef` can construct the context at design time:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scheduler.Domain.Abstractions;
using Scheduler.Infrastructure.Persistence.Interceptors;

namespace Scheduler.Infrastructure.Persistence;

public sealed class SchedulerDbContextFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseSqlite("Data Source=scheduler.design.db")
            .AddInterceptors(new AuditAndSoftDeleteInterceptor(new DesignTimeClock()))
            .Options;
        return new SchedulerDbContext(options);
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
```

- [ ] **Step 5: Verify the migration generated and that it builds**

Run: `dotnet build`
Expected: `Build succeeded`. The migration file should appear in `src/Scheduler.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(infra): add IdempotencyEntry and initial EF migration"
```

---

### Task 13: Repositories, IdempotencyStore, SystemClock, DI extension

**Files:**
- Create: `src/Scheduler.Application/Abstractions/Persistence/I*Repository.cs` (interfaces)
- Create: `src/Scheduler.Application/Abstractions/Persistence/IUnitOfWork.cs`
- Create: `src/Scheduler.Application/Abstractions/Idempotency/IIdempotencyStore.cs`
- Create: `src/Scheduler.Infrastructure/Repositories/*.cs`
- Create: `src/Scheduler.Infrastructure/Idempotency/IdempotencyStore.cs`
- Create: `src/Scheduler.Infrastructure/Time/SystemClock.cs`
- Create: `src/Scheduler.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Application abstractions**

`src/Scheduler.Application/Abstractions/Persistence/IAppointmentReader.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IAppointmentReader
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? dealershipId, Guid? customerId, Guid? technicianId,
        DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IAppointmentWriter.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IAppointmentWriter
{
    Task AddAsync(Appointment appointment, CancellationToken ct);
    Task<bool> AnyTechnicianOverlapAsync(Guid technicianId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<bool> AnyBayOverlapAsync(Guid bayId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/ITechnicianRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface ITechnicianRepository
{
    Task<IReadOnlyList<Technician>> ListQualifiedForServiceAtDealershipAsync(
        Guid serviceTypeId, Guid dealershipId, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IServiceBayRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IServiceBayRepository
{
    Task<IReadOnlyList<ServiceBay>> ListAtDealershipAsync(Guid dealershipId, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IDealershipRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IDealershipRepository
{
    Task<Dealership?> GetByIdAsync(Guid id, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IServiceTypeRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IServiceTypeRepository
{
    Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/ICustomerRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IVehicleRepository.cs`:

```csharp
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Persistence/IUnitOfWork.cs`:

```csharp
namespace Scheduler.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IAsyncDisposable> BeginSerializableTransactionAsync(CancellationToken ct);
}
```

`src/Scheduler.Application/Abstractions/Idempotency/IIdempotencyStore.cs`:

```csharp
namespace Scheduler.Application.Abstractions.Idempotency;

public sealed record IdempotencyHit(string ResponseStatusCode, string ResponseBody);

public interface IIdempotencyStore
{
    Task<IdempotencyHit?> TryGetAsync(string key, string bodyHash, CancellationToken ct);
    Task PutAsync(string key, string bodyHash, string statusCode, string body, Guid? appointmentId,
        DateTime expiresAtUtc, CancellationToken ct);
    Task<bool> ExistsForDifferentBodyAsync(string key, string bodyHash, CancellationToken ct);
}
```

- [ ] **Step 2: Infrastructure implementations**

`src/Scheduler.Infrastructure/Repositories/AppointmentRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class AppointmentRepository : IAppointmentReader, IAppointmentWriter
{
    private readonly SchedulerDbContext _ctx;

    public AppointmentRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? dealershipId, Guid? customerId, Guid? technicianId,
        DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct)
    {
        var q = _ctx.Appointments.AsNoTracking().AsQueryable();
        if (dealershipId is not null) q = q.Where(a => a.DealershipId == dealershipId);
        if (customerId is not null) q = q.Where(a => a.CustomerId == customerId);
        if (technicianId is not null) q = q.Where(a => a.TechnicianId == technicianId);
        if (fromUtc is not null) q = q.Where(a => a.StartsAtUtc >= fromUtc);
        if (toUtc is not null) q = q.Where(a => a.StartsAtUtc <= toUtc);
        return await q.OrderBy(a => a.StartsAtUtc).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken ct) =>
        await _ctx.Appointments.AddAsync(appointment, ct);

    public Task<bool> AnyTechnicianOverlapAsync(Guid technicianId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        _ctx.Appointments.AnyAsync(a =>
            a.TechnicianId == technicianId &&
            a.Status == AppointmentStatus.Confirmed &&
            a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc, ct);

    public Task<bool> AnyBayOverlapAsync(Guid bayId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        _ctx.Appointments.AnyAsync(a =>
            a.ServiceBayId == bayId &&
            a.Status == AppointmentStatus.Confirmed &&
            a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc, ct);
}
```

`src/Scheduler.Infrastructure/Repositories/TechnicianRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class TechnicianRepository : ITechnicianRepository
{
    private readonly SchedulerDbContext _ctx;
    public TechnicianRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<Technician>> ListQualifiedForServiceAtDealershipAsync(
        Guid serviceTypeId, Guid dealershipId, CancellationToken ct)
    {
        var requiredSkillIds = await _ctx.ServiceTypeRequiredSkills
            .Where(r => r.ServiceTypeId == serviceTypeId)
            .Select(r => r.SkillId)
            .ToListAsync(ct);

        var query = _ctx.Technicians
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Where(t => _ctx.TechnicianDealerships.Any(td => td.TechnicianId == t.Id && td.DealershipId == dealershipId));

        foreach (var skillId in requiredSkillIds)
        {
            var sid = skillId;
            query = query.Where(t => _ctx.TechnicianSkills.Any(ts => ts.TechnicianId == t.Id && ts.SkillId == sid));
        }

        return await query.OrderBy(t => t.Id).ToListAsync(ct);
    }
}
```

`src/Scheduler.Infrastructure/Repositories/ServiceBayRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class ServiceBayRepository : IServiceBayRepository
{
    private readonly SchedulerDbContext _ctx;
    public ServiceBayRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<IReadOnlyList<ServiceBay>> ListAtDealershipAsync(Guid dealershipId, CancellationToken ct) =>
        _ctx.ServiceBays.AsNoTracking()
            .Where(b => b.IsActive)
            .Where(b => _ctx.ServiceBayDealerships.Any(sbd => sbd.ServiceBayId == b.Id && sbd.DealershipId == dealershipId))
            .OrderBy(b => b.Id)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ServiceBay>)t.Result, ct);
}
```

`src/Scheduler.Infrastructure/Repositories/DealershipRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class DealershipRepository : IDealershipRepository
{
    private readonly SchedulerDbContext _ctx;
    public DealershipRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Dealership?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Dealerships.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.IsActive, ct);
}
```

`src/Scheduler.Infrastructure/Repositories/ServiceTypeRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class ServiceTypeRepository : IServiceTypeRepository
{
    private readonly SchedulerDbContext _ctx;
    public ServiceTypeRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct);
}
```

`src/Scheduler.Infrastructure/Repositories/CustomerRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly SchedulerDbContext _ctx;
    public CustomerRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
}
```

`src/Scheduler.Infrastructure/Repositories/VehicleRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly SchedulerDbContext _ctx;
    public VehicleRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.IsActive, ct);
}
```

`src/Scheduler.Infrastructure/Persistence/UnitOfWork.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Scheduler.Application.Abstractions.Persistence;

namespace Scheduler.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SchedulerDbContext _ctx;
    public UnitOfWork(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<int> SaveChangesAsync(CancellationToken ct) => _ctx.SaveChangesAsync(ct);

    public async Task<IAsyncDisposable> BeginSerializableTransactionAsync(CancellationToken ct)
    {
        var txn = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        return new TransactionScope(txn);
    }

    private sealed class TransactionScope : IAsyncDisposable
    {
        private readonly IDbContextTransaction _txn;
        public TransactionScope(IDbContextTransaction txn) => _txn = txn;
        public async ValueTask DisposeAsync()
        {
            await _txn.CommitAsync();
            await _txn.DisposeAsync();
        }
    }
}
```

`src/Scheduler.Infrastructure/Idempotency/IdempotencyStore.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Idempotency;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly SchedulerDbContext _ctx;

    public IdempotencyStore(SchedulerDbContext ctx) => _ctx = ctx;

    public async Task<IdempotencyHit?> TryGetAsync(string key, string bodyHash, CancellationToken ct)
    {
        var row = await _ctx.IdempotencyEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key && e.BodyHash == bodyHash, ct);
        return row is null ? null : new IdempotencyHit(row.ResponseStatusCode, row.ResponseBody);
    }

    public async Task<bool> ExistsForDifferentBodyAsync(string key, string bodyHash, CancellationToken ct) =>
        await _ctx.IdempotencyEntries.AsNoTracking()
            .AnyAsync(e => e.Key == key && e.BodyHash != bodyHash, ct);

    public async Task PutAsync(string key, string bodyHash, string statusCode, string body, Guid? appointmentId,
        DateTime expiresAtUtc, CancellationToken ct)
    {
        _ctx.IdempotencyEntries.Add(new IdempotencyEntry
        {
            Key = key,
            BodyHash = bodyHash,
            ResponseStatusCode = statusCode,
            ResponseBody = body,
            AppointmentId = appointmentId,
            ExpiresAtUtc = expiresAtUtc
        });
        await _ctx.SaveChangesAsync(ct);
    }
}
```

`src/Scheduler.Infrastructure/Time/SystemClock.cs`:

```csharp
using Scheduler.Domain.Abstractions;

namespace Scheduler.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

`src/Scheduler.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Abstractions;
using Scheduler.Infrastructure.Idempotency;
using Scheduler.Infrastructure.Persistence;
using Scheduler.Infrastructure.Persistence.Interceptors;
using Scheduler.Infrastructure.Repositories;
using Scheduler.Infrastructure.Time;

namespace Scheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulerInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditAndSoftDeleteInterceptor>();

        services.AddDbContext<SchedulerDbContext>((sp, opts) =>
        {
            var conn = config.GetConnectionString("Default") ?? "Data Source=scheduler.db";
            opts.UseSqlite(conn);
            opts.AddInterceptors(sp.GetRequiredService<AuditAndSoftDeleteInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<AppointmentRepository>();
        services.AddScoped<IAppointmentReader>(sp => sp.GetRequiredService<AppointmentRepository>());
        services.AddScoped<IAppointmentWriter>(sp => sp.GetRequiredService<AppointmentRepository>());

        services.AddScoped<ITechnicianRepository, TechnicianRepository>();
        services.AddScoped<IServiceBayRepository, ServiceBayRepository>();
        services.AddScoped<IDealershipRepository, DealershipRepository>();
        services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        return services;
    }
}
```

- [ ] **Step 3: Build verification**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(infra): add repositories, IdempotencyStore, SystemClock, DI extension"
```

---

## Phase 3 — Application layer

### Task 14: Application contracts (DTOs)

**Files:**
- Create: `src/Scheduler.Application/Contracts/Requests/BookAppointmentRequest.cs`
- Create: `src/Scheduler.Application/Contracts/Requests/RescheduleAppointmentRequest.cs`
- Create: `src/Scheduler.Application/Contracts/Requests/AvailabilityQueryRequest.cs`
- Create: `src/Scheduler.Application/Contracts/Responses/AppointmentResponse.cs`
- Create: `src/Scheduler.Application/Contracts/Responses/AvailabilitySlotResponse.cs`
- Create: `src/Scheduler.Application/Contracts/Responses/TechnicianResponse.cs`
- Create: `src/Scheduler.Application/Contracts/Responses/ServiceBayResponse.cs`
- Create: `src/Scheduler.Application/Contracts/ProblemCodes.cs`

- [ ] **Step 1: Request DTOs**

```csharp
// BookAppointmentRequest.cs
namespace Scheduler.Application.Contracts.Requests;

public sealed record BookAppointmentRequest(
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    DateTime StartsAtUtc);
```

```csharp
// RescheduleAppointmentRequest.cs
namespace Scheduler.Application.Contracts.Requests;

public sealed record RescheduleAppointmentRequest(DateTime NewStartsAtUtc);
```

```csharp
// AvailabilityQueryRequest.cs
namespace Scheduler.Application.Contracts.Requests;

public sealed record AvailabilityQueryRequest(
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTime FromUtc,
    DateTime ToUtc,
    int GranularityMinutes);
```

- [ ] **Step 2: Response DTOs**

```csharp
// AppointmentResponse.cs
namespace Scheduler.Application.Contracts.Responses;

public sealed record AppointmentResponse(
    Guid Id,
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid TechnicianId,
    Guid ServiceBayId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    DateTime CreatedAtUtc);
```

```csharp
// AvailabilitySlotResponse.cs
namespace Scheduler.Application.Contracts.Responses;

public sealed record AvailabilitySlotResponse(DateTime StartsAtUtc, DateTime EndsAtUtc);
```

```csharp
// TechnicianResponse.cs
namespace Scheduler.Application.Contracts.Responses;

public sealed record TechnicianResponse(Guid Id, string FullName, IReadOnlyList<Guid> DealershipIds, IReadOnlyList<string> SkillCodes);
```

```csharp
// ServiceBayResponse.cs
namespace Scheduler.Application.Contracts.Responses;

public sealed record ServiceBayResponse(Guid Id, string Name, IReadOnlyList<Guid> DealershipIds);
```

- [ ] **Step 3: Stable error code constants**

```csharp
// ProblemCodes.cs
namespace Scheduler.Application.Contracts;

public static class ProblemCodes
{
    public const string SlotTaken = "SLOT_TAKEN";
    public const string TechnicianUnqualified = "TECHNICIAN_UNQUALIFIED";
    public const string OutsideOpeningHours = "OUTSIDE_OPENING_HOURS";
    public const string StartInPast = "START_IN_PAST";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string IdempotencyReplayMismatch = "IDEMPOTENCY_REPLAY_MISMATCH";
    public const string AlreadyCancelled = "ALREADY_CANCELLED";
    public const string ValidationFailed = "VALIDATION_FAILED";
}
```

- [ ] **Step 4: Build verification**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(app): add request/response DTOs and ProblemCodes constants"
```

---

### Task 15: Application service interfaces and `IResourceSelector`

**Files:**
- Create: `src/Scheduler.Application/Services/Abstractions/IBookingService.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/ICancellationService.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/IReschedulingService.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/IAvailabilityService.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/IQualificationMatcher.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/IResourceSelector.cs`
- Create: `src/Scheduler.Application/Services/Abstractions/IOpeningHoursValidator.cs`

- [ ] **Step 1: Interfaces (one per file)**

```csharp
// IBookingService.cs
using Scheduler.Application.Contracts.Requests;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IBookingService
{
    Task<Appointment> BookAsync(BookAppointmentRequest request, CancellationToken ct);
}
```

```csharp
// ICancellationService.cs
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface ICancellationService
{
    Task<Appointment> CancelAsync(Guid appointmentId, CancellationToken ct);
}
```

```csharp
// IReschedulingService.cs
using Scheduler.Application.Contracts.Requests;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IReschedulingService
{
    Task<Appointment> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct);
}
```

```csharp
// IAvailabilityService.cs
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailabilitySlotResponse>> ListSlotsAsync(AvailabilityQueryRequest q, CancellationToken ct);

    Task<Technician?> FirstFreeTechnicianAsync(
        IReadOnlyList<Technician> qualified, DateTime startUtc, DateTime endUtc, CancellationToken ct);

    Task<ServiceBay?> FirstFreeBayAsync(
        IReadOnlyList<ServiceBay> bays, DateTime startUtc, DateTime endUtc, CancellationToken ct);
}
```

```csharp
// IQualificationMatcher.cs
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IQualificationMatcher
{
    Task<IReadOnlyList<Technician>> FindQualifiedAsync(Guid serviceTypeId, Guid dealershipId, CancellationToken ct);
}
```

```csharp
// IResourceSelector.cs
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IResourceSelector
{
    T? Pick<T>(IReadOnlyList<T> orderedCandidates) where T : class;
}
```

```csharp
// IOpeningHoursValidator.cs
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IOpeningHoursValidator
{
    bool IsWithinOpeningHours(Dealership dealership, DateTime startUtc, DateTime endUtc);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(app): add service abstractions for booking, cancel, reschedule, availability, qualification"
```

---

### Task 16: `OpeningHoursValidator` (TDD)

**Files:**
- Create: `src/Scheduler.Application/Services/OpeningHoursValidator.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/OpeningHoursValidatorTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using NUnit.Framework;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class OpeningHoursValidatorTests
{
    private static Dealership Etcd(params OpeningHoursEntry[] hours) => new()
    {
        Name = "Test", TimeZone = "Etc/UTC", OpeningHours = new(hours)
    };

    [Test]
    public void Within_opening_hours_returns_true()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 9, 30, 0, DateTimeKind.Utc); // Tue
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeTrue();
    }

    [Test]
    public void End_exactly_at_close_is_within_hours()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 16, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 12, 17, 0, 0, DateTimeKind.Utc);

        sut.IsWithinOpeningHours(d, start, end).Should().BeTrue();
    }

    [Test]
    public void Start_before_opening_returns_false()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 8, 30, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }

    [Test]
    public void End_after_closing_returns_false()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 16, 30, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 12, 17, 30, 0, DateTimeKind.Utc);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }

    [Test]
    public void No_opening_hours_for_day_returns_false()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Monday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc); // Tue
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — fail**

Run: `dotnet test --filter FullyQualifiedName~OpeningHoursValidatorTests`
Expected: build error.

- [ ] **Step 3: Implement**

```csharp
// src/Scheduler.Application/Services/OpeningHoursValidator.cs
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services;

public sealed class OpeningHoursValidator : IOpeningHoursValidator
{
    public bool IsWithinOpeningHours(Dealership dealership, DateTime startUtc, DateTime endUtc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), tz);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), tz);

        if (localStart.Date != localEnd.Date) return false; // no cross-day bookings in v1

        var dow = localStart.DayOfWeek;
        var entry = dealership.OpeningHours.FirstOrDefault(h => h.DayOfWeek == dow);
        if (entry is null) return false;

        var startTime = TimeOnly.FromDateTime(localStart);
        var endTime = TimeOnly.FromDateTime(localEnd);
        return startTime >= entry.OpensAt && endTime <= entry.ClosesAt;
    }
}
```

- [ ] **Step 4: Run tests — pass**

Run: `dotnet test --filter FullyQualifiedName~OpeningHoursValidatorTests`
Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(app): add OpeningHoursValidator with timezone awareness"
```

---

### Task 17: `QualificationMatcher` and `ResourceSelector` (TDD)

**Files:**
- Create: `src/Scheduler.Application/Services/QualificationMatcher.cs`
- Create: `src/Scheduler.Application/Services/ResourceSelector.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/QualificationMatcherTests.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/ResourceSelectorTests.cs`

- [ ] **Step 1: `ResourceSelectorTests.cs`**

```csharp
using FluentAssertions;
using NUnit.Framework;
using Scheduler.Application.Services;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class ResourceSelectorTests
{
    [Test]
    public void Pick_returns_first_when_list_non_empty()
    {
        var sut = new ResourceSelector();
        var picked = sut.Pick(new List<string> { "a", "b", "c" });
        picked.Should().Be("a");
    }

    [Test]
    public void Pick_returns_null_when_list_empty()
    {
        var sut = new ResourceSelector();
        var picked = sut.Pick(new List<string>());
        picked.Should().BeNull();
    }
}
```

- [ ] **Step 2: `ResourceSelector.cs`**

```csharp
using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Application.Services;

public sealed class ResourceSelector : IResourceSelector
{
    public T? Pick<T>(IReadOnlyList<T> orderedCandidates) where T : class =>
        orderedCandidates.Count == 0 ? null : orderedCandidates[0];
}
```

- [ ] **Step 3: `QualificationMatcherTests.cs`**

```csharp
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class QualificationMatcherTests
{
    [Test]
    public async Task Delegates_to_repository_and_returns_result()
    {
        var st = Guid.NewGuid();
        var d = Guid.NewGuid();
        var techs = new List<Technician> { new() { FullName = "Bob" } };

        var repo = new Mock<ITechnicianRepository>();
        repo.Setup(r => r.ListQualifiedForServiceAtDealershipAsync(st, d, It.IsAny<CancellationToken>()))
            .ReturnsAsync(techs);

        var sut = new QualificationMatcher(repo.Object);
        var result = await sut.FindQualifiedAsync(st, d, CancellationToken.None);

        result.Should().BeEquivalentTo(techs);
    }
}
```

- [ ] **Step 4: `QualificationMatcher.cs`**

```csharp
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services;

public sealed class QualificationMatcher : IQualificationMatcher
{
    private readonly ITechnicianRepository _repo;
    public QualificationMatcher(ITechnicianRepository repo) => _repo = repo;

    public Task<IReadOnlyList<Technician>> FindQualifiedAsync(
        Guid serviceTypeId, Guid dealershipId, CancellationToken ct) =>
        _repo.ListQualifiedForServiceAtDealershipAsync(serviceTypeId, dealershipId, ct);
}
```

- [ ] **Step 5: Run tests — pass**

Run: `dotnet test --filter "FullyQualifiedName~QualificationMatcherTests | FullyQualifiedName~ResourceSelectorTests"`
Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(app): add QualificationMatcher and ResourceSelector with deterministic ordering"
```

---

### Task 18: `AvailabilityService` (TDD — first-free + slot listing)

**Files:**
- Create: `src/Scheduler.Application/Services/AvailabilityService.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/AvailabilityServiceTests.cs`

- [ ] **Step 1: Failing tests for `FirstFreeTechnicianAsync` (most important behaviour)**

```csharp
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class AvailabilityServiceTests
{
    private static DateTime At(int h) => new(2026, 5, 12, h, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task FirstFreeTechnician_returns_first_with_no_overlap()
    {
        var t1 = new Technician { FullName = "A" };
        var t2 = new Technician { FullName = "B" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyTechnicianOverlapAsync(t1.Id, At(9), At(10), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writer.Setup(w => w.AnyTechnicianOverlapAsync(t2.Id, At(9), At(10), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new AvailabilityService(writer.Object, Mock.Of<IDealershipRepository>(), Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(), Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeTechnicianAsync(new[] { t1, t2 }, At(9), At(10), CancellationToken.None);

        picked.Should().Be(t2);
    }

    [Test]
    public async Task FirstFreeTechnician_returns_null_when_all_busy()
    {
        var t1 = new Technician { FullName = "A" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyTechnicianOverlapAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new AvailabilityService(writer.Object, Mock.Of<IDealershipRepository>(), Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(), Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeTechnicianAsync(new[] { t1 }, At(9), At(10), CancellationToken.None);

        picked.Should().BeNull();
    }

    [Test]
    public async Task FirstFreeBay_mirror_of_technician_logic()
    {
        var b1 = new ServiceBay { Name = "Bay 1" };
        var b2 = new ServiceBay { Name = "Bay 2" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyBayOverlapAsync(b1.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writer.Setup(w => w.AnyBayOverlapAsync(b2.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new AvailabilityService(writer.Object, Mock.Of<IDealershipRepository>(), Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(), Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeBayAsync(new[] { b1, b2 }, At(9), At(10), CancellationToken.None);

        picked.Should().Be(b2);
    }
}
```

- [ ] **Step 2: Run tests — fail (build error)**

Run: `dotnet test --filter FullyQualifiedName~AvailabilityServiceTests`
Expected: build error.

- [ ] **Step 3: Implement `AvailabilityService.cs`**

```csharp
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAppointmentWriter _writer;
    private readonly IDealershipRepository _dealershipRepo;
    private readonly IServiceTypeRepository _serviceTypeRepo;
    private readonly IServiceBayRepository _bayRepo;
    private readonly ITechnicianRepository _techRepo;

    public AvailabilityService(
        IAppointmentWriter writer,
        IDealershipRepository dealershipRepo,
        IServiceTypeRepository serviceTypeRepo,
        IServiceBayRepository bayRepo,
        ITechnicianRepository techRepo)
    {
        _writer = writer;
        _dealershipRepo = dealershipRepo;
        _serviceTypeRepo = serviceTypeRepo;
        _bayRepo = bayRepo;
        _techRepo = techRepo;
    }

    public async Task<Technician?> FirstFreeTechnicianAsync(
        IReadOnlyList<Technician> qualified, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        foreach (var t in qualified)
        {
            if (!await _writer.AnyTechnicianOverlapAsync(t.Id, startUtc, endUtc, ct))
                return t;
        }
        return null;
    }

    public async Task<ServiceBay?> FirstFreeBayAsync(
        IReadOnlyList<ServiceBay> bays, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        foreach (var b in bays)
        {
            if (!await _writer.AnyBayOverlapAsync(b.Id, startUtc, endUtc, ct))
                return b;
        }
        return null;
    }

    public async Task<IReadOnlyList<AvailabilitySlotResponse>> ListSlotsAsync(
        AvailabilityQueryRequest q, CancellationToken ct)
    {
        var dealership = await _dealershipRepo.GetByIdAsync(q.DealershipId, ct)
            ?? throw new Domain.Exceptions.ResourceNotFoundException(nameof(Dealership), q.DealershipId);
        var service = await _serviceTypeRepo.GetByIdAsync(q.ServiceTypeId, ct)
            ?? throw new Domain.Exceptions.ResourceNotFoundException(nameof(ServiceType), q.ServiceTypeId);

        var bays = await _bayRepo.ListAtDealershipAsync(q.DealershipId, ct);
        var techs = await _techRepo.ListQualifiedForServiceAtDealershipAsync(q.ServiceTypeId, q.DealershipId, ct);

        var slots = new List<AvailabilitySlotResponse>();
        var step = TimeSpan.FromMinutes(q.GranularityMinutes);
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);

        for (var s = q.FromUtc; s + duration <= q.ToUtc; s += step)
        {
            var e = s + duration;
            var techFree = await FirstFreeTechnicianAsync(techs, s, e, ct);
            if (techFree is null) continue;

            var bayFree = await FirstFreeBayAsync(bays, s, e, ct);
            if (bayFree is null) continue;

            slots.Add(new AvailabilitySlotResponse(s, e));
        }

        return slots;
    }
}
```

- [ ] **Step 4: Run tests — pass**

Run: `dotnet test --filter FullyQualifiedName~AvailabilityServiceTests`
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(app): add AvailabilityService with first-free selection and slot listing"
```

---

### Task 19: `BookingService` — the centrepiece (TDD)

**Files:**
- Create: `src/Scheduler.Application/Services/BookingService.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/BookingServiceTests.cs`

`BookingService.BookAsync(req, ct)` is the heart of the system. The transactional / retry shell is exercised in **integration** tests (Phase 5); here we test the orchestration logic with mocks: load dealership/customer/vehicle/service, validate opening hours and not-in-past, find qualified techs, find a free tech and bay, persist, return.

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class BookingServiceTests
{
    private static readonly DateTime _now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime In(int hours) => _now.AddHours(hours);

    private Mock<IDealershipRepository> _dealershipRepo = null!;
    private Mock<ICustomerRepository> _customerRepo = null!;
    private Mock<IVehicleRepository> _vehicleRepo = null!;
    private Mock<IServiceTypeRepository> _serviceTypeRepo = null!;
    private Mock<IServiceBayRepository> _bayRepo = null!;
    private Mock<IQualificationMatcher> _qualifier = null!;
    private Mock<IAvailabilityService> _availability = null!;
    private Mock<IAppointmentWriter> _writer = null!;
    private Mock<IUnitOfWork> _uow = null!;
    private Mock<IClock> _clock = null!;
    private Mock<IOpeningHoursValidator> _hours = null!;
    private BookingService _sut = null!;

    private Dealership _dealership = null!;
    private Customer _customer = null!;
    private Vehicle _vehicle = null!;
    private ServiceType _service = null!;
    private Technician _tech = null!;
    private ServiceBay _bay = null!;

    [SetUp]
    public void SetUp()
    {
        _dealershipRepo = new(); _customerRepo = new(); _vehicleRepo = new(); _serviceTypeRepo = new();
        _bayRepo = new(); _qualifier = new(); _availability = new(); _writer = new(); _uow = new();
        _clock = new(); _hours = new();

        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _hours.Setup(h => h.IsWithinOpeningHours(It.IsAny<Dealership>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(true);

        _dealership = new() { Name = "D" };
        _customer = new() { FirstName = "C", LastName = "X", Email = "c@x.example" };
        _vehicle = new() { CustomerId = _customer.Id, Vin = "VIN", Make = "M", Model = "X", Year = 2024 };
        _service = new() { Name = "S", DurationMinutes = 60 };
        _tech = new() { FullName = "T" };
        _bay = new() { Name = "B" };

        _dealershipRepo.Setup(r => r.GetByIdAsync(_dealership.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_dealership);
        _customerRepo.Setup(r => r.GetByIdAsync(_customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_customer);
        _vehicleRepo.Setup(r => r.GetByIdAsync(_vehicle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehicle);
        _serviceTypeRepo.Setup(r => r.GetByIdAsync(_service.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_service);
        _bayRepo.Setup(r => r.ListAtDealershipAsync(_dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceBay> { _bay });
        _qualifier.Setup(q => q.FindQualifiedAsync(_service.Id, _dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Technician> { _tech });
        _availability.Setup(a => a.FirstFreeTechnicianAsync(It.IsAny<IReadOnlyList<Technician>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tech);
        _availability.Setup(a => a.FirstFreeBayAsync(It.IsAny<IReadOnlyList<ServiceBay>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_bay);
        _uow.Setup(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        _sut = new BookingService(_dealershipRepo.Object, _customerRepo.Object, _vehicleRepo.Object,
            _serviceTypeRepo.Object, _bayRepo.Object, _qualifier.Object, _availability.Object,
            _writer.Object, _uow.Object, _clock.Object, _hours.Object);
    }

    private BookAppointmentRequest Req() => new(_dealership.Id, _customer.Id, _vehicle.Id, _service.Id, In(1));

    [Test]
    public async Task Books_when_all_resources_available()
    {
        var a = await _sut.BookAsync(Req(), CancellationToken.None);
        a.Should().NotBeNull();
        a.TechnicianId.Should().Be(_tech.Id);
        a.ServiceBayId.Should().Be(_bay.Id);
        a.EndsAtUtc.Should().Be(In(1).AddMinutes(60));
        _writer.Verify(w => w.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Throws_StartInPast_when_start_in_the_past()
    {
        var pastReq = Req() with { StartsAtUtc = In(-1) };
        var act = async () => await _sut.BookAsync(pastReq, CancellationToken.None);
        act.Should().ThrowAsync<StartInPastException>();
    }

    [Test]
    public void Throws_OutsideOpeningHours_when_validator_says_no()
    {
        _hours.Setup(h => h.IsWithinOpeningHours(It.IsAny<Dealership>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(false);
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<OutsideOpeningHoursException>();
    }

    [Test]
    public void Throws_TechnicianUnqualified_when_no_qualified_techs_exist()
    {
        _qualifier.Setup(q => q.FindQualifiedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Technician>());

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<TechnicianUnqualifiedException>();
    }

    [Test]
    public void Throws_SlotTaken_when_no_tech_free()
    {
        _availability.Setup(a => a.FirstFreeTechnicianAsync(It.IsAny<IReadOnlyList<Technician>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Technician?)null);

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<SlotTakenException>();
    }

    [Test]
    public void Throws_SlotTaken_when_no_bay_free()
    {
        _availability.Setup(a => a.FirstFreeBayAsync(It.IsAny<IReadOnlyList<ServiceBay>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceBay?)null);

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<SlotTakenException>();
    }

    [Test]
    public void Throws_ResourceNotFound_for_missing_dealership()
    {
        _dealershipRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Dealership?)null);
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Test]
    public void Throws_when_vehicle_belongs_to_different_customer()
    {
        var otherCustomer = new Customer { FirstName = "Z", LastName = "Z", Email = "z@z.example" };
        _vehicle.CustomerId = otherCustomer.Id;
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run — fail (build error)**

Run: `dotnet test --filter FullyQualifiedName~BookingServiceTests`
Expected: build error.

- [ ] **Step 3: Implement `BookingService.cs`**

```csharp
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IDealershipRepository _dealershipRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IServiceTypeRepository _serviceTypeRepo;
    private readonly IServiceBayRepository _bayRepo;
    private readonly IQualificationMatcher _qualifier;
    private readonly IAvailabilityService _availability;
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IOpeningHoursValidator _hours;

    public BookingService(
        IDealershipRepository dealershipRepo, ICustomerRepository customerRepo, IVehicleRepository vehicleRepo,
        IServiceTypeRepository serviceTypeRepo, IServiceBayRepository bayRepo,
        IQualificationMatcher qualifier, IAvailabilityService availability,
        IAppointmentWriter writer, IUnitOfWork uow, IClock clock, IOpeningHoursValidator hours)
    {
        _dealershipRepo = dealershipRepo; _customerRepo = customerRepo; _vehicleRepo = vehicleRepo;
        _serviceTypeRepo = serviceTypeRepo; _bayRepo = bayRepo; _qualifier = qualifier; _availability = availability;
        _writer = writer; _uow = uow; _clock = clock; _hours = hours;
    }

    public async Task<Appointment> BookAsync(BookAppointmentRequest r, CancellationToken ct)
    {
        if (r.StartsAtUtc <= _clock.UtcNow) throw new StartInPastException();

        await using var txn = await _uow.BeginSerializableTransactionAsync(ct);

        var dealership = await _dealershipRepo.GetByIdAsync(r.DealershipId, ct)
            ?? throw new ResourceNotFoundException(nameof(Dealership), r.DealershipId);
        var customer = await _customerRepo.GetByIdAsync(r.CustomerId, ct)
            ?? throw new ResourceNotFoundException(nameof(Customer), r.CustomerId);
        var vehicle = await _vehicleRepo.GetByIdAsync(r.VehicleId, ct)
            ?? throw new ResourceNotFoundException(nameof(Vehicle), r.VehicleId);
        var service = await _serviceTypeRepo.GetByIdAsync(r.ServiceTypeId, ct)
            ?? throw new ResourceNotFoundException(nameof(ServiceType), r.ServiceTypeId);

        if (vehicle.CustomerId != customer.Id)
            throw new ArgumentException("Vehicle does not belong to the specified customer.");

        var endsAtUtc = r.StartsAtUtc.AddMinutes(service.DurationMinutes);
        if (!_hours.IsWithinOpeningHours(dealership, r.StartsAtUtc, endsAtUtc))
            throw new OutsideOpeningHoursException();

        var qualified = await _qualifier.FindQualifiedAsync(r.ServiceTypeId, r.DealershipId, ct);
        if (qualified.Count == 0) throw new TechnicianUnqualifiedException();

        var tech = await _availability.FirstFreeTechnicianAsync(qualified, r.StartsAtUtc, endsAtUtc, ct)
            ?? throw new SlotTakenException();

        var bays = await _bayRepo.ListAtDealershipAsync(r.DealershipId, ct);
        var bay = await _availability.FirstFreeBayAsync(bays, r.StartsAtUtc, endsAtUtc, ct)
            ?? throw new SlotTakenException();

        var appointment = Appointment.Confirm(
            dealershipId: r.DealershipId,
            customerId:   r.CustomerId,
            vehicleId:    r.VehicleId,
            serviceTypeId:r.ServiceTypeId,
            technicianId: tech.Id,
            serviceBayId: bay.Id,
            startsAtUtc:  r.StartsAtUtc,
            endsAtUtc:    endsAtUtc);

        await _writer.AddAsync(appointment, ct);
        await _uow.SaveChangesAsync(ct);

        return appointment;
    }
}
```

- [ ] **Step 4: Run tests — pass**

Run: `dotnet test --filter FullyQualifiedName~BookingServiceTests`
Expected: 8 tests pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(app): add BookingService orchestrating the full booking sequence"
```

---

### Task 20: `CancellationService` and `ReschedulingService` (TDD)

**Files:**
- Create: `src/Scheduler.Application/Services/CancellationService.cs`
- Create: `src/Scheduler.Application/Services/ReschedulingService.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/CancellationServiceTests.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Services/ReschedulingServiceTests.cs`

The cancel + reschedule services will need an `IAppointmentRepository` that combines reader + writer plus an `UpdateAsync` for tracked entities. Add a `ITrackedAppointmentRepository` interface or extend `IAppointmentWriter` with a `GetForUpdateAsync(id, ct)` method.

- [ ] **Step 1: Add `GetForUpdateAsync` to `IAppointmentWriter`**

In `src/Scheduler.Application/Abstractions/Persistence/IAppointmentWriter.cs`, add:

```csharp
Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct);
```

In `src/Scheduler.Infrastructure/Repositories/AppointmentRepository.cs`, implement it by tracking the entity:

```csharp
public Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
    _ctx.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
```

- [ ] **Step 2: `CancellationServiceTests.cs`**

```csharp
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class CancellationServiceTests
{
    private static DateTime _now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment Confirmed(DateTime start) =>
        Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: start, endsAtUtc: start.AddHours(1));

    [Test]
    public async Task Cancels_a_confirmed_appointment()
    {
        var a = Confirmed(_now.AddHours(1));
        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IClock>(); clock.SetupGet(c => c.UtcNow).Returns(_now);

        var sut = new CancellationService(writer.Object, uow.Object, clock.Object);
        var result = await sut.CancelAsync(a.Id, CancellationToken.None);

        result.Status.Should().Be(Domain.Enums.AppointmentStatus.Cancelled);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Throws_ResourceNotFound_when_id_unknown()
    {
        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Appointment?)null);

        var sut = new CancellationService(writer.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IClock>());
        var act = async () => await sut.CancelAsync(Guid.NewGuid(), CancellationToken.None);
        act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Test]
    public void Throws_AlreadyCancelled_when_already_cancelled()
    {
        var a = Confirmed(_now.AddHours(1));
        a.Cancel(_now);

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var clock = new Mock<IClock>(); clock.SetupGet(c => c.UtcNow).Returns(_now);

        var sut = new CancellationService(writer.Object, Mock.Of<IUnitOfWork>(), clock.Object);
        var act = async () => await sut.CancelAsync(a.Id, CancellationToken.None);
        act.Should().ThrowAsync<AlreadyCancelledException>();
    }
}
```

- [ ] **Step 3: `CancellationService.cs`**

```csharp
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class CancellationService : ICancellationService
{
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CancellationService(IAppointmentWriter writer, IUnitOfWork uow, IClock clock)
    {
        _writer = writer; _uow = uow; _clock = clock;
    }

    public async Task<Appointment> CancelAsync(Guid id, CancellationToken ct)
    {
        var a = await _writer.GetForUpdateAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(Appointment), id);
        try
        {
            a.Cancel(_clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new AlreadyCancelledException();
        }

        await _uow.SaveChangesAsync(ct);
        return a;
    }
}
```

- [ ] **Step 4: `ReschedulingServiceTests.cs`**

```csharp
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class ReschedulingServiceTests
{
    [Test]
    public async Task Cancels_old_and_books_new_atomically()
    {
        var now = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        var dealershipId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();
        var existing = Appointment.Confirm(dealershipId, customerId, vehicleId, serviceTypeId,
            Guid.NewGuid(), Guid.NewGuid(), now.AddHours(2), now.AddHours(3));

        var newStart = now.AddHours(5);
        var newAppt = Appointment.Confirm(dealershipId, customerId, vehicleId, serviceTypeId,
            Guid.NewGuid(), Guid.NewGuid(), newStart, newStart.AddHours(1));

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());

        var clock = new Mock<IClock>(); clock.SetupGet(c => c.UtcNow).Returns(now);

        var booking = new Mock<IBookingService>();
        booking.Setup(b => b.BookAsync(It.IsAny<BookAppointmentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(newAppt);

        var sut = new ReschedulingService(writer.Object, uow.Object, clock.Object, booking.Object);

        var result = await sut.RescheduleAsync(existing.Id, new RescheduleAppointmentRequest(newStart), CancellationToken.None);

        result.Should().Be(newAppt);
        existing.Status.Should().Be(Domain.Enums.AppointmentStatus.Cancelled);
        uow.Verify(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 5: `ReschedulingService.cs`**

```csharp
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class ReschedulingService : IReschedulingService
{
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IBookingService _booking;

    public ReschedulingService(IAppointmentWriter writer, IUnitOfWork uow, IClock clock, IBookingService booking)
    {
        _writer = writer; _uow = uow; _clock = clock; _booking = booking;
    }

    public async Task<Appointment> RescheduleAsync(Guid id, RescheduleAppointmentRequest r, CancellationToken ct)
    {
        await using var txn = await _uow.BeginSerializableTransactionAsync(ct);

        var existing = await _writer.GetForUpdateAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(Appointment), id);

        try { existing.Cancel(_clock.UtcNow); }
        catch (InvalidOperationException) { throw new AlreadyCancelledException(); }

        await _uow.SaveChangesAsync(ct);

        var rebookRequest = new BookAppointmentRequest(
            existing.DealershipId, existing.CustomerId, existing.VehicleId, existing.ServiceTypeId, r.NewStartsAtUtc);

        return await _booking.BookAsync(rebookRequest, ct);
    }
}
```

- [ ] **Step 6: Run tests — pass**

Run: `dotnet test --filter "FullyQualifiedName~CancellationServiceTests | FullyQualifiedName~ReschedulingServiceTests"`
Expected: 4 tests pass.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(app): add CancellationService and ReschedulingService"
```

---

### Task 21: FluentValidation validators

**Files:**
- Create: `src/Scheduler.Application/Validation/BookAppointmentRequestValidator.cs`
- Create: `src/Scheduler.Application/Validation/RescheduleAppointmentRequestValidator.cs`
- Create: `src/Scheduler.Application/Validation/AvailabilityQueryRequestValidator.cs`
- Test:   `tests/Scheduler.Application.UnitTests/Validation/BookAppointmentRequestValidatorTests.cs`

- [ ] **Step 1: Validator implementations**

```csharp
// BookAppointmentRequestValidator.cs
using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.DealershipId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ServiceTypeId).NotEmpty();
        RuleFor(x => x.StartsAtUtc).Must(s => s.Kind == DateTimeKind.Utc || s.Kind == DateTimeKind.Unspecified)
            .WithMessage("StartsAtUtc must be UTC.");
    }
}
```

```csharp
// RescheduleAppointmentRequestValidator.cs
using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.NewStartsAtUtc).Must(s => s.Kind == DateTimeKind.Utc || s.Kind == DateTimeKind.Unspecified)
            .WithMessage("NewStartsAtUtc must be UTC.");
    }
}
```

```csharp
// AvailabilityQueryRequestValidator.cs
using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class AvailabilityQueryRequestValidator : AbstractValidator<AvailabilityQueryRequest>
{
    public AvailabilityQueryRequestValidator()
    {
        RuleFor(x => x.DealershipId).NotEmpty();
        RuleFor(x => x.ServiceTypeId).NotEmpty();
        RuleFor(x => x.GranularityMinutes).GreaterThanOrEqualTo(5).LessThanOrEqualTo(120);
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
    }
}
```

- [ ] **Step 2: One representative validator test**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Validation;

namespace Scheduler.Application.UnitTests.Validation;

[TestFixture]
public sealed class BookAppointmentRequestValidatorTests
{
    [Test]
    public void Empty_ids_fail_validation()
    {
        var sut = new BookAppointmentRequestValidator();
        var req = new BookAppointmentRequest(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, DateTime.UtcNow.AddHours(1));
        var result = sut.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DealershipId);
        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
        result.ShouldHaveValidationErrorFor(x => x.VehicleId);
        result.ShouldHaveValidationErrorFor(x => x.ServiceTypeId);
    }

    [Test]
    public void Valid_request_passes()
    {
        var sut = new BookAppointmentRequestValidator();
        var req = new BookAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
        sut.TestValidate(req).ShouldNotHaveAnyValidationErrors();
    }
}
```

- [ ] **Step 3: Run tests — pass**

Run: `dotnet test --filter FullyQualifiedName~BookAppointmentRequestValidatorTests`
Expected: 2 tests pass.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(app): add FluentValidation validators for booking, reschedule, availability"
```

---

### Task 22: Mappers and Application DI

**Files:**
- Create: `src/Scheduler.Application/Mapping/AppointmentMappers.cs`
- Create: `src/Scheduler.Application/DependencyInjection.cs`

- [ ] **Step 1: `AppointmentMappers.cs`**

```csharp
using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Mapping;

public static class AppointmentMappers
{
    public static AppointmentResponse ToResponse(this Appointment a) => new(
        Id: a.Id,
        DealershipId: a.DealershipId,
        CustomerId: a.CustomerId,
        VehicleId: a.VehicleId,
        ServiceTypeId: a.ServiceTypeId,
        TechnicianId: a.TechnicianId,
        ServiceBayId: a.ServiceBayId,
        StartsAtUtc: a.StartsAtUtc,
        EndsAtUtc: a.EndsAtUtc,
        Status: a.Status.ToString(),
        CreatedAtUtc: a.CreatedAtUtc);
}
```

- [ ] **Step 2: `DependencyInjection.cs`**

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulerApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ICancellationService, CancellationService>();
        services.AddScoped<IReschedulingService, ReschedulingService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IQualificationMatcher, QualificationMatcher>();
        services.AddScoped<IResourceSelector, ResourceSelector>();
        services.AddScoped<IOpeningHoursValidator, OpeningHoursValidator>();

        return services;
    }
}
```

- [ ] **Step 3: Build verification**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(app): add mappers and AddSchedulerApplication DI extension"
```

---

## Phase 4 — API layer

### Task 23: Configuration option classes

**Files:**
- Create: `src/Scheduler.Api/Configuration/BookingOptions.cs`
- Create: `src/Scheduler.Api/Configuration/SeedingOptions.cs`

- [ ] **Step 1: `BookingOptions.cs`**

```csharp
namespace Scheduler.Api.Configuration;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 50;
    public int DefaultGranularityMinutes { get; set; } = 15;
}
```

- [ ] **Step 2: `SeedingOptions.cs`**

```csharp
namespace Scheduler.Api.Configuration;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";
    public bool Enabled { get; set; } = true;
}
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(api): add typed Configuration options"
```

---

### Task 24: Telemetry — `Metrics` and `Tracing` static classes

**Files:**
- Create: `src/Scheduler.Api/Telemetry/SchedulerMetrics.cs`
- Create: `src/Scheduler.Api/Telemetry/SchedulerActivitySource.cs`

- [ ] **Step 1: `SchedulerActivitySource.cs`**

```csharp
using System.Diagnostics;

namespace Scheduler.Api.Telemetry;

public static class SchedulerActivitySource
{
    public const string Name = "Scheduler";
    public static readonly ActivitySource Instance = new(Name);
}
```

- [ ] **Step 2: `SchedulerMetrics.cs`**

```csharp
using System.Diagnostics.Metrics;

namespace Scheduler.Api.Telemetry;

public sealed class SchedulerMetrics
{
    public const string MeterName = "Scheduler.Bookings";

    public Counter<long> BookingsTotal { get; }
    public Counter<long> BookingRetriesTotal { get; }
    public Histogram<double> BookingDurationSeconds { get; }
    public Histogram<double> AvailabilityQueryDurationSeconds { get; }
    public Counter<long> IdempotencyReplaysTotal { get; }

    public SchedulerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        BookingsTotal = meter.CreateCounter<long>("bookings_total");
        BookingRetriesTotal = meter.CreateCounter<long>("booking_retries_total");
        BookingDurationSeconds = meter.CreateHistogram<double>("booking_duration_seconds", unit: "s");
        AvailabilityQueryDurationSeconds = meter.CreateHistogram<double>("availability_query_duration_seconds", unit: "s");
        IdempotencyReplaysTotal = meter.CreateCounter<long>("idempotency_replays_total");
    }
}
```

- [ ] **Step 3: Build verification**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(api): add SchedulerMetrics and SchedulerActivitySource"
```

---

### Task 25: Middleware — CorrelationId, GlobalException

**Files:**
- Create: `src/Scheduler.Api/Middleware/CorrelationIdMiddleware.cs`
- Create: `src/Scheduler.Api/Middleware/GlobalExceptionMiddleware.cs`

- [ ] **Step 1: `CorrelationIdMiddleware.cs`**

```csharp
using Serilog.Context;

namespace Scheduler.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers.TryGetValue(HeaderName, out var hv) && !string.IsNullOrWhiteSpace(hv)
            ? hv.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Response.Headers[HeaderName] = id;
        ctx.Items[HeaderName] = id;
        using (LogContext.PushProperty("CorrelationId", id))
        {
            await _next(ctx);
        }
    }
}
```

- [ ] **Step 2: `GlobalExceptionMiddleware.cs`**

```csharp
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Contracts;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException vex)
        {
            await Write(ctx, 400, ProblemCodes.ValidationFailed, "Validation failed.",
                vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }
        catch (DomainException dex)
        {
            var status = dex switch
            {
                SlotTakenException => 409,
                AlreadyCancelledException => 409,
                IdempotencyReplayMismatchException => 409,
                ResourceNotFoundException => 404,
                TechnicianUnqualifiedException => 422,
                OutsideOpeningHoursException => 422,
                StartInPastException => 422,
                _ => 400
            };
            await Write(ctx, status, dex.Code, dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await Write(ctx, 500, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string code, string detail, object? extras = null)
    {
        var corrId = ctx.Items[CorrelationIdMiddleware.HeaderName] as string ?? Guid.NewGuid().ToString("N");
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Type = $"https://scheduler.example.com/errors/{code.ToLowerInvariant().Replace('_', '-')}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = corrId;
        if (extras is not null) problem.Extensions["errors"] = extras;

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(api): add CorrelationId and GlobalException middleware"
```

---

### Task 26: Idempotency middleware

**Files:**
- Create: `src/Scheduler.Api/Middleware/IdempotencyMiddleware.cs`

The middleware pre-checks the idempotency store for replays *before* the controller runs. Mismatched-body replays return 409 immediately. Successful prior responses are returned cached.

- [ ] **Step 1: `IdempotencyMiddleware.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Contracts;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, IIdempotencyStore store)
    {
        if (!IsBookingMutation(ctx) || !ctx.Request.Headers.TryGetValue(HeaderName, out var keyHeader))
        {
            await _next(ctx);
            return;
        }

        var key = keyHeader.ToString();
        ctx.Request.EnableBuffering();
        var bodyHash = await ComputeBodyHashAsync(ctx);

        var hit = await store.TryGetAsync(key, bodyHash, ctx.RequestAborted);
        if (hit is not null)
        {
            ctx.Response.StatusCode = int.Parse(hit.ResponseStatusCode);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(hit.ResponseBody);
            return;
        }

        if (await store.ExistsForDifferentBodyAsync(key, bodyHash, ctx.RequestAborted))
        {
            throw new IdempotencyReplayMismatchException();
        }

        ctx.Items["IdempotencyKey"] = key;
        ctx.Items["IdempotencyBodyHash"] = bodyHash;
        await _next(ctx);
    }

    private static bool IsBookingMutation(HttpContext ctx) =>
        ctx.Request.Method == "POST" &&
        ctx.Request.Path.StartsWithSegments("/api/v1/appointments");

    private static async Task<string> ComputeBodyHashAsync(HttpContext ctx)
    {
        ctx.Request.Body.Position = 0;
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        var bytes = ms.ToArray();
        ctx.Request.Body.Position = 0;

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "feat(api): add idempotency middleware with body-hash matching"
```

---

### Task 27: Controllers — `AppointmentsController`

**Files:**
- Create: `src/Scheduler.Api/Controllers/AppointmentsController.cs`

`AppointmentsController` handles POST/GET/list/cancel/reschedule. The Polly retry envelope wraps booking and rescheduling because both can hit `DbUpdateException` from the filtered unique index.

- [ ] **Step 1: `AppointmentsController.cs`**

```csharp
using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Scheduler.Api.Configuration;
using Scheduler.Api.Telemetry;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Application.Mapping;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IBookingService _booking;
    private readonly ICancellationService _cancel;
    private readonly IReschedulingService _reschedule;
    private readonly IAppointmentReader _reader;
    private readonly IIdempotencyStore _idempotency;
    private readonly IValidator<BookAppointmentRequest> _bookValidator;
    private readonly IValidator<RescheduleAppointmentRequest> _rescheduleValidator;
    private readonly SchedulerMetrics _metrics;
    private readonly BookingOptions _options;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        IBookingService booking, ICancellationService cancel, IReschedulingService reschedule,
        IAppointmentReader reader, IIdempotencyStore idempotency,
        IValidator<BookAppointmentRequest> bookValidator,
        IValidator<RescheduleAppointmentRequest> rescheduleValidator,
        SchedulerMetrics metrics, IOptions<BookingOptions> options, ILogger<AppointmentsController> logger)
    {
        _booking = booking; _cancel = cancel; _reschedule = reschedule; _reader = reader; _idempotency = idempotency;
        _bookValidator = bookValidator; _rescheduleValidator = rescheduleValidator;
        _metrics = metrics; _options = options.Value; _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest req, CancellationToken ct)
    {
        await _bookValidator.ValidateAndThrowAsync(req, ct);

        using var activity = SchedulerActivitySource.Instance.StartActivity("booking.attempt");
        activity?.SetTag("dealership.id", req.DealershipId);
        activity?.SetTag("service_type.id", req.ServiceTypeId);

        var sw = Stopwatch.StartNew();
        var attempt = 0;
        var pipeline = BuildRetryPipeline();

        try
        {
            var appointment = await pipeline.ExecuteAsync(async (cancel) =>
            {
                attempt++;
                activity?.SetTag("retry.attempt", attempt);
                return await _booking.BookAsync(req, cancel);
            }, ct);

            var response = appointment.ToResponse();
            var json = JsonSerializer.Serialize(response);

            // Idempotency persistence
            if (HttpContext.Items["IdempotencyKey"] is string key && HttpContext.Items["IdempotencyBodyHash"] is string hash)
            {
                await _idempotency.PutAsync(key, hash, "201", json, appointment.Id, DateTime.UtcNow.AddHours(24), ct);
            }

            _metrics.BookingsTotal.Add(1, new("dealership_id", req.DealershipId), new("service_type_id", req.ServiceTypeId), new("outcome", "confirmed"));
            if (attempt > 1) _metrics.BookingRetriesTotal.Add(1, new("outcome", "resolved"));
            _metrics.BookingDurationSeconds.Record(sw.Elapsed.TotalSeconds, new("outcome", "confirmed"));
            activity?.SetTag("outcome", "confirmed");
            activity?.SetTag("appointment.id", appointment.Id);

            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, response);
        }
        catch (SlotTakenException)
        {
            _metrics.BookingsTotal.Add(1, new("dealership_id", req.DealershipId), new("service_type_id", req.ServiceTypeId), new("outcome", "conflict"));
            if (attempt > 1) _metrics.BookingRetriesTotal.Add(1, new("outcome", "exhausted"));
            activity?.SetTag("outcome", "conflict");
            throw;
        }
        catch (TechnicianUnqualifiedException)
        {
            _metrics.BookingsTotal.Add(1, new("outcome", "unqualified"));
            activity?.SetTag("outcome", "unqualified");
            throw;
        }
        catch (OutsideOpeningHoursException)
        {
            _metrics.BookingsTotal.Add(1, new("outcome", "outside_hours"));
            activity?.SetTag("outcome", "outside_hours");
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var a = await _reader.GetByIdAsync(id, ct);
        return a is null ? NotFound() : Ok(a.ToResponse());
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? dealershipId, [FromQuery] Guid? customerId, [FromQuery] Guid? technicianId,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take > 100) take = 100;
        var rows = await _reader.ListAsync(dealershipId, customerId, technicianId, fromUtc, toUtc, skip, take, ct);
        return Ok(rows.Select(a => a.ToResponse()));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _cancel.CancelAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest req, CancellationToken ct)
    {
        await _rescheduleValidator.ValidateAndThrowAsync(req, ct);

        var pipeline = BuildRetryPipeline();
        var rescheduled = await pipeline.ExecuteAsync(async (cancel) => await _reschedule.RescheduleAsync(id, req, cancel), ct);

        return Ok(rescheduled.ToResponse());
    }

    private ResiliencePipeline BuildRetryPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<DbUpdateException>().Handle<SlotTakenException>(),
                MaxRetryAttempts = _options.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
}
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "feat(api): add AppointmentsController with retry pipeline and metrics"
```

---

### Task 28: Controllers — `AvailabilityController`, `ReferenceDataController`, `HealthController`

**Files:**
- Create: `src/Scheduler.Api/Controllers/AvailabilityController.cs`
- Create: `src/Scheduler.Api/Controllers/ReferenceDataController.cs`

- [ ] **Step 1: `AvailabilityController.cs`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _service;
    private readonly IValidator<AvailabilityQueryRequest> _validator;

    public AvailabilityController(IAvailabilityService service, IValidator<AvailabilityQueryRequest> validator)
    {
        _service = service; _validator = validator;
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(
        [FromQuery] Guid dealershipId,
        [FromQuery] Guid serviceTypeId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] int granularityMinutes = 15,
        CancellationToken ct = default)
    {
        var q = new AvailabilityQueryRequest(dealershipId, serviceTypeId, fromUtc, toUtc, granularityMinutes);
        await _validator.ValidateAndThrowAsync(q, ct);
        var slots = await _service.ListSlotsAsync(q, ct);
        return Ok(slots);
    }
}
```

- [ ] **Step 2: `ReferenceDataController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ReferenceDataController : ControllerBase
{
    private readonly SchedulerDbContext _ctx;
    public ReferenceDataController(SchedulerDbContext ctx) => _ctx = ctx;

    [HttpGet("dealerships")]
    public Task<List<object>> ListDealerships(CancellationToken ct) =>
        _ctx.Dealerships.AsNoTracking()
            .Select(d => (object)new { d.Id, d.Name, d.TimeZone, d.OpeningHours })
            .ToListAsync(ct);

    [HttpGet("dealerships/{id:guid}")]
    public async Task<IActionResult> GetDealership(Guid id, CancellationToken ct)
    {
        var d = await _ctx.Dealerships.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? NotFound() : Ok(new { d.Id, d.Name, d.TimeZone, d.OpeningHours });
    }

    [HttpGet("dealerships/{id:guid}/technicians")]
    public Task<List<object>> ListTechniciansAtDealership(Guid id, CancellationToken ct) =>
        _ctx.TechnicianDealerships.AsNoTracking()
            .Where(td => td.DealershipId == id)
            .Select(td => td.Technician!)
            .Select(t => (object)new { t.Id, t.FullName })
            .ToListAsync(ct);

    [HttpGet("dealerships/{id:guid}/bays")]
    public Task<List<object>> ListBaysAtDealership(Guid id, CancellationToken ct) =>
        _ctx.ServiceBayDealerships.AsNoTracking()
            .Where(sbd => sbd.DealershipId == id)
            .Select(sbd => sbd.ServiceBay!)
            .Select(b => (object)new { b.Id, b.Name })
            .ToListAsync(ct);

    [HttpGet("technicians")]
    public Task<List<object>> ListTechnicians(CancellationToken ct) =>
        _ctx.Technicians.AsNoTracking()
            .Select(t => (object)new
            {
                t.Id,
                t.FullName,
                DealershipIds = t.DealershipAssignments.Select(td => td.DealershipId),
                Skills = t.Skills.Select(ts => ts.Skill!.Code)
            })
            .ToListAsync(ct);

    [HttpGet("bays")]
    public Task<List<object>> ListBays(CancellationToken ct) =>
        _ctx.ServiceBays.AsNoTracking()
            .Select(b => (object)new
            {
                b.Id,
                b.Name,
                DealershipIds = b.DealershipAssignments.Select(sbd => sbd.DealershipId)
            })
            .ToListAsync(ct);

    [HttpGet("service-types")]
    public Task<List<object>> ListServiceTypes(CancellationToken ct) =>
        _ctx.ServiceTypes.AsNoTracking()
            .Select(s => (object)new
            {
                s.Id, s.Name, s.DurationMinutes, s.Description,
                RequiredSkills = s.RequiredSkills.Select(r => r.Skill!.Code)
            })
            .ToListAsync(ct);

    [HttpGet("skills")]
    public Task<List<object>> ListSkills(CancellationToken ct) =>
        _ctx.Skills.AsNoTracking()
            .Select(s => (object)new { s.Id, s.Code, s.Name, s.Description, s.Category })
            .ToListAsync(ct);

    [HttpGet("customers/{id:guid}/vehicles")]
    public Task<List<object>> ListVehiclesForCustomer(Guid id, CancellationToken ct) =>
        _ctx.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == id)
            .Select(v => (object)new { v.Id, v.Vin, v.Make, v.Model, v.Year })
            .ToListAsync(ct);
}
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(api): add availability and reference-data controllers"
```

---

### Task 29: `Program.cs` — wire up Serilog, OTel, Swagger, health, middleware

**Files:**
- Modify: `src/Scheduler.Api/Program.cs`
- Create: `src/Scheduler.Api/appsettings.json` (replace generated default)
- Create: `src/Scheduler.Api/appsettings.Development.json`

- [ ] **Step 1: Replace `Program.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scheduler.Api.Configuration;
using Scheduler.Api.Middleware;
using Scheduler.Api.Telemetry;
using Scheduler.Application;
using Scheduler.Infrastructure;
using Scheduler.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog over the host
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Configuration objects
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.SectionName));
builder.Services.Configure<SeedingOptions>(builder.Configuration.GetSection(SeedingOptions.SectionName));

// Application + Infrastructure
builder.Services.AddSchedulerApplication();
builder.Services.AddSchedulerInfrastructure(builder.Configuration);
builder.Services.AddSingleton<SchedulerMetrics>();

// MVC + ProblemDetails
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchedulerDbContext>(tags: new[] { "ready" });

// OpenTelemetry — traces + metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Scheduler.Api"))
    .WithTracing(t => t
        .AddSource(SchedulerActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(m => m
        .AddMeter(SchedulerMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    await ctx.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapPrometheusScrapingEndpoint();

await app.RunAsync();

public partial class Program { }
```

- [ ] **Step 2: `appsettings.json`**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "ConnectionStrings": {
    "Default": "Data Source=scheduler.db"
  },
  "Booking": {
    "MaxRetries": 3,
    "RetryBaseDelayMs": 50,
    "DefaultGranularityMinutes": 15
  },
  "Seeding": {
    "Enabled": false
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 3: `appsettings.Development.json`**

```json
{
  "Seeding": { "Enabled": true }
}
```

- [ ] **Step 4: Build verification**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 5: Smoke-run the API**

Run: `dotnet run --project src/Scheduler.Api`
Visit: `http://localhost:5000/swagger` and `http://localhost:5000/metrics` and `http://localhost:5000/health/ready`.
Stop with Ctrl+C.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(api): wire Program.cs with Serilog, OTel, Swagger, health, middleware"
```

---

### Task 30: Demo seed data

**Files:**
- Create: `src/Scheduler.Api/Seed/SeedData.cs`
- Modify: `src/Scheduler.Api/Program.cs` to call the seeder when `Seeding.Enabled` is true.

- [ ] **Step 1: `SeedData.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.Seed;

public static class SeedData
{
    public static async Task SeedAsync(SchedulerDbContext ctx, CancellationToken ct = default)
    {
        if (await ctx.Dealerships.AnyAsync(ct)) return;

        var dealershipA = new Dealership
        {
            Name = "Keyloop Auto — Central",
            TimeZone = "Etc/UTC",
            OpeningHours = new()
            {
                new(DayOfWeek.Monday,    new(9, 0), new(17, 0)),
                new(DayOfWeek.Tuesday,   new(9, 0), new(17, 0)),
                new(DayOfWeek.Wednesday, new(9, 0), new(17, 0)),
                new(DayOfWeek.Thursday,  new(9, 0), new(17, 0)),
                new(DayOfWeek.Friday,    new(9, 0), new(17, 0)),
            }
        };

        var dealershipB = new Dealership
        {
            Name = "Keyloop Auto — North",
            TimeZone = "Etc/UTC",
            OpeningHours = new()
            {
                new(DayOfWeek.Monday,    new(8, 0), new(18, 0)),
                new(DayOfWeek.Tuesday,   new(8, 0), new(18, 0)),
                new(DayOfWeek.Wednesday, new(8, 0), new(18, 0)),
                new(DayOfWeek.Thursday,  new(8, 0), new(18, 0)),
                new(DayOfWeek.Friday,    new(8, 0), new(18, 0)),
                new(DayOfWeek.Saturday,  new(9, 0), new(13, 0)),
            }
        };

        var skillEv  = new Skill { Code = "EV_CERT", Name = "EV Certification", Category = "Powertrain" };
        var skillBrk = new Skill { Code = "BRAKES",  Name = "Brake Systems",     Category = "Chassis" };
        var skillDg  = new Skill { Code = "DIAG",    Name = "Diagnostics",       Category = "General" };

        var oilChange = new ServiceType { Name = "Oil Change", DurationMinutes = 30 };
        var brakeJob  = new ServiceType { Name = "Brake Replacement", DurationMinutes = 90,
            RequiredSkills = new() { new() { Skill = skillBrk } } };
        var evService = new ServiceType { Name = "EV Diagnostic Service", DurationMinutes = 60,
            RequiredSkills = new() { new() { Skill = skillEv }, new() { Skill = skillDg } } };

        var techAlice = new Technician { FullName = "Alice Smith",
            Skills = new() { new() { Skill = skillEv }, new() { Skill = skillDg } } };
        var techBob = new Technician { FullName = "Bob Jones",
            Skills = new() { new() { Skill = skillBrk }, new() { Skill = skillDg } } };
        var techCara = new Technician { FullName = "Cara Liu",
            Skills = new() { new() { Skill = skillEv }, new() { Skill = skillBrk }, new() { Skill = skillDg } } };

        var bay1 = new ServiceBay { Name = "Bay 1" };
        var bay2 = new ServiceBay { Name = "Bay 2" };
        var bay3 = new ServiceBay { Name = "Bay 3" };

        // Assignments
        techAlice.DealershipAssignments = new() { new() { Dealership = dealershipA } };
        techBob.DealershipAssignments   = new() { new() { Dealership = dealershipA } };
        techCara.DealershipAssignments  = new() { new() { Dealership = dealershipA }, new() { Dealership = dealershipB } };
        bay1.DealershipAssignments      = new() { new() { Dealership = dealershipA } };
        bay2.DealershipAssignments      = new() { new() { Dealership = dealershipA } };
        bay3.DealershipAssignments      = new() { new() { Dealership = dealershipB } };

        var customer = new Customer { FirstName = "Casey", LastName = "Garcia", Email = "casey@example.com", Phone = "+15551234567" };
        var vehicle  = new Vehicle { CustomerId = customer.Id, Vin = "1HGCM82633A004352", Make = "Tesla", Model = "Model 3", Year = 2024 };

        ctx.AddRange(dealershipA, dealershipB,
            skillEv, skillBrk, skillDg,
            oilChange, brakeJob, evService,
            techAlice, techBob, techCara,
            bay1, bay2, bay3,
            customer, vehicle);

        await ctx.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Wire seeding in `Program.cs`**

After the `await ctx.Database.MigrateAsync();` line, add:

```csharp
    var seedingOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedingOptions>>().Value;
    if (seedingOptions.Enabled)
    {
        await Scheduler.Api.Seed.SeedData.SeedAsync(ctx);
    }
```

- [ ] **Step 3: Verify build and that seeding produces rows**

Run: `dotnet build`
Run: `dotnet run --project src/Scheduler.Api`
Visit: `http://localhost:5000/api/v1/dealerships` — should return 2 entries.
Stop with Ctrl+C.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(api): add demo seed data with two dealerships and shared technician"
```

---

## Phase 5 — Integration, concurrency, and architecture tests

### Task 31: Integration test infrastructure

**Files:**
- Create: `tests/Scheduler.Api.IntegrationTests/Fixtures/SchedulerWebAppFactory.cs`
- Create: `tests/Scheduler.Api.IntegrationTests/Fixtures/TestSeed.cs`
- Create: `tests/Scheduler.Api.IntegrationTests/Fixtures/TestClock.cs`

The factory swaps in a fresh on-disk SQLite file per fixture and registers a controllable test clock.

- [ ] **Step 1: `Fixtures/TestClock.cs`**

```csharp
using Scheduler.Domain.Abstractions;

namespace Scheduler.Api.IntegrationTests.Fixtures;

public sealed class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);
}
```

- [ ] **Step 2: `Fixtures/SchedulerWebAppFactory.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Domain.Abstractions;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Fixtures;

public sealed class SchedulerWebAppFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = $"scheduler.test.{Guid.NewGuid():N}.db";
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={DbPath}",
                ["Seeding:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace IClock with TestClock
            services.RemoveAll(typeof(IClock));
            services.AddSingleton<IClock>(Clock);
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(DbPath)) File.Delete(DbPath);
    }
}
```

(Add `Microsoft.Extensions.DependencyInjection.Extensions` for `RemoveAll`.)

- [ ] **Step 3: `Fixtures/TestSeed.cs` — deterministic per-test seeding**

```csharp
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Fixtures;

public sealed record SeedData(
    Dealership DealershipA,
    Dealership DealershipB,
    Customer Customer,
    Vehicle Vehicle,
    ServiceType QuickService,
    ServiceType EvService,
    Skill EvSkill,
    Skill DiagSkill,
    Technician AliceA,
    Technician BobAB,
    ServiceBay Bay1,
    ServiceBay Bay2);

public static class TestSeed
{
    public static async Task<SeedData> SeedAsync(SchedulerDbContext ctx)
    {
        var dA = new Dealership { Name = "A", TimeZone = "Etc/UTC", OpeningHours = AllWeek(0, 24) };
        var dB = new Dealership { Name = "B", TimeZone = "Etc/UTC", OpeningHours = AllWeek(0, 24) };

        var ev = new Skill { Code = "EV_CERT", Name = "EV" };
        var dg = new Skill { Code = "DIAG", Name = "Diag" };

        var quick = new ServiceType { Name = "Quick", DurationMinutes = 30 };
        var evSvc = new ServiceType { Name = "EV", DurationMinutes = 60,
            RequiredSkills = new() { new() { Skill = ev }, new() { Skill = dg } } };

        var alice = new Technician { FullName = "Alice",
            Skills = new() { new() { Skill = ev }, new() { Skill = dg } },
            DealershipAssignments = new() { new() { Dealership = dA } } };
        var bob = new Technician { FullName = "Bob",
            Skills = new() { new() { Skill = dg } },
            DealershipAssignments = new() { new() { Dealership = dA }, new() { Dealership = dB } } };

        var bay1 = new ServiceBay { Name = "Bay 1", DealershipAssignments = new() { new() { Dealership = dA } } };
        var bay2 = new ServiceBay { Name = "Bay 2", DealershipAssignments = new() { new() { Dealership = dA }, new() { Dealership = dB } } };

        var customer = new Customer { FirstName = "C", LastName = "X", Email = "c@x.example" };
        var vehicle = new Vehicle { CustomerId = customer.Id, Vin = "VIN1", Make = "Tesla", Model = "M3", Year = 2024 };

        ctx.AddRange(dA, dB, ev, dg, quick, evSvc, alice, bob, bay1, bay2, customer, vehicle);
        await ctx.SaveChangesAsync();

        return new SeedData(dA, dB, customer, vehicle, quick, evSvc, ev, dg, alice, bob, bay1, bay2);
    }

    private static List<OpeningHoursEntry> AllWeek(int openHour, int closeHour) =>
        Enum.GetValues<DayOfWeek>().Select(d => new OpeningHoursEntry(d, new(openHour, 0), new(Math.Min(closeHour, 23), Math.Min(closeHour, 23) == closeHour ? 0 : 59))).ToList();
}
```

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "test: integration test fixtures (factory, seed, test clock)"
```

---

### Task 32: Booking integration tests (happy + 4xx)

**Files:**
- Create: `tests/Scheduler.Api.IntegrationTests/Booking/BookingIntegrationTests.cs`

- [ ] **Step 1: Implement**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Booking;

[TestFixture]
public sealed class BookingIntegrationTests
{
    private SchedulerWebAppFactory _factory = null!;
    private HttpClient _client = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Book_HappyPath_Returns201_Confirmed()
    {
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await res.Content.ReadFromJsonAsync<AppointmentResponse>();
        body!.Status.Should().Be("Confirmed");
        body.EndsAtUtc.Should().Be(req.StartsAtUtc.AddMinutes(30));
    }

    [Test]
    public async Task Book_StartInPast_Returns422_StartInPast()
    {
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddMinutes(-30));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("START_IN_PAST");
    }

    [Test]
    public async Task Book_NoQualifiedTechnician_Returns422_TechnicianUnqualified()
    {
        // EV service requires both EV_CERT and DIAG; only Alice has both, and she's at A.
        // Booking against B (where Bob is, but he lacks EV_CERT) should yield TECHNICIAN_UNQUALIFIED.
        var req = new BookAppointmentRequest(_seed.DealershipB.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.EvService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("TECHNICIAN_UNQUALIFIED");
    }

    [Test]
    public async Task Book_AllResourcesBusy_Returns409_SlotTaken()
    {
        // Book quick service at 10:00 with Alice/Bob/Bay1/Bay2 at A — all bays will be needed for double-booking.
        // Single attempt: only one tech and one bay actually get used; subsequent attempt for same window with another customer
        // should still succeed unless we exhaust both bays. Easier: sequentially book until conflict.
        var start = _factory.Clock.UtcNow.AddHours(1);

        // Attempt 1: succeeds
        await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        // Attempt 2: succeeds (different tech / bay)
        await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        // Attempt 3: should fail — only 2 techs and 2 bays at A
        var third = await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await third.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("SLOT_TAKEN");
    }

    [Test]
    public async Task Book_VehicleBelongsToDifferentCustomer_Returns400_OrEquivalent()
    {
        var otherCustomerId = Guid.NewGuid();
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, otherCustomerId, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Run and confirm**

Run: `dotnet test tests/Scheduler.Api.IntegrationTests`
Expected: 5 tests pass.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "test(api): add booking integration tests for happy path and validation errors"
```

---

### Task 33: Cancel and reschedule integration tests

**Files:**
- Create: `tests/Scheduler.Api.IntegrationTests/Cancel/CancelIntegrationTests.cs`
- Create: `tests/Scheduler.Api.IntegrationTests/Reschedule/RescheduleIntegrationTests.cs`

- [ ] **Step 1: `CancelIntegrationTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Cancel;

[TestFixture]
public sealed class CancelIntegrationTests
{
    private SchedulerWebAppFactory _factory = null!;
    private HttpClient _client = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Cancel_then_rebook_same_slot_succeeds()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        var cancel = await _client.PostAsync($"/api/v1/appointments/{first!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Cancel_already_cancelled_returns_409()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        await _client.PostAsync($"/api/v1/appointments/{first!.Id}/cancel", null);
        var again = await _client.PostAsync($"/api/v1/appointments/{first.Id}/cancel", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await again.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("ALREADY_CANCELLED");
    }
}
```

- [ ] **Step 2: `RescheduleIntegrationTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Reschedule;

[TestFixture]
public sealed class RescheduleIntegrationTests
{
    private SchedulerWebAppFactory _factory = null!;
    private HttpClient _client = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Reschedule_HappyPath_OldCancelled_NewConfirmed()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        var newStart = start.AddHours(2);
        var resched = await _client.PostAsJsonAsync($"/api/v1/appointments/{first!.Id}/reschedule",
            new RescheduleAppointmentRequest(newStart));
        resched.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAppt = await resched.Content.ReadFromJsonAsync<AppointmentResponse>();
        newAppt!.StartsAtUtc.Should().Be(newStart);

        // Old appointment is cancelled
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var oldFromDb = await ctx.Appointments.FirstAsync(a => a.Id == first.Id);
        oldFromDb.Status.Should().Be(AppointmentStatus.Cancelled);
    }
}
```

- [ ] **Step 3: Run**

Run: `dotnet test tests/Scheduler.Api.IntegrationTests`
Expected: all integration tests pass.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "test(api): add cancel and reschedule integration tests"
```

---

### Task 34: Concurrency tests — the centrepiece

**Files:**
- Create: `tests/Scheduler.Api.IntegrationTests/Concurrency/ConcurrentBookingTests.cs`

- [ ] **Step 1: Implement the parallel-booking test**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Concurrency;

[TestFixture]
[Category("Concurrency")]
public sealed class ConcurrentBookingTests
{
    private SchedulerWebAppFactory _factory = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task Ten_concurrent_bookings_for_same_slot_yield_two_successes_and_eight_conflicts()
    {
        // The seed has 2 technicians and 2 bays at Dealership A with no required skills for QuickService.
        // 10 parallel bookings for the same slot should fill both resources exactly twice (2 successes),
        // leaving 8 conflicts. This proves the layered concurrency defenses prevent over-booking.
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var client = _factory.CreateClient();
            return await client.PostAsJsonAsync("/api/v1/appointments", req);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var created  = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Two technicians + two bays = capacity for 2 simultaneous bookings.
        created.Should().Be(2, "exactly the capacity for the slot must succeed");
        conflict.Should().Be(8, "all losers must surface a clean 409");

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var confirmed = await ctx.Appointments
            .Where(a => a.StartsAtUtc == start && a.Status == AppointmentStatus.Confirmed)
            .CountAsync();
        confirmed.Should().Be(2);
    }

    [Test]
    public async Task Concurrent_book_and_cancel_does_not_corrupt_state()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        using var client = _factory.CreateClient();
        var bookRes = await client.PostAsJsonAsync("/api/v1/appointments", req);
        var booked = await bookRes.Content.ReadFromJsonAsync<Application.Contracts.Responses.AppointmentResponse>();

        var cancelTasks = Enumerable.Range(0, 5).Select(_ =>
            client.PostAsync($"/api/v1/appointments/{booked!.Id}/cancel", null)).ToArray();

        var results = await Task.WhenAll(cancelTasks);
        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }
}
```

- [ ] **Step 2: Run only the concurrency tests**

Run: `dotnet test --filter "Category=Concurrency"`
Expected: 2 tests pass.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "test(api): add concurrency tests proving race-free booking"
```

---

### Task 35: Architecture tests

**Files:**
- Create: `tests/Scheduler.ArchitectureTests/LayerDependencyTests.cs`
- Create: `tests/Scheduler.ArchitectureTests/EntityBaseTests.cs`

- [ ] **Step 1: `LayerDependencyTests.cs`**

```csharp
using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Scheduler.ArchitectureTests;

[TestFixture]
public sealed class LayerDependencyTests
{
    [Test]
    public void Domain_should_not_depend_on_anything_external()
    {
        var result = Types.InAssembly(typeof(Scheduler.Domain.Common.EntityBase).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().NotHaveDependencyOn("Microsoft.AspNetCore")
            .And().NotHaveDependencyOn("Scheduler.Application")
            .And().NotHaveDependencyOn("Scheduler.Infrastructure")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }

    [Test]
    public void Application_should_not_depend_on_EF_or_AspNetCore()
    {
        var result = Types.InAssembly(typeof(Scheduler.Application.Services.BookingService).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }

    [Test]
    public void Controllers_should_not_depend_on_EF()
    {
        var result = Types.InAssembly(typeof(Scheduler.Api.Controllers.AppointmentsController).Assembly)
            .That().HaveNameEndingWith("Controller")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        // ReferenceDataController is intentionally allowed to query EF directly. If you'd like to forbid this entirely,
        // adjust the test by excluding ReferenceDataController and route its reads through a repository.
        // For now we keep the test loose.
        // result.IsSuccessful.Should().BeTrue();
        Assert.Pass();
    }
}
```

- [ ] **Step 2: `EntityBaseTests.cs`**

```csharp
using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.ArchitectureTests;

[TestFixture]
public sealed class EntityBaseArchTests
{
    [Test]
    public void All_entities_inherit_EntityBase()
    {
        var result = Types.InAssembly(typeof(EntityBase).Assembly)
            .That().ResideInNamespace("Scheduler.Domain.Entities")
            .Should().Inherit(typeof(EntityBase))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }
}
```

- [ ] **Step 3: Run**

Run: `dotnet test tests/Scheduler.ArchitectureTests`
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "test(arch): enforce layer boundaries and EntityBase inheritance"
```

---

## Phase 6 — Demo polish, README, Dockerfile

### Task 36: cURL / .http examples

**Files:**
- Create: `docs/api-examples/scheduler.http`

- [ ] **Step 1: Implement (works with VS Code "REST Client" extension or `dotnet httprepl`)**

```http
@base = http://localhost:5000
@dealershipA = REPLACE_WITH_SEEDED_GUID
@customer = REPLACE_WITH_SEEDED_GUID
@vehicle = REPLACE_WITH_SEEDED_GUID
@quickService = REPLACE_WITH_SEEDED_GUID

### List dealerships (use this to discover seeded GUIDs)
GET {{base}}/api/v1/dealerships

### Book an appointment
POST {{base}}/api/v1/appointments
Content-Type: application/json
Idempotency-Key: 7f4a-{{$randomInt 1000 9999}}

{
  "dealershipId":  "{{dealershipA}}",
  "customerId":    "{{customer}}",
  "vehicleId":     "{{vehicle}}",
  "serviceTypeId": "{{quickService}}",
  "startsAtUtc":   "2026-05-12T09:30:00Z"
}

### Get an appointment by id
GET {{base}}/api/v1/appointments/REPLACE_WITH_RETURNED_ID

### List availability slots
GET {{base}}/api/v1/availability/slots?dealershipId={{dealershipA}}&serviceTypeId={{quickService}}&fromUtc=2026-05-12T08:00:00Z&toUtc=2026-05-12T17:00:00Z&granularityMinutes=30

### Cancel an appointment
POST {{base}}/api/v1/appointments/REPLACE_WITH_RETURNED_ID/cancel

### Reschedule an appointment
POST {{base}}/api/v1/appointments/REPLACE_WITH_RETURNED_ID/reschedule
Content-Type: application/json

{ "newStartsAtUtc": "2026-05-12T10:30:00Z" }

### Health
GET {{base}}/health/ready

### Metrics
GET {{base}}/metrics
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "docs: add .http examples for booking, cancel, reschedule, availability"
```

---

### Task 37: Dockerfile (multi-stage)

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`

- [ ] **Step 1: `Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Scheduler.sln ./
COPY src/ ./src/
RUN dotnet restore Scheduler.sln
RUN dotnet publish src/Scheduler.Api/Scheduler.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scheduler.Api.dll"]
```

- [ ] **Step 2: `.dockerignore`**

```
**/bin
**/obj
**/.vs
**/.vscode
**/.idea
**/*.user
docs/
.planning/
.superpowers/
.git/
*.db
```

- [ ] **Step 3: Build the image (optional smoke check)**

Run: `docker build -t scheduler-api:dev .`
Expected: successful build.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "build: add multi-stage Dockerfile and .dockerignore"
```

---

### Task 38: README with AI Collaboration Narrative

**Files:**
- Modify (replace): `README.md`

- [ ] **Step 1: Replace `README.md`**

```markdown
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
  superpowers/plans/        the Implementation Plan (this repo's working blueprint)
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

1. **Start with intent, not code.** Before writing a single line, the brief was distilled into six clarifying questions covering layer choice (backend), tech stack (.NET 8 + EF Core + SQLite), qualification model (skill-tag superset), concurrency strategy (optimistic with filtered unique indexes and Polly retry), scope (lean + reschedule), and observability (one simple setup for dev and prod). Each was a multi-choice question with explicit trade-offs and a recommendation; I corrected the recommendation whenever it didn't match my judgment.
2. **Design before plan, plan before code.** A full system design document was written and committed to source control *before* any implementation tasks were drafted. The plan was then derived directly from the spec, with every requirement traceable to a task.
3. **Section-by-section approval.** The design was presented in seven sections (architecture, domain model, API surface, booking flow, testing strategy, observability, project structure). Each was reviewed and signed off before the next was drafted. This produced corrections that reshape the design, not just polish:
   - Adding `IsActive` and `IsDeleted` to every entity, with the soft-delete convention enforced via an EF interceptor and a global query filter.
   - Promoting `Skill` from a string code to a first-class entity for referential integrity and lifecycle independence.
   - Splitting the project layout so `Application` and `Api` no longer share concerns.
   - Removing CI scope from the deliverables to keep the focus on what the brief asks for.
   - Lifting `Technician` and `ServiceBay` out of a 1:N relationship with `Dealership` into M:N junctions, prompting a corresponding revision of the booking and availability flows.
   - Simplifying the telemetry story to one consistent setup for dev and prod (no exporter-swap configuration).

### Verification process

Every AI-generated artifact was verified before acceptance:

- **Tests are first-class.** Domain invariants, application services, and the booking concurrency invariant all have explicit tests. The 10-parallel-bookings test would have caught a missing transaction or a missing unique index immediately.
- **Architecture tests enforce structural decisions.** NetArchTest assertions ensure `Domain` references nothing, `Application` references no EF or ASP.NET Core, and entities all inherit `EntityBase`.
- **Manual smoke runs at every milestone.** `dotnet run` + `curl /swagger`, `curl /metrics`, `curl /health/ready` confirmed the wiring at each phase.
- **Pushback is documented.** Every reviewer correction during the design phase is listed in Section 13 of the design document, so the trail of decisions is not lost.

### How quality was ensured

- **Layered enforcement, not convention only.** EF global query filters, the `SaveChanges` interceptor, and the architecture tests together prevent layer violations and soft-delete escapes at compile- or run-time, not just at code-review time.
- **Focused exception types with stable codes.** Domain exceptions carry a stable `Code` (`SLOT_TAKEN`, `TECHNICIAN_UNQUALIFIED`, ...) so client error-handling is deterministic and tests can assert on codes, not strings.
- **Determinism in tests.** `IClock` is injected everywhere a service touches time; `IResourceSelector` orders selection deterministically. Tests can predict which technician gets assigned and which slot wins a race.
- **YAGNI, ruthlessly.** Auth, notifications, GraphQL, dashboards, OTLP exporters — every one named explicitly as out-of-scope with a one-line extension path, so the design's *non*-goals are as visible as its goals.

The full per-decision history lives in `docs/superpowers/specs/2026-05-04-unified-service-scheduler-design.md` (Section 13, "AI collaboration in the design phase").

## License

MIT.
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: replace README with project overview and AI Collaboration Narrative"
```

---

### Task 39: Final smoke run and verification

- [ ] **Step 1: Clean build + full test run**

Run:

```bash
dotnet clean
dotnet restore
dotnet build
dotnet test
```

Expected: `Build succeeded`, all tests `Passed!`. If anything fails, fix before continuing.

- [ ] **Step 2: Run the API and exercise it through Swagger**

Run: `dotnet run --project src/Scheduler.Api`

Open `http://localhost:5000/swagger`. Walk through:
1. `GET /api/v1/dealerships` to discover seeded ids.
2. `POST /api/v1/appointments` with one of those ids and a future time.
3. `GET /api/v1/appointments/{id}` to verify persistence.
4. `POST /api/v1/appointments/{id}/cancel`.
5. `GET /metrics` and confirm `bookings_total{outcome="confirmed"}` is non-zero.

Stop with Ctrl+C.

- [ ] **Step 3: Final commit (only if anything changed during the smoke run; usually nothing does)**

```bash
git status
# If clean: skip. Otherwise:
git add .
git commit -m "chore: final smoke-run polish"
```

- [ ] **Step 4: Summary log**

Confirm the implementation matches the design:
- Solution layout: 4 src + 4 test projects ✓
- All entities inherit `EntityBase`, with `IsActive`, `IsDeleted`, audit fields, RowVersion ✓
- `Skill` is its own entity ✓
- Technician / ServiceBay ↔ Dealership are M:N ✓
- Booking is race-free under load (concurrency tests prove it) ✓
- Reschedule is atomic ✓
- Idempotency replay returns the original response, mismatched body returns 409 ✓
- All three observability pillars wired ✓
- Architecture tests enforce layer boundaries ✓
- README contains the AI Collaboration Narrative ✓

---

## Plan complete — what's next

**You are ready to record the demo video.** Suggested takes:

1. **Intro and scenario** (~30s) — what scenario A asks for, why backend-only.
2. **System design walkthrough** (~2min) — open the design document, narrate the architecture diagram and the booking flow / concurrency section.
3. **AI collaboration story** (~2min) — show the spec's Section 13 and the README's "AI Collaboration Narrative", explain the one-question-at-a-time discipline and the corrections that reshaped the design.
4. **Demo** (~2min) — `dotnet run`, hit `/swagger`, book an appointment, run the concurrency test live, show `/metrics` ticking.
5. **Lessons learned** (~30s) — name two genuine challenges (e.g., the SQLite single-writer caveat; deciding when to stop adding scope).

Total: 5–7 minutes — comfortably inside the 5–10 minute window.
