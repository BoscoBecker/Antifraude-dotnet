using AntiFraud.Application;
using AntiFraud.Infrastructure;
using AntiFraud.Infrastructure.Persistence;
using Serilog;



var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, configuration) =>
        configuration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AntiFraud.Worker")
        .WriteTo.Console());



builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, InfrastructureHostRole.Worker);

var host = builder.Build();
await DatabaseSchemaBootstrap.EnsureReadyAsync(host.Services);
await host.RunAsync();


