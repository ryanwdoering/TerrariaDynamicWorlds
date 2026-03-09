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
using Terraria.ObjectData;

namespace DynamicWorlds
{
    // ---------------------------------------------------------------------
    //  Saved tile data for a single Anchored Tile position
    // ---------------------------------------------------------------------
    public struct AnchoredTileData
    {
        public Point16 Position;

        public bool Active;
        public ushort TileType;
        public short FrameX;
        public short FrameY;

        public ushort WallType;

        public byte Liquid;
        public byte LiquidType;

        public bool HalfBlock;
        public SlopeType Slope;

        public bool WireRed;
        public bool WireBlue;
        public bool WireGreen;
        public bool WireYellow;

        public bool HasActuator;
        public bool IsActuated;

        public TagCompound ToTag()
        {
            return new TagCompound
            {
                ["x"] = Position.X,
                ["y"] = Position.Y,

                ["a"] = Active,
                ["t"] = TileType,
                ["fx"] = FrameX,
                ["fy"] = FrameY,

                ["w"] = WallType,

                ["l"]  = Liquid,
                ["lt"] = LiquidType,

                ["hb"] = HalfBlock,
                ["s"]  = (byte)Slope,

                ["wr"] = WireRed,
                ["wb"] = WireBlue,
                ["wg"] = WireGreen,
                ["wy"] = WireYellow,

                ["ha"] = HasActuator,
                ["ia"] = IsActuated
            };
        }

        public static AnchoredTileData FromTag(TagCompound tag)
        {
            var pos = new Point16(tag.GetShort("x"), tag.GetShort("y"));
            return new AnchoredTileData
            {
                Position   = pos,
                Active     = tag.GetBool("a"),
                TileType   = tag.Get<ushort>("t"),
                FrameX     = tag.GetShort("fx"),
                FrameY     = tag.GetShort("fy"),
                WallType   = tag.Get<ushort>("w"),
                Liquid     = tag.GetByte("l"),
                LiquidType = tag.GetByte("lt"),
                HalfBlock  = tag.GetBool("hb"),
                Slope      = (SlopeType)tag.GetByte("s"),
                WireRed    = tag.GetBool("wr"),
                WireBlue   = tag.GetBool("wb"),
                WireGreen  = tag.GetBool("wg"),
                WireYellow = tag.GetBool("wy"),
                HasActuator = tag.GetBool("ha"),
                IsActuated  = tag.GetBool("ia")
            };
        }

        public static AnchoredTileData CaptureFromWorld(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);

            return new AnchoredTileData
            {
                Position = new Point16(x, y),

                Active   = tile.HasTile,
                TileType = tile.TileType,
                FrameX   = tile.TileFrameX,
                FrameY   = tile.TileFrameY,

                WallType = tile.WallType,

                Liquid     = tile.LiquidAmount,
                LiquidType = (byte)tile.LiquidType,

                HalfBlock  = tile.IsHalfBlock,
                Slope      = tile.Slope,

                WireRed    = tile.RedWire,
                WireBlue   = tile.BlueWire,
                WireGreen  = tile.GreenWire,
                WireYellow = tile.YellowWire,

                HasActuator = tile.HasActuator,
                IsActuated  = tile.IsActuated
            };
        }

