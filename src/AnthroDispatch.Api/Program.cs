using AnthroDispatch.Api.Endpoints;
using AnthroDispatch.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new() { Title = "AnthroDispatch API", Version = "v1" }); });
builder.Logging.AddConsole(); // built-in logging

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AnthroDispatch v1"));

app.MapHealthEndpoints();
app.MapDatasetEndpoints();
app.MapOptimizationEndpoints();
app.MapExplanationEndpoints();
app.MapScoreIaEndpoints();
app.MapConformanceEndpoints();
app.MapWhatIfEndpoints();
app.MapSraEndpoints();
app.MapExperimentEndpoints();

app.Run();

public partial class Program
{
} // for WebApplicationFactory in tests