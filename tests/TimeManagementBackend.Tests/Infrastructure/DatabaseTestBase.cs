using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>
/// Base for tests that need a real Postgres. xUnit builds a new instance of the test class per
/// test method, so every test method gets its own database and cannot see another test's rows.
/// </summary>
public abstract class DatabaseTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly List<AppDbContext> _contexts = [];
    private readonly List<ServiceProvider> _providers = [];
    private string _connectionString = string.Empty;

    /// <summary>The context used to arrange data. Services under test normally get their own.</summary>
    protected AppDbContext Db { get; private set; } = null!;

    /// <summary>This test's own database, for code that builds its own DI container.</summary>
    protected string ConnectionString => _connectionString;

    protected IMapper Mapper { get; } = TestMapper.Create();

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateDatabaseAsync();
        Db = NewContext();
    }

    /// <summary>
    /// A second context over the same database. Read results back through one of these: the
    /// arrange context's change tracker would otherwise hand back in-memory entities and hide
    /// a service that never actually persisted its work.
    /// </summary>
    protected AppDbContext NewContext()
    {
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);
        _contexts.Add(context);
        return context;
    }

    /// <summary>A real UserManager over the given context — not a substitute, so password hashing,
    /// validators and the Identity stores behave exactly as they do in the running app.</summary>
    protected UserManager<User> CreateUserManager(AppDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        }).AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<UserManager<User>>();
    }

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts) await context.DisposeAsync();
        foreach (var provider in _providers) await provider.DisposeAsync();
    }
}
