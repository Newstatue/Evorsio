using System.Security.Claims;
using Evorsio.BotService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Evorsio.BotService.Controllers;

[ApiController]
[Route("bot/telegram")]
public class TelegramBotController(
	ITelegramBotClient telegramBotClient,
	TelegramAuthSessionStore authSessionStore,
	IConfiguration configuration,
	ILogger<TelegramBotController> logger)
	: ControllerBase
{
	private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";
	private static readonly ReplyKeyboardMarkup HelpKeyboard = new(
    [
        ["/start", "/login"],
		["/profile", "/help"]
	])
	{
		ResizeKeyboard = true,
		IsPersistent = true
	};

	[HttpPost("webhook")]
	public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken cancellationToken)
	{
		var expectedSecret = configuration["TELEGRAM_WEBHOOK_SECRET"];
		if (!string.IsNullOrWhiteSpace(expectedSecret))
		{
			if (!Request.Headers.TryGetValue(SecretHeaderName, out var actualSecret) ||
				!string.Equals(actualSecret.ToString(), expectedSecret, StringComparison.Ordinal))
			{
				logger.LogWarning("收到无效的 Telegram webhook secret。");
				return Unauthorized();
			}
		}

		if (update.Type != UpdateType.Message || update.Message?.Text is null)
		{
			return Ok();
		}

		var chatId = update.Message.Chat.Id;
		var text = update.Message.Text.Trim();

		if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
		{
			await telegramBotClient.SendMessage(
				chatId,
				"Evorsio Bot 已上线，Webhook 工作正常。发送 /login 开始 Keycloak 登录认证。",
				cancellationToken: cancellationToken);

			return Ok();
		}

		if (string.Equals(text, "/login", StringComparison.OrdinalIgnoreCase))
		{
			var token = await authSessionStore.CreateAsync(chatId, TimeSpan.FromMinutes(5));
			var loginUrl = BuildLoginUrl(token);

			await telegramBotClient.SendMessage(
				chatId,
				$"点击登录 Keycloak 完成认证（5 分钟有效）：\n{loginUrl}",
				cancellationToken: cancellationToken);

			return Ok();
		}

		if (string.Equals(text, "/profile", StringComparison.OrdinalIgnoreCase))
		{
			var accountUrl = BuildKeycloakAccountUrl();

			await telegramBotClient.SendMessage(
				chatId,
				$"打开 Keycloak 个人信息页面：\n{accountUrl}",
				cancellationToken: cancellationToken);

			return Ok();
		}

		if (string.Equals(text, "/help", StringComparison.OrdinalIgnoreCase))
		{
			await telegramBotClient.SendMessage(
				chatId,
				"请选择命令：",
				replyMarkup: HelpKeyboard,
				cancellationToken: cancellationToken);

			return Ok();
		}

		await telegramBotClient.SendMessage(
			chatId,
			$"收到消息：{text}",
			cancellationToken: cancellationToken);

		return Ok();
	}

	[HttpGet("auth/start")]
	public async Task<IActionResult> StartAuth([FromQuery] string token)
	{
		if (string.IsNullOrWhiteSpace(token) || !await authSessionStore.IsValidAsync(token))
		{
			return BadRequest("登录链接已失效，请回到 Telegram 重新发送 /login。");
		}

		var properties = new AuthenticationProperties
		{
			RedirectUri = $"/bot/telegram/auth/callback?token={Uri.EscapeDataString(token)}"
		};

		return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
	}

	[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
	[HttpGet("auth/callback")]
	public async Task<IActionResult> AuthCallback([FromQuery] string token, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return BadRequest("登录会话已失效，请回到 Telegram 重新发送 /login。");
		}

		var consumeResult = await authSessionStore.TryConsumeAsync(token);
		if (!consumeResult.Success)
		{
			return BadRequest("登录会话已失效，请回到 Telegram 重新发送 /login。");
		}

		var chatId = consumeResult.ChatId;

		var userLabel =
			User.FindFirst("preferred_username")?.Value ??
			User.Identity?.Name ??
			User.FindFirst(ClaimTypes.Email)?.Value ??
			User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
			"unknown";

		await telegramBotClient.SendMessage(
			chatId,
			$"✅ 认证成功，当前账号：{userLabel}",
			cancellationToken: cancellationToken);

		return Content("认证成功，结果已发送到 Telegram。你可以关闭此页面。", "text/plain; charset=utf-8");
	}

	private string BuildLoginUrl(string token)
	{
		var publicBaseUrl = configuration["PUBLIC_BASE_URL"];
		if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
		{
			var builder = new UriBuilder(publicBaseUri)
			{
				Path = "/bot/telegram/auth/start",
				Query = $"token={Uri.EscapeDataString(token)}"
			};

			return builder.Uri.ToString();
		}

		return $"/bot/telegram/auth/start?token={Uri.EscapeDataString(token)}";
	}

	private string BuildKeycloakAccountUrl()
	{
		var keycloakRealm = configuration["KEYCLOAK_REALM"];
		if (string.IsNullOrWhiteSpace(keycloakRealm))
		{
			throw new InvalidOperationException("缺少 KEYCLOAK_REALM 配置。");
		}

		var publicBaseUrl = configuration["PUBLIC_BASE_URL"];
		if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
		{
			var builder = new UriBuilder(publicBaseUri)
			{
				Path = $"/auth/realms/{keycloakRealm}/account",
				Query = string.Empty
			};

			return builder.Uri.ToString();
		}

		throw new InvalidOperationException("缺少有效的 PUBLIC_BASE_URL 配置。");
	}
}
