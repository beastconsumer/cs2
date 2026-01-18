using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using System.Text;

namespace Statistics;

[MinimumApiVersion(0)]
public sealed class StatisticsPlugin : BasePlugin
{
	public override string ModuleName => "Statistics";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Sistema de estatísticas com top frags e K/D para surf combat.";

	// Armazena estatísticas por SteamID64
	private readonly Dictionary<ulong, PlayerStats> _playerStats = new();
	private readonly string _dataFilePath = "addons/counterstrikesharp/configs/plugins/Statistics/stats.json";

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);

		AddCommand("css_stats", "Mostra suas estatísticas", OnStatsCommand);
		AddCommand("css_top", "Mostra top 10 jogadores", OnTopCommand);
		AddCommand("css_rank", "Mostra seu ranking", OnRankCommand);

		LoadStatsData();
		AddTimer(300.0f, SaveStatsData, TimerFlags.REPEAT);
	}

	public override void Unload(bool hotReload)
	{
		SaveStatsData();
		base.Unload(hotReload);
	}

	private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var attacker = @event.Attacker;
		var victim = @event.Userid;
		var assister = @event.Assister;

		// Registra kill do atacante
		if (attacker != null && attacker.IsValid && !IsBot(attacker))
		{
			var stats = GetOrCreateStats(attacker);
			stats.Kills++;
			
			if (@event.Headshot)
				stats.Headshots++;

			// Se matou alguém do mesmo time (friendly fire pode estar ligado)
			if (victim != null && victim.IsValid && attacker.TeamNum == victim.TeamNum && attacker != victim)
			{
				stats.TeamKills++;
			}
		}

		// Registra assistência
		if (assister != null && assister.IsValid && !IsBot(assister))
		{
			var stats = GetOrCreateStats(assister);
			stats.Assists++;
		}

		// Registra death da vítima
		if (victim != null && victim.IsValid && !IsBot(victim))
		{
			var stats = GetOrCreateStats(victim);
			stats.Deaths++;
		}

		return HookResult.Continue;
	}

	private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
	{
		var attacker = @event.Attacker;
		if (attacker == null || !attacker.IsValid || IsBot(attacker))
			return HookResult.Continue;

		var stats = GetOrCreateStats(attacker);
		stats.DamageDealt += @event.DmgHealth;
		stats.Hits++;

		return HookResult.Continue;
	}

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		// Incrementa rounds jogados para jogadores ativos
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		foreach (var player in players)
		{
			var stats = GetOrCreateStats(player);
			stats.RoundsPlayed++;

			// Se estava vivo no final, incrementa rounds ganhos
			var pawn = player.PlayerPawn.Value;
			if (pawn != null && pawn.LifeState == 0 && player.TeamNum == @event.Winner)
			{
				stats.RoundsWon++;
			}
		}

		return HookResult.Continue;
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		return HookResult.Continue;
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnStatsCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var stats = GetOrCreateStats(player);
		var kd = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;
		var hsPercent = stats.Kills > 0 ? (float)stats.Headshots / stats.Kills * 100f : 0f;

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Default}[{ChatColors.Green}ESTATÍSTICAS{ChatColors.Default}] {ChatColors.Yellow}{NormalizeName(player.PlayerName)}");
		player.PrintToChat($" {ChatColors.Default}Kills: {ChatColors.Green}{stats.Kills}{ChatColors.Default} | Deaths: {ChatColors.Red}{stats.Deaths}{ChatColors.Default} | K/D: {ChatColors.Yellow}{kd:F2}");
		player.PrintToChat($" {ChatColors.Default}Assists: {ChatColors.LightBlue}{stats.Assists}{ChatColors.Default} | HS: {ChatColors.Orange}{stats.Headshots} ({hsPercent:F1}%)");
		player.PrintToChat($" {ChatColors.Default}Dano Total: {ChatColors.LightBlue}{stats.DamageDealt}{ChatColors.Default} | Acertos: {ChatColors.White}{stats.Hits}");
		player.PrintToChat($" {ChatColors.Default}Rounds: {ChatColors.Green}{stats.RoundsWon}{ChatColors.Default}W/{ChatColors.Red}{stats.RoundsPlayed - stats.RoundsWon}{ChatColors.Default}L ({stats.RoundsPlayed} total)");
		if (stats.TeamKills > 0)
			player.PrintToChat($" {ChatColors.Red}Team Kills: {stats.TeamKills}");
		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnTopCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		// Ordena por kills (critério principal), depois por K/D
		var topPlayers = _playerStats
			.Where(kvp => kvp.Value.Kills > 0)
			.OrderByDescending(kvp => kvp.Value.Kills)
			.ThenByDescending(kvp => kvp.Value.Deaths > 0 ? (float)kvp.Value.Kills / kvp.Value.Deaths : kvp.Value.Kills)
			.Take(10)
			.ToList();

		if (topPlayers.Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[TOP]{ChatColors.Default} Nenhuma estatística registrada ainda.");
			return;
		}

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[TOP 10 JOGADORES - KILLS]{ChatColors.Default}");
		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");

		for (int i = 0; i < topPlayers.Count; i++)
		{
			var (steamId, stats) = topPlayers[i];
			var kd = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;
			var playerName = GetPlayerNameBySteamId(steamId) ?? "Desconhecido";

			var rankColor = i switch
			{
				0 => ChatColors.Gold,
				1 => ChatColors.Silver,
				2 => ChatColors.Orange,
				_ => ChatColors.Default
			};

			var medal = i switch
			{
				0 => "🥇",
				1 => "🥈",
				2 => "🥉",
				_ => $"{i + 1}."
			};

			player.PrintToChat($" {rankColor}{medal}{ChatColors.Default} {ChatColors.Yellow}{NormalizeName(playerName, 20)}{ChatColors.Default} | K: {ChatColors.Green}{stats.Kills}{ChatColors.Default} | D: {ChatColors.Red}{stats.Deaths}{ChatColors.Default} | K/D: {ChatColors.LightBlue}{kd:F2}");
		}

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnRankCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var stats = GetOrCreateStats(player);
		var kd = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;

		// Calcula ranking
		var betterPlayers = _playerStats
			.Where(kvp => kvp.Value.Kills > stats.Kills || 
			              (kvp.Value.Kills == stats.Kills && kvp.Value.Deaths > 0 && kvp.Key != player.SteamID))
			.Count();

		var rank = betterPlayers + 1;
		var totalPlayers = _playerStats.Count(kvp => kvp.Value.Kills > 0);

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[SEU RANKING]{ChatColors.Default}");
		player.PrintToChat($" {ChatColors.Default}Posição: {ChatColors.Gold}#{rank}{ChatColors.Default} de {ChatColors.LightBlue}{totalPlayers}{ChatColors.Default} jogadores");
		player.PrintToChat($" {ChatColors.Default}Kills: {ChatColors.Green}{stats.Kills}{ChatColors.Default} | K/D: {ChatColors.Yellow}{kd:F2}");
		
		if (rank <= 3)
		{
			var medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => "" };
			player.PrintToChat($" {ChatColors.Gold}{medal} VOCÊ ESTÁ NO PODIUM! {medal}");
		}

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
	}

	private PlayerStats GetOrCreateStats(CCSPlayerController player)
	{
		var steamId = player.SteamID;
		if (!_playerStats.ContainsKey(steamId))
		{
			_playerStats[steamId] = new PlayerStats
			{
				PlayerName = player.PlayerName
			};
		}
		else
		{
			// Atualiza nome caso tenha mudado
			_playerStats[steamId].PlayerName = player.PlayerName;
		}

		return _playerStats[steamId];
	}

	private string? GetPlayerNameBySteamId(ulong steamId)
	{
		if (_playerStats.TryGetValue(steamId, out var stats))
			return stats.PlayerName;

		// Tenta encontrar jogador online
		var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId);
		return player?.PlayerName;
	}

	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}

	private static string NormalizeName(string name, int maxLength = 32)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (trimmed.Length <= maxLength)
			return trimmed;

		return trimmed[..(maxLength - 1)] + "…";
	}

	private class PlayerStats
	{
		public string PlayerName { get; set; } = string.Empty;
		public int Kills { get; set; }
		public int Deaths { get; set; }
		public int Assists { get; set; }
		public int Headshots { get; set; }
		public int DamageDealt { get; set; }
		public int Hits { get; set; }
		public int RoundsPlayed { get; set; }
		public int RoundsWon { get; set; }
		public int TeamKills { get; set; }
	}
}

