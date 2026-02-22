using Evorsio.AuthService.Data;
using Evorsio.AuthService.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();

builder.AddNpgsqlDbContext<UserDbContext>(connectionName: "userdb");

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var publicBaseUrl = Environment.GetEnvironmentVariable("PUBLIC_BASE_URL");
if (string.IsNullOrWhiteSpace(publicBaseUrl))
{
    throw new Exception("请设置 PUBLIC_BASE_URL。");
}

var keycloakRealm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM");
if (string.IsNullOrWhiteSpace(keycloakRealm))
{
    throw new Exception("请设置 KEYCLOAK_REALM。");
}

var keycloakAuthority = $"{publicBaseUrl.TrimEnd('/')}/auth/realms/{keycloakRealm}";

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.MetadataAddress = $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";
        options.Audience = "account";
        options.RequireHttpsMetadata = false;

        options.Events.OnTokenValidated = context =>
        {
            var jwt = context.SecurityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
            var azpClaim = jwt?.Claims.FirstOrDefault(c => c.Type == "azp")?.Value;

            if (azpClaim != "user-service")
            {
                context.Fail("JWT Token 的 azp 与预期客户端不匹配");
            }

            return Task.CompletedTask;
        };
    });

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    context.Database.Migrate();

    if (!context.Users.Any())
    {
        context.Users.Add(new User
        {
            Name = "test",
            Email = "test@evorsio.com",
            Locale = "zh-CN"
        });
        context.SaveChanges();
    }
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days.
    // You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Run();