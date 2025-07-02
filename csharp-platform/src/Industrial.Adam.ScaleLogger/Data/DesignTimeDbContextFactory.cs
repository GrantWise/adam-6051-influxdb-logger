// Industrial.Adam.ScaleLogger - Design-time DbContext Factory
// Required for Entity Framework migrations

using Industrial.Adam.ScaleLogger.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.ScaleLogger.Data;

/// <summary>
/// Design-time factory for ScaleLoggerDbContext
/// Required for Entity Framework CLI tools to create migrations
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ScaleLoggerDbContext>
{
    public ScaleLoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScaleLoggerDbContext>();
        
        // Use SQLite for design-time migrations (simplest for development)
        optionsBuilder.UseSqlite("Data Source=design_time.db");
        
        // Create design-time database configuration
        var databaseConfig = new DatabaseConfig
        {
            Provider = DatabaseProvider.SQLite,
            ConnectionString = "Data Source=design_time.db"
        };
        
        var configOptions = Options.Create(databaseConfig);
        
        return new ScaleLoggerDbContext(optionsBuilder.Options, configOptions);
    }
}