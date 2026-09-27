using AntiFraud.AppHost;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Postgres;
var builder = DistributedApplication.CreateBuilder(args);

var devCredentials = LocalDevCredentials.FromConfiguration(builder.Configuration);

Console.WriteLine(
    "[AppHost] User Secrets (API): Postgres user={0}, database={1}, senha length={2}.",
    devCredentials.PostgresUser,
    devCredentials.PostgresDatabase,
    devCredentials.PostgresPassword.Length);

var postgresUser = builder.AddParameter("postgres-user", devCredentials.PostgresUser);
var postgresPassword = builder.AddParameter("postgres-password", devCredentials.PostgresPassword, secret: true);
var rabbitUser = builder.AddParameter("rabbitmq-user", devCredentials.RabbitUser);
var rabbitPassword = builder.AddParameter("rabbitmq-password", devCredentials.RabbitPassword, secret: true);

var initDir = ResolvePostgresInitDirectory();

var postgresBuilder = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithImageTag("16")
    .WithDataVolume("antifraud-aspire-postgres-data", isReadOnly: false)
    .WithPgAdmin(pgAdmin => ConfigurePgAdminWithLogin(pgAdmin, devCredentials));

if (initDir is not null)
{
    postgresBuilder = postgresBuilder.WithInitFiles(initDir);
}

var antifraudDb = postgresBuilder.AddDatabase("AntiFraud");
var rabbit = builder.AddRabbitMQ("rabbitmq", rabbitUser, rabbitPassword)
    .WithDataVolume("antifraud-aspire-rabbitmq-data", isReadOnly: false)
    .WithManagementPlugin();

var api = builder.AddProject("antifraud-api", "../src/AntiFraud.Api/AntiFraud.Api.csproj");
var worker = builder.AddProject("antifraud-worker", "../src/AntiFraud.Worker/AntiFraud.Worker.csproj");

api.WithReference(antifraudDb)
    .WithReference(rabbit)
    .WaitFor(antifraudDb)
    .WaitFor(rabbit);

worker.WithReference(antifraudDb)
    .WithReference(rabbit)
    .WaitFor(antifraudDb)
    .WaitFor(rabbit);

Console.WriteLine(
    "[AppHost] Volumes: antifraud-aspire-postgres-data / antifraud-aspire-rabbitmq-data.");
Console.WriteLine(
    "[AppHost] pgAdmin Aspire: http://localhost:15433 (login: {0}).",
    devCredentials.PgAdminEmail);

builder.Build().Run();

static void ConfigurePgAdminWithLogin(
    IResourceBuilder<PgAdminContainerResource> pgAdmin,
    LocalDevCredentials credentials)
{
    pgAdmin.WithHostPort(15433)
        .WithEnvironment(context =>
        {
            context.EnvironmentVariables["PGADMIN_CONFIG_SERVER_MODE"] = "True";
            context.EnvironmentVariables["PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED"] = "False";
            context.EnvironmentVariables["PGADMIN_DEFAULT_EMAIL"] = credentials.PgAdminEmail;
            context.EnvironmentVariables["PGADMIN_DEFAULT_PASSWORD"] = credentials.PgAdminPassword;
        });
}

static string? ResolvePostgresInitDirectory()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "postgres-init"),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "postgres-init")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "scripts"))
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "ddl.sql")))
        {
            return dir;
        }
    }

    return null;
}
