using Telegram.Bot;

namespace Evorsio.BotService.Services;

public class WebhookInitializer(ITelegramBotClient botClient, ILogger<WebhookInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var webhookUrl = Environment.GetEnvironmentVariable("TELEGRAM_WEBHOOK_URL");
        var webhookSecret = Environment.GetEnvironmentVariable("TELEGRAM_WEBHOOK_SECRET");

        if (string.IsNullOrEmpty(webhookUrl))
        {
            logger.LogWarning("TELEGRAM_WEBHOOK_URL 环境变量未设置，跳过 Webhook 配置");
            return;
        }

        try
        {
            logger.LogInformation("正在设置 Telegram Webhook: {WebhookUrl}", webhookUrl);

            var webhookInfo = await botClient.GetWebhookInfo(cancellationToken);

            // 如果 webhook 已经设置且 URL 相同，则跳过
            if (webhookInfo.Url == webhookUrl)
            {
                logger.LogInformation("Webhook 已经设置，跳过");
                return;
            }

            // 设置 webhook
            await botClient.SetWebhook(
                url: webhookUrl,
                secretToken: webhookSecret,
                cancellationToken: cancellationToken
            );

            logger.LogInformation("Telegram Webhook 设置成功: {WebhookUrl}", webhookUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "设置 Telegram Webhook 失败");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // 应用关闭时不需要清理 webhook
        return Task.CompletedTask;
    }
}
