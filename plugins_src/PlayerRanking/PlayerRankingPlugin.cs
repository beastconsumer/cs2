using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace PlayerRanking;

[MinimumApiVersion(0)]
public sealed class PlayerRankingPlugin : BasePlugin
{
	public override string ModuleName => "PlayerRanking";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Sistema de pontos, XP e ranking para surf combat.";

	// Configuração de pontos
	private const int PointsKill = 10;
	private const int PointsAssist = 5;
	private const int PointsHeadshot = 3;
	private const int PointsRoundWin = 5;
	private const int PointsStreakBonus = 2; // Bônus por killstreak

	// Sistema de XP (XP necessário = Level * 100)
	private const int BaseXPPerLevel = 100;

	// Armazena dados de ranking
	private readonly Dictionary<ulong, PlayerRankData> _playerRanks = new();
	private readonly string _dataFilePath = "addons/counterstrikesharp/configs/plugins/PlayerRanking/ranks.json";

	// Killstreak tracking
	private readonly Dictionary<ulong, int> _killStreaks = new();

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
		RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

		AddCommand("css_points", "Mostra seus pontos e XP", OnPointsCommand);
		AddCommand("css_level", "Mostra seu nível e progresso", OnLevelCommand);
		AddCommand("css_leaderboard", "Mostra leaderboard de pontos", OnLeaderboardCommand);
		AddCommand("css_toprank", "Mostra top 10 por ranking", OnTopRankCommand);

		// Carrega dados salvos
		LoadRankData();

