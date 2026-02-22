using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;

namespace Evorsio.BotService.Services;

public class BotService(ITelegramBotClient botClient, ILogger<BotService> logger) : IUpdateHandler
{
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text ?? "";

            logger.LogInformation("收到来自 {ChatId} 的消息: {Text}", chatId, text);

            await HandleMessageAsync(chatId, text);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram Bot 错误 (来源: {Source})", source);
        return Task.CompletedTask;
    }

    public async Task HandleMessageAsync(long chatId, string text)
    {
        // 检查是否是 /start 命令
        if (text.Trim() == "/start")
        {
            const string message = "欢迎使用 Evorsio Bot！\n\n" +
                                   "你可以发送菜单、查看订单或获取帮助。\n" +
                                   "示例命令：\n" +
                                   "/menu - 查看菜单\n" +
                                   "/help - 获取帮助";

            await botClient.SendMessage(chatId, message);
            return;
        }

        // 其他消息逻辑
        await botClient.SendMessage(chatId, $"你发送了：{text}");
    }
}
