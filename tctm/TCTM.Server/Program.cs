using Microsoft.EntityFrameworkCore;
using TCTM.Server;
using TCTM.Server.DataModel;
using TCTM.Server.Hubs;
using TCTM.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<TctmConfiguration>(
    builder.Configuration.GetSection(TctmConfiguration.SectionName));

builder.Services.AddDbContext<TctmDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSingleton<TournamentNotificationService>();
builder.Services.AddSingleton<GamePresenceTracker>();
builder.Services.AddScoped<LiveGameService>();
builder.Services.AddHostedService<ClockMonitorService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure the database is created and up-to-date.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TctmDbContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.MapHub<TournamentHub>("/hubs/tournament");
app.MapHub<LiveGameHub>("/liveGameHub");

app.MapFallbackToFile("/index.html");

app.Run();
