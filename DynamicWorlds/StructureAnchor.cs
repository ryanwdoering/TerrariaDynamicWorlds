using System;
using System.Collections.Generic;
using System.Linq;
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
    //  A single saved structure zone: bounds + full per-tile snapshot.
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

        // Unique id so multiple Structure Anchors can coexist
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
        public int CenterX => (TopLeft.X + BottomRight.X) / 2;

        // Returns true if this tile should be treated as open air for ground detection
        // (platforms, trees, cacti, sunflowers, and other surface plants).
        private const int MaxSupportBridgeDepth = 8;

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
        public static int FindGroundY(int x, int startY = 0)
        {
            int searchStartY = System.Math.Max(10, startY);
            for (int y = searchStartY; y < Main.maxTilesY - 10; y++)
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

        private static int FindNearbySupportY(int x, int startY)
        {
            for (int y = startY; y <= startY + MaxSupportBridgeDepth && y < Main.maxTilesY - 10; y++)
            {
                if (!WorldGen.InWorld(x, y, 1))
                    break;

                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && !TileID.Sets.Platforms[tile.TileType])
                    return y;
            }

            return -1;
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

            ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                $"[SA] Captured zone #{id}: TL=({topLeft.X},{topLeft.Y}) BR=({bottomRight.X},{bottomRight.Y}) centerX={centerX} SavedGroundY={zone.SavedGroundY} worldSurface={Main.worldSurface:F0}");

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

        public ZoneRestorePlacement PredictRestorePlacement(int targetCenterX, int groundSearchStartY = 0)
        {
            int deltaX = targetCenterX - CenterX;
            int newGroundY = FindGroundY(targetCenterX, groundSearchStartY);

            // Place the zone so its bottom row sits one tile INTO the ground,
            // giving a natural embedded/planted look.
            int newBottomY = newGroundY;
            int deltaY = newBottomY - BottomRight.Y;

            return new ZoneRestorePlacement(
                new Point16((short)(TopLeft.X + deltaX), (short)(TopLeft.Y + deltaY)),
                new Point16((short)(BottomRight.X + deltaX), (short)newBottomY),
                deltaX,
                deltaY,
                newGroundY);
        }

        // ── Restore (translated) ──────────────────────────────────────────────
        // Called after worldgen. Finds new ground level at the same X column,
        // computes deltaY = newGroundY - savedGroundY, shifts every tile by deltaY,
        // fills gaps, then re-captures the zone at its new position so the next
        // regen uses up-to-date coordinates.
        public void RestoreToWorld()
        {
            RestoreToPlacement(PredictRestorePlacement(CenterX), "[SA]");
        }

        public void RestoreToPlacement(ZoneRestorePlacement placement, string logPrefix)
        {
            int centerX = CenterX + placement.DeltaX;
            int newGroundY = placement.GroundY;
            int deltaX = placement.DeltaX;
            int deltaY = placement.DeltaY;
            int newTopY = placement.TopLeft.Y;
            int newBottomY = placement.BottomRight.Y;
            int newLeftX = placement.TopLeft.X;
            int newRightX = placement.BottomRight.X;

            ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                $"{logPrefix} RestoreZone #{Id}: centerX={centerX} SavedGroundY={SavedGroundY} newGroundY={newGroundY} delta=({deltaX},{deltaY}) TL=({TopLeft.X},{TopLeft.Y})->({newLeftX},{newTopY}) BR=({BottomRight.X},{BottomRight.Y})->({newRightX},{newBottomY}) worldSurface={Main.worldSurface:F0}");

            // 1. Bridge only small support gaps beneath the restored footprint.
            //    If the structure lands on a floating island, leave it floating
            //    instead of creating a dirt pillar down to the surface below.
            for (int x = newLeftX; x <= newRightX; x++)
            {
                int supportY = FindNearbySupportY(x, newBottomY + 1);
                if (supportY <= newBottomY + 1)
                    continue;

                for (int y = newBottomY + 1; y < supportY; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Framing.GetTileSafely(x, y);
                    tile.ClearEverything();
                    tile.HasTile  = true;
                    tile.TileType = BiomeTileAt(x, y);
                }
            }

            // 2. Clear only the exact footprint where building tiles will go.
            //    Do NOT clear above the building - this prevents filling dirt over it.
            //    Only clear the building's own footprint + a small area below for settling.
            if (deltaX != 0 || deltaY != 0)
            {
                for (int x = newLeftX; x <= newRightX; x++)
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
                int nx = kv.Key.X + deltaX;
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
                int nx = kv.Key.X + deltaX;
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
                    p.SpawnX = SavedSpawn.X + deltaX;
                    p.SpawnY = SavedSpawn.Y + deltaY;
                }
            }

            WorldGen.RangeFrame(
                System.Math.Max(0, newLeftX - 2),
                System.Math.Max(0, newTopY - 2),
                System.Math.Min(Main.maxTilesX, newRightX + 2),
                System.Math.Min(Main.maxTilesY, newBottomY + 2));

            // 7. Update zone metadata to the new position so the next regen is correct.
            //    Always update — even if deltaY==0, SavedGroundY may have changed.
            {
                var newTl = new Point16((short)newLeftX, (short)newTopY);
                var newBr = new Point16((short)newRightX, (short)newBottomY);

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
                    var newPos = new Point16((short)(kv.Key.X + deltaX), (short)(kv.Key.Y + deltaY));
                    newChests[newPos] = new SavedChestContents { Position = newPos, Items = kv.Value.Items };
                }

                TopLeft      = newTl;
                BottomRight  = newBr;
                SavedGroundY = newGroundY;
                Tiles        = newTiles;
                Chests       = newChests;

                if (SavedSpawn.X >= 0)
                    SavedSpawn = new Point16((short)(SavedSpawn.X + deltaX), (short)(SavedSpawn.Y + deltaY));
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
    //  World-level system: holds all registered structure zones.
    // -------------------------------------------------------------------------
    public readonly struct ZoneRestorePlacement
    {
        public readonly Point16 TopLeft;
        public readonly Point16 BottomRight;
        public readonly short DeltaX;
        public readonly short DeltaY;
        public readonly int GroundY;

        public ZoneRestorePlacement(Point16 topLeft, Point16 bottomRight, int deltaX, int deltaY, int groundY)
        {
            TopLeft = topLeft;
            BottomRight = bottomRight;
            DeltaX = (short)deltaX;
            DeltaY = (short)deltaY;
            GroundY = groundY;
        }
    }

    public readonly struct RestoredZoneTransform
    {
        public readonly Point16 OriginalTopLeft;
        public readonly Point16 OriginalBottomRight;
        public readonly short DeltaX;
        public readonly short DeltaY;

        public RestoredZoneTransform(Point16 originalTopLeft, Point16 originalBottomRight, int deltaX, int deltaY)
        {
            OriginalTopLeft = originalTopLeft;
            OriginalBottomRight = originalBottomRight;
            DeltaX = (short)deltaX;
            DeltaY = (short)deltaY;
        }

        public bool Contains(Point16 point) =>
            point.X >= OriginalTopLeft.X && point.X <= OriginalBottomRight.X &&
            point.Y >= OriginalTopLeft.Y && point.Y <= OriginalBottomRight.Y;

        public Point16 Translate(Point16 point) =>
            new Point16((short)(point.X + DeltaX), (short)(point.Y + DeltaY));
    }

    public class StructureAnchorSystem : ModSystem
    {
        // Keyed by zone Id
        public static readonly Dictionary<int, BuildingZone> Zones = new();
        private static readonly List<RestoredZoneTransform> LastRestoreTransforms = new();

        // Overlay texture drawn on zone tiles while the item is held
        public static Asset<Texture2D> ZoneIcon;

        private static int _nextId = 1;
        public static int NextId() => _nextId++;
        public static void RecalculateNextId() => _nextId = Zones.Count == 0 ? 1 : Zones.Keys.Max() + 1;

        public override void OnWorldLoad()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
            _nextId = 1;

            if (ZoneIcon == null || !ZoneIcon.IsLoaded)
                ZoneIcon = ModContent.Request<Texture2D>("DynamicWorlds/AnchoredTile");
        }

        public override void OnWorldUnload()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
        }

        // Refresh the snapshot of chest contents in all structure zones.
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
        public static void RestoreAllZones(bool announce = true)
        {
            LastRestoreTransforms.Clear();
            if (Zones.Count == 0) return;

            foreach (var kv in Zones)
            {
                Point16 oldTopLeft = kv.Value.TopLeft;
                Point16 oldBottomRight = kv.Value.BottomRight;

                kv.Value.RestoreToWorld();
                LastRestoreTransforms.Add(new RestoredZoneTransform(
                    oldTopLeft,
                    oldBottomRight,
                    kv.Value.TopLeft.X - oldTopLeft.X,
                    kv.Value.TopLeft.Y - oldTopLeft.Y));
            }

            int restoredPylons = PylonRestoreHelper.RestoreVanillaPylons(
                Zones.Values.SelectMany(zone => zone.Tiles.Keys));
            if (restoredPylons > 0)
                ModContent.GetInstance<DynamicWorlds>().Logger.Info($"[StructureAnchor] Re-registered {restoredPylons} restored vanilla pylon(s).");

            if (announce && Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText($"Restored {Zones.Count} structure zone{(Zones.Count == 1 ? "" : "s")}.", 180, 220, 255);
        }

        public static bool TryTranslateSavedPoint(Point16 savedPoint, out Point16 translatedPoint)
        {
            foreach (var transform in LastRestoreTransforms)
            {
                if (transform.Contains(savedPoint))
                {
                    translatedPoint = transform.Translate(savedPoint);
                    return true;
                }
            }

            translatedPoint = default;
            return false;
        }

        public static bool TryGetZoneAt(Point16 tilePosition, out int zoneId)
        {
            foreach (var kv in Zones)
            {
                BuildingZone zone = kv.Value;
                if (tilePosition.X >= zone.TopLeft.X && tilePosition.X <= zone.BottomRight.X &&
                    tilePosition.Y >= zone.TopLeft.Y && tilePosition.Y <= zone.BottomRight.Y)
                {
                    zoneId = kv.Key;
                    return true;
                }
            }

            zoneId = -1;
            return false;
        }

        public static bool TryFindOverlappingAnchoredTile(Point16 topLeft, Point16 bottomRight, out Point16 overlapTile)
        {
            foreach (Point16 tilePosition in AnchoredTileSystem.AnchoredTiles.Keys)
            {
                if (tilePosition.X >= topLeft.X && tilePosition.X <= bottomRight.X &&
                    tilePosition.Y >= topLeft.Y && tilePosition.Y <= bottomRight.Y)
                {
                    overlapTile = tilePosition;
                    return true;
                }
            }

            overlapTile = default;
            return false;
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

            if (_nextId <= 0 || _nextId <= Zones.Keys.DefaultIfEmpty(0).Max())
                RecalculateNextId();
        }

        // ── Visual overlay ────────────────────────────────────────────────────
        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player?.HeldItem == null) return;
            if (!WorldToolOverlayHelper.IsHoldingWorldTool(player)) return;

            SpriteBatch sb       = Main.spriteBatch;
            Vector2     screenPos = Main.screenPosition;

            WorldToolOverlayHelper.BeginOverlay(sb);

            // Draw all registered zones
            foreach (var kv in Zones)
            {
                var zone = kv.Value;
                WorldToolOverlayHelper.DrawAreaOverlay(sb, zone.TopLeft, zone.BottomRight, screenPos,
                    new Color(100, 180, 255) * 0.35f, Color.DeepSkyBlue);
            }

            // Draw in-progress drag preview for the item being held
            var mp = player.GetModPlayer<StructureAnchorPlayer>();
            if (mp.IsDragging)
            {
                int x0 = System.Math.Min(mp.DragStart.X, mp.DragEnd.X);
                int x1 = System.Math.Max(mp.DragStart.X, mp.DragEnd.X);
                int y0 = System.Math.Min(mp.DragStart.Y, mp.DragEnd.Y);
                int y1 = System.Math.Max(mp.DragStart.Y, mp.DragEnd.Y);
                WorldToolOverlayHelper.DrawAreaOverlay(sb,
                    new Point16(x0, y0), new Point16(x1, y1), screenPos,
                    Color.Gold * 0.3f, Color.Gold);
            }

            sb.End();
        }

    }

    // -------------------------------------------------------------------------
    //  ModPlayer: tracks drag state and the zone assigned to this item instance.
    //  Each StructureAnchorItem stores its own zone id in the item's tag.
    // -------------------------------------------------------------------------
    public class StructureAnchorPlayer : ModPlayer
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

            bool holding = Player.HeldItem?.type == ModContent.ItemType<StructureAnchorItem>();
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
                Main.NewText("Drag a larger area to define a structure zone.", 255, 200, 80);
                return;
            }

            var tl = new Point16(x0, y0);
            var br = new Point16(x1, y1);

            // Check for overlap with ANY existing zone — no zone may overlap another
            foreach (var kv in StructureAnchorSystem.Zones)
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

            if (StructureAnchorSystem.TryFindOverlappingAnchoredTile(tl, br, out Point16 anchoredOverlap))
            {
                Main.NewText(
                    $"Structure zones cannot overlap individually anchored tiles. Remove the anchor at ({anchoredOverlap.X}, {anchoredOverlap.Y}) first.",
                    255, 120, 120);
                return;
            }

            int newId = StructureAnchorSystem.NextId();
            var zone  = BuildingZone.Capture(tl, br, newId);
            StructureAnchorSystem.Zones[newId] = zone;

            int area = (x1 - x0 + 1) * (y1 - y0 + 1);
            Main.NewText(
                $"Structure zone #{newId} created: {zone.Width}×{zone.Height} ({area} tiles). Ground ref Y={zone.SavedGroundY}.",
                100, 200, 255);
        }

        private void RemoveZoneAtPosition(Point16 clickPos)
        {
            // Find which zone (if any) contains this click position
            int zoneIdToRemove = -1;
            foreach (var kv in StructureAnchorSystem.Zones)
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
                if (StructureAnchorSystem.Zones.Remove(zoneIdToRemove))
                {
                    SoundEngine.PlaySound(SoundID.Item14, Player.position);
                    Main.NewText($"Structure zone #{zoneIdToRemove} removed. ({StructureAnchorSystem.Zones.Count} zones remain)", 255, 150, 100);
                }
            }
            else
            {
                Main.NewText("Shift+Click on a structure zone to remove it.", 255, 200, 80);
            }
        }

        public void CancelDrag()
        {
            IsDragging           = false;
            _wasHoldingLastFrame = false;
        }
    }

    // -------------------------------------------------------------------------
    //  The Structure Anchor item. Now purely a tool for creating/editing zones.
    //  Zones are owned by the world, not by individual items.
    // -------------------------------------------------------------------------
    public class StructureAnchorItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width        = 32;
            Item.height       = 32;
            Item.useStyle     = ItemUseStyleID.Shoot;
            Item.useTime      = 12;
            Item.useAnimation = 18;
            Item.rare         = ItemRarityID.LightRed;
            Item.value        = Item.buyPrice(gold: 1);
            Item.maxStack     = 1;
            Item.consumable   = false;
            Item.noMelee      = true;
            Item.noUseGraphic = false;
            Item.UseSound     = SoundID.Item8;
        }

        public override bool CanUseItem(Player player) => true;
        public override bool ConsumeItem(Player player) => false;

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "BAInfo1",
                "Left-click and drag to project structure zones.")
                { OverrideColor = Color.LimeGreen });
            tooltips.Add(new TooltipLine(Mod, "BAInfo2",
                "Shift+Click inside a structure zone to remove it.")
                { OverrideColor = Color.LightBlue });
            tooltips.Add(new TooltipLine(Mod, "BAInfo3",
                "Zones are saved to the world and persist through resets.")
                { OverrideColor = Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "BAInfo4",
                "Structure zones cannot overlap individually anchored tiles.")
                { OverrideColor = Color.Orange });
            tooltips.Add(new TooltipLine(Mod, "BAInfo5",
                "Hold any world tool to see anchors, erasures, and structure zones.")
                { OverrideColor = Color.LightSkyBlue });
            
            int zoneCount = StructureAnchorSystem.Zones.Count;
            if (zoneCount > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "BAZoneCount",
                    $"World has {zoneCount} structure zone{(zoneCount == 1 ? "" : "s")}")
                    { OverrideColor = Color.DeepSkyBlue });
            }
        }
    }
}
