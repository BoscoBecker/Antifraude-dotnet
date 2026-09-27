using AntiFraud.Application;
using AntiFraud.Infrastructure;
using AntiFraud.Infrastructure.Persistence;
using AntiFraud.ServiceDefaults;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Host.UseSerilog(
    (context, _, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AntiFraud.Api")
        .Enrich.WithProperty("ServiceRole", "api")
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(),
    writeToProviders: true);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "AntiFraud API", Version = "v1" });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, InfrastructureHostRole.Api);

var app = builder.Build();
await DatabaseSchemaBootstrap.EnsureReadyAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapDefaultEndpoints();
app.MapControllers();
app.Run();
