using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Telegram.Bot;

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

// Map endpoints
app.MapControllers();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.Run();