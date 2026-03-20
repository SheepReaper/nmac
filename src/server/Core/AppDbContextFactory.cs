using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NMAC.Core;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=subscribing-test;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}