extern alias Index;

using Index::MangaMesh.Backend.Tracker.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MangaMesh.IntegrationTests
{
    public abstract class IndexIntegrationTestBase : IDisposable
    {
        protected WebApplicationFactory<Index::Program> Factory { get; set; }
        protected HttpClient Client { get; set; }

        protected IndexIntegrationTestBase()
        {
            Factory = new WebApplicationFactory<Index::Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Aggressive removal of all TrackerDbContext related services
                        var dbContextType = typeof(TrackerDbContext);
                        var servicesToRemove = services.Where(d =>
                            d.ServiceType == dbContextType ||
                            d.ServiceType == typeof(DbContextOptions) ||
                            (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(dbContextType))
                        ).ToList();

                        foreach (var descriptor in servicesToRemove)
                        {
                            services.Remove(descriptor);
                        }

                        // Add InMemory DbContext
                        var dbName = "IndexTestDb_" + Guid.NewGuid();
                        services.AddDbContext<TrackerDbContext>(options =>
                        {
                            options.UseInMemoryDatabase(dbName);
                        });
                    });
                });

            Client = Factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }
}