        public void RestoreToWorld()
        {
            int x = Position.X;
            int y = Position.Y;
            if (!WorldGen.InWorld(x, y, 1))
                return;

            Tile tile = Framing.GetTileSafely(x, y);

            tile.ClearEverything();

            tile.HasTile    = Active;
            tile.TileType   = TileType;
            tile.TileFrameX = FrameX;
            tile.TileFrameY = FrameY;

            tile.WallType   = WallType;

            tile.LiquidAmount = Liquid;
            tile.LiquidType   = LiquidType;

            tile.IsHalfBlock = HalfBlock;
            tile.Slope       = Slope;

            tile.RedWire    = WireRed;
            tile.BlueWire   = WireBlue;
            tile.GreenWire  = WireGreen;
            tile.YellowWire = WireYellow;

            tile.HasActuator = HasActuator;
            tile.IsActuated  = IsActuated;
        }
    }

    // ---------------------------------------------------------------------
    //  Saved chest/dresser contents for a container at a given position
    // ---------------------------------------------------------------------
    public class SavedChestContents
    {
        // Top-left tile position of the container
        public Point16 Position;
        // Up to 40 item slots (null entries are empty)
        public Item[] Items;

        public TagCompound ToTag()
        {
            var itemTags = new List<TagCompound>();
            foreach (var item in Items)
                itemTags.Add(ItemIO.Save(item ?? new Item()));

            return new TagCompound
            {
                ["x"]     = Position.X,
                ["y"]     = Position.Y,
                ["items"] = itemTags
            };
        }

        public static SavedChestContents FromTag(TagCompound tag)
        {
            var pos   = new Point16(tag.GetShort("x"), tag.GetShort("y"));
            var list  = tag.GetList<TagCompound>("items");
            var items = new Item[Chest.maxItems];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new Item();
                if (i < list.Count)
                    ItemIO.Load(items[i], list[i]);
            }
            return new SavedChestContents { Position = pos, Items = items };
        }

        // Capture the current contents of the chest at the given top-left tile
        public static SavedChestContents CaptureFromWorld(Point16 topLeft)
        {
            int idx = Chest.FindChest(topLeft.X, topLeft.Y);
            var items = new Item[Chest.maxItems];
            for (int i = 0; i < Chest.maxItems; i++)
            {
                items[i] = new Item();
                if (idx >= 0 && Main.chest[idx] != null && Main.chest[idx].item[i] != null)
                    items[i] = Main.chest[idx].item[i].Clone();
            }
            return new SavedChestContents { Position = topLeft, Items = items };
        }

        // Restore contents into the live chest at the saved position
        public void RestoreToWorld()
        {
            int idx = Chest.FindChest(Position.X, Position.Y);
            if (idx < 0 || Main.chest[idx] == null)
                return;

            for (int i = 0; i < Chest.maxItems; i++)
            {
                Main.chest[idx].item[i] = (Items[i] != null)
                    ? Items[i].Clone()
                    : new Item();
            }
        }
    }

    // ---------------------------------------------------------------------
    //  World-level system: stores all Anchored Tiles and restores them
    //  after world regeneration.
    // ---------------------------------------------------------------------
    public class AnchoredTileSystem : ModSystem
    {
        // All tiles that are currently anchored (marked for preservation)
        public static readonly Dictionary<Point16, AnchoredTileData> AnchoredTiles
            = new Dictionary<Point16, AnchoredTileData>();

        // Saved contents of any containers (chests/dressers) that sit on anchored tiles
        // Keyed by the TOP-LEFT tile of the container
        public static readonly Dictionary<Point16, SavedChestContents> AnchoredChests
            = new Dictionary<Point16, SavedChestContents>();

        // Overlay icon (AnchoredTile.png) drawn on each anchored tile
        public static Asset<Texture2D> ActuatorIcon;

        public override void OnWorldLoad()
        {
            AnchoredTiles.Clear();
            AnchoredChests.Clear();

            if (ActuatorIcon == null || !ActuatorIcon.IsLoaded)
                ActuatorIcon = ModContent.Request<Texture2D>("DynamicWorlds/AnchoredTile");
        }

        public override void OnWorldUnload()
        {
            AnchoredTiles.Clear();
            AnchoredChests.Clear();
        }

        // -------------------------------------------------------------
        //  Tile cap — scales with boss progression
        // -------------------------------------------------------------
        // Base + increments per milestone. Total at Moon Lord: 100,000.
        //
        //  Base (no bosses)        5,000
        //  King Slime              + 500   →   5,500
        //  Eye of Cthulhu          + 500   →   6,000
        //  Evil boss (EoW/BoC)     + 500   →   6,500
        //  Skeletron               + 500   →   7,000
        //  Queen Bee               + 500   →   7,500
        //  Deerclops               + 500   →   8,000
        //  Wall of Flesh (HM)    +2,000   →  10,000
        //  Twins                 +3,000   →  13,000
        //  Destroyer             +3,000   →  16,000
        //  Skeletron Prime       +3,000   →  19,000
        //  Plantera              +6,000   →  25,000
        //  Golem                +10,000   →  35,000
        //  Duke Fishron         +10,000   →  45,000
        //  Empress of Light     +10,000   →  55,000
        //  Lunatic Cultist      +10,000   →  65,000
        //  Moon Lord            +35,000   → 100,000
        public static int GetTileCap()
        {
            int cap = 5_000;

            if (NPC.downedSlimeKing)      cap +=    500;
            if (NPC.downedBoss1)          cap +=    500;
            if (NPC.downedBoss2)          cap +=    500;
            if (NPC.downedBoss3)          cap +=    500;
            if (NPC.downedQueenBee)       cap +=    500;
            if (NPC.downedDeerclops)      cap +=    500;

            if (Main.hardMode)            cap +=  2_000;

            if (NPC.downedMechBoss1)      cap +=  3_000;
            if (NPC.downedMechBoss2)      cap +=  3_000;
            if (NPC.downedMechBoss3)      cap +=  3_000;
            if (NPC.downedPlantBoss)      cap +=  6_000;
            if (NPC.downedGolemBoss)      cap += 10_000;
            if (NPC.downedFishron)        cap += 10_000;
            if (NPC.downedEmpressOfLight) cap += 10_000;
            if (NPC.downedAncientCultist) cap += 10_000;
            if (NPC.downedMoonlord)       cap += 35_000;

            return cap;
        }

        // Anchor a tile (add only — used during drag)
        public static void AnchorTile(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            var pos = new Point16(x, y);
            if (!AnchoredTiles.ContainsKey(pos))
            {
                AnchoredTiles[pos] = AnchoredTileData.CaptureFromWorld(x, y);
                TryCaptureContainer(x, y);
            }
        }

        // Unanchor a tile (remove only — used during drag)
        public static void UnanchorTile(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            AnchoredTiles.Remove(new Point16(x, y));
            TryReleaseContainer(x, y);
        }

        // Toggle anchor on a single tile (used for single clicks)
        public static void ToggleAnchor(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            var pos = new Point16(x, y);
            if (AnchoredTiles.ContainsKey(pos))
            {
                AnchoredTiles.Remove(pos);
                TryReleaseContainer(x, y);
            }
            else
            {
                int cap = GetTileCap();
                if (AnchoredTiles.Count >= cap)
                {
                    Main.NewText($"Anchor cap reached ({cap}). Defeat more bosses to expand your limit.", 255, 200, 80);
                    return;
                }
                AnchoredTiles[pos] = AnchoredTileData.CaptureFromWorld(x, y);
                TryCaptureContainer(x, y);
            }
        }

        // Apply a rectangle of anchors. If the start tile was already anchored,
        // the whole rectangle is unanchored; otherwise it is anchored.
        public static void ApplyRectangle(Point16 start, Point16 end, bool removing)
        {
            int x0 = System.Math.Min(start.X, end.X);
            int x1 = System.Math.Max(start.X, end.X);
            int y0 = System.Math.Min(start.Y, end.Y);
            int y1 = System.Math.Max(start.Y, end.Y);

            int cap     = GetTileCap();
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
                        AnchoredTiles.Remove(pos);
                        TryReleaseContainer(x, y);
                    }
                    else if (!AnchoredTiles.ContainsKey(pos))
                    {
                        if (AnchoredTiles.Count >= cap)
                        {
                            skipped++;
                            continue;
                        }
                        AnchoredTiles[pos] = AnchoredTileData.CaptureFromWorld(x, y);
                        TryCaptureContainer(x, y);
                        added++;
                    }
                }
            }

            int w = x1 - x0 + 1;
            int h = y1 - y0 + 1;
            if (removing)
            {
                Main.NewText($"Unanchored {w}×{h} region.", 255, 100, 100);
            }
            else
            {
                Main.NewText($"Anchored {added} tile{(added == 1 ? "" : "s")} in {w}×{h} region. ({AnchoredTiles.Count}/{cap} used)", 100, 255, 100);
                if (skipped > 0)
                    Main.NewText($"{skipped} tile{(skipped == 1 ? "" : "s")} skipped — anchor cap reached. Defeat more bosses to expand your limit.", 255, 200, 80);
            }
        }

        // ---- Container helpers ----

        // If the tile at (x,y) belongs to a container, capture its contents.
        // Only stores one entry per container (keyed by top-left corner).
        private static void TryCaptureContainer(int x, int y)
        {
            Point16? topLeft = GetContainerTopLeft(x, y);
            if (topLeft == null) return;

            // Only capture once per container
            if (!AnchoredChests.ContainsKey(topLeft.Value))
                AnchoredChests[topLeft.Value] = SavedChestContents.CaptureFromWorld(topLeft.Value);
        }

        // If removing a tile means no anchored tiles remain on this container, drop the snapshot.
        private static void TryReleaseContainer(int x, int y)
        {
            Point16? topLeft = GetContainerTopLeft(x, y);
            if (topLeft == null) return;

            if (!AnyAnchoredTileOnContainer(topLeft.Value))
                AnchoredChests.Remove(topLeft.Value);
        }

        // Returns the top-left tile position of the container that tile (x,y) belongs to,
        // or null if the tile is not part of a container.
        // Matches Terraria's own per-type frame math used in TileInteractionsUse.
        private static Point16? GetContainerTopLeft(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            if (!tile.HasTile) return null;

            if (TileID.Sets.BasicChest[tile.TileType])
            {
                // Chests are 2 wide × 2 tall.
                // frameX can be 0, 18, 36, 54, ... (18 px per style column, 2 columns per style).
                // Terraria reduces: col = frameX/18 mod 2.
                int col = (tile.TileFrameX / 18) % 2;
                int row = tile.TileFrameY / 18;   // 0 or 1
                return new Point16(x - col, y - row);
            }

            if (TileID.Sets.BasicDresser[tile.TileType])
            {
                // Dressers are 3 wide × 2 tall.
                // frameX advances 18 px per tile, cycling 0/18/36 within each style.
                int col = (tile.TileFrameX / 18) % 3;
                int row = tile.TileFrameY / 18;   // 0 or 1
                return new Point16(x - col, y - row);
            }

            return null;
        }

        // Returns true if at least one entry in AnchoredTiles belongs to the container
        // whose top-left is at topLeft.
        private static bool AnyAnchoredTileOnContainer(Point16 topLeft)
        {
            // Containers are at most 3 wide × 2 tall (dressers), so check a small neighborhood.
            for (int dx = 0; dx < 3; dx++)
            {
                for (int dy = 0; dy < 2; dy++)
                {
                    var check = new Point16(topLeft.X + dx, topLeft.Y + dy);
                    if (AnchoredTiles.ContainsKey(check))
                    {
                        Point16? tl2 = GetContainerTopLeft(check.X, check.Y);
                        if (tl2.HasValue && tl2.Value == topLeft)
                            return true;
                    }
                }
            }
            return false;
        }

        // Call this immediately before worldgen to snapshot the latest chest contents.
        // This ensures any items added to a container after it was anchored are preserved.
        public static void RefreshAllChestSnapshots()
        {
            AnchoredChests.Clear();
            foreach (var kv in AnchoredTiles)
            {
                Point16? topLeft = GetContainerTopLeft(kv.Key.X, kv.Key.Y);
                if (topLeft == null) continue;

                if (!AnchoredChests.ContainsKey(topLeft.Value))
                    AnchoredChests[topLeft.Value] = SavedChestContents.CaptureFromWorld(topLeft.Value);
            }
        }

        // Call this at the end of world regeneration to restore all anchored tiles.
        public static void RestoreAllAnchoredTiles()
        {
            // 1. Restore all tile data (geometry, walls, wires, etc.)
            foreach (var kv in AnchoredTiles)
                kv.Value.RestoreToWorld();

            WorldGen.RangeFrame(0, 0, Main.maxTilesX, Main.maxTilesY);

            // 2. Re-register every restored container in Main.chest[].
            //    Worldgen wipes Main.chest[], so we must call Chest.CreateChest for
            //    each container top-left to make them interactable again.
            var processedTopLefts = new System.Collections.Generic.HashSet<Point16>();
            foreach (var kv in AnchoredTiles)
            {
                Point16 pos = kv.Key;
                Tile tile = Framing.GetTileSafely(pos.X, pos.Y);
                if (!tile.HasTile) continue;
                if (!TileID.Sets.BasicChest[tile.TileType] && !TileID.Sets.BasicDresser[tile.TileType])
                    continue;

                Point16? topLeftNullable = GetContainerTopLeft(pos.X, pos.Y);
                if (topLeftNullable == null) continue;
                Point16 tl = topLeftNullable.Value;

                if (!processedTopLefts.Add(tl)) continue;  // already handled

                int idx = Chest.FindChest(tl.X, tl.Y);
                if (idx < 0)
                    idx = Chest.CreateChest(tl.X, tl.Y);

                if (idx >= 0 && AnchoredChests.TryGetValue(tl, out var savedContents))
                    savedContents.RestoreToWorld();
            }

            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText("All anchored tiles restored.", 150, 255, 150);
        }

        // --- Save / Load -------------------------------------------------
        public override void SaveWorldData(TagCompound tag)
        {
            var list = new List<TagCompound>();
            foreach (var kv in AnchoredTiles)
                list.Add(kv.Value.ToTag());
            tag["SaveActuators"] = list;       // keep legacy key for save compatibility

            var chestList = new List<TagCompound>();
            foreach (var kv in AnchoredChests)
                chestList.Add(kv.Value.ToTag());
            tag["SaveActuatorChests"] = chestList;  // keep legacy key for save compatibility
        }

        public override void LoadWorldData(TagCompound tag)
        {
            AnchoredTiles.Clear();
            AnchoredChests.Clear();

            if (tag.ContainsKey("SaveActuators"))
            {
                var list = tag.GetList<TagCompound>("SaveActuators");
                foreach (var t in list)
                {
                    var saved = AnchoredTileData.FromTag(t);
                    AnchoredTiles[saved.Position] = saved;
                }
            }

            if (tag.ContainsKey("SaveActuatorChests"))
            {
                var list = tag.GetList<TagCompound>("SaveActuatorChests");
                foreach (var t in list)
                {
                    var saved = SavedChestContents.FromTag(t);
                    AnchoredChests[saved.Position] = saved;
                }
            }
        }

        // --- Visual overlay ---
        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player == null || player.HeldItem == null)
                return;

            if (player.HeldItem.type != ModContent.ItemType<RealityAnchor>())
                return;

            if (ActuatorIcon == null || !ActuatorIcon.IsLoaded)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D tex = ActuatorIcon.Value;
            Vector2 screenPos = Main.screenPosition;

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null, null, null,
                Main.GameViewMatrix.TransformationMatrix);

            // Draw all anchored tiles
            foreach (var kv in AnchoredTiles)
            {
                Point16 p = kv.Key;

                if (!IsOnScreen(p))
                    continue;

                DrawAnchorIcon(spriteBatch, tex, p, screenPos, Color.White * 0.8f);
            }

            // Draw the live drag-preview rectangle
            var mp = player.GetModPlayer<RealityAnchorPlayer>();
            if (mp.IsDragging)
            {
                Point16 dragStart = mp.DragStart;
                Point16 dragEnd   = mp.DragEnd;

                int x0 = System.Math.Min(dragStart.X, dragEnd.X);
                int x1 = System.Math.Max(dragStart.X, dragEnd.X);
                int y0 = System.Math.Min(dragStart.Y, dragEnd.Y);
                int y1 = System.Math.Max(dragStart.Y, dragEnd.Y);

                Color previewColor = mp.DragRemoving
                    ? Color.Red * 0.5f
                    : Color.Cyan * 0.5f;

                // Filled translucent rectangle preview
                Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                Rectangle screenRect = new Rectangle(
                    (int)(x0 * 16 - screenPos.X),
                    (int)(y0 * 16 - screenPos.Y),
                    (x1 - x0 + 1) * 16,
                    (y1 - y0 + 1) * 16);
                spriteBatch.Draw(pixel, screenRect, previewColor);

                // Outline
                Color outlineColor = mp.DragRemoving ? Color.Red : Color.Cyan;
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

                        if (!AnchoredTiles.ContainsKey(pp))
                            DrawAnchorIcon(spriteBatch, tex, pp, screenPos, previewColor * 1.6f);
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

        private static void DrawAnchorIcon(SpriteBatch sb, Texture2D tex, Point16 p, Vector2 screenPos, Color color)
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
    //  ModPlayer: tracks drag state per-player
    // ---------------------------------------------------------------------
    public class RealityAnchorPlayer : ModPlayer
    {
        public bool    IsDragging   = false;
        public bool    DragRemoving = false;
        public Point16 DragStart;
        public Point16 DragEnd;

        private bool _wasHoldingLastFrame = false;

        public override void PostUpdate()
        {
            // Only act when holding the Reality Anchor in singleplayer
            if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool holdingTool = Player.HeldItem != null
                && Player.HeldItem.type == ModContent.ItemType<RealityAnchor>();

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
                    DragRemoving = AnchoredTileSystem.AnchoredTiles.ContainsKey(DragStart);
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
                AnchoredTileSystem.ApplyRectangle(DragStart, DragEnd, DragRemoving);
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
    //  The Reality Anchor item
    // ---------------------------------------------------------------------
    public class RealityAnchor : ModItem
    {
        public override void SetDefaults()
        {
            Item.width        = 32;
            Item.height       = 32;
            Item.useStyle     = ItemUseStyleID.HoldUp;
            Item.useTime      = 1;
            Item.useAnimation = 1;
            Item.rare         = ItemRarityID.LightRed;
            Item.value        = Item.buyPrice(gold: 1);
            Item.maxStack     = 1;
            Item.consumable   = false;
            Item.noUseGraphic = false;
            Item.UseSound     = null;
        }

        public override bool CanUseItem(Player player)
        {
            // Returning false keeps the item "held" without triggering vanilla use logic.
            // All drag logic is handled in RealityAnchorPlayer.PostUpdate.
            return false;
        }

        // Right-click in inventory: manually restore all anchored tiles
        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                Main.NewText("Anchor actions are single-player only.", 255, 80, 80);
                return;
            }

            if (AnchoredTileSystem.AnchoredTiles.Count == 0)
            {
                Main.NewText("No anchored tiles to restore.", 255, 200, 80);
                return;
            }

            AnchoredTileSystem.RestoreAllAnchoredTiles();
        }

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            int count = AnchoredTileSystem.AnchoredTiles.Count;
            int cap   = AnchoredTileSystem.GetTileCap();
            string status = count == 0
                ? $"No tiles anchored. (0/{cap})"
                : $"{count}/{cap} tile{(count == 1 ? "" : "s")} anchored.";

            tooltips.Add(new TooltipLine(Mod, "DynamicWorldsStatus", status));

            if (count >= cap)
                tooltips.Add(new TooltipLine(Mod, "DynamicWorldsCap", "Anchor cap reached! Defeat more bosses to unlock more slots.") { OverrideColor = Color.Orange });
        }
    }
}
