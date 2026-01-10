using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace MenuCentral;

[MinimumApiVersion(0)]
public sealed class MenuCentralPlugin : BasePlugin
{
	public override string ModuleName => "MenuCentral";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Menu centralizado (!menu) com todos os comandos disponíveis no servidor.";

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnMenuCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		ShowMainMenu(player);
	}

	private void ShowMainMenu(CCSPlayerController player)
	{
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}          🎮 MENU PRINCIPAL - ASTRA SURF COMBAT 🎮");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Green}📊 ESTATÍSTICAS E RANKING:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Green}!stats{ChatColors.Default}     - Suas estatísticas (K/D, HS, etc)");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Green}!top{ChatColors.Default}       - Top 10 jogadores por kills");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Green}!rank{ChatColors.Default}      - Seu ranking no servidor");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.LightBlue}⭐ PONTOS E NÍVEIS:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.LightBlue}!points{ChatColors.Default}    - Seus pontos e XP");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.LightBlue}!level{ChatColors.Default}     - Seu nível e progresso");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.LightBlue}!leaderboard{ChatColors.Default} - Top 10 por pontos");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.LightBlue}!toprank{ChatColors.Default}   - Top 10 por nível");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Orange}🎨 PERSONALIZAÇÃO:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Orange}!trail{ChatColors.Default}     - Liga/desliga trail visual");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Orange}!trails{ChatColors.Default}    - Liga/desliga trail visual");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Purple}🗺️  MAPAS:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Purple}!rtv{ChatColors.Default}       - Votar para trocar mapa");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Red}⚙️  OUTROS:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Red}!menu{ChatColors.Default}      - Mostra este menu");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Red}!help{ChatColors.Default}      - Mostra este menu");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
		player.PrintToChat($" {ChatColors.Default}💡 Digite {ChatColors.Yellow}!<comando>{ChatColors.Default} no chat para usar");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");

		// Mostra também no centro da tela
		player.PrintToCenterHtml(@$"
<font class='fontSize-l' color='#FFD700'>═══════════════════════════</font><br/>
<font class='fontSize-xl' color='#FFFF00'>🎮 MENU PRINCIPAL</font><br/>
<font class='fontSize-l' color='#FFD700'>═══════════════════════════</font><br/>
<font color='#00FF00'>📊 !stats !top !rank</font><br/>
<font color='#87CEEB'>⭐ !points !level !leaderboard</font><br/>
<font color='#FFA500'>🎨 !trail</font><br/>
<font color='#9370DB'>🗺️  !rtv</font><br/>
<font class='fontSize-l' color='#FFD700'>═══════════════════════════</font>
");
	}

	public override void Load(bool hotReload)
	{
		AddCommand("css_menu", "Mostra menu principal com todos os comandos", OnMenuCommand);
		AddCommand("css_help", "Mostra menu principal com todos os comandos", OnMenuCommand);
		AddCommand("css_comandos", "Mostra menu principal com todos os comandos", OnMenuCommand);
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
}

