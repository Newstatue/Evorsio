using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols;
using Telegram.Bot;
using Evorsio.BotService.Services;

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

var publicBaseUrl = Environment.GetEnvironmentVariable("PUBLIC_BASE_URL");
if (string.IsNullOrWhiteSpace(publicBaseUrl))
{
    throw new Exception("请在环境变量中设置 PUBLIC_BASE_URL");
}

var keycloakRealm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM");
if (string.IsNullOrWhiteSpace(keycloakRealm))
{
    throw new Exception("请在环境变量中设置 KEYCLOAK_REALM");
}
var keycloakAuthority = $"{publicBaseUrl.TrimEnd('/')}/auth/realms/{keycloakRealm}";

builder.Services.AddSingleton<ITelegramBotClient>(_ =>
    new TelegramBotClient(botToken, cancellationToken: cts.Token)
);
builder.Services.AddSingleton<TelegramAuthSessionStore>();
builder.Services.AddHostedService<TelegramWebhookRegistrationService>();


// 注册认证
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = keycloakAuthority;
        options.MetadataAddress = $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";
        options.ClientId = "bot-service";
        options.ClientSecret = botSecret;
        options.CallbackPath = "/bot/telegram/signin-oidc";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = false;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.RedirectUri = $"{publicBaseUrl.TrimEnd('/')}{options.CallbackPath}";
            return Task.CompletedTask;
        };
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
