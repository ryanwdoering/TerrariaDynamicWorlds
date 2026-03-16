using Terraria.ModLoader.Config;
using Terraria.ModLoader;
using System.Text.Json.Serialization;

namespace DynamicWorlds
{
	[Label("Dynamic Worlds")]
	public class DynamicWorldsConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[Label("Enable Regen Counter")]
		[Tooltip("Shows the regeneration counter text on screen")]
		public bool EnableRegenCounter = true;

	[Label("Allow Cheats")]
	[Tooltip("Enable cheat commands and functions (e.g., /down, Reality Anchor/Eraser right-click, speed commands)")]
	public bool AllowCheats = true;

	// ── World Generation Settings ─────────────────────────────────────		[Label("Preserve Evil Type")]
		[Tooltip("If enabled, regenerated worlds will have the same evil (Crimson or Corruption) as before")]
		public bool PreserveEvilType = true;

		[JsonIgnore]
		[Label("Preserve Dungeon Side")]
		[Tooltip("If enabled, regenerated worlds will have the dungeon on the same side as before")]
		public bool PreserveDungeonSide = true;

		[JsonIgnore]
		[Label("Preserve Biome Features")]
		[Tooltip("If enabled, world features like jungle, sky islands, etc. will be in similar locations")]
		public bool PreserveBiomeFeatures = true;

		[Label("Randomize World Each Regen")]
		[Tooltip("If disabled, the same seed is used for each regeneration (same layout). If enabled, random seed each time (different layout)")]
		public bool RandomizeSeedEachRegen = true;

		public override void OnLoaded()
		{
			// Called when config is loaded
		}
	}
}
