using AuthApi.Core.Startup;
using AuthApi.Data.Seeding;
using AuthApi.Extensions;


var builder = WebApplication.CreateBuilder(args);
builder.RegisterServices();
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();

var app = builder.Build();

var env = app.Services.GetRequiredService<IHostEnvironment>();
Console.WriteLine($"Environment: {env.EnvironmentName}");

// to run data seeding use on terminal the command "dotnet run seeddata"
if (args.Length == 1 && args[0].ToLower() == "seeddata") SeedData(app);

app.ConfigureSwagger();
app.ConfigureExceptionHandler();
//app.ConfigureStaticFiles();

app.UseCors("AllowSpecificOrigins");
app.UseAuthorization();
app.UseAuthentication();

app.RegisterEndpoints();

app.Run();

static void SeedData(IHost app)
{
  var scopedFactory = app.Services.GetService<IServiceScopeFactory>();

  using var scope = scopedFactory.CreateScope();
  var service = scope.ServiceProvider.GetService<DataSeeder>();
  service.Seed();
}
