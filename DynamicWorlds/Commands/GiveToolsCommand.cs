using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace DynamicWorlds
{
	public class GiveToolsCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;

		public override string Command => "dwtools";

		public override string Usage => "/dwtools";

		public override string Description => "Gives you all Dynamic Worlds tools: Anchor, Eraser, Structure Anchor, Biome Dowser, and Prefab Tool.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player player = caller.Player;

			// Give Reality Anchor
			int anchorType = ModContent.ItemType<RealityAnchor>();
			if (anchorType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), anchorType);
			}

			// Give Reality Eraser
			int eraserType = ModContent.ItemType<RealityEraser>();
			if (eraserType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), eraserType);
			}

			// Give Structure Anchor
			int builderType = ModContent.ItemType<StructureAnchorItem>();
			if (builderType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), builderType);
			}

			// Give Biome Dowser
			int dowserType = ModContent.ItemType<BiomeDowser>();
			if (dowserType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), dowserType);
			}

			// Give Prefab Tool
			int prefabType = ModContent.ItemType<PrefabToolItem>();
			if (prefabType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), prefabType);
			}

			caller.Reply("Given all Dynamic Worlds tools!", Color.LimeGreen);
		}
	}
}
