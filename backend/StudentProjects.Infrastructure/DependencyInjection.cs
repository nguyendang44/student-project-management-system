using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentProjects.Infrastructure.Persistence;
namespace StudentProjects.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureSkeleton(this IServiceCollection services, IConfiguration configuration)
    {
        // WebApplicationFactory may apply test configuration after Program's service-registration phase.
        // Resolve the real connection only when the DbContext options are constructed; this
        // also lets integration tests replace the SQL Server provider with EF Core InMemory.
        services.AddDbContext<StudentProjectsDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure ConnectionStrings:Default for SQL Server.");
            options.UseSqlServer(connection);
        });
        return services;
    }
}
