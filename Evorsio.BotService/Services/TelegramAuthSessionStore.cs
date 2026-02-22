using System.Collections.Concurrent;

namespace Evorsio.BotService.Services;

public class TelegramAuthSessionStore
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();

    public string Create(long chatId, TimeSpan ttl)
    {
        CleanupExpired();

        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new AuthSession(chatId, DateTimeOffset.UtcNow.Add(ttl));
        return token;
    }

    public bool IsValid(string token)
    {
        CleanupExpired();
        return _sessions.ContainsKey(token);
    }

    public bool TryConsume(string token, out long chatId)
    {
        CleanupExpired();

        chatId = default;
        if (!_sessions.TryRemove(token, out var session))
        {
            return false;
        }

        chatId = session.ChatId;
        return true;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record AuthSession(long ChatId, DateTimeOffset ExpiresAt);
}