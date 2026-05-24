using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HR.Employee.API.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Docker SQL Server ki connection string sa login aur password ke sath
        // Note: 'YourPasswordHere' ki jagah apna asli password likhein jo apne container banate waqt rakha tha
        optionsBuilder.UseSqlServer("Server=127.0.0.1,1435;Database=HR_Employee_Db;User Id=sa;Password=Dev@12345!;TrustServerCertificate=True;Encrypt=False");

        return new AppDbContext(optionsBuilder.Options);
    }
}
