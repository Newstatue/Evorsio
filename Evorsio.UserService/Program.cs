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

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm:"Evorsio",
        options =>
        {
            options.Audience = "account";
            
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
            
            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        }
    );

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
            Name = "Admin",
            Email = "admin@evorsio.local",
            Locale = "en-US"
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