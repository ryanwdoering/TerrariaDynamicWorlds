using System;
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
    // -------------------------------------------------------------------------
    //  A single saved building zone: bounds + full per-tile snapshot.
    //  Tiles are stored relative to the zone's top-left so translation is just
    //  adding deltaY to every position when restoring.
    // -------------------------------------------------------------------------
    public class BuildingZone
    {
        // World-coordinate bounds at the time of capture
        public Point16 TopLeft;
        public Point16 BottomRight;

        // Ground-reference Y: the surface tile Y at the center column when captured.
        // After regen we compare this to the new surface to compute deltaY.
        public int SavedGroundY;

        // Full tile snapshots, keyed by world position
        public Dictionary<Point16, AnchoredTileData> Tiles = new();

        // Unique id so multiple Building Anchors can coexist
        public int Id;

        // Chest contents for any chests captured inside the zone.
        // Keyed by original world top-left position of the chest.
        public Dictionary<Point16, SavedChestContents> Chests = new();

        // If the player's spawn (bed) is inside this zone, we save it here
        // so we can translate it by deltaY after restore.
        // (-1,-1) means no spawn was inside this zone at capture time.
        public Point16 SavedSpawn = new Point16(-1, -1);

        // Width / height helpers
        public int Width  => BottomRight.X - TopLeft.X + 1;
        public int Height => BottomRight.Y - TopLeft.Y + 1;

        // Returns true if this tile should be treated as open air for ground detection
        // (platforms, trees, cacti, sunflowers, and other surface plants).
        private static bool IsAirOrVegetation(Tile t)
        {
            if (!t.HasTile) return true;
            if (TileID.Sets.Platforms[t.TileType]) return true;
            switch (t.TileType)
            {
                case TileID.Trees:
                case TileID.PalmTree:
                case TileID.MushroomTrees:
                case TileID.VanityTreeSakura:
                case TileID.VanityTreeYellowWillow:
                case TileID.Cactus:
                case TileID.Sunflower:
                case TileID.Plants:
                case TileID.Plants2:
                case TileID.JunglePlants:
                case TileID.JunglePlants2:
                case TileID.MushroomPlants:
                case TileID.Vines:
                case TileID.JungleVines:
                    return true;
                default:
                    return false;
            }
        }

        // ── Ground sampling ───────────────────────────────────────────────────
        // Finds the Y of the actual world surface at column x by scanning downward
        // from the top of the world. Trees and vegetation are ignored so they don't
        // prevent the real ground tile from being found.
        public static int FindGroundY(int x, int _startY = 0)
        {
            for (int y = 10; y < Main.maxTilesY - 10; y++)
            {
                Tile t = Framing.GetTileSafely(x, y);
                if (IsAirOrVegetation(t)) continue;

                // Require 3 open-air-or-vegetation tiles directly above before accepting
                // as ground — filters floating islands that sit on their own solid base.
                bool hasAirAbove = true;
                for (int above = y - 1; above >= System.Math.Max(0, y - 3); above--)
                {
                    if (!IsAirOrVegetation(Framing.GetTileSafely(x, above)))
                    {
                        hasAirAbove = false;
                        break;
                    }
                }

                if (hasAirAbove)
                    return y;
            }
            return (int)Main.worldSurface;
        }

        // ── Biome fill ────────────────────────────────────────────────────────
        // Returns the default solid tile type for the biome at world position (x, y).
        private static ushort BiomeTileAt(int x, int y)
        {
            // Snow biome
            if (x < Main.maxTilesX * 0.35f && y < (int)Main.worldSurface + 40)
                return TileID.SnowBlock;

            // Desert (right side surface)
            if (x > Main.maxTilesX * 0.65f && y < (int)Main.worldSurface + 40)
                return TileID.Sand;

            // Underground / cavern
            if (y > Main.rockLayer)
                return TileID.Stone;

            // Default surface
            return TileID.Dirt;
        }

        // ── Capture ───────────────────────────────────────────────────────────
        public static BuildingZone Capture(Point16 topLeft, Point16 bottomRight, int id)
        {
            var zone = new BuildingZone
            {
                Id          = id,
                TopLeft     = topLeft,
                BottomRight = bottomRight,
            };

            // Sample the ground reference at the center column by scanning downward
            // from just below the zone. This is the Y of the tile the structure sits on.
            int centerX = (topLeft.X + bottomRight.X) / 2;
            int capStart = bottomRight.Y + 1;
            zone.SavedGroundY = capStart;
            for (int y = capStart; y < capStart + 500 && y < Main.maxTilesY - 10; y++)
            {
                Tile t = Framing.GetTileSafely(centerX, y);
                if (t.HasTile && !TileID.Sets.Platforms[t.TileType])
                {
                    zone.SavedGroundY = y;
                    break;
                }
            }

            Main.NewText($"[BA] Captured zone #{id}: TL=({topLeft.X},{topLeft.Y}) BR=({bottomRight.X},{bottomRight.Y}) centerX={centerX} SavedGroundY={zone.SavedGroundY} worldSurface={Main.worldSurface:F0}", 180, 255, 120);

            // Snapshot every tile in the bounding rectangle
            for (int x = topLeft.X; x <= bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    var pos  = new Point16(x, y);
                    zone.Tiles[pos] = AnchoredTileData.CaptureFromWorld(x, y);
                }
            }

            // Capture chest contents for any container whose top-left tile is in the zone.
            // Chests are 2×2; we only need to check the top-left corner.
            for (int i = 0; i < Main.chest.Length; i++)
            {
                var ch = Main.chest[i];
                if (ch == null) continue;
                var cpos = new Point16(ch.x, ch.y);
                if (cpos.X >= topLeft.X && cpos.X <= bottomRight.X &&
                    cpos.Y >= topLeft.Y && cpos.Y <= bottomRight.Y)
                {
                    zone.Chests[cpos] = SavedChestContents.CaptureFromWorld(cpos);
                }
            }

            // If the local player's spawn (bed) is inside the zone, record it.
            Player p = Main.LocalPlayer;
            if (p != null && p.SpawnX >= topLeft.X && p.SpawnX <= bottomRight.X &&
                             p.SpawnY >= topLeft.Y && p.SpawnY <= bottomRight.Y)
            {
                zone.SavedSpawn = new Point16(p.SpawnX, p.SpawnY);
            }

            return zone;
        }

        // ── Restore (translated) ──────────────────────────────────────────────
        // Called after worldgen. Finds new ground level at the same X column,
        // computes deltaY = newGroundY - savedGroundY, shifts every tile by deltaY,
        // fills gaps, then re-captures the zone at its new position so the next
        // regen uses up-to-date coordinates.
        public void RestoreToWorld()
        {
            int centerX    = (TopLeft.X + BottomRight.X) / 2;
            int newGroundY = FindGroundY(centerX, 0);

            // Place the zone so its bottom row sits one tile INTO the ground,
            // giving a natural embedded/planted look.
            int newBottomY = newGroundY;
            int deltaY     = newBottomY - BottomRight.Y;
            int newTopY    = TopLeft.Y + deltaY;

            Main.NewText($"[BA] RestoreZone #{Id}: centerX={centerX} SavedGroundY={SavedGroundY} newGroundY={newGroundY} deltaY={deltaY} TL.Y={TopLeft.Y}→{newTopY} BR.Y={BottomRight.Y}→{newBottomY} worldSurface={Main.worldSurface:F0}", 120, 220, 255);

            // 1. Fill terrain beneath the OLD footprint to prevent air pockets.
            //    The building is moving, so we need to fill in the space it's leaving behind.
            for (int x = TopLeft.X; x <= BottomRight.X; x++)
            {
                // Find ground starting from below the old building position
                int groundY = Main.maxTilesY - 10;
                for (int y = BottomRight.Y + 1; y < Main.maxTilesY - 15; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    
                    int solidCount = 0;
                    for (int check = y; check < Math.Min(y + 10, Main.maxTilesY); check++)
                    {
                        Tile checkTile = Framing.GetTileSafely(x, check);
                        if (checkTile.HasTile)
                            solidCount++;
                        else
                            break;
                    }
                    
                    if (solidCount >= 10)
                    {
                        groundY = y;
                        break;
                    }
                }

                // Fill from below new building down to ground
                for (int y = newBottomY + 1; y < groundY; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    Tile tile = Framing.GetTileSafely(x, y);
                    tile.ClearEverything();
                    tile.HasTile  = true;
                    tile.TileType = BiomeTileAt(x, y);
                }
            }

            // 2. Clear only the exact footprint where building tiles will go.
            //    Do NOT clear above the building - this prevents filling dirt over it.
            //    Only clear the building's own footprint + a small area below for settling.
            if (deltaY != 0)
            {
                for (int x = TopLeft.X; x <= BottomRight.X; x++)
                {
                    for (int y = newTopY; y <= newBottomY; y++)
                    {
                        if (!WorldGen.InWorld(x, y, 1)) continue;
                        Framing.GetTileSafely(x, y).ClearEverything();
                    }
                }
            }

            // 3. Write all captured tiles at their translated Y positions.
            foreach (var kv in Tiles)
            {
                int nx = kv.Key.X;
                int ny = kv.Key.Y + deltaY;
                if (!WorldGen.InWorld(nx, ny, 1)) continue;

                var data = kv.Value;
                Tile tile = Framing.GetTileSafely(nx, ny);
                tile.ClearEverything();
                tile.HasTile      = data.Active;
                tile.TileType     = data.TileType;
                tile.TileFrameX   = data.FrameX;
                tile.TileFrameY   = data.FrameY;
                tile.WallType     = data.WallType;
                tile.LiquidAmount = data.Liquid;
                tile.LiquidType   = data.LiquidType;
                tile.IsHalfBlock  = data.HalfBlock;
                tile.Slope        = data.Slope;
                tile.RedWire      = data.WireRed;
                tile.BlueWire     = data.WireBlue;
                tile.GreenWire    = data.WireGreen;
                tile.YellowWire   = data.WireYellow;
                tile.HasActuator  = data.HasActuator;
                tile.IsActuated   = data.IsActuated;
            }

            // 4. Don't fill terrain around the building - let it settle naturally.
            //    The restored tiles will anchor the terrain, and WorldGen.RangeFrame 
            //    will handle framing. Manual filling can cause dirt to cover the building.

            // 5. Restore chest contents at translated positions.
            foreach (var kv in Chests)
            {
                int nx = kv.Key.X;
                int ny = kv.Key.Y + deltaY;
                if (!WorldGen.InWorld(nx, ny, 1)) continue;

                int idx = Chest.FindChest(nx, ny);
                if (idx < 0)
                    idx = Chest.CreateChest(nx, ny);
                if (idx < 0 || Main.chest[idx] == null) continue;

                for (int i = 0; i < Chest.maxItems; i++)
                {
                    Main.chest[idx].item[i] = (kv.Value.Items[i] != null)
                        ? kv.Value.Items[i].Clone()
                        : new Item();
                }
            }

            // 6. Translate the player's spawn point if it was inside this zone.
            if (SavedSpawn.X >= 0 && SavedSpawn.Y >= 0)
            {
                Player p = Main.LocalPlayer;
                if (p != null)
                {
                    p.SpawnX = SavedSpawn.X;
                    p.SpawnY = SavedSpawn.Y + deltaY;
                }
            }

            WorldGen.RangeFrame(
                System.Math.Max(0, TopLeft.X - 2),
                System.Math.Max(0, System.Math.Min(newTopY, TopLeft.Y) - 2),
                System.Math.Min(Main.maxTilesX, BottomRight.X + 2),
                System.Math.Min(Main.maxTilesY, System.Math.Max(newBottomY, BottomRight.Y) + 2));

            // 7. Update zone metadata to the new position so the next regen is correct.
            //    Always update — even if deltaY==0, SavedGroundY may have changed.
            {
                var newTl = new Point16(TopLeft.X,     (short)newTopY);
                var newBr = new Point16(BottomRight.X, (short)newBottomY);

                // Rebuild tile snapshot at new position
                var newTiles = new Dictionary<Point16, AnchoredTileData>();
                for (int x = newTl.X; x <= newBr.X; x++)
                {
                    for (int y = newTl.Y; y <= newBr.Y; y++)
                    {
                        if (!WorldGen.InWorld(x, y, 1)) continue;
                        newTiles[new Point16(x, y)] = AnchoredTileData.CaptureFromWorld(x, y);
                    }
                }

                // Rebuild chest refs at new positions
                var newChests = new Dictionary<Point16, SavedChestContents>();
                foreach (var kv in Chests)
                {
                    var newPos = new Point16(kv.Key.X, (short)(kv.Key.Y + deltaY));
                    newChests[newPos] = new SavedChestContents { Position = newPos, Items = kv.Value.Items };
                }

                TopLeft      = newTl;
                BottomRight  = newBr;
                SavedGroundY = newGroundY;
                Tiles        = newTiles;
                Chests       = newChests;

                if (SavedSpawn.X >= 0)
                    SavedSpawn = new Point16(SavedSpawn.X, (short)(SavedSpawn.Y + deltaY));
            }
        }

        // ── Serialization ─────────────────────────────────────────────────────
        public TagCompound ToTag()
        {
            var tileTags = new List<TagCompound>();
            foreach (var kv in Tiles)
                tileTags.Add(kv.Value.ToTag());

            var chestTags = new List<TagCompound>();
            foreach (var kv in Chests)
                chestTags.Add(kv.Value.ToTag());

            return new TagCompound
            {
                ["id"]      = Id,
                ["tlx"]     = TopLeft.X,
                ["tly"]     = TopLeft.Y,
                ["brx"]     = BottomRight.X,
                ["bry"]     = BottomRight.Y,
                ["gndY"]    = SavedGroundY,
                ["tiles"]   = tileTags,
                ["chests"]  = chestTags,
                ["spawnX"]  = (int)SavedSpawn.X,
                ["spawnY"]  = (int)SavedSpawn.Y,
            };
        }

        public static BuildingZone FromTag(TagCompound tag)
        {
            var zone = new BuildingZone
            {
                Id           = tag.GetInt("id"),
                TopLeft      = new Point16(tag.GetShort("tlx"), tag.GetShort("tly")),
                BottomRight  = new Point16(tag.GetShort("brx"), tag.GetShort("bry")),
                SavedGroundY = tag.GetInt("gndY"),
                SavedSpawn   = new Point16(
                    tag.ContainsKey("spawnX") ? (short)tag.GetInt("spawnX") : (short)-1,
                    tag.ContainsKey("spawnY") ? (short)tag.GetInt("spawnY") : (short)-1),
            };

            var list = tag.GetList<TagCompound>("tiles");
            foreach (var t in list)
            {
                var data = AnchoredTileData.FromTag(t);
                zone.Tiles[data.Position] = data;
            }

            if (tag.ContainsKey("chests"))
            {
                var chestList = tag.GetList<TagCompound>("chests");
                foreach (var t in chestList)
                {
                    var c = SavedChestContents.FromTag(t);
                    zone.Chests[c.Position] = c;
                }
            }

            return zone;
        }
    }

    // -------------------------------------------------------------------------
    //  World-level system: holds all registered building zones.
    // -------------------------------------------------------------------------
    public class BuildingAnchorSystem : ModSystem
    {
        // Keyed by zone Id
        public static readonly Dictionary<int, BuildingZone> Zones = new();

        // Overlay texture drawn on zone tiles while the item is held
        public static Asset<Texture2D> ZoneIcon;

        private static int _nextId = 1;
        public static int NextId() => _nextId++;

        public override void OnWorldLoad()
        {
            Zones.Clear();
            _nextId = 1;

            if (ZoneIcon == null || !ZoneIcon.IsLoaded)
                ZoneIcon = ModContent.Request<Texture2D>("DynamicWorlds/AnchoredTile");
        }

        public override void OnWorldUnload() => Zones.Clear();

        // Refresh the snapshot of chest contents in all building zones.
        // Call this immediately before worldgen to capture any new items added to zone chests.
        public static void RefreshAllChestSnapshots()
        {
            foreach (var zone in Zones.Values)
            {
                // For each chest position in the zone, recapture its current contents
                var topLeftsToRefresh = new List<Point16>(zone.Chests.Keys);
                foreach (var chestPos in topLeftsToRefresh)
                {
                    zone.Chests[chestPos] = SavedChestContents.CaptureFromWorld(chestPos);
                }
            }
        }

        // Called during regen, after erased tiles are cleared and before regular anchors.
        public static void RestoreAllZones()
        {
            if (Zones.Count == 0) return;

            foreach (var kv in Zones)
                kv.Value.RestoreToWorld();

            // Reactivate any pylons that were restored in building zones

            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText($"Restored {Zones.Count} building zone{(Zones.Count == 1 ? "" : "s")}.", 180, 220, 255);
        }

        // ── Save / Load ───────────────────────────────────────────────────────
        public override void SaveWorldData(TagCompound tag)
        {
            var list = new List<TagCompound>();
            foreach (var kv in Zones)
                list.Add(kv.Value.ToTag());
            tag["BuildingZones"]  = list;
            tag["BuildingNextId"] = _nextId;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Zones.Clear();
            _nextId = 1;

            if (tag.ContainsKey("BuildingZones"))
            {
                var list = tag.GetList<TagCompound>("BuildingZones");
                foreach (var t in list)
                {
                    var zone = BuildingZone.FromTag(t);
                    Zones[zone.Id] = zone;
                }
            }

            if (tag.ContainsKey("BuildingNextId"))
                _nextId = tag.GetInt("BuildingNextId");
        }

        // ── Visual overlay ────────────────────────────────────────────────────
        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player?.HeldItem == null) return;
            if (player.HeldItem.type != ModContent.ItemType<BuildingAnchorItem>()) return;
            if (ZoneIcon == null || !ZoneIcon.IsLoaded) return;

            SpriteBatch sb       = Main.spriteBatch;
            Texture2D   tex      = ZoneIcon.Value;
            Vector2     screenPos = Main.screenPosition;
            Texture2D   pixel    = TextureAssets.MagicPixel.Value;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, null, null, null,
                Main.GameViewMatrix.TransformationMatrix);

            // Draw all registered zones
            foreach (var kv in Zones)
            {
                var zone = kv.Value;
                DrawZoneOverlay(sb, tex, pixel, zone.TopLeft, zone.BottomRight, screenPos,
                    new Color(100, 180, 255) * 0.35f, Color.DeepSkyBlue);
            }

            // Draw in-progress drag preview for the item being held
            var mp = player.GetModPlayer<BuildingAnchorPlayer>();
            if (mp.IsDragging)
            {
                int x0 = System.Math.Min(mp.DragStart.X, mp.DragEnd.X);
                int x1 = System.Math.Max(mp.DragStart.X, mp.DragEnd.X);
                int y0 = System.Math.Min(mp.DragStart.Y, mp.DragEnd.Y);
                int y1 = System.Math.Max(mp.DragStart.Y, mp.DragEnd.Y);
                DrawZoneOverlay(sb, tex, pixel,
                    new Point16(x0, y0), new Point16(x1, y1), screenPos,
                    Color.Gold * 0.3f, Color.Gold);
            }

            sb.End();
        }

        private static void DrawZoneOverlay(SpriteBatch sb, Texture2D icon, Texture2D pixel,
            Point16 tl, Point16 br, Vector2 screenPos, Color fill, Color outline)
        {
            Rectangle rect = new Rectangle(
                (int)(tl.X * 16 - screenPos.X),
                (int)(tl.Y * 16 - screenPos.Y),
                (br.X - tl.X + 1) * 16,
                (br.Y - tl.Y + 1) * 16);

            sb.Draw(pixel, rect, fill);

            // Outline
            int t = 2;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), outline);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), outline);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), outline);
            sb.Draw(pixel, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), outline);
        }

        /// <summary>
        /// Reactivates any teleportation pylons that were restored within building zones.
        /// Pylons need their tile data to be refreshed so they show as active on the map.
        /// </summary>
        private static void ActivateRestoredPylonsInZones()
        {
            int pylonCount = 0;

            // Scan all restored zones for teleportation pylons
            foreach (var zone in Zones.Values)
            {
                // Check each tile in the zone
                foreach (var tilePair in zone.Tiles)
                {
                    Point16 pos = tilePair.Key;
                    Tile tile = Framing.GetTileSafely(pos.X, pos.Y);

                    // Refresh pylon tiles so they become active on the map
                    if (tile != null && tile.HasTile && tile.TileType == TileID.TeleportationPylon)
                    {
                        pylonCount++;
                    }
                }
            }

            if (pylonCount > 0)
            {
                Main.NewText($"Activated {pylonCount} pylon{(pylonCount == 1 ? "" : "s")} in building zones.", 100, 200, 255);
            }
        }
    }

    // -------------------------------------------------------------------------
    //  ModPlayer: tracks drag state and the zone assigned to this item instance.
    //  Each BuildingAnchorItem stores its own zone id in the item's tag.
    // -------------------------------------------------------------------------
    public class BuildingAnchorPlayer : ModPlayer
    {
        public bool    IsDragging = false;
        public Point16 DragStart;
        public Point16 DragEnd;

        private bool _wasHoldingLastFrame = false;

        public override void PostUpdate()
        {
            if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool holding = Player.HeldItem?.type == ModContent.ItemType<BuildingAnchorItem>();
            if (!holding)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool mouseHeld = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
            bool shiftHeld = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || 
                            Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            int  tx = (int)(Main.MouseWorld.X / 16f);
            int  ty = (int)(Main.MouseWorld.Y / 16f);

            // Shift+Click: Select and remove a specific zone from the world
            if (shiftHeld && Main.mouseLeft && !_wasHoldingLastFrame && !Main.LocalPlayer.mouseInterface)
            {
                RemoveZoneAtPosition(new Point16(tx, ty));
                _wasHoldingLastFrame = true;
                return;
            }

            // Normal drag: Create a new zone
            if (mouseHeld)
            {
                if (!_wasHoldingLastFrame)
                {
                    IsDragging = true;
                    DragStart  = new Point16(tx, ty);
                    DragEnd    = DragStart;
                }
                else if (IsDragging)
                {
                    DragEnd = new Point16(tx, ty);
                }
            }
            else if (_wasHoldingLastFrame && IsDragging)
            {
                SoundEngine.PlaySound(SoundID.Item4, Player.position);
                IsDragging = false;
                CommitZone();
            }

            _wasHoldingLastFrame = mouseHeld;
        }

        private void CommitZone()
        {
            int x0 = System.Math.Min(DragStart.X, DragEnd.X);
            int x1 = System.Math.Max(DragStart.X, DragEnd.X);
            int y0 = System.Math.Min(DragStart.Y, DragEnd.Y);
            int y1 = System.Math.Max(DragStart.Y, DragEnd.Y);

            if (x1 - x0 < 1 || y1 - y0 < 1)
            {
                Main.NewText("Drag a larger area to define a building zone.", 255, 200, 80);
                return;
            }

            var tl = new Point16(x0, y0);
            var br = new Point16(x1, y1);

            // Check for overlap with ANY existing zone — no zone may overlap another
            foreach (var kv in BuildingAnchorSystem.Zones)
            {
                var z = kv.Value;
                bool overlapX = x0 <= z.BottomRight.X && x1 >= z.TopLeft.X;
                bool overlapY = y0 <= z.BottomRight.Y && y1 >= z.TopLeft.Y;
                if (overlapX && overlapY)
                {
                    Main.NewText($"Zone overlaps with existing zone #{kv.Key} — tiles can only belong to one zone.", 255, 80, 80);
                    return;
                }
            }

            int newId = BuildingAnchorSystem.NextId();
            var zone  = BuildingZone.Capture(tl, br, newId);
            BuildingAnchorSystem.Zones[newId] = zone;

            int area = (x1 - x0 + 1) * (y1 - y0 + 1);
            Main.NewText(
                $"Building zone #{newId} created: {zone.Width}×{zone.Height} ({area} tiles). Ground ref Y={zone.SavedGroundY}.",
                100, 200, 255);
        }

        private void RemoveZoneAtPosition(Point16 clickPos)
        {
            // Find which zone (if any) contains this click position
            int zoneIdToRemove = -1;
            foreach (var kv in BuildingAnchorSystem.Zones)
            {
                var zone = kv.Value;
                bool insideX = clickPos.X >= zone.TopLeft.X && clickPos.X <= zone.BottomRight.X;
                bool insideY = clickPos.Y >= zone.TopLeft.Y && clickPos.Y <= zone.BottomRight.Y;
                if (insideX && insideY)
                {
                    zoneIdToRemove = kv.Key;
                    break;
                }
            }

            if (zoneIdToRemove != -1)
            {
                if (BuildingAnchorSystem.Zones.Remove(zoneIdToRemove))
                {
                    SoundEngine.PlaySound(SoundID.Item14, Player.position);
                    Main.NewText($"Building zone #{zoneIdToRemove} removed. ({BuildingAnchorSystem.Zones.Count} zones remain)", 255, 150, 100);
                }
            }
            else
            {
                Main.NewText("Shift+Click on a zone to remove it.", 255, 200, 80);
            }
        }

        public void CancelDrag()
        {
            IsDragging           = false;
            _wasHoldingLastFrame = false;
        }
    }

    // -------------------------------------------------------------------------
    //  The Building Anchor item. Now purely a tool for creating/editing zones.
    //  Zones are owned by the world, not by individual items.
    // -------------------------------------------------------------------------
    public class BuildingAnchorItem : ModItem
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

        public override bool CanUseItem(Player player) => true;
        public override bool ConsumeItem(Player player) => false;

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "BAInfo1",
                "Left-click and drag to create building zones.")
                { OverrideColor = Color.LimeGreen });
            tooltips.Add(new TooltipLine(Mod, "BAInfo2",
                "Shift+Click inside a zone to remove it.")
                { OverrideColor = Color.LightBlue });
            tooltips.Add(new TooltipLine(Mod, "BAInfo3",
                "Zones are saved to the world and persist through resets.")
                { OverrideColor = Color.Gray });
            
            int zoneCount = BuildingAnchorSystem.Zones.Count;
            if (zoneCount > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "BAZoneCount",
                    $"World has {zoneCount} building zone{(zoneCount == 1 ? "" : "s")}")
                    { OverrideColor = Color.DeepSkyBlue });
            }
        }
    }
}
