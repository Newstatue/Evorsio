using Telegram.Bot;

namespace Evorsio.BotService.Services;

public class TelegramWebhookRegistrationService(
    ITelegramBotClient telegramBotClient,
    IConfiguration configuration,
    ILogger<TelegramWebhookRegistrationService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? webhookUrl = null;
        var publicBaseUrl = configuration["PUBLIC_BASE_URL"];
        if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
        {
            var builder = new UriBuilder(publicBaseUri)
            {
                Path = "/bot/telegram/webhook",
                Query = string.Empty
            };

            webhookUrl = builder.Uri.ToString();
        }

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogWarning("无法从 PUBLIC_BASE_URL 推导 webhook 地址，跳过 webhook 注册。");
            return;
        }

        var webhookSecret = configuration["TELEGRAM_WEBHOOK_SECRET"];

        await telegramBotClient.SetWebhook(
            webhookUrl,
            secretToken: webhookSecret,
            cancellationToken: cancellationToken);

        logger.LogInformation("Telegram webhook 已设置为 {WebhookUrl}", webhookUrl);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}