		// Salva dados periodicamente
		AddTimer(300.0f, SaveRankData, TimerFlags.REPEAT);
	}

	public override void Unload(bool hotReload)
	{
		SaveRankData();
		base.Unload(hotReload);
	}

	private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var attacker = @event.Attacker;
		var victim = @event.Userid;
		var assister = @event.Assister;

		// Processa pontos do atacante
		if (attacker != null && attacker.IsValid && !IsBot(attacker))
		{
			var rankData = GetOrCreateRankData(attacker);
			int pointsGained = PointsKill;

			// Killstreak
			if (!_killStreaks.ContainsKey(attacker.SteamID))
				_killStreaks[attacker.SteamID] = 0;

			_killStreaks[attacker.SteamID]++;
			var streak = _killStreaks[attacker.SteamID];

			// Bônus por killstreak (a cada 3 kills)
			if (streak % 3 == 0 && streak >= 3)
			{
				pointsGained += PointsStreakBonus;
				attacker.PrintToChat($" {ChatColors.Gold}[RANKING]{ChatColors.Default} Killstreak {streak}! +{PointsStreakBonus} pontos bônus!");
			}

			// Bônus headshot
			if (@event.Headshot)
			{
				pointsGained += PointsHeadshot;
				rankData.HeadshotKills++;
			}

			rankData.Points += pointsGained;
			rankData.Kills++;
			rankData.TotalXP += pointsGained;

			CheckLevelUp(attacker, rankData, pointsGained);
		}

		// Reset killstreak da vítima
		if (victim != null && victim.IsValid && !IsBot(victim))
		{
			if (_killStreaks.ContainsKey(victim.SteamID))
			{
				_killStreaks[victim.SteamID] = 0;
			}

			var rankData = GetOrCreateRankData(victim);
			rankData.Deaths++;
		}

		// Processa pontos do assistente
		if (assister != null && assister.IsValid && !IsBot(assister))
		{
			var rankData = GetOrCreateRankData(assister);
			rankData.Points += PointsAssist;
			rankData.TotalXP += PointsAssist;
			rankData.Assists++;

			CheckLevelUp(assister, rankData, PointsAssist);
		}

		return HookResult.Continue;
	}

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		// Distribui pontos para time vencedor
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		
		foreach (var player in players)
		{
			var pawn = player.PlayerPawn.Value;
			if (pawn != null && pawn.LifeState == 0 && player.TeamNum == @event.Winner)
			{
				var rankData = GetOrCreateRankData(player);
				rankData.Points += PointsRoundWin;
				rankData.TotalXP += PointsRoundWin;
				rankData.RoundsWon++;

				CheckLevelUp(player, rankData, PointsRoundWin);
			}
		}

		// Reseta killstreaks ao final do round
		_killStreaks.Clear();

		return HookResult.Continue;
	}

	private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		// Carrega dados do jogador quando conecta
		var rankData = GetOrCreateRankData(player);
		
		AddTimer(3.0f, () =>
		{
			if (player.IsValid)
			{
				player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
				player.PrintToChat($" {ChatColors.Yellow}[RANKING] Bem-vindo, {NormalizeName(player.PlayerName)}!");
				player.PrintToChat($" {ChatColors.Default}Nível: {ChatColors.Gold}{rankData.Level}{ChatColors.Default} | Pontos: {ChatColors.Green}{rankData.Points}{ChatColors.Default} | XP: {ChatColors.LightBlue}{rankData.TotalXP}");
				player.PrintToChat($" {ChatColors.Default}Digite {ChatColors.Yellow}!level{ChatColors.Default} ou {ChatColors.Yellow}!points{ChatColors.Default} para ver seus dados");
				player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
			}
		}, TimerFlags.STOP_ON_MAPCHANGE);

		return HookResult.Continue;
	}

	private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid)
			return HookResult.Continue;

		// Remove killstreak ao desconectar
		if (_killStreaks.ContainsKey(player.SteamID))
		{
			_killStreaks.Remove(player.SteamID);
		}

		return HookResult.Continue;
	}

	private void CheckLevelUp(CCSPlayerController player, PlayerRankData rankData, int xpGained)
	{
		var xpNeededForCurrentLevel = rankData.Level * BaseXPPerLevel;
		var xpNeededForNextLevel = (rankData.Level + 1) * BaseXPPerLevel;

		// Verifica se subiu de nível
		if (rankData.TotalXP >= xpNeededForNextLevel)
		{
			rankData.Level++;
			
			// Bônus de pontos por subir de nível
			var levelBonus = rankData.Level * 10;
			rankData.Points += levelBonus;

			player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
			player.PrintToChat($" {ChatColors.Gold}🎉 LEVEL UP! 🎉");
			player.PrintToChat($" {ChatColors.Default}Você subiu para o nível {ChatColors.Gold}{rankData.Level}{ChatColors.Default}!");
			player.PrintToChat($" {ChatColors.Default}Bônus: +{ChatColors.Green}{levelBonus}{ChatColors.Default} pontos");
			player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");

			player.PrintToCenter($" {ChatColors.Gold}LEVEL UP!\nNível {rankData.Level}");

			// Notifica outros jogadores (opcional)
			Server.PrintToChatAll($" {ChatColors.Yellow}[RANKING]{ChatColors.Default} {ChatColors.Gold}{NormalizeName(player.PlayerName)}{ChatColors.Default} subiu para o nível {ChatColors.Gold}{rankData.Level}{ChatColors.Default}!");
		}
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnPointsCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var rankData = GetOrCreateRankData(player);
		var xpNeededForNextLevel = (rankData.Level + 1) * BaseXPPerLevel;
		var xpProgress = rankData.TotalXP - (rankData.Level * BaseXPPerLevel);
		var xpNeeded = xpNeededForNextLevel - rankData.TotalXP;
		var progressPercent = xpNeededForNextLevel > rankData.TotalXP 
			? (float)xpProgress / (xpNeededForNextLevel - (rankData.Level * BaseXPPerLevel)) * 100f 
			: 100f;

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[PONTOS] {NormalizeName(player.PlayerName)}");
		player.PrintToChat($" {ChatColors.Default}Pontos Totais: {ChatColors.Gold}{rankData.Points}");
		player.PrintToChat($" {ChatColors.Default}XP Total: {ChatColors.LightBlue}{rankData.TotalXP}");
		player.PrintToChat($" {ChatColors.Default}Kills: {ChatColors.Green}{rankData.Kills}{ChatColors.Default} | Deaths: {ChatColors.Red}{rankData.Deaths}");
		player.PrintToChat($" {ChatColors.Default}Assists: {ChatColors.LightBlue}{rankData.Assists}{ChatColors.Default} | HS Kills: {ChatColors.Orange}{rankData.HeadshotKills}");
		player.PrintToChat($" {ChatColors.Default}Rounds Ganhos: {ChatColors.Green}{rankData.RoundsWon}");
		
		if (_killStreaks.TryGetValue(player.SteamID, out var streak) && streak > 0)
		{
			player.PrintToChat($" {ChatColors.Gold}Killstreak Atual: {streak}");
		}

		player.PrintToChat($" {ChatColors.Green}═══════════════════════════════════");
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnLevelCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var rankData = GetOrCreateRankData(player);
		var xpNeededForNextLevel = (rankData.Level + 1) * BaseXPPerLevel;
		var xpProgress = rankData.TotalXP - (rankData.Level * BaseXPPerLevel);
		var xpNeeded = Math.Max(0, xpNeededForNextLevel - rankData.TotalXP);
		var xpForLevel = xpNeededForNextLevel - (rankData.Level * BaseXPPerLevel);
		var progressPercent = xpForLevel > 0 ? (float)xpProgress / xpForLevel * 100f : 100f;

		var rankTitle = GetRankTitle(rankData.Level);

		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[NÍVEL] {NormalizeName(player.PlayerName)}");
		player.PrintToChat($" {ChatColors.Default}Nível: {ChatColors.Gold}{rankData.Level}{ChatColors.Default} ({ChatColors.LightBlue}{rankTitle}{ChatColors.Default})");
		player.PrintToChat($" {ChatColors.Default}XP: {ChatColors.LightBlue}{rankData.TotalXP}{ChatColors.Default} / {ChatColors.Yellow}{xpNeededForNextLevel}");
		player.PrintToChat($" {ChatColors.Default}Progresso: {ChatColors.Green}{progressPercent:F1}%{ChatColors.Default} ({xpProgress} / {xpForLevel} XP)");
		player.PrintToChat($" {ChatColors.Default}Faltam: {ChatColors.Yellow}{xpNeeded}{ChatColors.Default} XP para o próximo nível");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnLeaderboardCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var topPlayers = _playerRanks
			.Where(kvp => kvp.Value.Points > 0)
			.OrderByDescending(kvp => kvp.Value.Points)
			.ThenByDescending(kvp => kvp.Value.TotalXP)
			.Take(10)
			.ToList();

		if (topPlayers.Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[LEADERBOARD]{ChatColors.Default} Nenhum ranking registrado ainda.");
			return;
		}

		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[LEADERBOARD - TOP 10 PONTOS]{ChatColors.Default}");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");

		for (int i = 0; i < topPlayers.Count; i++)
		{
			var (steamId, rankData) = topPlayers[i];
			var playerName = GetPlayerNameBySteamId(steamId) ?? rankData.PlayerName ?? "Desconhecido";

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

			var title = GetRankTitle(rankData.Level);
			player.PrintToChat($" {rankColor}{medal}{ChatColors.Default} {ChatColors.Yellow}{NormalizeName(playerName, 18)}{ChatColors.Default} | Lv.{ChatColors.LightBlue}{rankData.Level}{ChatColors.Default} ({title}) | {ChatColors.Green}{rankData.Points}pts");
		}

		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnTopRankCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var topPlayers = _playerRanks
			.Where(kvp => kvp.Value.Level > 0)
			.OrderByDescending(kvp => kvp.Value.Level)
			.ThenByDescending(kvp => kvp.Value.TotalXP)
			.Take(10)
			.ToList();

		if (topPlayers.Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[TOP RANK]{ChatColors.Default} Nenhum ranking registrado ainda.");
			return;
		}

		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}[TOP 10 POR NÍVEL]{ChatColors.Default}");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");

		for (int i = 0; i < topPlayers.Count; i++)
		{
			var (steamId, rankData) = topPlayers[i];
			var playerName = GetPlayerNameBySteamId(steamId) ?? rankData.PlayerName ?? "Desconhecido";

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

			var title = GetRankTitle(rankData.Level);
			var kd = rankData.Deaths > 0 ? (float)rankData.Kills / rankData.Deaths : rankData.Kills;

			player.PrintToChat($" {rankColor}{medal}{ChatColors.Default} {ChatColors.Yellow}{NormalizeName(playerName, 18)}{ChatColors.Default} | Nível {ChatColors.Gold}{rankData.Level}{ChatColors.Default} ({ChatColors.LightBlue}{title}{ChatColors.Default}) | K/D: {ChatColors.Green}{kd:F2}");
		}

		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════");
	}

	private PlayerRankData GetOrCreateRankData(CCSPlayerController player)
	{
		var steamId = player.SteamID;
		if (!_playerRanks.ContainsKey(steamId))
		{
			_playerRanks[steamId] = new PlayerRankData
			{
				PlayerName = player.PlayerName,
				SteamId = steamId
			};
		}
		else
		{
			_playerRanks[steamId].PlayerName = player.PlayerName;
		}

		return _playerRanks[steamId];
	}

	private string? GetPlayerNameBySteamId(ulong steamId)
	{
		if (_playerRanks.TryGetValue(steamId, out var rankData))
			return rankData.PlayerName;

		var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId);
		return player?.PlayerName;
	}

	private static string GetRankTitle(int level)
	{
		return level switch
		{
			< 5 => "Recruta",
			< 10 => "Soldado",
			< 15 => "Veterano",
			< 20 => "Especialista",
			< 25 => "Elite",
			< 30 => "Mestre",
			< 35 => "Lenda",
			< 40 => "Ídolo",
			< 50 => "Mito",
			_ => "LENDÁRIO"
		};
	}

	private void LoadRankData()
	{
		try
		{
			var fullPath = Path.Combine(Server.GameDirectory, _dataFilePath);
			var directory = Path.GetDirectoryName(fullPath);
			
			if (directory != null && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			if (File.Exists(fullPath))
			{
				var json = File.ReadAllText(fullPath);
				var data = JsonSerializer.Deserialize<Dictionary<ulong, PlayerRankData>>(json);
				
				if (data != null)
				{
					_playerRanks.Clear();
					foreach (var kvp in data)
					{
						_playerRanks[kvp.Key] = kvp.Value;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerRanking] Erro ao carregar dados: {ex.Message}");
		}
	}

	private void SaveRankData()
	{
		try
		{
			var fullPath = Path.Combine(Server.GameDirectory, _dataFilePath);
			var directory = Path.GetDirectoryName(fullPath);
			
			if (directory != null && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var options = new JsonSerializerOptions { WriteIndented = true };
			var json = JsonSerializer.Serialize(_playerRanks, options);
			File.WriteAllText(fullPath, json);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerRanking] Erro ao salvar dados: {ex.Message}");
		}
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

	private class PlayerRankData
	{
		public string PlayerName { get; set; } = string.Empty;
		public ulong SteamId { get; set; }
		public int Level { get; set; } = 1;
		public int Points { get; set; }
		public int TotalXP { get; set; }
		public int Kills { get; set; }
		public int Deaths { get; set; }
		public int Assists { get; set; }
		public int HeadshotKills { get; set; }
		public int RoundsWon { get; set; }
	}
}

