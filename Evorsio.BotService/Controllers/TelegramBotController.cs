using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace Evorsio.BotService.Controllers;

[ApiController]
[Route("api/bot/telegram")]
public class TelegramBotController(Services.BotService botService) : ControllerBase
{
    [HttpPost("update")]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
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