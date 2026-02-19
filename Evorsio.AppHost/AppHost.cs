var builder = DistributedApplication.CreateBuilder(args);

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password",secret:true);
var botServiceSecret = builder.AddParameter("bot-service-secret",secret:true);

var telegramBotSecret = builder.AddParameter("telegram-bot-secret",secret:true);

var postgres = builder.AddPostgres("postgres");
var userdb = postgres.AddDatabase("userdb");

var keycloak = builder.AddKeycloak("keycloak", adminPassword: keycloakAdminPassword, port: 7180)
    .WithRealmImport("./Realms")
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    .WithOtlpExporter();

var userService = builder.AddProject<Projects.Evorsio_UserService>("user-service")
    .WithDaprSidecar()
    .WithReference(userdb)
    .WaitFor(userdb)
    .WithReference(keycloak)
    .WaitFor(keycloak);

var botService = builder.AddProject<Projects.Evorsio_BotService>("bot-service")
    .WithDaprSidecar()
    .WithEnvironment("TELEGRAM_BOT_TOKEN",telegramBotSecret)
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret);

var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/user/{**catch-all}", userService);
    });


builder.Build().Run();