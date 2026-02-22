using Dapr.Client;

namespace Evorsio.BotService.Services;

public class TelegramAuthSessionStore(DaprClient daprClient, IConfiguration configuration)
{
    private const string DefaultStateStoreName = "statestore";
    private const string SessionKeyPrefix = "telegram-auth-session";
    private readonly string _stateStoreName = configuration["DAPR_STATESTORE_NAME"] ?? DefaultStateStoreName;

    public async Task<string> CreateAsync(long chatId, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        var session = new AuthSession(chatId, DateTimeOffset.UtcNow.Add(ttl));

        await daprClient.SaveStateAsync(_stateStoreName, BuildSessionKey(token), session);
        return token;
    }

    public async Task<bool> IsValidAsync(string token)
    {
        var session = await daprClient.GetStateAsync<AuthSession?>(_stateStoreName, BuildSessionKey(token));
        if (session is null)
        {
            return false;
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await daprClient.DeleteStateAsync(_stateStoreName, BuildSessionKey(token));
            return false;
        }

        return true;
    }

    public async Task<(bool Success, long ChatId)> TryConsumeAsync(string token)
    {
        var session = await daprClient.GetStateAsync<AuthSession?>(_stateStoreName, BuildSessionKey(token));
        if (session is null)
        {
            return (false, default);
        }

        await daprClient.DeleteStateAsync(_stateStoreName, BuildSessionKey(token));
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return (false, default);
        }

        return (true, session.ChatId);
    }

    private static string BuildSessionKey(string token) => $"{SessionKeyPrefix}:{token}";

    private sealed record AuthSession(long ChatId, DateTimeOffset ExpiresAt);
}