using Evorsio.BotService.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Telegram.Bot;
using Telegram.Bot.Polling;

var builder = WebApplication.CreateBuilder(args);

// ✅ 先注册所有服务
builder.Services.AddControllers();
builder.Services.AddDaprClient();

// 注册 TelegramBotClient 单例
var cts = new CancellationTokenSource();
var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
if (string.IsNullOrEmpty(botToken))
{
    throw new Exception("请在环境变量中设置 TELEGRAM_BOT_TOKEN");
}

var botSecret = Environment.GetEnvironmentVariable("BOT_SERVICE_SECRET");
if (string.IsNullOrEmpty(botSecret))
{
    throw new Exception("请在环境变量中设置 BOT_SERVICE_SECRET");
}

builder.Services.AddSingleton<ITelegramBotClient>(_ =>
    new TelegramBotClient(botToken, cancellationToken: cts.Token)
);

// ✅ 注册 BotService 到 DI 容器 (作为 IUpdateHandler 单例)
builder.Services.AddSingleton<IUpdateHandler>(sp =>
{
    var botClient = sp.GetRequiredService<ITelegramBotClient>();
    var logger = sp.GetRequiredService<ILogger<BotService>>();
    return new BotService(botClient, logger);
});

// ✅ 根据环境选择 Webhook 或 Polling 模式
var useWebhook = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TELEGRAM_WEBHOOK_URL"));

if (useWebhook)
{
    // 生产环境使用 Webhook
    builder.Services.AddHostedService<WebhookInitializer>();
    Console.WriteLine("Bot 将使用 Webhook 模式");
}
else
{
    // 开发环境使用 Long Polling
    builder.Services.AddHostedService<UpdatePollingService>();
    Console.WriteLine("Bot 将使用 Long Polling 模式");
}

// 注册认证
builder.Services.AddAuthentication(options =>
    {
        // 默认 Scheme 用 Cookie
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // 远程认证用 OIDC
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddKeycloakOpenIdConnect(
        serviceName: "keycloak",
        realm: "Evorsio",
        options =>
        {
            options.ClientId = "bot-service";
            options.ClientSecret = botSecret;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;

            // 开发环境允许 HTTP
            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }

            // 指定 SignInScheme 为 Cookie
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        });


// 注册 OpenApi
builder.Services.AddOpenApi();

// ✅ AddServiceDefaults() 放最后
builder.AddServiceDefaults();

var app = builder.Build();

// ✅ 中间件顺序：先认证授权，再映射端点
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ✅ 端点映射放在最后
app.MapControllers();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ✅ 应用关闭时释放 CancellationTokenSource
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    cts.Dispose();
});

app.Run();
