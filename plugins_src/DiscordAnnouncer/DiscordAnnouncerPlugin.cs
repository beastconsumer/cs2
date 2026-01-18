using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;

namespace DiscordAnnouncer;

[MinimumApiVersion(0)]
public sealed class DiscordAnnouncerPlugin : BasePlugin
{
	public override string ModuleName => "DiscordAnnouncer";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "Local";
	public override string ModuleDescription => "Advertises the server Discord periodically in chat.";

	// Ajuste aqui sem precisar mexer no resto do plugin
	private const string ServerBrand = "ASTRA SURF COMBAT";
	private const string DiscordUrl = "https://discord.gg/SEU_LINK";
	private const float IntervalSeconds = 300.0f; // 5 minutos

	public override void Load(bool hotReload)
	{
		AddTimer(IntervalSeconds, AnnounceDiscord, TimerFlags.REPEAT);
	}

	private static void AnnounceDiscord()
	{
		var players = Utilities.GetPlayers();
		if (players == null || players.Count == 0)
			return;

		// Evita spam com server vazio. Bots costumam reportar 127.0.0.1.
		var hasHuman = players.Any(p => p != null && p.IsValid && !IsProbablyBot(p));
		if (!hasHuman)
			return;

		var line1 = $"[{ServerBrand}] Discord: {DiscordUrl}";
		var line2 = $"[{ServerBrand}] Entre para novidades, eventos e suporte.";

		foreach (var player in players)
		{
			if (player == null || !player.IsValid)
				continue;

			player.PrintToChat(line1);
			player.PrintToChat(line2);
		}
	}

	private static bool IsProbablyBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}
}
