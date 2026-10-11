using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrimeInnovation.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core tooling (migrations add/list, migration bundles) instead of booting the API host,
/// so tooling never runs Program.cs and needs only a connection string — not JWT keys or other secrets.
/// The connection string comes from ConnectionStrings__Default, falling back to local SQL Express.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDefault =
        "Server=localhost\\SQLEXPRESS;Database=PrimeInnovationDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? LocalDefault : connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
