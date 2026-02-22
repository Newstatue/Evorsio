using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace Evorsio.BotService.Controllers;

[ApiController]
[Route("api/bot/telegram")]
public class TelegramBotController(Services.BotService botService) : ControllerBase
{
    private const string WebhookSecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    [HttpPost("update")]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromHeader(Name = WebhookSecretHeader)] string? secretToken)
    {
        // ✅ 验证 Webhook Secret Token
        var expectedSecret = Environment.GetEnvironmentVariable("TELEGRAM_WEBHOOK_SECRET");
        if (string.IsNullOrEmpty(expectedSecret))
        {
            // 如果没有配置 secret，记录警告但继续处理（开发环境）
            // 生产环境应该始终配置此环境变量
        }
        else if (secretToken != expectedSecret)
        {
            return Unauthorized("Invalid webhook secret token");
        }

        if (update.Message != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text ?? "";

            // 调用 BotService 处理消息
            await botService.HandleMessageAsync(chatId, text);
        }

        return Ok();
    }
}
