using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

namespace DamageInfo;

[MinimumApiVersion(0)]
public sealed class DamageInfoPlugin : BasePlugin
{
	public override string ModuleName => "DamageInfo";
	public override string ModuleVersion => "1.0.2";
	public override string ModuleAuthor => "Local";
	public override string ModuleDescription => "Shows hit location, damage, and remaining HP to the attacker.";

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
	}

	private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
	{
		var attacker = @event.Attacker;
		var victim = @event.Userid;

		if (attacker == null || victim == null)
			return HookResult.Continue;

		if (!attacker.IsValid || !victim.IsValid)
			return HookResult.Continue;

		if (attacker == victim)
			return HookResult.Continue;

		if (attacker.TeamNum <= 1)
			return HookResult.Continue;

		var damage = @event.DmgHealth;
		if (damage <= 0)
			return HookResult.Continue;

		var remainingHp = @event.Health;
		var hitgroupName = HitgroupToPtBr(@event.Hitgroup);
		var victimName = NormalizeName(victim.PlayerName);

		// "telinha" no centro: sem linhas em branco (elas fazem a fonte ficar minúscula)
		attacker.PrintToCenter($"{victimName}\n-{damage} {hitgroupName}\nHP {remainingHp}");

		return HookResult.Continue;
	}

	private static string HitgroupToPtBr(int hitgroup)
	{
		return hitgroup switch
		{
			1 => "cabeça",
			2 => "peito",
			3 => "estômago",
			4 => "braço E",
			5 => "braço D",
			6 => "perna E",
			7 => "perna D",
			8 => "pescoço",
			_ => "corpo"
		};
	}

	private static string NormalizeName(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (trimmed.Length <= 18)
			return trimmed;

		return trimmed[..17] + "…";
	}
}
