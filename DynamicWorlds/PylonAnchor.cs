// Temporarily disabled while the pylon-anchor feature is being iterated on.
#if false
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    public enum PylonPlacementPreference
    {
        Surface,
        Underground,
    }

    internal static class PylonPlacementPreferenceHelper
    {
        public static bool SupportsDepthPreference(TeleportPylonType pylonType)
        {
            return pylonType == TeleportPylonType.Jungle
                || pylonType == TeleportPylonType.Hallow
                || pylonType == TeleportPylonType.Desert
                || pylonType == TeleportPylonType.Snow;
        }

        public static string GetLabel(PylonPlacementPreference preference)
        {
            return preference == PylonPlacementPreference.Underground ? "underground" : "surface";
        }
    }

    public readonly struct ZoneTileBounds
    {
        public readonly Point16 TopLeft;
        public readonly Point16 BottomRight;

        public ZoneTileBounds(Point16 topLeft, Point16 bottomRight)
        {
            TopLeft = topLeft;
            BottomRight = bottomRight;
        }

        public bool Contains(Point16 point) =>
            point.X >= TopLeft.X && point.X <= BottomRight.X &&
            point.Y >= TopLeft.Y && point.Y <= BottomRight.Y;

        public bool Overlaps(ZoneTileBounds other) =>
            TopLeft.X <= other.BottomRight.X && BottomRight.X >= other.TopLeft.X &&
            TopLeft.Y <= other.BottomRight.Y && BottomRight.Y >= other.TopLeft.Y;
    }

    public class PylonStructureZone
    {
        public BuildingZone Zone = new();
        public TeleportPylonType PylonType;
        public Point16 PylonOffset;
        public PylonPlacementPreference PlacementPreference = PylonPlacementPreference.Surface;

        public int Id => Zone.Id;
        public Point16 TopLeft => Zone.TopLeft;
        public Point16 BottomRight => Zone.BottomRight;
        public int Width => Zone.Width;
        public int Height => Zone.Height;

        public Point16 GetPylonTopLeft()
        {
            return new Point16(
                (short)(Zone.TopLeft.X + PylonOffset.X),
                (short)(Zone.TopLeft.Y + PylonOffset.Y));
        }

        public void RefreshChestSnapshots()
        {
            var chestPositions = new List<Point16>(Zone.Chests.Keys);
            foreach (Point16 chestPos in chestPositions)
                Zone.Chests[chestPos] = SavedChestContents.CaptureFromWorld(chestPos);
        }

        public bool TryRestoreToMatchingBiome(IEnumerable<ZoneTileBounds> reservedBounds, out string failureReason)
        {
            if (TryRestoreWithPreferencePass(reservedBounds, fallbackPass: false))
            {
                failureReason = null;
                return true;
            }

            if (PylonPlacementPreferenceHelper.SupportsDepthPreference(PylonType) && TryRestoreWithPreferencePass(reservedBounds, fallbackPass: true))
            {
                failureReason = null;
                return true;
            }

            string preferenceSuffix = PylonPlacementPreferenceHelper.SupportsDepthPreference(PylonType)
                ? $" with a {PylonPlacementPreferenceHelper.GetLabel(PlacementPreference)} preference"
                : string.Empty;
            failureReason = $"No valid {GetPylonTypeName(PylonType)} biome placement was found for pylon zone #{Id}{preferenceSuffix}.";
            return false;
        }

        public TagCompound ToTag()
        {
            return new TagCompound
            {
                ["zone"] = Zone.ToTag(),
                ["pylonType"] = (int)PylonType,
                ["pylonOffsetX"] = (int)PylonOffset.X,
                ["pylonOffsetY"] = (int)PylonOffset.Y,
                ["placementPreference"] = (int)PlacementPreference,
            };
        }

        public static PylonStructureZone FromTag(TagCompound tag)
        {
            return new PylonStructureZone
            {
                Zone = BuildingZone.FromTag(tag.Get<TagCompound>("zone")),
                PylonType = (TeleportPylonType)tag.GetInt("pylonType"),
                PylonOffset = new Point16(
                    (short)tag.GetInt("pylonOffsetX"),
                    (short)tag.GetInt("pylonOffsetY")),
                PlacementPreference = tag.ContainsKey("placementPreference")
                    ? (PylonPlacementPreference)tag.GetInt("placementPreference")
                    : PylonPlacementPreference.Surface,
            };
        }

        public static bool TryCapture(Point16 topLeft, Point16 bottomRight, int id, PylonPlacementPreference placementPreference, out PylonStructureZone zone, out string errorMessage)
        {
            zone = null;
            List<Point16> pylons = FindContainedVanillaPylons(topLeft, bottomRight);

            if (pylons.Count == 0)
            {
                errorMessage = "Pylon zones must fully contain exactly one vanilla pylon.";
                return false;
            }

            if (pylons.Count > 1)
            {
                errorMessage = "Pylon zones can only contain one pylon. Shrink the selection until only one remains.";
                return false;
            }

            Point16 pylonTopLeft = pylons[0];
            zone = new PylonStructureZone
            {
                Zone = BuildingZone.Capture(topLeft, bottomRight, id),
                PylonType = GetPylonType(pylonTopLeft),
                PylonOffset = new Point16(
                    (short)(pylonTopLeft.X - topLeft.X),
                    (short)(pylonTopLeft.Y - topLeft.Y)),
                PlacementPreference = placementPreference,
            };

            errorMessage = null;
            return true;
        }

        private IEnumerable<int> EnumerateCandidateCenterXs()
        {
            const int candidateStep = 16;
            int minCenterX = Math.Max(12, Zone.Width / 2 + 2);
            int maxCenterX = Math.Min(Main.maxTilesX - 12, Main.maxTilesX - ((Zone.Width + 1) / 2) - 2);

            if (minCenterX > maxCenterX)
                yield break;

            if (PylonType == TeleportPylonType.Beach)
            {
                for (int offset = 0; ; offset += candidateStep)
                {
                    bool added = false;
                    int left = minCenterX + offset;
                    int right = maxCenterX - offset;

                    if (left <= maxCenterX)
                    {
                        yield return left;
                        added = true;
                    }

                    if (right >= minCenterX && right != left)
                    {
                        yield return right;
                        added = true;
                    }

                    if (!added)
                        yield break;
                }
            }

            int preferredCenterX = Math.Clamp(Zone.CenterX, minCenterX, maxCenterX);
            yield return preferredCenterX;

            for (int offset = candidateStep; ; offset += candidateStep)
            {
                bool added = false;
                int left = preferredCenterX - offset;
                int right = preferredCenterX + offset;

                if (left >= minCenterX)
                {
                    yield return left;
                    added = true;
                }

                if (right <= maxCenterX)
                {
                    yield return right;
                    added = true;
                }

                if (!added)
                    yield break;
            }
        }

        private bool TryRestoreWithPreferencePass(IEnumerable<ZoneTileBounds> reservedBounds, bool fallbackPass)
        {
            foreach (int candidateCenterX in EnumerateCandidateCenterXs())
            {
                ZoneRestorePlacement placement = Zone.PredictRestorePlacement(candidateCenterX, GetGroundSearchStartY(fallbackPass));
                if (!IsPlacementUsable(placement))
                    continue;

                var placementBounds = new ZoneTileBounds(placement.TopLeft, placement.BottomRight);
                if (OverlapsReserved(placementBounds, reservedBounds))
                    continue;

                if (ContainsAnchoredTile(placementBounds))
                    continue;

                Point16 pylonTopLeft = new Point16(
                    (short)(placement.TopLeft.X + PylonOffset.X),
                    (short)(placement.TopLeft.Y + PylonOffset.Y));

                if (!MatchesDepthPreference(pylonTopLeft, fallbackPass))
                    continue;

                if (!MatchesPylonBiome(PylonType, pylonTopLeft))
                    continue;

                Zone.RestoreToPlacement(placement, "[PylonAnchor]");
                return true;
            }

            return false;
        }

        private int GetGroundSearchStartY(bool fallbackPass)
        {
            if (PylonPlacementPreferenceHelper.SupportsDepthPreference(PylonType))
            {
                bool preferUnderground = PlacementPreference == PylonPlacementPreference.Underground;
                if (fallbackPass)
                    preferUnderground = !preferUnderground;

                return preferUnderground ? Math.Max(10, (int)Main.worldSurface + 16) : 0;
            }

            return PylonType switch
            {
                TeleportPylonType.Underground => Math.Max(10, (int)Main.worldSurface + 16),
                TeleportPylonType.GlowingMushroom => Math.Max(10, (int)Main.worldSurface + 16),
                _ => 0,
            };
        }

        private bool MatchesDepthPreference(Point16 pylonTopLeft, bool fallbackPass)
        {
            if (!PylonPlacementPreferenceHelper.SupportsDepthPreference(PylonType))
                return true;

            bool preferUnderground = PlacementPreference == PylonPlacementPreference.Underground;
            if (fallbackPass)
                preferUnderground = !preferUnderground;

            return preferUnderground
                ? pylonTopLeft.Y > Main.worldSurface
                : pylonTopLeft.Y <= Main.worldSurface;
        }

        private static bool IsPlacementUsable(ZoneRestorePlacement placement)
        {
            return WorldGen.InWorld(placement.TopLeft.X, placement.TopLeft.Y, 5)
                && WorldGen.InWorld(placement.BottomRight.X, placement.BottomRight.Y, 5);
        }

        private static bool OverlapsReserved(ZoneTileBounds placementBounds, IEnumerable<ZoneTileBounds> reservedBounds)
        {
            if (reservedBounds == null)
                return false;

            foreach (ZoneTileBounds reserved in reservedBounds)
            {
                if (placementBounds.Overlaps(reserved))
                    return true;
            }

            return false;
        }

        private static bool ContainsAnchoredTile(ZoneTileBounds placementBounds)
        {
            foreach (Point16 anchoredTile in AnchoredTileSystem.AnchoredTiles.Keys)
            {
                if (placementBounds.Contains(anchoredTile))
                    return true;
            }

            return false;
        }

        private static List<Point16> FindContainedVanillaPylons(Point16 topLeft, Point16 bottomRight)
        {
            var pylons = new List<Point16>();
            var seen = new HashSet<Point16>();

            for (int x = topLeft.X; x <= bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != TileID.TeleportationPylon)
                        continue;

                    Point16 pylonTopLeft = TileObjectData.TopLeft(x, y);
                    if (!seen.Add(pylonTopLeft))
                        continue;

                    if (IsContainedPylon(pylonTopLeft, topLeft, bottomRight))
                        pylons.Add(pylonTopLeft);
                }
            }

            return pylons;
        }

        private static bool IsContainedPylon(Point16 pylonTopLeft, Point16 selectionTopLeft, Point16 selectionBottomRight)
        {
            if (!WorldGen.InWorld(pylonTopLeft.X, pylonTopLeft.Y, 1))
                return false;

            Tile tile = Framing.GetTileSafely(pylonTopLeft.X, pylonTopLeft.Y);
            if (!tile.HasTile || tile.TileType != TileID.TeleportationPylon)
                return false;

            TileObjectData data = TileObjectData.GetTileData(tile);
            if (data == null)
                return false;

            int pylonBottomX = pylonTopLeft.X + data.Width - 1;
            int pylonBottomY = pylonTopLeft.Y + data.Height - 1;

            return pylonTopLeft.X >= selectionTopLeft.X
                && pylonTopLeft.Y >= selectionTopLeft.Y
                && pylonBottomX <= selectionBottomRight.X
                && pylonBottomY <= selectionBottomRight.Y;
        }

        private static TeleportPylonType GetPylonType(Point16 pylonTopLeft)
        {
            Tile tile = Framing.GetTileSafely(pylonTopLeft.X, pylonTopLeft.Y);
            int style = tile.TileFrameX / 54;
            return (TeleportPylonType)style;
        }

        private static string GetPylonTypeName(TeleportPylonType pylonType)
        {
            return pylonType switch
            {
                TeleportPylonType.SurfacePurity => "surface",
                TeleportPylonType.Jungle => "jungle",
                TeleportPylonType.Hallow => "hallow",
                TeleportPylonType.Underground => "underground",
                TeleportPylonType.Desert => "desert",
                TeleportPylonType.Snow => "snow",
                TeleportPylonType.Beach => "beach",
                TeleportPylonType.GlowingMushroom => "glowing mushroom",
                TeleportPylonType.Victory => "universal",
                _ => pylonType.ToString(),
            };
        }

        private static bool MatchesPylonBiome(TeleportPylonType pylonType, Point16 pylonTopLeft)
        {
            if (!WorldGen.InWorld(pylonTopLeft.X, pylonTopLeft.Y, 10))
                return false;

            var sceneMetrics = new SceneMetrics();
            sceneMetrics.ScanAndExportToMain(new SceneMetricsScanSettings
            {
                BiomeScanCenterPositionInWorld = pylonTopLeft.ToWorldCoordinates(),
            });

            return pylonType switch
            {
                TeleportPylonType.SurfacePurity =>
                    pylonTopLeft.Y <= Main.worldSurface &&
                    !sceneMetrics.EnoughTilesForJungle &&
                    !sceneMetrics.EnoughTilesForSnow &&
                    !sceneMetrics.EnoughTilesForDesert &&
                    !sceneMetrics.EnoughTilesForGlowingMushroom &&
                    !sceneMetrics.EnoughTilesForHallow &&
                    !sceneMetrics.EnoughTilesForCrimson &&
                    !sceneMetrics.EnoughTilesForCorruption,
                TeleportPylonType.Jungle => sceneMetrics.EnoughTilesForJungle,
                TeleportPylonType.Hallow => sceneMetrics.EnoughTilesForHallow,
                TeleportPylonType.Underground => pylonTopLeft.Y >= Main.worldSurface,
                TeleportPylonType.Desert => sceneMetrics.EnoughTilesForDesert,
                TeleportPylonType.Snow => sceneMetrics.EnoughTilesForSnow,
                TeleportPylonType.Beach => IsBeachCandidate(pylonTopLeft),
                TeleportPylonType.GlowingMushroom => sceneMetrics.EnoughTilesForGlowingMushroom,
                TeleportPylonType.Victory => true,
                _ => false,
            };
        }

        private static bool IsBeachCandidate(Point16 pylonTopLeft)
        {
            bool withinSurfaceBand = pylonTopLeft.Y <= Main.worldSurface
                && pylonTopLeft.Y > Main.worldSurface * 0.35f;
            if (!withinSurfaceBand)
                return false;

            const int oceanBandWidth = 380;
            return pylonTopLeft.X <= oceanBandWidth
                || pylonTopLeft.X >= Main.maxTilesX - oceanBandWidth;
        }
    }

    public class PylonAnchorSystem : ModSystem
    {
        public static readonly Dictionary<int, PylonStructureZone> Zones = new();
        private static readonly List<RestoredZoneTransform> LastRestoreTransforms = new();
        private static int _nextId = 1;

        public static int NextId() => _nextId++;
        public static void RecalculateNextId() => _nextId = Zones.Count == 0 ? 1 : Zones.Keys.Max() + 1;

        public override void OnWorldLoad()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
            _nextId = 1;
        }

        public override void OnWorldUnload()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
        }

        public static void RefreshAllChestSnapshots()
        {
            foreach (PylonStructureZone zone in Zones.Values)
                zone.RefreshChestSnapshots();
        }

        public static void RestoreAllZones(bool announce = true)
        {
            LastRestoreTransforms.Clear();
            if (Zones.Count == 0)
                return;

            var reservedBounds = new List<ZoneTileBounds>();
            foreach (BuildingZone structureZone in StructureAnchorSystem.Zones.Values)
            {
                ZoneRestorePlacement placement = structureZone.PredictRestorePlacement(structureZone.CenterX);
                reservedBounds.Add(new ZoneTileBounds(placement.TopLeft, placement.BottomRight));
            }

            int placedCount = 0;
            foreach (var kv in Zones.OrderBy(kv => kv.Key))
            {
                PylonStructureZone zone = kv.Value;
                Point16 oldTopLeft = zone.Zone.TopLeft;
                Point16 oldBottomRight = zone.Zone.BottomRight;

                if (!zone.TryRestoreToMatchingBiome(reservedBounds, out string failureReason))
                {
                    ModContent.GetInstance<DynamicWorlds>().Logger.Warn($"[PylonAnchor] {failureReason} Falling back to the original X position.");
                    zone.Zone.RestoreToWorld();
                }

                reservedBounds.Add(new ZoneTileBounds(zone.Zone.TopLeft, zone.Zone.BottomRight));
                LastRestoreTransforms.Add(new RestoredZoneTransform(
                    oldTopLeft,
                    oldBottomRight,
                    zone.Zone.TopLeft.X - oldTopLeft.X,
                    zone.Zone.TopLeft.Y - oldTopLeft.Y));
                placedCount++;
            }

            int restoredPylons = PylonRestoreHelper.RestoreVanillaPylons(
                Zones.Values.SelectMany(zone => zone.Zone.Tiles.Keys));
            if (restoredPylons > 0)
            {
                ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                    $"[PylonAnchor] Re-registered {restoredPylons} restored vanilla pylon(s).");
            }

            if (announce && Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText(
                    $"Restored {placedCount} pylon zone{(placedCount == 1 ? "" : "s")}.",
                    255,
                    220,
                    120);
            }
        }

        public static bool TryTranslateSavedPoint(Point16 savedPoint, out Point16 translatedPoint)
        {
            foreach (RestoredZoneTransform transform in LastRestoreTransforms)
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
                if (tilePosition.X >= kv.Value.TopLeft.X && tilePosition.X <= kv.Value.BottomRight.X &&
                    tilePosition.Y >= kv.Value.TopLeft.Y && tilePosition.Y <= kv.Value.BottomRight.Y)
                {
                    zoneId = kv.Key;
                    return true;
                }
            }

            zoneId = -1;
            return false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            var list = new List<TagCompound>();
            foreach (PylonStructureZone zone in Zones.Values)
                list.Add(zone.ToTag());

            tag["PylonZones"] = list;
            tag["PylonZoneNextId"] = _nextId;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Zones.Clear();
            _nextId = 1;

            if (tag.ContainsKey("PylonZones"))
            {
                foreach (TagCompound zoneTag in tag.GetList<TagCompound>("PylonZones"))
                {
                    PylonStructureZone zone = PylonStructureZone.FromTag(zoneTag);
                    Zones[zone.Id] = zone;
                }
            }

            if (tag.ContainsKey("PylonZoneNextId"))
                _nextId = tag.GetInt("PylonZoneNextId");

            if (_nextId <= 0 || _nextId <= Zones.Keys.DefaultIfEmpty(0).Max())
                RecalculateNextId();
        }

        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player?.HeldItem == null)
                return;

            if (!WorldToolOverlayHelper.IsHoldingWorldTool(player))
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Vector2 screenPos = Main.screenPosition;
            WorldToolOverlayHelper.BeginOverlay(spriteBatch);

            foreach (PylonStructureZone zone in Zones.Values)
            {
                WorldToolOverlayHelper.DrawAreaOverlay(
                    spriteBatch,
                    zone.TopLeft,
                    zone.BottomRight,
                    screenPos,
                    new Color(255, 210, 90) * 0.32f,
                    new Color(255, 180, 70));
            }

            PylonAnchorPlayer pylonPlayer = player.GetModPlayer<PylonAnchorPlayer>();
            if (pylonPlayer.IsDragging)
            {
                int x0 = Math.Min(pylonPlayer.DragStart.X, pylonPlayer.DragEnd.X);
                int x1 = Math.Max(pylonPlayer.DragStart.X, pylonPlayer.DragEnd.X);
                int y0 = Math.Min(pylonPlayer.DragStart.Y, pylonPlayer.DragEnd.Y);
                int y1 = Math.Max(pylonPlayer.DragStart.Y, pylonPlayer.DragEnd.Y);

                WorldToolOverlayHelper.DrawAreaOverlay(
                    spriteBatch,
                    new Point16(x0, y0),
                    new Point16(x1, y1),
                    screenPos,
                    new Color(255, 225, 140) * 0.36f,
                    Color.Gold);
            }

            spriteBatch.End();
        }
    }

    public class PylonAnchorPlayer : ModPlayer
    {
        public bool IsDragging;
        public Point16 DragStart;
        public Point16 DragEnd;
        public PylonPlacementPreference PlacementPreference = PylonPlacementPreference.Surface;

        private bool _wasHoldingLastFrame;
        private bool _wasRightMouseHeldLastFrame;

        public override void PostUpdate()
        {
            if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
            {
                if (IsDragging)
                    CancelDrag();

                _wasHoldingLastFrame = false;
                _wasRightMouseHeldLastFrame = false;
                return;
            }

            bool holding = Player.HeldItem?.type == ModContent.ItemType<PylonAnchorItem>();
            if (!holding)
            {
                if (IsDragging)
                    CancelDrag();

                _wasHoldingLastFrame = false;
                _wasRightMouseHeldLastFrame = false;
                return;
            }

            bool mouseHeld = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
            bool rightMouseHeld = Main.mouseRight && !Main.LocalPlayer.mouseInterface;
            bool shiftHeld = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            int tileX = (int)(Main.MouseWorld.X / 16f);
            int tileY = (int)(Main.MouseWorld.Y / 16f);
            Point16 mouseTile = new Point16(tileX, tileY);

            if (rightMouseHeld && !_wasRightMouseHeldLastFrame && !IsDragging)
            {
                TogglePlacementPreference();
                _wasRightMouseHeldLastFrame = true;
                return;
            }

            if (shiftHeld && mouseHeld && !_wasHoldingLastFrame && !Main.LocalPlayer.mouseInterface)
            {
                RemoveZoneAtPosition(mouseTile);
                _wasHoldingLastFrame = true;
                _wasRightMouseHeldLastFrame = rightMouseHeld;
                return;
            }

            if (mouseHeld)
            {
                if (!_wasHoldingLastFrame)
                {
                    IsDragging = true;
                    DragStart = mouseTile;
                    DragEnd = mouseTile;
                }
                else if (IsDragging)
                {
                    DragEnd = mouseTile;
                }
            }
            else if (_wasHoldingLastFrame && IsDragging)
            {
                SoundEngine.PlaySound(SoundID.Item4, Player.position);
                IsDragging = false;
                CommitZone();
            }

            _wasHoldingLastFrame = mouseHeld;
            _wasRightMouseHeldLastFrame = rightMouseHeld;
        }

        private void CommitZone()
        {
            int x0 = Math.Min(DragStart.X, DragEnd.X);
            int x1 = Math.Max(DragStart.X, DragEnd.X);
            int y0 = Math.Min(DragStart.Y, DragEnd.Y);
            int y1 = Math.Max(DragStart.Y, DragEnd.Y);

            if (x1 - x0 < 2 || y1 - y0 < 3)
            {
                Main.NewText("Drag a larger area so the full pylon and its structure are inside the zone.", 255, 210, 100);
                return;
            }

            var topLeft = new Point16(x0, y0);
            var bottomRight = new Point16(x1, y1);

            foreach (var kv in StructureAnchorSystem.Zones)
            {
                ZoneTileBounds other = new ZoneTileBounds(kv.Value.TopLeft, kv.Value.BottomRight);
                if (new ZoneTileBounds(topLeft, bottomRight).Overlaps(other))
                {
                    Main.NewText($"Pylon zone overlaps with structure zone #{kv.Key}. Zones cannot share tiles.", 255, 120, 120);
                    return;
                }
            }

            foreach (var kv in PylonAnchorSystem.Zones)
            {
                ZoneTileBounds other = new ZoneTileBounds(kv.Value.TopLeft, kv.Value.BottomRight);
                if (new ZoneTileBounds(topLeft, bottomRight).Overlaps(other))
                {
                    Main.NewText($"Pylon zone overlaps with pylon zone #{kv.Key}. Zones cannot share tiles.", 255, 120, 120);
                    return;
                }
            }

            if (StructureAnchorSystem.TryFindOverlappingAnchoredTile(topLeft, bottomRight, out Point16 anchoredOverlap))
            {
                Main.NewText(
                    $"Pylon zones cannot overlap individually anchored tiles. Remove the anchor at ({anchoredOverlap.X}, {anchoredOverlap.Y}) first.",
                    255,
                    120,
                    120);
                return;
            }

            int zoneId = PylonAnchorSystem.NextId();
            if (!PylonStructureZone.TryCapture(topLeft, bottomRight, zoneId, PlacementPreference, out PylonStructureZone zone, out string errorMessage))
            {
                Main.NewText(errorMessage, 255, 200, 80);
                return;
            }

            PylonAnchorSystem.Zones[zoneId] = zone;
            string preferenceText = PylonPlacementPreferenceHelper.SupportsDepthPreference(zone.PylonType)
                ? $" ({PylonPlacementPreferenceHelper.GetLabel(zone.PlacementPreference)} preferred)"
                : " (normal depth rules)";
            Main.NewText(
                $"Pylon zone #{zoneId} created for a {zone.PylonType} pylon{preferenceText}: {zone.Width}x{zone.Height}.",
                255,
                215,
                120);
        }

        private void RemoveZoneAtPosition(Point16 clickPos)
        {
            int zoneIdToRemove = -1;
            foreach (var kv in PylonAnchorSystem.Zones)
            {
                if (clickPos.X >= kv.Value.TopLeft.X && clickPos.X <= kv.Value.BottomRight.X &&
                    clickPos.Y >= kv.Value.TopLeft.Y && clickPos.Y <= kv.Value.BottomRight.Y)
                {
                    zoneIdToRemove = kv.Key;
                    break;
                }
            }

            if (zoneIdToRemove != -1)
            {
                if (PylonAnchorSystem.Zones.Remove(zoneIdToRemove))
                {
                    SoundEngine.PlaySound(SoundID.Item14, Player.position);
                    Main.NewText(
                        $"Pylon zone #{zoneIdToRemove} removed. ({PylonAnchorSystem.Zones.Count} zones remain)",
                        255,
                        180,
                        120);
                }

                return;
            }

            Main.NewText("Shift+Click on a pylon zone to remove it.", 255, 220, 120);
        }

        public void CancelDrag()
        {
            IsDragging = false;
            _wasHoldingLastFrame = false;
            _wasRightMouseHeldLastFrame = false;
        }

        private void TogglePlacementPreference()
        {
            PlacementPreference = PlacementPreference == PylonPlacementPreference.Surface
                ? PylonPlacementPreference.Underground
                : PylonPlacementPreference.Surface;

            Main.NewText(
                $"Pylon Anchor now prefers {PylonPlacementPreferenceHelper.GetLabel(PlacementPreference)} placement for jungle, hallow, desert, and snow pylons.",
                255,
                220,
                120);
        }
    }

    public class PylonAnchorItem : ModItem
    {
        public override string Texture => "DynamicWorlds/pylonanchor";

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 12;
            Item.useAnimation = 18;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 1);
            Item.maxStack = 1;
            Item.consumable = false;
            Item.noMelee = true;
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item8;
        }

        public override bool CanUseItem(Player player) => true;
        public override bool ConsumeItem(Player player) => false;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "PAInfo1",
                "Left-click and drag to project a pylon relocation zone.")
                { OverrideColor = Color.Gold });
            tooltips.Add(new TooltipLine(Mod, "PAInfo2",
                "Each zone must fully contain exactly one vanilla pylon.")
                { OverrideColor = Color.LightGoldenrodYellow });
            tooltips.Add(new TooltipLine(Mod, "PAInfo3",
                "On regen, the whole structure moves to a biome where that pylon can function.")
                { OverrideColor = Color.LightSkyBlue });
            tooltips.Add(new TooltipLine(Mod, "PAInfo4",
                "Pylon zones cannot overlap structure zones or individually anchored tiles.")
                { OverrideColor = Color.Orange });
            tooltips.Add(new TooltipLine(Mod, "PAInfo5",
                "Right-click while holding to toggle surface or underground preference for snow, desert, jungle, and hallow pylons.")
                { OverrideColor = Color.LightGoldenrodYellow });
            tooltips.Add(new TooltipLine(Mod, "PAInfo6",
                "Shift+Click inside a pylon zone to remove it.")
                { OverrideColor = Color.LightBlue });
            tooltips.Add(new TooltipLine(Mod, "PAInfo7",
                "Hold any world tool to see anchors, erasures, structure zones, and pylon zones.")
                { OverrideColor = Color.LightSkyBlue });

            PylonAnchorPlayer modPlayer = Main.LocalPlayer?.GetModPlayer<PylonAnchorPlayer>();
            string preferenceLabel = PylonPlacementPreferenceHelper.GetLabel(
                modPlayer?.PlacementPreference ?? PylonPlacementPreference.Surface);
            tooltips.Add(new TooltipLine(Mod, "PAInfoPreference",
                $"Current depth preference for supported pylons: {preferenceLabel}")
                { OverrideColor = Color.Gold });

            int zoneCount = PylonAnchorSystem.Zones.Count;
            if (zoneCount > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "PAZoneCount",
                    $"World has {zoneCount} pylon zone{(zoneCount == 1 ? "" : "s")}")
                    { OverrideColor = Color.Gold });
            }
        }
    }
}
#endif
