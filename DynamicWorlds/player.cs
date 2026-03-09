using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace DynamicWorlds
{
    public class DynamicWorldsPlayer : ModPlayer
    {
        public override void OnEnterWorld()
        {
            // Only give one if they don't already have it
            int toolType = ModContent.ItemType<RealityAnchor>();
            bool hasTool = Player.inventory.Any(i => i != null && i.type == toolType);

            if (!hasTool)
            {
                Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), toolType);
            }
        }
    }
}
