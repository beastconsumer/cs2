using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace MenuCentral;

[MinimumApiVersion(0)]
public sealed class MenuCentralPlugin : BasePlugin
{
	public override string ModuleName => "MenuCentral";
	public override string ModuleVersion => "2.1.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Menu HTML interativo estilo CS:GO com navegação por números.";

	private readonly Dictionary<ulong, int> _playerMenus = new(); // SteamID -> MenuPage

	public override void Load(bool hotReload)
	{
		AddCommand("css_menu", "Abre menu principal interativo", OnMenuCommand);
		AddCommand("css_help", "Abre menu principal interativo", OnMenuCommand);
		AddCommand("css_comandos", "Abre menu principal interativo", OnMenuCommand);
		
		// Comandos numéricos para navegação
		AddCommand("css_1", "Seleciona opção 1", OnNumberCommand);
		AddCommand("css_2", "Seleciona opção 2", OnNumberCommand);
		AddCommand("css_3", "Seleciona opção 3", OnNumberCommand);
		AddCommand("css_4", "Seleciona opção 4", OnNumberCommand);
		AddCommand("css_5", "Seleciona opção 5", OnNumberCommand);
		AddCommand("css_6", "Seleciona opção 6", OnNumberCommand);
		AddCommand("css_7", "Seleciona opção 7", OnNumberCommand);
		AddCommand("css_8", "Seleciona opção 8", OnNumberCommand);
		AddCommand("css_9", "Seleciona opção 9", OnNumberCommand);
		AddCommand("css_0", "Fecha o menu", OnCloseMenuCommand);
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnMenuCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		ShowMainMenu(player);
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnNumberCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		if (!_playerMenus.ContainsKey(player.SteamID))
		{
			player.PrintToChat($" {ChatColors.Red}[MENU]{ChatColors.Default} Digite {ChatColors.Yellow}!menu{ChatColors.Default} primeiro!");
			return;
		}

		var command = commandInfo.GetCommandString.Replace("css_", "").Trim();
		if (int.TryParse(command, out var option))
		{
			ExecuteMenuOption(player, option);
		}
	}

	[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
	private void OnCloseMenuCommand(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid || IsBot(player))
			return;

		CloseMenu(player);
		player.PrintToChat($" {ChatColors.Default}[MENU] Menu fechado.");
	}

	private void ShowMainMenu(CCSPlayerController player)
	{
		_playerMenus[player.SteamID] = 0; // Menu principal

		// Menu exibido no chat (lado esquerdo da tela) - não interfere com avisos centrais
		// O menu aparece uma vez quando o jogador digita !menu
		DisplayMenuInChat(player);

		player.PrintToChat($" {ChatColors.Gold}[MENU]{ChatColors.Default} Menu aberto! Digite o {ChatColors.Yellow}número{ChatColors.Default} da opção ou {ChatColors.Yellow}0{ChatColors.Default} para fechar.");
	}

	private void DisplayMenuInChat(CCSPlayerController player)
	{
		// Exibe menu formatado no chat (lado esquerdo da tela, não interfere com centro)
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
		player.PrintToChat($" {ChatColors.Yellow}          🎮 MENU PRINCIPAL - ASTRA SURF COMBAT 🎮");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Green}📊 ESTATÍSTICAS E RANKING:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[1]{ChatColors.Default} {ChatColors.Green}!stats{ChatColors.Default}     - Suas estatísticas (K/D, HS, etc)");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[2]{ChatColors.Default} {ChatColors.Green}!top{ChatColors.Default}       - Top 10 jogadores por kills");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[3]{ChatColors.Default} {ChatColors.Green}!rank{ChatColors.Default}      - Seu ranking no servidor");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.LightBlue}⭐ PONTOS E NÍVEIS:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[4]{ChatColors.Default} {ChatColors.LightBlue}!points{ChatColors.Default}    - Seus pontos e XP");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[5]{ChatColors.Default} {ChatColors.LightBlue}!level{ChatColors.Default}     - Seu nível e progresso");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[6]{ChatColors.Default} {ChatColors.LightBlue}!leaderboard{ChatColors.Default} - Top 10 por pontos");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[7]{ChatColors.Default} {ChatColors.LightBlue}!toprank{ChatColors.Default}   - Top 10 por nível");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Orange}🎨 PERSONALIZAÇÃO:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[8]{ChatColors.Default} {ChatColors.Orange}!trail{ChatColors.Default}     - Liga/desliga trail visual");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Purple}🗺️  MAPAS:");
		player.PrintToChat($" {ChatColors.Default}  {ChatColors.Yellow}[9]{ChatColors.Default} {ChatColors.Purple}!rtv{ChatColors.Default}       - Votar para trocar mapa");
		player.PrintToChat($" ");
		player.PrintToChat($" {ChatColors.Default}💡 Digite o {ChatColors.Yellow}número{ChatColors.Default} da opção ou {ChatColors.Yellow}0{ChatColors.Default} para fechar");
		player.PrintToChat($" {ChatColors.Gold}═══════════════════════════════════════════════════");
	}

	private void ExecuteMenuOption(CCSPlayerController player, int option)
	{
		CloseMenu(player);
		
		// Executa o comando correspondente via chat do jogador
		var command = option switch
		{
			1 => "!stats",
			2 => "!top",
			3 => "!rank",
			4 => "!points",
			5 => "!level",
			6 => "!leaderboard",
			7 => "!toprank",
			8 => "!trail",
			9 => "!rtv",
			_ => null
		};

		if (command != null)
		{
			// Envia comando via console do jogador
			player.ExecuteClientCommand($"say {command}");
		}
		else
		{
			player.PrintToChat($" {ChatColors.Red}[MENU]{ChatColors.Default} Opção inválida!");
		}
	}

	private void CloseMenu(CCSPlayerController player)
	{
		var steamId = player.SteamID;
		
		if (_playerMenus.ContainsKey(steamId))
		{
			_playerMenus.Remove(steamId);
		}
	}

	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}
}

