using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace ChatTags;

[MinimumApiVersion(0)]
public sealed class ChatTagsPlugin : BasePlugin
{
	public override string ModuleName => "ChatTags";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "ASTRA SURF COMBAT";
	public override string ModuleDescription => "Adiciona tags de nível nas mensagens de chat dos jogadores.";

	private readonly string _rankingDataPath = "addons/counterstrikesharp/configs/plugins/PlayerRanking/ranks.json";

	public override void Load(bool hotReload)
	{
		// Para interceptar chat no CS2, precisamos usar hooks específicos
		// Por enquanto, o plugin está preparado para interceptação futura
		// A funcionalidade será implementada quando houver suporte adequado na API
		Console.WriteLine("[ChatTags] Plugin carregado. Tags serão exibidas quando disponível.");
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

	private static string NormalizeName(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (trimmed.Length <= 32)
			return trimmed;
		return trimmed[..31] + "…";
	}

	private static bool IsBot(CCSPlayerController player)
	{
		if (player == null || !player.IsValid)
			return true;

		return player.IsBot;
	}

	private class RankingData
	{
		public int Level { get; set; } = 1;
	}
}

