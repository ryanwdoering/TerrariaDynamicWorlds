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

		public override string Description => "Gives you all three Dynamic Worlds tools: Reality Anchor, Reality Eraser, and Building Anchor.";

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

			// Give Building Anchor
			int builderType = ModContent.ItemType<BuildingAnchorItem>();
			if (builderType > 0)
			{
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), builderType);
			}

			caller.Reply("Given all three Dynamic Worlds tools!", Color.LimeGreen);
		}
	}
}
