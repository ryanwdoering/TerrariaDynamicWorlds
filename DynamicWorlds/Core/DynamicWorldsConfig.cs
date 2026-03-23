using Terraria.ModLoader.Config;
using Terraria.ModLoader;
using System.Text.Json.Serialization;

namespace DynamicWorlds
{
	public class DynamicWorldsConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public bool EnableRegenCounter = true;

		[Range(1, 100)]
		public int ScheduledRegenIntervalDays = 7;

		public bool AllowCheats = true;

		public bool RegenOnDeath = false;

		// Chat/debug logging
		public bool BiomeDowserRegenChatLog = false;

		// ── World Generation Settings ─────────────────────────────────────
		public bool PreserveEvilType = true;

		[JsonIgnore]
		public bool PreserveDungeonSide = true;

		[JsonIgnore]
		public bool PreserveBiomeFeatures = true;

		public bool RandomizeSeedEachRegen = true;

		public override void OnLoaded()
		{
			// Called when config is loaded
		}
	}
}
