using Aspire.Hosting.Yarp.Transforms;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var dapr = builder.AddDapr();

var compose = builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(enabled: true);

var redis = builder.AddRedis("redis")
    .WithEnvironment("REDIS_TLS", "no");

if (builder.Environment.IsProduction())
{
    redis.WithDataVolume("redis-data");
}

redis.WithRedisInsight();
var redisDaprHost = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var redisDaprPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

// Dapr Redis 组件配置 - 使用 Redis 主端点 TLS（发布安全）
var stateStore = dapr.AddDaprComponent("statestore", "state.redis")
    .WithMetadata("redisHost", ReferenceExpression.Create($"{redisDaprHost}:{redisDaprPort}"))
    .WithMetadata("enableTLS", "true")
    .WaitFor(redis);

var pubSub = dapr.AddDaprComponent("pubsub", "pubsub.redis")
    .WithMetadata("redisHost", ReferenceExpression.Create($"{redisDaprHost}:{redisDaprPort}"))
    .WithMetadata("enableTLS", "true")
    .WaitFor(redis);

if (redis.Resource.PasswordParameter is not null)
{
    stateStore.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
    pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
}

var SmtpUser = builder.AddParameter("smtp-user");
var SmtpPassword = builder.AddParameter("smtp-password", secret: true);
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloakAdminUsername = builder.AddParameter("keycloak-admin-username");
var postgresUsername = builder.AddParameter("postgres-username");
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var publicBaseUrl = builder.AddParameter("public-base-url");
var keycloakRealm = builder.AddParameter("keycloak-realm");
var keycloakHostname = builder.AddParameter("keycloak-hostname");
var evorsioDefaultUserPassword = builder.AddParameter("evorsio-default-user-password", secret: true);
var botServiceSecret = builder.AddParameter("bot-service-secret", secret: true);
var aspireDashboardPublicUrl = builder.AddParameter("aspire-dashboard-public-url");
var aspireDashboardClientSecret = builder.AddParameter("aspire-dashboard-client-secret", secret: true);
var blogPublicUrl = builder.AddParameter("blog-public-url");


// Telegram Bot 令牌
var telegramBotToken = builder.AddParameter("telegram-bot-token", secret: true);

// Telegram Webhook 密钥 
var telegramWebhookSecret = builder.AddParameter("telegram-webhook-secret", secret: true);

var postgres = builder.AddPostgres("postgres", userName: postgresUsername, password: postgresPassword);
postgres.WithBindMount("./DatabaseInit", "/docker-entrypoint-initdb.d", isReadOnly: true);

if (builder.Environment.IsProduction())
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
    .WithEnvironment("ASPIRE_DASHBOARD_PUBLIC_URL", aspireDashboardPublicUrl)
    .WithEnvironment("ASPIRE_DASHBOARD_CLIENT_SECRET", aspireDashboardClientSecret)
    .WithEnvironment("PUBLIC_BASE_URL", publicBaseUrl)
    .WithEnvironment("KEYCLOAK_HOSTNAME", keycloakHostname)
    .WithEnvironment("EVORSIO_DEFAULT_USER_PASSWORD", evorsioDefaultUserPassword)

    .WithEnvironment("KC_HOSTNAME", keycloakHostname)
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL_HOST", postgresHost)
    .WithEnvironment("KC_DB_URL_PORT", postgresPort)
    .WithEnvironment("KC_DB_URL_DATABASE", "keycloakdb")
    .WithEnvironment("KC_DB_USERNAME", postgresUsername)
    .WithEnvironment("KC_DB_PASSWORD", postgresPassword)
    .WithEnvironment("KC_HOSTNAME_STRICT", "true")
    .WithEnvironment("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", "false")
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

var ghost = builder.AddContainer("ghost", "ghost", "5-alpine")
    .WithEnvironment("url", blogPublicUrl)
    .WithEnvironment("NODE_ENV", builder.Environment.IsProduction() ? "production" : "development")
    .WithEnvironment("database__client", "sqlite3")
    .WithEnvironment("database__connection__filename", "content/data/ghost.db")

    // --- Mailgun SMTP 配置 ---
    .WithEnvironment("mail__from", $"Blog <{SmtpUser}>")
    .WithEnvironment("mail__transport", "SMTP")
    .WithEnvironment("mail__options__host", "smtp.mailgun.org")
    .WithEnvironment("mail__options__port", "465")
    .WithEnvironment("mail__options__secure", "true")
    .WithEnvironment("mail__options__auth__user", SmtpUser)
    .WithEnvironment("mail__options__auth__pass", SmtpPassword)

    .WithEndpoint(targetPort: 2368, scheme: "http", name: "http")
    .WithOtlpExporter();

if (builder.Environment.IsProduction())
{
    ghost.WithVolume("ghost-content", "/var/lib/ghost/content");
}

var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/auth/{**catch-all}", keycloak.GetEndpoint("http"))
            .WithTransformXForwarded();
        // User service API
        yarp.AddRoute("/user/{**catch-all}", userService.GetEndpoint("http"));
        // Bot service API
        yarp.AddRoute("/bot/{**catch-all}", botService.GetEndpoint("http"));
    });

if (builder.Environment.IsProduction())
{
    builder.AddContainer("nginx", "nginx", "1.27-alpine")
           .WithBindMount("./Nginx/nginx.conf", "/etc/nginx/nginx.conf", isReadOnly: true)
           .WithBindMount("./Nginx/certs", "/etc/nginx/certs")
           .WithBindMount("./Nginx/acme-challenge", "/var/www/certbot")
           .WithEndpoint(port: 80, targetPort: 80, scheme: "http", name: "http", isExternal: true)
           .WithEndpoint(port: 443, targetPort: 443, scheme: "https", name: "https", isExternal: true)
           .WaitFor(gateway)
           .WaitFor(ghost);
}

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