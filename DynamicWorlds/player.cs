using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DynamicWorlds
{
    public class DynamicWorldsPlayer : ModPlayer
    {
        // Last saved world position in tile coordinates.
        private int _savedTileX = -1;
        private int _savedTileY = -1;

        public override void OnEnterWorld()
        {
            // Gift Reality Anchor if not already in inventory
            int anchorType = ModContent.ItemType<RealityAnchor>();
            bool hasAnchor = Player.inventory.Any(i => i != null && i.type == anchorType);
            if (!hasAnchor)
                Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), anchorType);

            // Gift Reality Eraser if not already in inventory
            int eraserType = ModContent.ItemType<RealityEraser>();
            bool hasEraser = Player.inventory.Any(i => i != null && i.type == eraserType);
            if (!hasEraser)
                Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), eraserType);

            // Teleport to last saved position if we have one.
            if (_savedTileX > 0 && _savedTileY > 0)
            {
                // Convert tile coords to world pixels, offset up slightly so we're not inside a block.
                Vector2 pos = new Vector2(_savedTileX * 16f, _savedTileY * 16f - 48f);
                Player.Teleport(pos, 1);
                Player.fallStart = (int)(Player.position.Y / 16f);
                _savedTileX = -1;
                _savedTileY = -1;
            }
        }

        public override void PreSavePlayer()
        {
            // Capture tile position just before the player file is written.
            _savedTileX = (int)(Player.position.X / 16f);
            _savedTileY = (int)(Player.position.Y / 16f);
        }

        // ── Persistence ──────────────────────────────────────────────────

        public override void SaveData(TagCompound tag)
        {
            // Last known position
            if (_savedTileX > 0 && _savedTileY > 0)
            {
                tag["lastTileX"] = _savedTileX;
                tag["lastTileY"] = _savedTileY;
            }
            else if (Player.active)
            {
                // PreSavePlayer may not have fired yet in all paths — fall back to current pos.
                tag["lastTileX"] = (int)(Player.position.X / 16f);
                tag["lastTileY"] = (int)(Player.position.Y / 16f);
            }

            // Bed spawn backup (safety net for post-regen world reloads).
            if (Player.SpawnX >= 0 && Player.SpawnY >= 0)
            {
                tag["spawnX"] = Player.SpawnX;
                tag["spawnY"] = Player.SpawnY;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            // Restore last position — will be applied in OnEnterWorld once the world is ready.
            if (tag.ContainsKey("lastTileX"))
            {
                _savedTileX = tag.GetInt("lastTileX");
                _savedTileY = tag.GetInt("lastTileY");
            }

            // Restore bed spawn if vanilla lost it.
            if (Player.SpawnX < 0 && tag.ContainsKey("spawnX"))
            {
                Player.SpawnX = tag.GetInt("spawnX");
                Player.SpawnY = tag.GetInt("spawnY");
            }
        }
    }
}
