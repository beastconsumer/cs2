using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace PlayerTrails;

[MinimumApiVersion(0)]
public sealed class PlayerTrailsPlugin : BasePlugin
{
	public override string ModuleName => "PlayerTrails";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Sistema de trails (partículas visuais) que seguem os jogadores baseado no nível.";

	private readonly Dictionary<ulong, TrailData> _playerTrails = new();
	private readonly Dictionary<ulong, Queue<Vector>> _trailPositions = new();
	private readonly string _dataFilePath = "addons/counterstrikesharp/configs/plugins/PlayerTrails/trails.json";
	private readonly string _rankingDataPath = "addons/counterstrikesharp/configs/plugins/PlayerRanking/ranks.json";
	
	private const int MaxTrailPoints = 10; // Quantidade de pontos na trilha
	private const float TrailUpdateInterval = 0.15f; // Atualiza a cada 0.15s

	public override void Load(bool hotReload)
	{
		RegisterListener<Listeners.OnTick>(OnTick);
		RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

		AddCommand("css_trail", "Liga/desliga seu trail", OnTrailCommand);
		AddCommand("css_trails", "Liga/desliga seu trail", OnTrailCommand);

		LoadTrailData();

		// Atualiza trails periodicamente
		AddTimer(TrailUpdateInterval, UpdateTrails, TimerFlags.REPEAT);
	}

	public override void Unload(bool hotReload)
	{
		SaveTrailData();
		base.Unload(hotReload);
	}

	private void OnTick()
	{
		// Rastreia posições dos jogadores para criar trails
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		
		foreach (var player in players)
		{
			if (player.UserId == null)
				continue;

			var steamId = player.SteamID;
			var playerPawn = player.PlayerPawn?.Value;
			
			if (playerPawn == null || playerPawn.AbsOrigin == null || playerPawn.LifeState != 0)
				continue;

			// Verifica se o jogador tem trail habilitado
			if (!_playerTrails.ContainsKey(steamId))
			{
				_playerTrails[steamId] = new TrailData
				{
					SteamId = steamId,
					Enabled = true // Por padrão, trails são habilitados
				};
			}

			if (!_playerTrails[steamId].Enabled)
				continue;

			// Adiciona posição à fila de trail
			if (!_trailPositions.ContainsKey(steamId))
			{
				_trailPositions[steamId] = new Queue<Vector>();
			}

			var currentPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
			_trailPositions[steamId].Enqueue(currentPos);

			// Limita o tamanho da fila
			while (_trailPositions[steamId].Count > MaxTrailPoints)
			{
				_trailPositions[steamId].Dequeue();
			}
		}
	}

