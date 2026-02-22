using Telegram.Bot;

namespace Evorsio.BotService.Services;

public class TelegramWebhookRegistrationService : IHostedService
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramWebhookRegistrationService> _logger;

    public TelegramWebhookRegistrationService(
        ITelegramBotClient telegramBotClient,
        IConfiguration configuration,
        ILogger<TelegramWebhookRegistrationService> logger)
    {
        _telegramBotClient = telegramBotClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var webhookUrl = _configuration["TELEGRAM_WEBHOOK_URL"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning("未设置 TELEGRAM_WEBHOOK_URL，跳过 webhook 注册。");
            return;
        }

        var webhookSecret = _configuration["TELEGRAM_WEBHOOK_SECRET"];

        await _telegramBotClient.SetWebhook(
            webhookUrl,
            secretToken: webhookSecret,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Telegram webhook 已设置为 {WebhookUrl}", webhookUrl);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}