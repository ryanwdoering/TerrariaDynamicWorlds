using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DynamicWorlds
{
    // ---------------------------------------------------------------------
    //  World-level system: stores all Erased Tile positions and clears them
    //  before anchored tiles are placed after world regeneration.
    // ---------------------------------------------------------------------
    public class ErasedTileSystem : ModSystem
    {
        // All tile positions marked for erasure on next regen
        public static readonly HashSet<Point16> ErasedTiles = new HashSet<Point16>();

        // Overlay icon (ErasedTile.png) drawn on each erased tile when tool is held
        public static Asset<Texture2D> ErasedIcon;

        public override void OnWorldLoad()
        {
            ErasedTiles.Clear();

            if (ErasedIcon == null || !ErasedIcon.IsLoaded)
                ErasedIcon = ModContent.Request<Texture2D>("DynamicWorlds/ErasedTile");
        }

        public override void OnWorldUnload()
        {
            ErasedTiles.Clear();
        }

        // -------------------------------------------------------------
        //  Tile cap — much more generous than the Anchor cap
        // -------------------------------------------------------------
        //  Base (no bosses)       50,000
        //  King Slime             + 5,000  →   55,000
        //  Eye of Cthulhu         + 5,000  →   60,000
        //  Evil boss (EoW/BoC)    + 5,000  →   65,000
        //  Skeletron              + 5,000  →   70,000
        //  Queen Bee              + 5,000  →   75,000
        //  Deerclops              + 5,000  →   80,000
        //  Wall of Flesh (HM)    +20,000  →  100,000
        //  Twins                 +30,000  →  130,000
        //  Destroyer             +30,000  →  160,000
        //  Skeletron Prime       +30,000  →  190,000
        //  Plantera              +60,000  →  250,000
        //  Golem                +100,000  →  350,000
        //  Duke Fishron         +100,000  →  450,000
        //  Empress of Light     +100,000  →  550,000
        //  Lunatic Cultist      +100,000  →  650,000
        //  Moon Lord            +350,000  → 1,000,000
        public static int GetErasureCap()
        {
            int cap = 50_000;

            if (NPC.downedSlimeKing)      cap +=   5_000;
            if (NPC.downedBoss1)          cap +=   5_000;
            if (NPC.downedBoss2)          cap +=   5_000;
            if (NPC.downedBoss3)          cap +=   5_000;
            if (NPC.downedQueenBee)       cap +=   5_000;
            if (NPC.downedDeerclops)      cap +=   5_000;

            if (Main.hardMode)            cap +=  20_000;

            if (NPC.downedMechBoss1)      cap +=  30_000;
            if (NPC.downedMechBoss2)      cap +=  30_000;
            if (NPC.downedMechBoss3)      cap +=  30_000;
            if (NPC.downedPlantBoss)      cap +=  60_000;
            if (NPC.downedGolemBoss)      cap += 100_000;
            if (NPC.downedFishron)        cap += 100_000;
            if (NPC.downedEmpressOfLight) cap += 100_000;
            if (NPC.downedAncientCultist) cap += 100_000;
            if (NPC.downedMoonlord)       cap += 350_000;

            return cap;
        }

        // Mark a tile for erasure (add only — used during drag)
        public static void EraseTile(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            ErasedTiles.Add(new Point16(x, y));
        }

        // Unmark a tile for erasure (remove only — used during drag)
        public static void UnEraseTile(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            ErasedTiles.Remove(new Point16(x, y));
        }

        // Toggle erasure mark on a single tile (used for single clicks)
        public static void ToggleErase(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            var pos = new Point16(x, y);
            if (ErasedTiles.Contains(pos))
            {
                ErasedTiles.Remove(pos);
            }
            else
            {
                int cap = GetErasureCap();
                if (ErasedTiles.Count >= cap)
                {
                    Main.NewText($"Erasure cap reached ({cap}). Defeat more bosses to expand your limit.", 255, 200, 80);
                    return;
                }
                ErasedTiles.Add(pos);
            }
        }

        // Apply a rectangle of eraser marks.
        // If the start tile was already marked, the whole rectangle is unmarked; otherwise it is marked.
        public static void ApplyRectangle(Point16 start, Point16 end, bool removing)
        {
            int x0 = System.Math.Min(start.X, end.X);
            int x1 = System.Math.Max(start.X, end.X);
            int y0 = System.Math.Min(start.Y, end.Y);
            int y1 = System.Math.Max(start.Y, end.Y);

            int cap     = GetErasureCap();
            int added   = 0;
            int skipped = 0;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    var pos = new Point16(x, y);
                    if (removing)
                    {
                        ErasedTiles.Remove(pos);
                    }
                    else if (!ErasedTiles.Contains(pos))
                    {
                        if (ErasedTiles.Count >= cap)
                        {
                            skipped++;
                            continue;
                        }
                        ErasedTiles.Add(pos);
                        added++;
                    }
                }
            }

            int w = x1 - x0 + 1;
            int h = y1 - y0 + 1;
            if (removing)
            {
                Main.NewText($"Unmarked {w}×{h} region for erasure.", 255, 150, 100);
            }
            else
            {
                Main.NewText($"Marked {added} tile{(added == 1 ? "" : "s")} for erasure in {w}×{h} region. ({ErasedTiles.Count}/{cap} used)", 255, 120, 60);
                if (skipped > 0)
                    Main.NewText($"{skipped} tile{(skipped == 1 ? "" : "s")} skipped — erasure cap reached. Defeat more bosses to expand your limit.", 255, 200, 80);
            }
        }

        // Call this during world regeneration — BEFORE anchored tiles are restored.
        // Clears every tile position marked for erasure.
        public static void ClearAllErasedTiles()
        {
            int cleared = 0;
            foreach (var pos in ErasedTiles)
            {
                int x = pos.X;
                int y = pos.Y;
                if (!WorldGen.InWorld(x, y, 1))
                    continue;

                Tile tile = Framing.GetTileSafely(x, y);
                tile.ClearEverything();
                cleared++;
            }

            if (cleared > 0)
                WorldGen.RangeFrame(0, 0, Main.maxTilesX, Main.maxTilesY);

            if (Main.netMode == NetmodeID.SinglePlayer && cleared > 0)
                Main.NewText($"Erased {cleared} marked tile{(cleared == 1 ? "" : "s")}.", 255, 150, 80);
        }

        // --- Save / Load -------------------------------------------------
        public override void SaveWorldData(TagCompound tag)
        {
            var list = new List<TagCompound>();
            foreach (var pos in ErasedTiles)
            {
                list.Add(new TagCompound
                {
                    ["x"] = pos.X,
                    ["y"] = pos.Y
                });
            }
            tag["ErasedTiles"] = list;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            ErasedTiles.Clear();

            if (tag.ContainsKey("ErasedTiles"))
            {
                var list = tag.GetList<TagCompound>("ErasedTiles");
                foreach (var t in list)
                    ErasedTiles.Add(new Point16(t.GetShort("x"), t.GetShort("y")));
            }
        }

        // --- Visual overlay ----------------------------------------------
        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player == null || player.HeldItem == null)
                return;

            if (player.HeldItem.type != ModContent.ItemType<RealityEraser>())
                return;

            if (ErasedIcon == null || !ErasedIcon.IsLoaded)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D tex = ErasedIcon.Value;
            Vector2 screenPos = Main.screenPosition;

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null, null, null,
                Main.GameViewMatrix.TransformationMatrix);

            // Draw all erased tiles
            foreach (var pos in ErasedTiles)
            {
                if (!IsOnScreen(pos))
                    continue;

                DrawEraserIcon(spriteBatch, tex, pos, screenPos, Color.White * 0.8f);
            }

            // Draw the live drag-preview rectangle
            var mp = player.GetModPlayer<RealityEraserPlayer>();
            if (mp.IsDragging)
            {
                Point16 dragStart = mp.DragStart;
                Point16 dragEnd   = mp.DragEnd;

                int x0 = System.Math.Min(dragStart.X, dragEnd.X);
                int x1 = System.Math.Max(dragStart.X, dragEnd.X);
                int y0 = System.Math.Min(dragStart.Y, dragEnd.Y);
                int y1 = System.Math.Max(dragStart.Y, dragEnd.Y);

                Color previewColor = mp.DragRemoving
                    ? Color.Lime * 0.5f
                    : Color.OrangeRed * 0.5f;

                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Rectangle screenRect = new Rectangle(
                    (int)(x0 * 16 - screenPos.X),
                    (int)(y0 * 16 - screenPos.Y),
                    (x1 - x0 + 1) * 16,
                    (y1 - y0 + 1) * 16);
                spriteBatch.Draw(pixel, screenRect, previewColor);

                // Outline
                Color outlineColor = mp.DragRemoving ? Color.Lime : Color.OrangeRed;
                DrawRectangleOutline(spriteBatch, pixel, screenRect, outlineColor, 2);

                // Icons on perimeter tiles of the preview
                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        bool onEdge = x == x0 || x == x1 || y == y0 || y == y1;
                        if (!onEdge && (x1 - x0) > 4 && (y1 - y0) > 4)
                            continue;

                        var pp = new Point16(x, y);
                        if (!IsOnScreen(pp))
                            continue;

                        if (!ErasedTiles.Contains(pp))
                            DrawEraserIcon(spriteBatch, tex, pp, screenPos, previewColor * 1.6f);
                    }
                }
            }

            spriteBatch.End();
        }

        private static bool IsOnScreen(Point16 p)
        {
            return p.X >= Main.screenPosition.X / 16 - 2
                && p.X <= (Main.screenPosition.X + Main.screenWidth)  / 16 + 2
                && p.Y >= Main.screenPosition.Y / 16 - 2
                && p.Y <= (Main.screenPosition.Y + Main.screenHeight) / 16 + 2;
        }

        private static void DrawEraserIcon(SpriteBatch sb, Texture2D tex, Point16 p, Vector2 screenPos, Color color)
        {
            Vector2 drawPos = new Vector2(p.X * 16 + 8f, p.Y * 16 + 8f) - screenPos;
            Vector2 origin  = new Vector2(tex.Width / 2f, tex.Height / 2f);
            sb.Draw(tex, drawPos, null, color, 0f, origin, 0.6f, SpriteEffects.None, 0f);
        }

        private static void DrawRectangleOutline(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }

    // ---------------------------------------------------------------------
    //  ModPlayer: tracks drag state per-player for the Reality Eraser
    // ---------------------------------------------------------------------
    public class RealityEraserPlayer : ModPlayer
    {
        public bool    IsDragging   = false;
        public bool    DragRemoving = false;
        public Point16 DragStart;
        public Point16 DragEnd;

        private bool _wasHoldingLastFrame = false;

        public override void PostUpdate()
        {
            // Only act when holding the Reality Eraser in singleplayer
            if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool holdingTool = Player.HeldItem != null
                && Player.HeldItem.type == ModContent.ItemType<RealityEraser>();

            if (!holdingTool)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool mouseHeld = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
            int tileX = (int)(Main.MouseWorld.X / 16f);
            int tileY = (int)(Main.MouseWorld.Y / 16f);

            if (mouseHeld)
            {
                if (!_wasHoldingLastFrame)
                {
                    // First frame the button is pressed — start drag
                    IsDragging   = true;
                    DragStart    = new Point16(tileX, tileY);
                    DragEnd      = DragStart;
                    DragRemoving = ErasedTileSystem.ErasedTiles.Contains(DragStart);
                }
                else if (IsDragging)
                {
                    // Subsequent frames — update the endpoint every frame
                    DragEnd = new Point16(tileX, tileY);
                }
            }
            else if (_wasHoldingLastFrame && IsDragging)
            {
                // Button just released — commit the rectangle
                SoundEngine.PlaySound(SoundID.Item4, Player.position);
                IsDragging = false;
                ErasedTileSystem.ApplyRectangle(DragStart, DragEnd, DragRemoving);
            }

            _wasHoldingLastFrame = mouseHeld;
        }

        public void CancelDrag()
        {
            IsDragging           = false;
            _wasHoldingLastFrame = false;
        }
    }

    // ---------------------------------------------------------------------
    //  The Reality Eraser item
    // ---------------------------------------------------------------------
    public class RealityEraser : ModItem
    {
        public override void SetDefaults()
        {
            Item.width        = 32;
            Item.height       = 32;
            Item.useStyle     = ItemUseStyleID.Swing;
            Item.useTime      = 20;
            Item.useAnimation = 20;
            Item.rare         = ItemRarityID.LightRed;
            Item.value        = Item.buyPrice(gold: 1);
            Item.maxStack     = 1;
            Item.consumable   = false;
            Item.noUseGraphic = false;
            Item.UseSound     = SoundID.Item1;
        }

        public override bool CanUseItem(Player player)
        {
            // Allow vanilla to run the swing animation, but do nothing on use —
            // all eraser/drag logic is handled in RealityEraserPlayer.PostUpdate.
            return true;
        }

        // Right-click in inventory: immediately clear all erased tile positions now
        public override bool CanRightClick() => true;

        // Prevent the item from being consumed on right-click.
        public override bool ConsumeItem(Player player) => false;

        public override void RightClick(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                Main.NewText("Eraser actions are single-player only.", 255, 80, 80);
                return;
            }

            if (ErasedTileSystem.ErasedTiles.Count == 0)
            {
                Main.NewText("No tiles marked for erasure.", 255, 200, 80);
                return;
            }

            ErasedTileSystem.ClearAllErasedTiles();
        }

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            int count = ErasedTileSystem.ErasedTiles.Count;
            int cap   = ErasedTileSystem.GetErasureCap();

            // ── Erasure count / cap ───────────────────────────────────────
            float fill = cap > 0 ? (float)count / cap : 1f;
            Color countColor = fill < 0.75f ? Color.LightGreen
                             : fill < 0.95f ? Color.Orange
                             :                Color.Red;

            string countLine = count == 0
                ? $"No tiles marked for erasure  (0 / {cap})"
                : $"{count} / {cap} tile{(count == 1 ? "" : "s")} marked for erasure";

            tooltips.Add(new TooltipLine(Mod, "EraserCount", countLine)
                { OverrideColor = countColor });

            // ── Cap warning ───────────────────────────────────────────────
            if (count >= cap)
                tooltips.Add(new TooltipLine(Mod, "EraserCapWarning",
                    "⚠ Cap reached! Defeat more bosses to unlock more slots.")
                    { OverrideColor = Color.Orange });

            // ── Right-click hint ─────────────────────────────────────────
            if (count > 0)
                tooltips.Add(new TooltipLine(Mod, "EraserRightClick",
                    "Right-click to erase all marked tiles now.")
                    { OverrideColor = Color.LightBlue });
        }
    }
}
