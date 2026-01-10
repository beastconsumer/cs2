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
	public override string ModuleVersion => "2.0.0";
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

		if (_playerMenus.ContainsKey(player.SteamID))
		{
			_playerMenus.Remove(player.SteamID);
			player.PrintToCenterHtml("");
			player.PrintToChat($" {ChatColors.Default}[MENU] Menu fechado.");
		}
	}

	private void ShowMainMenu(CCSPlayerController player)
	{
		_playerMenus[player.SteamID] = 0; // Menu principal

		// Menu HTML no canto esquerdo superior (estilo CS:GO) - apenas uma vez, sem spam
		var menuHtml = $@"
<font class='fontSize-xl' color='#FFD700'><b>═══════════════════════════</b></font><br/>
<font class='fontSize-l' color='#FFFF00'><b>🎮 MENU PRINCIPAL</b></font><br/>
<font class='fontSize-xl' color='#FFD700'><b>═══════════════════════════</b></font><br/>
<font class='fontSize-m' color='#00FF00'><b>📊 ESTATÍSTICAS:</b></font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [1] !stats - Suas estatísticas</font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [2] !top - Top 10 jogadores</font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [3] !rank - Seu ranking</font><br/>
<font class='fontSize-m' color='#87CEEB'><b>⭐ PONTOS E NÍVEIS:</b></font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [4] !points - Seus pontos</font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [5] !level - Seu nível</font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [6] !leaderboard - Top pontos</font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [7] !toprank - Top nível</font><br/>
<font class='fontSize-m' color='#FFA500'><b>🎨 PERSONALIZAÇÃO:</b></font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [8] !trail - Trail visual</font><br/>
<font class='fontSize-m' color='#9370DB'><b>🗺️  MAPAS:</b></font><br/>
<font class='fontSize-s' color='#FFFFFF'>  [9] !rtv - Votar mapa</font><br/>
<font class='fontSize-s' color='#CCCCCC'>Digite o número ou [0] para fechar</font><br/>
<font class='fontSize-xl' color='#FFD700'><b>═══════════════════════════</b></font>
";

		player.PrintToCenterHtml(menuHtml);
		player.PrintToChat($" {ChatColors.Gold}[MENU]{ChatColors.Default} Menu aberto! Digite o {ChatColors.Yellow}número{ChatColors.Default} da opção ou {ChatColors.Yellow}0{ChatColors.Default} para fechar.");
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
		if (_playerMenus.ContainsKey(player.SteamID))
		{
			_playerMenus.Remove(player.SteamID);
			AddTimer(0.1f, () => player.PrintToCenterHtml(""));
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
}

