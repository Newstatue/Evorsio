using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;

namespace Evorsio.BotService.Services;

public class UpdatePollingService(ITelegramBotClient botClient, IUpdateHandler updateHandler, ILogger<UpdatePollingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("开始接收 @{BotName} 的更新", me.Username);

        // 开始接收更新
        await botClient.ReceiveAsync(
            updateHandler: updateHandler,
            cancellationToken: stoppingToken
        );
    }
}
