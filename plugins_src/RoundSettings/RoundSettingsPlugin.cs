using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;

namespace RoundSettings;

[MinimumApiVersion(0)]
public sealed class RoundSettingsPlugin : BasePlugin
{
	public override string ModuleName => "RoundSettings";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "Local";
	public override string ModuleDescription => "Forces 5-minute rounds (mp_roundtime*) and normal win conditions.";

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventRoundStart>(OnRoundStart);

		// Gamemode cfgs can apply *after* plugins load; delay + keep enforcing.
		AddTimer(5.0f, ApplySettings);
		AddTimer(15.0f, ApplySettings, TimerFlags.REPEAT);
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		ApplySettings();
		return HookResult.Continue;
	}

	private static void ApplySettings()
	{
		// CS2 can override these via gamemode configs; re-apply at runtime.
		// Surf Combat uses 10-minute rounds
		Server.ExecuteCommand("mp_ignore_round_win_conditions 0");
		Server.ExecuteCommand("mp_roundtime 10");
		Server.ExecuteCommand("mp_roundtime_defuse 10");
		Server.ExecuteCommand("mp_roundtime_hostage 10");
	}
}
