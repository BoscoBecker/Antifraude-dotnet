using AntiFraud.Application;
using AntiFraud.Infrastructure;
using AntiFraud.Infrastructure.Persistence;
using AntiFraud.ServiceDefaults;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSerilog(
    (_, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AntiFraud.Worker")
        .Enrich.WithProperty("ServiceRole", "fraud-evaluation-consumer")
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(),
    writeToProviders: true);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, InfrastructureHostRole.Worker);

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("AntiFraud.Worker")
    .LogInformation("AntiFraud.Worker starting (RabbitMQ consumer; structured logs → OpenTelemetry)");

await DatabaseSchemaBootstrap.EnsureReadyAsync(host.Services);
await host.RunAsync();
