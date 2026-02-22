using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

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

var keycloak = builder.AddKeycloak("keycloak", adminPassword: keycloakAdminPassword, port: 7180)
    .WithRealmImport("./Realms")
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret)
    .WithEnvironment("KC_HOSTNAME", "api.evorsio.local")
    .WithEnvironment("KC_HOSTNAME_PORT", "443")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HTTPS_ENABLED", "false")
    .WithOtlpExporter();

var userService = builder.AddProject<Projects.Evorsio_UserService>("user-service")
    .WithDaprSidecar()
    .WithReference(userdb)
    .WaitFor(userdb)
    .WithReference(keycloak);

var botService = builder.AddProject<Projects.Evorsio_BotService>("bot-service")
    .WithDaprSidecar()
    .WithEnvironment("TELEGRAM_BOT_TOKEN", telegramBotToken)
    // .WithEnvironment("TELEGRAM_WEBHOOK_URL", "https://your-ngrok-url.ngrok-free.app/api/bot/telegram/update") // 本地开发注释掉，生产环境启用
    .WithEnvironment("TELEGRAM_WEBHOOK_SECRET", telegramWebhookSecret)
    .WithEnvironment("BOT_SERVICE_SECRET", botServiceSecret);

var gateway = builder.AddYarp("gateway")
    .WithHostPort(8080)
    .WithConfiguration(yarp =>
    {
        // Keycloak - catch all other routes
        yarp.AddRoute("/{**catch-all}", keycloak);
        // User service API
        yarp.AddRoute("/api/user/{**catch-all}", userService);
    });

var cloudflared = builder.AddContainer("cloudflared", "cloudflare/cloudflared", "latest")
    .WithEnvironment("TUNNEL_TOKEN", cloudflareTunnelToken)
    .WithEntrypoint("/busybox/sh")
    .WithArgs(["-c", "echo 'nameserver 8.8.8.8' > /etc/resolv.conf && cloudflared tunnel --no-autoupdate run"])
    .WaitFor(gateway);

builder.Build().Run();