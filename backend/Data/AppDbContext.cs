using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TimeLog> TimeLogs => Set<TimeLog>();
}
