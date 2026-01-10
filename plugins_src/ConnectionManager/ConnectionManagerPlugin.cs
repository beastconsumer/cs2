using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace ConnectionManager;

[MinimumApiVersion(0)]
public sealed class ConnectionManagerPlugin : BasePlugin
{
	public override string ModuleName => "ConnectionManager";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Mensagens personalizadas de conexão e desconexão de jogadores.";

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
		RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
	}

	private HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		// Mensagem quando está conectando (ainda não está no jogo)
		AddTimer(1.0f, () =>
		{
			if (player.IsValid && !IsBot(player))
			{
				var playerName = NormalizeName(player.PlayerName);
				Server.PrintToChatAll($" {ChatColors.Green}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
				Server.PrintToChatAll($" {ChatColors.Yellow}🔌 {ChatColors.Default}Jogador {ChatColors.Green}{playerName}{ChatColors.Default} está conectando...");
				Server.PrintToChatAll($" {ChatColors.Green}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			}
		});

		return HookResult.Continue;
	}

	private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		// Mensagem de boas-vindas personalizada após conexão completa
		AddTimer(2.0f, () =>
		{
			if (player.IsValid && !IsBot(player))
			{
				var playerName = NormalizeName(player.PlayerName);
				var playerCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !IsBot(p));

				// Obtém tag do jogador se disponível
				var playerTag = GetPlayerTag(player);
				var formattedName = !string.IsNullOrEmpty(playerTag) ? $"{playerTag} {playerName}" : playerName;

				// Mensagem para todos
				Server.PrintToChatAll($" {ChatColors.Green}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
				Server.PrintToChatAll($" {ChatColors.Gold}🎉 {ChatColors.Green}{formattedName}{ChatColors.Default} entrou no servidor!");
				Server.PrintToChatAll($" {ChatColors.Default}Jogadores online: {ChatColors.Green}{playerCount}{ChatColors.Default}/24");
				Server.PrintToChatAll($" {ChatColors.Green}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

				// Mensagem personalizada para o jogador
				AddTimer(1.0f, () =>
				{
					if (player.IsValid)
					{
						player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
						player.PrintToChat($" {ChatColors.Yellow}👋 Bem-vindo ao {ChatColors.Gold}ASTRA SURF COMBAT{ChatColors.Yellow}!");
						player.PrintToChat($" ");
						player.PrintToChat($" {ChatColors.Default}📊 Comandos úteis:");
						player.PrintToChat($" {ChatColors.Green}!stats{ChatColors.Default} - Suas estatísticas");
						player.PrintToChat($" {ChatColors.Green}!top{ChatColors.Default} - Top 10 jogadores");
						player.PrintToChat($" {ChatColors.Green}!points{ChatColors.Default} - Seus pontos e XP");
						player.PrintToChat($" {ChatColors.Green}!level{ChatColors.Default} - Seu nível e progresso");
						player.PrintToChat($" {ChatColors.Green}!rtv{ChatColors.Default} - Votar para trocar mapa");
						player.PrintToChat($" ");
						player.PrintToChat($" {ChatColors.Default}🎮 Digite {ChatColors.Yellow}!menu{ChatColors.Default} para ver todos os comandos");
						player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");

						player.PrintToCenter($" {ChatColors.Gold}Bem-vindo ao ASTRA SURF COMBAT!\n{ChatColors.Default}Digite !menu para ver os comandos");
					}
				});
			}
		});

		return HookResult.Continue;
	}

	private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		var playerName = NormalizeName(player.PlayerName);

		// Mensagem de desconexão
		Server.PrintToChatAll($" {ChatColors.Red}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		Server.PrintToChatAll($" {ChatColors.Red}🔌 {ChatColors.Default}Jogador {ChatColors.Red}{playerName}{ChatColors.Default} desconectou do servidor");

		var playerCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !IsBot(p));
		Server.PrintToChatAll($" {ChatColors.Default}Jogadores online: {ChatColors.Green}{playerCount}{ChatColors.Default}/24");
		Server.PrintToChatAll($" {ChatColors.Red}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

		return HookResult.Continue;
	}


	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		try
		{
			var ip = player.IpAddress;
			return !string.IsNullOrWhiteSpace(ip) && ip.StartsWith("127.");
		}
		catch
		{
			return true;
		}
	}

	private string GetPlayerTag(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return string.Empty;

		try
		{
			// Tenta obter nível do PlayerRanking
			var rankingPath = "addons/counterstrikesharp/configs/plugins/PlayerRanking/ranks.json";
			var fullPath = Path.Combine(Server.GameDirectory, rankingPath);
			
			if (File.Exists(fullPath))
			{
				var json = File.ReadAllText(fullPath);
				var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<ulong, RankingData>>(json);
				
				if (data != null && data.TryGetValue(player.SteamID, out var rankData))
				{
					var level = rankData.Level > 0 ? rankData.Level : 1;
					var color = GetLevelColor(level);
					return $"[{color}Lv.{level}{ChatColors.Default}]";
				}
			}
		}
		catch
		{
			// Ignora erros
		}

		return string.Empty;
	}

	private string GetLevelColor(int level)
	{
		return level switch
		{
			< 5 => $"{ChatColors.Grey}",
			< 10 => $"{ChatColors.Default}",
			< 15 => $"{ChatColors.Green}",
			< 20 => $"{ChatColors.LightBlue}",
			< 25 => $"{ChatColors.Blue}",
			< 30 => $"{ChatColors.Purple}",
			< 35 => $"{ChatColors.Orange}",
			< 40 => $"{ChatColors.Gold}",
			< 50 => $"{ChatColors.Red}",
			_ => $"{ChatColors.Yellow}"
		};
	}

	private static string NormalizeName(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (trimmed.Length <= 32)
			return trimmed;

		return trimmed[..31] + "…";
	}

	private class RankingData
	{
		public int Level { get; set; } = 1;
	}
}

