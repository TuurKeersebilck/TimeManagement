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

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING environment variable is required but not set. Please set it in your .env file.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(11, 0, 0))
        );

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
