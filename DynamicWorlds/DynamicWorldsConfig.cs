using Terraria.ModLoader.Config;
using Terraria.ModLoader;
using System.Text.Json.Serialization;

namespace DynamicWorlds
{
	[Label("Dynamic Worlds")]
	public class DynamicWorldsConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[Label("Enable Scheduled Regen")]
		[Tooltip("Enable or pause the automatic world regeneration scheduler")]
		public bool EnableRegenCounter = true;

		[Range(1, 100)]
		[Label("Scheduled Regen Interval (Days)")]
		[Tooltip("How many in-game days pass between automatic world regenerations when scheduled regen is enabled")]
		public int ScheduledRegenIntervalDays = 7;

	[Label("Allow Cheats")]
	[Tooltip("Enable cheat commands and manual tool actions such as /down, /hardmode, and Reality Anchor/Eraser inventory right-click")]
	public bool AllowCheats = true;

	// ── World Generation Settings ─────────────────────────────────────		[Label("Preserve Evil Type")]
		[Tooltip("If enabled, regenerated worlds will have the same evil (Crimson or Corruption) as before")]
		public bool PreserveEvilType = true;

		[JsonIgnore]
		[Label("Preserve Dungeon Side")]
		[Tooltip("Planned setting. Currently documented, but not yet enforced during world generation")]
		public bool PreserveDungeonSide = true;

		[JsonIgnore]
		[Label("Preserve Biome Features")]
		[Tooltip("Planned setting. Currently documented, but not yet enforced during world generation")]
		public bool PreserveBiomeFeatures = true;

		[Label("Randomize World Each Regen")]
		[Tooltip("If disabled, regen reuses the current world seed when available. If enabled, each regeneration picks a fresh seed")]
		public bool RandomizeSeedEachRegen = true;

		public override void OnLoaded()
		{
			// Called when config is loaded
		}
	}
}