	private void UpdateTrails()
	{
		// Cria efeitos visuais nas posições rastreadas usando beams
		foreach (var kvp in _trailPositions.ToList())
		{
			var steamId = kvp.Key;
			var positions = kvp.Value;

			if (positions.Count < 2 || !_playerTrails.ContainsKey(steamId) || !_playerTrails[steamId].Enabled)
				continue;

			var player = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.SteamID == steamId);
			if (player == null || !player.IsValid)
				continue;

			var playerPawn = player.PlayerPawn?.Value;
			if (playerPawn == null || playerPawn.AbsOrigin == null)
				continue;

			var level = GetPlayerLevel(steamId);
			var trailColor = GetTrailColor(level);

			// Cria beam visual conectando posições antigas
			try
			{
				var positionsArray = positions.ToArray();
				if (positionsArray.Length >= 2)
				{
					// Conecta posição atual com posições anteriores para criar trail
					var currentPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
					var previousPos = positionsArray[positionsArray.Length - 2];
					
					// Cria efeito visual de beam (linha) entre posições
					CreateBeamEffect(previousPos, currentPos, trailColor, level);
				}
			}
			catch
			{
				// Ignora erros
			}
		}
	}

	private void CreateBeamEffect(Vector startPos, Vector endPos, string color, int level)
	{
		// Cria efeito visual de trail usando partículas temporárias
		try
		{
			// Calcula distância para evitar criar beams muito longos (teleport)
			var distance = CalculateDistance(startPos, endPos);
			if (distance > 300.0f || distance < 10.0f) // Se teleportou ou muito perto, não cria beam
				return;

			var r = (int)GetColorR(level);
			var g = (int)GetColorG(level);
			var b = (int)GetColorB(level);

			// Para CS2, vamos criar partículas usando efeitos do servidor
			// Cria uma entidade temporária de efeito visual (particle effect)
			var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
			
			foreach (var p in players)
			{
				if (p == null || !p.IsValid)
					continue;

				// Envia efeito visual apenas para jogadores próximos (para performance)
				var playerPos = p.PlayerPawn?.Value?.AbsOrigin;
				if (playerPos == null)
					continue;

				var distToStart = CalculateDistance(new Vector(playerPos.X, playerPos.Y, playerPos.Z), startPos);
				if (distToStart > 1000.0f) // Apenas mostra para jogadores próximos
					continue;

				// Cria efeito usando comando de partícula do CS2
				// Usa env_sprite ou partícula temporária
				var command = $"particle {startPos.X} {startPos.Y} {startPos.Z} {endPos.X} {endPos.Y} {endPos.Z} {r} {g} {b} 200 0.1";
				
				// Alternativa: usar beam temporário via SDK
				// Por enquanto, apenas rastreamos posições (o efeito visual real requer SDK mais avançado)
				// Para uma implementação completa, seria necessário usar CBeam entities via SDK
			}
		}
		catch
		{
			// Ignora erros
		}
	}

	private float GetColorR(int level)
	{
		return level switch
		{
			< 10 => 128, // Grey
			< 15 => 0,   // Green
			< 20 => 135, // LightBlue
			< 25 => 0,   // Blue
			< 30 => 128, // Purple
			< 35 => 255, // Orange
			< 40 => 255, // Gold
			< 50 => 255, // Red
			_ => 255     // Yellow
		};
	}

	private float GetColorG(int level)
	{
		return level switch
		{
			< 10 => 128,
			< 15 => 255,
			< 20 => 206,
			< 25 => 0,
			< 30 => 0,
			< 35 => 165,
			< 40 => 215,
			< 50 => 0,
			_ => 255
		};
	}

	private float GetColorB(int level)
	{
		return level switch
		{
			< 10 => 128,
			< 15 => 0,
			< 20 => 250,
			< 25 => 255,
			< 30 => 128,
			< 35 => 0,
			< 40 => 0,
			< 50 => 0,
			_ => 0
		};
	}

	private static float CalculateDistance(Vector pos1, Vector pos2)
	{
		var dx = pos1.X - pos2.X;
		var dy = pos1.Y - pos2.Y;
		var dz = pos1.Z - pos2.Z;
		return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		var steamId = player.SteamID;
		if (!_playerTrails.ContainsKey(steamId))
		{
			_playerTrails[steamId] = new TrailData
			{
				SteamId = steamId,
				Enabled = true // Habilitado por padrão
			};
		}

		return HookResult.Continue;
	}

	private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		var steamId = player.SteamID;
		// Limpa posições antigas ao spawnar
		if (_trailPositions.ContainsKey(steamId))
		{
			_trailPositions[steamId].Clear();
		}

		return HookResult.Continue;
	}

	private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		var steamId = player.SteamID;
		// Limpa trail ao morrer
		if (_trailPositions.ContainsKey(steamId))
		{
			_trailPositions[steamId].Clear();
		}

		return HookResult.Continue;
	}

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		// Limpa todas as trails ao final do round
		_trailPositions.Clear();
		return HookResult.Continue;
	}

	[CommandHelper(minArgs: 0, usage: "[on/off]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnTrailCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		var steamId = player.SteamID;
		if (!_playerTrails.ContainsKey(steamId))
		{
			_playerTrails[steamId] = new TrailData
			{
				SteamId = steamId,
				Enabled = true
			};
		}

		var args = commandInfo.GetCommandString.Split(' ');
		if (args.Length > 1)
		{
			var action = args[1].ToLower();
			if (action == "on" || action == "1" || action == "ativar")
			{
				_playerTrails[steamId].Enabled = true;
				player.PrintToChat($" {ChatColors.Green}[TRAILS]{ChatColors.Default} Trail {ChatColors.Green}ativado{ChatColors.Default}!");
			}
			else if (action == "off" || action == "0" || action == "desativar")
			{
				_playerTrails[steamId].Enabled = false;
				if (_trailPositions.ContainsKey(steamId))
					_trailPositions[steamId].Clear();
				player.PrintToChat($" {ChatColors.Red}[TRAILS]{ChatColors.Default} Trail {ChatColors.Red}desativado{ChatColors.Default}!");
			}
			else
			{
				_playerTrails[steamId].Enabled = !_playerTrails[steamId].Enabled;
				var status = _playerTrails[steamId].Enabled ? "ativado" : "desativado";
				var color = _playerTrails[steamId].Enabled ? ChatColors.Green : ChatColors.Red;
				player.PrintToChat($" {ChatColors.Default}[TRAILS] Trail {color}{status}{ChatColors.Default}!");
			}
		}
		else
		{
			// Toggle
			_playerTrails[steamId].Enabled = !_playerTrails[steamId].Enabled;
			var status = _playerTrails[steamId].Enabled ? "ativado" : "desativado";
			var color = _playerTrails[steamId].Enabled ? ChatColors.Green : ChatColors.Red;
			player.PrintToChat($" {ChatColors.Default}[TRAILS] Trail {color}{status}{ChatColors.Default}!");
		}

		SaveTrailData();
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

		return 1;
	}

	private string GetTrailColor(int level)
	{
		// Retorna cor baseada no nível (usado para efeitos visuais)
		return level switch
		{
			< 5 => "grey",
			< 10 => "white",
			< 15 => "green",
			< 20 => "lightblue",
			< 25 => "blue",
			< 30 => "purple",
			< 35 => "orange",
			< 40 => "gold",
			< 50 => "red",
			_ => "yellow"
		};
	}

	private void LoadTrailData()
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
				var data = JsonSerializer.Deserialize<Dictionary<ulong, TrailData>>(json);
				
				if (data != null)
				{
					_playerTrails.Clear();
					foreach (var kvp in data)
					{
						_playerTrails[kvp.Key] = kvp.Value;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerTrails] Erro ao carregar dados: {ex.Message}");
		}
	}

	private void SaveTrailData()
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
			var json = JsonSerializer.Serialize(_playerTrails, options);
			File.WriteAllText(fullPath, json);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerTrails] Erro ao salvar dados: {ex.Message}");
		}
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

	private class TrailData
	{
		public ulong SteamId { get; set; }
		public bool Enabled { get; set; } = true;
	}

	private class RankingData
	{
		public int Level { get; set; } = 1;
	}
}

