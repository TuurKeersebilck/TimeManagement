using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace TimeManagementBackend.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        LoadDotEnv();

        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseSqlite("Data Source=timemanagement.db");
        }
        else
        {
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(11, 0, 0)));
        }

        return new AppDbContext(optionsBuilder.Options);
    }

    private static void LoadDotEnv()
    {
        var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envFile)) return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
                value = value[1..^1];

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
