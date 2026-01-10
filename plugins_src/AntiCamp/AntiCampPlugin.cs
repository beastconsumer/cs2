using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace AntiCamp;

[MinimumApiVersion(0)]
public sealed class AntiCampPlugin : BasePlugin
{
	public override string ModuleName => "AntiCamp";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Detecta e previne camping em surf combat.";

	// Configurações
	private const float CheckInterval = 1.0f; // Verifica a cada 1 segundo
	private const float CampThreshold = 5.0f; // Tempo em segundos para considerar camping
	private const float PositionTolerance = 50.0f; // Distância máxima para considerar "mesma posição" (unidades Source)
	private const int MaxWarnings = 3; // Avisos antes de aplicar punição

	// Armazena informações de cada jogador
	private readonly Dictionary<uint, PlayerCampData> _playerData = new();

	public override void Load(bool hotReload)
	{
		RegisterListener<Listeners.OnTick>(OnTick);
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

		AddTimer(CheckInterval, CheckCamping, TimerFlags.REPEAT);

		if (hotReload)
		{
			// Limpa dados em hot reload
			_playerData.Clear();
		}
	}

	private void OnTick()
	{
		// Atualiza posições dos jogadores vivos
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		
		foreach (var player in players)
		{
			if (player.UserId == null)
				continue;
				
			var userId = (uint)player.UserId;
			var playerPawn = player.PlayerPawn?.Value;
			
			// Verifica se está vivo através do pawn
			if (playerPawn == null || playerPawn.AbsOrigin == null || playerPawn.LifeState != 0)
				continue;

			if (!_playerData.ContainsKey(userId))
			{
				_playerData[userId] = new PlayerCampData();
			}

			var data = _playerData[userId];

			var currentPos = playerPawn.AbsOrigin;
			var currentTime = Server.CurrentTime;
			var currentVector = new Vector(currentPos.X, currentPos.Y, currentPos.Z);

			// Se mudou significativamente de posição, reseta o timer
			if (data.LastPosition != null)
			{
				var distance = CalculateDistance(data.LastPosition, currentVector);
				if (distance > PositionTolerance)
				{
					data.LastPosition = currentVector;
					data.CampStartTime = currentTime;
					data.WarningCount = 0;
					data.LastWarningTime = 0;
				}
				else
				{
					// Mesma posição, atualiza apenas se necessário
					if (data.CampStartTime == 0)
					{
						data.CampStartTime = currentTime;
					}
				}
			}
			else
			{
				data.LastPosition = currentVector;
				data.CampStartTime = currentTime;
			}
		}
	}

	private void CheckCamping()
	{
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));

		foreach (var player in players)
		{
			if (player.UserId == null)
				continue;
				
			var userId = (uint)player.UserId;
			var playerPawn = player.PlayerPawn?.Value;
			
			// Verifica se está vivo através do pawn
			if (playerPawn == null || playerPawn.LifeState != 0)
				continue;

			if (!_playerData.ContainsKey(userId))
				continue;

			var data = _playerData[userId];
			var currentTime = Server.CurrentTime;

			// Verifica se está campando
			if (data.CampStartTime > 0 && (currentTime - data.CampStartTime) >= CampThreshold)
			{
				var campTime = currentTime - data.CampStartTime;

				// Evita spam de avisos (1 aviso a cada 2 segundos)
				if ((currentTime - data.LastWarningTime) >= 2.0f)
				{
					data.WarningCount++;
					data.LastWarningTime = currentTime;

					var playerName = NormalizeName(player.PlayerName);
					var seconds = (int)campTime;

					// Avisos progressivos
					if (data.WarningCount <= MaxWarnings)
					{
						player.PrintToChat($" {ChatColors.Red}[ANTI-CAMP]{ChatColors.Default} Você está campando há {seconds}s! Movimente-se!");
						player.PrintToCenter($" {ChatColors.Red}⚠ CAMPANDO ⚠{ChatColors.Default}\n{seconds} segundos no mesmo lugar");
					}
					else
					{
						// Aplicar dano progressivo após avisos
						var damage = 5 * (data.WarningCount - MaxWarnings);
						player.PrintToChat($" {ChatColors.Red}[ANTI-CAMP]{ChatColors.Default} Dano aplicado por camping: {damage}HP");
						player.PrintToCenter($" {ChatColors.Red}⚡ DANO POR CAMPING ⚡{ChatColors.Default}\n-{damage} HP");

						if (playerPawn != null && playerPawn.Health > damage)
						{
							playerPawn.Health -= damage;
						}
						else if (playerPawn != null && playerPawn.Health > 0)
						{
							// Se o dano seria fatal, apenas deixa com 1 HP
							playerPawn.Health = 1;
						}
					}
				}
			}
		}
	}

	private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || player.UserId == null)
			return HookResult.Continue;

		var userId = (uint)player.UserId;
		
		// Reseta dados ao spawnar
		if (_playerData.ContainsKey(userId))
		{
			_playerData[userId] = new PlayerCampData();
		}

		return HookResult.Continue;
	}

	private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || player.UserId == null)
			return HookResult.Continue;

		var userId = (uint)player.UserId;
		if (_playerData.ContainsKey(userId))
		{
			// Reseta ao morrer
			_playerData[userId] = new PlayerCampData();
		}

		return HookResult.Continue;
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		// Limpa dados ao iniciar round (opcional - pode manter histórico)
		// _playerData.Clear();
		return HookResult.Continue;
	}

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		// Reseta contadores ao final do round
		foreach (var data in _playerData.Values)
		{
			data.CampStartTime = 0;
			data.WarningCount = 0;
		}
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

	private static string NormalizeName(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (trimmed.Length <= 32)
			return trimmed;

		return trimmed[..31] + "…";
	}

	private static float CalculateDistance(Vector pos1, Vector pos2)
	{
		var dx = pos1.X - pos2.X;
		var dy = pos1.Y - pos2.Y;
		var dz = pos1.Z - pos2.Z;
		return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	private class PlayerCampData
	{
		public Vector? LastPosition { get; set; }
		public float CampStartTime { get; set; }
		public int WarningCount { get; set; }
		public float LastWarningTime { get; set; }
	}
}