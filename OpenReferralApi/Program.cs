using System.Reflection;
using System.Text.Json.Serialization;
using OpenReferralApi.Models.Settings;
using OpenReferralApi.Repositories;
using OpenReferralApi.Repositories.Interfaces;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("ORUK_API_");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddHealthChecks();

builder.Services.AddHttpClient();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database")); 
builder.Services.Configure<GithubSettings>(builder.Configuration.GetSection("Github")); 
builder.Services.Configure<ValidatorSettings>(builder.Configuration.GetSection("Validator")); 
builder.Services.AddSingleton<IDataRepository, DataRepository>();
builder.Services.AddScoped<IPaginationTestingService, PaginationTestingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IValidatorService, ValidatorService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<ITestProfileService, TestProfileService>();

builder.Services.AddScoped<DashboardService>();
// Register as singleton first so it can be injected through Dependency Injection
builder.Services.AddSingleton<PeriodicValidationService>();
// Add as hosted service using the instance registered as singleton before
builder.Services.AddHostedService(provider => provider.GetRequiredService<PeriodicValidationService>());

builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health-check");

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

app.Run();
