using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DynamicWorlds
{
    internal static class WorldToolOverlayHelper
    {
        public static bool IsHoldingWorldTool(Player player)
        {
            if (player?.HeldItem == null)
                return false;

            int heldType = player.HeldItem.type;
            return heldType == ModContent.ItemType<RealityAnchor>()
                || heldType == ModContent.ItemType<RealityEraser>()
                || heldType == ModContent.ItemType<StructureAnchorItem>();
        }

        public static bool IsTileOnScreen(Point16 tilePosition)
        {
            return tilePosition.X >= Main.screenPosition.X / 16 - 2
                && tilePosition.X <= (Main.screenPosition.X + Main.screenWidth) / 16 + 2
                && tilePosition.Y >= Main.screenPosition.Y / 16 - 2
                && tilePosition.Y <= (Main.screenPosition.Y + Main.screenHeight) / 16 + 2;
        }

        public static void BeginOverlay(SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null, null, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        public static void DrawTileOverlay(SpriteBatch spriteBatch, Point16 tilePosition, Vector2 screenPos, Color fill, Color outline)
        {
            DrawAreaOverlay(spriteBatch, tilePosition, tilePosition, screenPos, fill, outline);
        }

        public static void DrawAreaOverlay(SpriteBatch spriteBatch, Point16 topLeft, Point16 bottomRight, Vector2 screenPos, Color fill, Color outline)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle(
                (int)(topLeft.X * 16 - screenPos.X),
                (int)(topLeft.Y * 16 - screenPos.Y),
                (bottomRight.X - topLeft.X + 1) * 16,
                (bottomRight.Y - topLeft.Y + 1) * 16);

            spriteBatch.Draw(pixel, rect, fill);
            DrawRectangleOutline(spriteBatch, pixel, rect, outline, 2);
        }

        private static void DrawRectangleOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
