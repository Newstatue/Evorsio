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

namespace Evorsio.BotService.Controllers;

[ApiController]
[Route("bot/telegram")]
public class TelegramBotController : ControllerBase
{
	private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

	private readonly ITelegramBotClient _telegramBotClient;
	private readonly TelegramAuthSessionStore _authSessionStore;
	private readonly IConfiguration _configuration;
	private readonly ILogger<TelegramBotController> _logger;

	public TelegramBotController(
		ITelegramBotClient telegramBotClient,
		TelegramAuthSessionStore authSessionStore,
		IConfiguration configuration,
		ILogger<TelegramBotController> logger)
	{
		_telegramBotClient = telegramBotClient;
		_authSessionStore = authSessionStore;
		_configuration = configuration;
		_logger = logger;
	}

	[HttpPost("webhook")]
	public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken cancellationToken)
	{
		var expectedSecret = _configuration["TELEGRAM_WEBHOOK_SECRET"];
		if (!string.IsNullOrWhiteSpace(expectedSecret))
		{
			if (!Request.Headers.TryGetValue(SecretHeaderName, out var actualSecret) ||
				!string.Equals(actualSecret.ToString(), expectedSecret, StringComparison.Ordinal))
			{
				_logger.LogWarning("收到无效的 Telegram webhook secret。");
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
			await _telegramBotClient.SendMessage(
				chatId,
				"Evorsio Bot 已上线，Webhook 工作正常。发送 /login 开始 Keycloak 登录认证。",
				cancellationToken: cancellationToken);

			return Ok();
		}

		if (string.Equals(text, "/login", StringComparison.OrdinalIgnoreCase))
		{
			var token = _authSessionStore.Create(chatId, TimeSpan.FromMinutes(5));
			var loginUrl = BuildLoginUrl(token);

			await _telegramBotClient.SendMessage(
				chatId,
				$"点击登录 Keycloak 完成认证（5 分钟有效）：\n{loginUrl}",
				cancellationToken: cancellationToken);

			return Ok();
		}

		await _telegramBotClient.SendMessage(
			chatId,
			$"收到消息：{text}",
			cancellationToken: cancellationToken);

		return Ok();
	}

	[HttpGet("auth/start")]
	public IActionResult StartAuth([FromQuery] string token)
	{
		if (string.IsNullOrWhiteSpace(token) || !_authSessionStore.IsValid(token))
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
		if (string.IsNullOrWhiteSpace(token) || !_authSessionStore.TryConsume(token, out var chatId))
		{
			return BadRequest("登录会话已失效，请回到 Telegram 重新发送 /login。");
		}

		var userLabel =
			User.FindFirst("preferred_username")?.Value ??
			User.Identity?.Name ??
			User.FindFirst(ClaimTypes.Email)?.Value ??
			User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
			"unknown";

		await _telegramBotClient.SendMessage(
			chatId,
			$"✅ 认证成功，当前账号：{userLabel}",
			cancellationToken: cancellationToken);

		return Content("认证成功，结果已发送到 Telegram。你可以关闭此页面。", "text/plain; charset=utf-8");
	}

	private string BuildLoginUrl(string token)
	{
		var webhookUrl = _configuration["TELEGRAM_WEBHOOK_URL"];
		if (Uri.TryCreate(webhookUrl, UriKind.Absolute, out var webhookUri))
		{
			var authPath = webhookUri.AbsolutePath.EndsWith("/webhook", StringComparison.OrdinalIgnoreCase)
				? webhookUri.AbsolutePath[..^"/webhook".Length] + "/auth/start"
				: "/bot/telegram/auth/start";

			var builder = new UriBuilder(webhookUri)
			{
				Path = authPath,
				Query = $"token={Uri.EscapeDataString(token)}"
			};

			return builder.Uri.ToString();
		}

		return $"/bot/telegram/auth/start?token={Uri.EscapeDataString(token)}";
	}
}
