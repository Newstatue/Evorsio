using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password",secret:true);
var botServiceSecret = builder.AddParameter("bot-service-secret",secret:true);

// Telegram Bot 令牌
var telegramBotToken = builder.AddParameter("telegram-bot-token",secret:true);

// Telegram Webhook 密钥 
var telegramWebhookSecret = builder.AddParameter("telegram-webhook-secret",secret:true);

// Cloudflare Tunnel Token
var cloudflareTunnelToken = builder.AddParameter("cloudflare-tunnel-token", secret: true);

var postgres = builder.AddPostgres("postgres");
var userdb = postgres.AddDatabase("userdb");

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.4")
    .WithHttpEndpoint(port: 7180, targetPort: 8080, name: "http")
    .WithHttpEndpoint(port: 7181, targetPort: 9000, name: "management")
    .WithBindMount("./Realms", "/opt/keycloak/data/import", isReadOnly: true)
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)

    // keycloak client 环境变量占位符配置
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    
    .WithEnvironment("KC_HOSTNAME", "https://api.evorsio.com/auth")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", "true")
    .WithEnvironment("KC_HTTP_RELATIVE_PATH", "/auth")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HTTPS_ENABLED", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
    .WithEnvironment("KC_TRUSTED_PROXIES", "*")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithArgs("start", "--import-realm", "--db=dev-file")
    .WithHttpHealthCheck("/auth/health/ready", endpointName: "management")
    .WithOtlpExporter();

var userService = builder.AddProject<Projects.Evorsio_UserService>("user-service")
    .WithReference(userdb)
    .WaitFor(userdb)
    .WithEnvironment("KEYCLOAK_AUTHORITY", "https://api.evorsio.com/auth/realms/Evorsio")
    .WaitFor(keycloak)
    .WithDaprSidecar();

var botService = builder.AddProject<Projects.Evorsio_BotService>("bot-service")
    .WithEnvironment("PUBLIC_BASE_URL", "https://api.evorsio.com")
    .WithEnvironment("TELEGRAM_BOT_TOKEN", telegramBotToken)
    .WithEnvironment("TELEGRAM_WEBHOOK_URL", "https://api.evorsio.com/bot/telegram/webhook")
    .WithEnvironment("TELEGRAM_WEBHOOK_SECRET", telegramWebhookSecret)
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    .WithEnvironment("KEYCLOAK_AUTHORITY", "https://api.evorsio.com/auth/realms/Evorsio")
    .WaitFor(keycloak)
    .WithDaprSidecar();

var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/auth/{**catch-all}", keycloak.GetEndpoint("http"))
            .WithTransformXForwarded();
        // User service API
        yarp.AddRoute("/user/{**catch-all}", userService);
        // Bot service API
        yarp.AddRoute("/bot/{**catch-all}", botService);
    });

var cloudflared = builder.AddContainer("cloudflared", "cloudflare/cloudflared", "1818-66587173e2cd")
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithArgs(
        "tunnel",
        "--no-autoupdate",
        "run",
        "--token",
        cloudflareTunnelToken
    );

builder.Build().Run();