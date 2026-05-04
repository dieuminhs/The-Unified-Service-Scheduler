using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            // Replace IClock with TestClock so tests can control time
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
