using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentProjects.Infrastructure.Persistence;
namespace StudentProjects.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureSkeleton(this IServiceCollection services, IConfiguration configuration)
    {
        // Optional: the scaffold starts without a database. Set connection string after schema approval.
        var connection = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(connection))
            services.AddDbContext<StudentProjectsDbContext>(options => options.UseSqlServer(connection));
        return services;
    }
}
