using Aspire.Hosting.Yarp.Transforms;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

var compose = builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(enabled: true);

var redis = builder.AddRedis("redis");

if (!builder.Environment.IsDevelopment())
{
    redis.WithDataVolume("redis-data");
}

redis.WithRedisInsight();
var redisHost = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var redisPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

var stateStore = builder.AddDaprComponent("statestore", "state.redis")
    .WithMetadata("redisHost", ReferenceExpression.Create($"{redisHost}:{redisPort}"))
    .WithMetadata("enableTLS", "true")
    .WaitFor(redis);

var pubSub = builder.AddDaprComponent("pubsub", "pubsub.redis")
    .WithMetadata("redisHost", ReferenceExpression.Create($"{redisHost}:{redisPort}"))
    .WithMetadata("enableTLS", "true")
    .WaitFor(redis);

if (redis.Resource.PasswordParameter is not null)
{
    stateStore.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
    pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
}

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password",secret:true);
var keycloakAdminUsername = builder.AddParameter("keycloak-admin-username");
var postgresUsername = builder.AddParameter("postgres-username");
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var publicBaseUrl = builder.AddParameter("public-base-url");
var keycloakRealm = builder.AddParameter("keycloak-realm");
var keycloakHostname = builder.AddParameter("keycloak-hostname");
var botServiceSecret = builder.AddParameter("bot-service-secret",secret:true);

// Telegram Bot 令牌
var telegramBotToken = builder.AddParameter("telegram-bot-token",secret:true);

// Telegram Webhook 密钥 
var telegramWebhookSecret = builder.AddParameter("telegram-webhook-secret",secret:true);

var postgres = builder.AddPostgres("postgres", userName: postgresUsername, password: postgresPassword);

if (!builder.Environment.IsDevelopment())
{
    postgres.WithDataVolume("postgres-data");
}

postgres.WithPgAdmin();
var userDb = postgres.AddDatabase("userdb");
var keycloakDb = postgres.AddDatabase("keycloakdb");
var postgresHost = postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var postgresPort = postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.4")
    .WithHttpEndpoint(port: 7180, targetPort: 8080, name: "http")
    .WithHttpEndpoint(port: 7181, targetPort: 9000, name: "management")
    .WithBindMount("./Realms", "/opt/keycloak/data/import", isReadOnly: true)
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", keycloakAdminUsername)
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)

    // keycloak client 环境变量占位符配置
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    
    .WithEnvironment("KC_HOSTNAME", keycloakHostname)
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL_HOST", postgresHost)
    .WithEnvironment("KC_DB_URL_PORT", postgresPort)
    .WithEnvironment("KC_DB_URL_DATABASE", "keycloakdb")
    .WithEnvironment("KC_DB_USERNAME", postgresUsername)
    .WithEnvironment("KC_DB_PASSWORD", postgresPassword)
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", "true")
    .WithEnvironment("KC_HTTP_RELATIVE_PATH", "/auth")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HTTPS_ENABLED", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
    .WithEnvironment("KC_TRUSTED_PROXIES", "*")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WaitFor(keycloakDb)
    .WithArgs("start", "--import-realm", "--db=postgres")
    .WithHttpHealthCheck("/auth/health/ready", endpointName: "management")
    .WithOtlpExporter();

var userService = builder.AddProject<Projects.Evorsio_UserService>("user-service")
    .WithReference(userDb)
    .WaitFor(userDb)
    .WithEnvironment("PUBLIC_BASE_URL", publicBaseUrl)
    .WithEnvironment("KEYCLOAK_REALM", keycloakRealm)
    .WaitFor(keycloak)
    .WaitFor(redis)
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

var botService = builder.AddProject<Projects.Evorsio_BotService>("bot-service")
    .WithEnvironment("PUBLIC_BASE_URL", publicBaseUrl)
    .WithEnvironment("KEYCLOAK_REALM", keycloakRealm)
    .WithEnvironment("TELEGRAM_BOT_TOKEN", telegramBotToken)
    .WithEnvironment("TELEGRAM_WEBHOOK_SECRET", telegramWebhookSecret)
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    .WaitFor(keycloak)
    .WaitFor(redis)
    .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore).WithReference(pubSub));

var gateway = builder.AddYarp("gateway")
    .WithHostPort(8000)
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/auth/{**catch-all}", keycloak.GetEndpoint("http"))
            .WithTransformXForwarded();
        // User service API
        yarp.AddRoute("/user/{**catch-all}", userService);
        // Bot service API
        yarp.AddRoute("/bot/{**catch-all}", botService);
    });



if (builder.Environment.IsDevelopment())
{
    // Cloudflare Tunnel Token
    var cloudflareTunnelToken = builder.AddParameter("cloudflare-tunnel-token", secret: true);
    builder.AddContainer("cloudflared", "cloudflare/cloudflared", "1818-66587173e2cd")
        .WithReference(gateway)
        .WaitFor(gateway)
        .WithArgs(
            "tunnel",
            "--no-autoupdate",
            "run",
            "--token",
            cloudflareTunnelToken
        );
}

builder.Build().Run();