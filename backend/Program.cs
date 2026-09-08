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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Server=localhost;Port=3306;Database=issue_tracker;User=root;Password=root;";
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.EnsureSchema(db);
    DbSeeder.Seed(db);
}

app.UseCors();
app.MapControllers();
app.Run();
