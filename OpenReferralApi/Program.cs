using System.Reflection;
using System.Text.Json.Serialization;
using OpenReferralApi.Models;
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

builder.Services.AddHttpClient();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database")); 
builder.Services.AddSingleton<IDataRepository, DataRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IValidatorService, ValidatorService>();
builder.Services.AddScoped<IRequestService, RequestService>();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

app.Run();
