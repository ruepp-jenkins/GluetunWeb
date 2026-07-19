using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GluetunWeb.Api.Data;

/// <summary>
/// Used only by the EF Core CLI (migrations). Having this means the tools construct the context
/// directly instead of executing Program.cs startup (which would try to migrate/connect Docker).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=gluetunweb-design.db")
            .Options;
        return new AppDbContext(options);
    }
}
