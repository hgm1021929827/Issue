using Issue.Api.Data;
using Issue.Api.Filters;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<AppExceptionFilter>());
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<IssueService>();
builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<DirectoryService>();
builder.Services.AddScoped<TrackTodoService>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<WorkHourService>();
builder.Services.AddScoped<ProjectImportService>();
builder.Services.AddScoped<IssueImportService>();
builder.Services.AddSingleton<ImportNotFoundCache>();
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin => IsAllowedCorsOrigin(origin, corsOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var provider = (builder.Configuration["Database:Provider"] ?? "SqlServer").Trim();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var cs = builder.Configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("缺少 ConnectionStrings:SqlServer。");
        options.UseSqlServer(cs);
    }
    else if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
        var cs = builder.Configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("缺少 ConnectionStrings:MySql。");
        options.UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 36)));
    }
    else
    {
        throw new InvalidOperationException(
            $"Database:Provider 必須是 SqlServer 或 MySql，目前為「{provider}」。");
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.EnsureDatabase(db);
}

if (args.Any(a => string.Equals(a, "--ensure-db", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("資料庫檢查與初始資料補齊完成。");
    return;
}

app.UseCors();
app.MapControllers();
app.Run();

static bool IsAllowedCorsOrigin(string origin, string[] extraOrigins)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (extraOrigins.Any(o => string.Equals(o?.Trim(), origin, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}
