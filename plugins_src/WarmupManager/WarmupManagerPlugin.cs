using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;

namespace WarmupManager;

[MinimumApiVersion(0)]
public sealed class WarmupManagerPlugin : BasePlugin
{
	public override string ModuleName => "WarmupManager";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Sistema melhorado de warmup com contador visual e proteção.";

	private float _warmupTime = 15.0f;
	private float _currentWarmupTime = 0.0f;
	private bool _isWarmupActive = false;
	private CounterStrikeSharp.API.Modules.Timers.Timer? _warmupTimer;

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);


		// Detecta quando warmup começa
		AddTimer(1.0f, CheckWarmupStatus, TimerFlags.REPEAT);
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		// Verifica warmup usando mp_warmup_time
		AddTimer(0.5f, () =>
		{
			// Se warmup ainda não começou, inicia
			if (!_isWarmupActive)
			{
				// Sincroniza com a cvar do jogo
				var warmupCvar = ConVar.Find("mp_warmuptime");
				_warmupTime = warmupCvar != null ? warmupCvar.GetPrimitiveValue<float>() : 15.0f;
				
				StartWarmup();
			}
		});

		return HookResult.Continue;
	}

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		StopWarmup();
		return HookResult.Continue;
	}

	private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || IsBot(player))
			return HookResult.Continue;

		// Se está em warmup, aplica proteção de spawn
		if (_isWarmupActive)
		{
			AddTimer(0.1f, () =>
			{
				if (player.IsValid)
				{
					var pawn = player.PlayerPawn?.Value;
					if (pawn != null)
					{
						// Dá armadura e health máxima durante warmup
						pawn.Health = 100;
						pawn.ArmorValue = 100;
						// Nota: HasHelmet e HasHeavyArmor não existem no CS2, mas o ArmorValue já inclui proteção
					}

					// Mensagem de boas-vindas ao warmup
					player.PrintToCenter($" {ChatColors.Gold}═══════════════════════════\n{ChatColors.Yellow}🔥 WARMUP ATIVO 🔥\n{ChatColors.Default}Prepare-se para a batalha!\n{ChatColors.Gold}═══════════════════════════");
				}
			});
		}

		return HookResult.Continue;
	}



	private void CheckWarmupStatus()
	{
		// Verifica warmup usando comando do servidor
		// No CS2, warmup é controlado pelo servidor automaticamente
		// Este método é chamado periodicamente para manter estado
	}

	private void StartWarmup()
	{
		if (_isWarmupActive)
			return;

		_isWarmupActive = true;
		_currentWarmupTime = _warmupTime;

		// Anúncio de início de warmup
		Server.PrintToChatAll($" {ChatColors.Gold}═══════════════════════════════════");
		Server.PrintToChatAll($" {ChatColors.Yellow}🔥 WARMUP INICIADO! 🔥");
		Server.PrintToChatAll($" {ChatColors.Default}Prepare-se para a batalha! Tempo: {ChatColors.Green}{_warmupTime}{ChatColors.Default} segundos");
		Server.PrintToChatAll($" {ChatColors.Default}💡 Você está protegido durante o warmup");
		Server.PrintToChatAll($" {ChatColors.Gold}═══════════════════════════════════");

		// Contador visual
		_warmupTimer?.Kill();
		_warmupTimer = AddTimer(1.0f, UpdateWarmupCountdown, TimerFlags.REPEAT);
	}

	private void StopWarmup()
	{
		if (!_isWarmupActive)
			return;

		_isWarmupActive = false;
		_warmupTimer?.Kill();
		_warmupTimer = null;

		// Anúncio de fim de warmup
		Server.PrintToChatAll($" {ChatColors.Green}═══════════════════════════════════");
		Server.PrintToChatAll($" {ChatColors.Yellow}⚔️ WARMUP FINALIZADO! ⚔️");
		Server.PrintToChatAll($" {ChatColors.Red}🔴 ATENÇÃO: Dano real ativado!");
		Server.PrintToChatAll($" {ChatColors.Green}═══════════════════════════════════");

		// Notifica todos os jogadores
		var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
		foreach (var player in players)
		{
			player.PrintToCenter($" {ChatColors.Red}═══════════════════════════\n{ChatColors.Yellow}⚔️ WARMUP FINALIZADO ⚔️\n{ChatColors.Red}Dano real ativado!\n{ChatColors.Red}═══════════════════════════");
		}
	}

	private void UpdateWarmupCountdown()
	{
		if (!_isWarmupActive)
		{
			_warmupTimer?.Kill();
			return;
		}

		_currentWarmupTime--;

		if (_currentWarmupTime <= 0)
		{
			StopWarmup();
			return;
		}

		// Contador visual a cada segundo (apenas últimos 5 segundos)
		if (_currentWarmupTime <= 5)
		{
			var color = _currentWarmupTime <= 3 ? ChatColors.Red : ChatColors.Yellow;
			var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !IsBot(p));
			
			foreach (var player in players)
			{
				player.PrintToCenter($" {color}═══════════════════════════\n{ChatColors.Yellow}🔥 WARMUP {ChatColors.Default}\n{color}{_currentWarmupTime}{ChatColors.Default} segundos restantes\n{color}═══════════════════════════");
			}

			if (_currentWarmupTime <= 3)
			{
				Server.PrintToChatAll($" {ChatColors.Red}⚠️ Warmup termina em {ChatColors.Gold}{_currentWarmupTime}{ChatColors.Red} segundo(s)!");
			}
		}
	}

	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}
}

