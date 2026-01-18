using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace PlayerTags;

[MinimumApiVersion(0)]
public sealed class PlayerTagsPlugin : BasePlugin
{
	public override string ModuleName => "PlayerTags";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Tags visuais no nome baseadas no nível e ranking do jogador.";

	private readonly Dictionary<ulong, PlayerTagData> _playerTags = new();
	private readonly string _dataFilePath = "addons/counterstrikesharp/configs/plugins/PlayerTags/tags.json";
	private readonly string _rankingDataPath = "addons/counterstrikesharp/configs/plugins/PlayerRanking/ranks.json";

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);

		AddTimer(1.0f, UpdateAllPlayerTags, TimerFlags.REPEAT);
		AddTimer(300.0f, SaveTagData, TimerFlags.REPEAT);

		LoadTagData();

		if (hotReload)
		{
			// Aplica tags em jogadores já conectados
			AddTimer(2.0f, () =>
			{
				var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
				foreach (var player in players)
				{
					ApplyTag(player);
				}
			});
		}
	}

	private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		AddTimer(3.0f, () =>
		{
			if (player.IsValid)
			{
				ApplyTag(player);
			}
		});

		return HookResult.Continue;
	}

	private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		AddTimer(0.5f, () =>
		{
			if (player.IsValid)
			{
				ApplyTag(player);
			}
		});

		return HookResult.Continue;
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		// Atualiza tags no início de cada round
		AddTimer(1.0f, () =>
		{
			var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
			foreach (var player in players)
			{
				ApplyTag(player);
			}
		});

		return HookResult.Continue;
	}

	private void UpdateAllPlayerTags()
	{
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		foreach (var player in players)
		{
			// Aplica tag periodicamente (apenas se mudou)
			ApplyTag(player);
		}
	}

	private void ApplyTag(CCSPlayerController player)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		try
		{
			var steamId = player.SteamID;
			
			// Salva dados do jogador
			if (!_playerTags.ContainsKey(steamId))
			{
				_playerTags[steamId] = new PlayerTagData
				{
					SteamId = steamId,
					OriginalName = player.PlayerName
				};
			}
			else
			{
				// Atualiza nome se mudou
				_playerTags[steamId].OriginalName = player.PlayerName;
			}
			
			// Removido SaveTagData() daqui para evitar I/O excessivo
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerTags] Erro ao aplicar tag: {ex.Message}");
		}
	}

	// Método público para obter tag formatada (usado por outros plugins)
	public string GetFormattedPlayerName(CCSPlayerController player)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return player?.PlayerName ?? "Player";

		var steamId = player.SteamID;
		var level = GetPlayerLevel(steamId);
		var color = GetLevelColor(level);
		var originalName = GetOriginalName(steamId);

		// Formato: [Lv.15] Nome
		return $"[{color}Lv.{level}{ChatColors.Default}] {originalName}";
	}

	// Método público para obter apenas a tag
	public string GetPlayerTag(ulong steamId)
	{
		var level = GetPlayerLevel(steamId);
		var color = GetLevelColor(level);
		return $"[{color}Lv.{level}{ChatColors.Default}]";
	}

	private int GetPlayerLevel(ulong steamId)
	{
		try
		{
			var fullPath = Path.Combine(Server.GameDirectory, _rankingDataPath);
			if (File.Exists(fullPath))
			{
				var json = File.ReadAllText(fullPath);
				var data = JsonSerializer.Deserialize<Dictionary<ulong, RankingData>>(json);
				
				if (data != null && data.TryGetValue(steamId, out var rankData))
				{
					return rankData.Level > 0 ? rankData.Level : 1;
				}
			}
		}
		catch
		{
			// Ignora erros
		}

		return 1; // Nível padrão
	}

	private string GetRankTitle(int level)
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

	private string GetOriginalName(ulong steamId)
	{
		if (_playerTags.TryGetValue(steamId, out var tagData))
			return tagData.OriginalName;

		var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId);
		return player?.PlayerName ?? "Player";
	}

	private void LoadTagData()
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
				var data = JsonSerializer.Deserialize<Dictionary<ulong, PlayerTagData>>(json);
				
				if (data != null)
				{
					_playerTags.Clear();
					foreach (var kvp in data)
					{
						_playerTags[kvp.Key] = kvp.Value;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerTags] Erro ao carregar dados: {ex.Message}");
		}
	}

	private void SaveTagData()
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
			var json = JsonSerializer.Serialize(_playerTags, options);
			File.WriteAllText(fullPath, json);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerTags] Erro ao salvar dados: {ex.Message}");
		}
	}

	public override void Unload(bool hotReload)
	{
		SaveTagData();
		base.Unload(hotReload);
	}

	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}

	private class PlayerTagData
	{
		public ulong SteamId { get; set; }
		public string OriginalName { get; set; } = string.Empty;
	}

	private class RankingData
	{
		public int Level { get; set; } = 1;
	}
}

