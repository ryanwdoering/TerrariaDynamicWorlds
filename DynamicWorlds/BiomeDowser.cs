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
    public enum BiomeDowserPlacementMode
    {
        Surface,
        Underground,
        Floating,
    }

    internal static class BiomeDowserPlacementHelper
    {
        public static string GetLabel(BiomeDowserPlacementMode mode)
        {
            return mode switch
            {
                BiomeDowserPlacementMode.Underground => "underground",
                BiomeDowserPlacementMode.Floating => "floating",
                _ => "surface",
            };
        }

        public static BiomeDowserPlacementMode[] GetSupportedModes(TeleportPylonType pylonType)
        {
            return pylonType switch
            {
                TeleportPylonType.SurfacePurity => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Floating },
                TeleportPylonType.Jungle => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Underground },
                TeleportPylonType.Hallow => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Underground, BiomeDowserPlacementMode.Floating },
                TeleportPylonType.Underground => new[] { BiomeDowserPlacementMode.Underground },
                TeleportPylonType.Desert => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Underground },
                TeleportPylonType.Snow => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Underground },
                TeleportPylonType.Beach => new[] { BiomeDowserPlacementMode.Surface },
                TeleportPylonType.GlowingMushroom => new[] { BiomeDowserPlacementMode.Underground },
                TeleportPylonType.Victory => new[] { BiomeDowserPlacementMode.Surface, BiomeDowserPlacementMode.Floating },
                _ => new[] { BiomeDowserPlacementMode.Surface },
            };
        }

        public static bool SupportsMode(TeleportPylonType pylonType, BiomeDowserPlacementMode mode)
        {
            return GetSupportedModes(pylonType).Contains(mode);
        }

        public static BiomeDowserPlacementMode SanitizeForPylon(TeleportPylonType pylonType, BiomeDowserPlacementMode requested)
        {
            BiomeDowserPlacementMode[] supportedModes = GetSupportedModes(pylonType);
            if (supportedModes.Length == 0)
                return BiomeDowserPlacementMode.Surface;

            return supportedModes.Contains(requested)
                ? requested
                : supportedModes[0];
        }

        public static IEnumerable<BiomeDowserPlacementMode> EnumeratePlacementAttempts(TeleportPylonType pylonType, BiomeDowserPlacementMode preferred)
        {
            BiomeDowserPlacementMode[] supportedModes = GetSupportedModes(pylonType);
            if (supportedModes.Length == 0)
                yield break;

            BiomeDowserPlacementMode first = SanitizeForPylon(pylonType, preferred);
            yield return first;

            foreach (BiomeDowserPlacementMode mode in supportedModes)
            {
                if (mode != first)
                    yield return mode;
            }
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

    public sealed class BiomeDowserZone
    {
        private const int GlowingMushroomTileThreshold = 100;
        private const ushort VanillaMushroomVinesTileId = 528;
        private static readonly Point ApproximateSceneMetricsZoneScanSize = new Point(169, 124);

        public BuildingZone Zone = new();
        public TeleportPylonType PylonType;
        public Point16 PylonOffset;
        public BiomeDowserPlacementMode PlacementMode = BiomeDowserPlacementMode.Surface;
        public int FloatingYOffsetFromSurface = -96;

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
            foreach (BiomeDowserPlacementMode mode in BiomeDowserPlacementHelper.EnumeratePlacementAttempts(PylonType, PlacementMode))
            {
                if (TryRestoreWithMode(reservedBounds, mode))
                {
                    failureReason = null;
                    return true;
                }
            }

            failureReason =
                $"No valid {GetPylonTypeName(PylonType)} biome placement was found for Biome Dowser zone #{Id} using a {BiomeDowserPlacementHelper.GetLabel(PlacementMode)} preference.";
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
                ["placementMode"] = (int)PlacementMode,
                ["floatingYOffsetFromSurface"] = FloatingYOffsetFromSurface,
            };
        }

        public static BiomeDowserZone FromTag(TagCompound tag)
        {
            return new BiomeDowserZone
            {
                Zone = BuildingZone.FromTag(tag.Get<TagCompound>("zone")),
                PylonType = (TeleportPylonType)tag.GetInt("pylonType"),
                PylonOffset = new Point16(
                    (short)tag.GetInt("pylonOffsetX"),
                    (short)tag.GetInt("pylonOffsetY")),
                PlacementMode = tag.ContainsKey("placementMode")
                    ? (BiomeDowserPlacementMode)tag.GetInt("placementMode")
                    : BiomeDowserPlacementMode.Surface,
                FloatingYOffsetFromSurface = tag.ContainsKey("floatingYOffsetFromSurface")
                    ? tag.GetInt("floatingYOffsetFromSurface")
                    : -96,
            };
        }

        public static bool TryCapture(
            Point16 topLeft,
            Point16 bottomRight,
            int id,
            BiomeDowserPlacementMode requestedMode,
            out BiomeDowserZone zone,
            out string errorMessage)
        {
            zone = null;
            List<Point16> pylons = FindContainedVanillaPylons(topLeft, bottomRight);

            if (pylons.Count == 0)
            {
                errorMessage = "Biome Dowser zones must fully contain exactly one vanilla pylon.";
                return false;
            }

            if (pylons.Count > 1)
            {
                errorMessage = "Biome Dowser zones can only contain one pylon. Shrink the selection until only one remains.";
                return false;
            }

            Point16 pylonTopLeft = pylons[0];
            TeleportPylonType pylonType = GetPylonType(pylonTopLeft);
            BuildingZone capturedZone = BuildingZone.CaptureConnected(
                topLeft,
                bottomRight,
                EnumerateFootprintTiles(pylonTopLeft),
                id);

            BiomeDowserPlacementMode placementMode = BiomeDowserPlacementHelper.SanitizeForPylon(pylonType, requestedMode);
            zone = new BiomeDowserZone
            {
                Zone = capturedZone,
                PylonType = pylonType,
                PylonOffset = new Point16(
                    (short)(pylonTopLeft.X - capturedZone.TopLeft.X),
                    (short)(pylonTopLeft.Y - capturedZone.TopLeft.Y)),
                PlacementMode = placementMode,
                FloatingYOffsetFromSurface = Math.Min(-48, (int)Math.Round(pylonTopLeft.Y - Main.worldSurface)),
            };

            errorMessage = null;
            return true;
        }

        private bool TryRestoreWithMode(IEnumerable<ZoneTileBounds> reservedBounds, BiomeDowserPlacementMode mode)
        {
            var validCandidates = new List<(ZoneRestorePlacement placement, int centerX)>();

            foreach (int candidateCenterX in EnumerateCandidateCenterXs())
            {
                ZoneRestorePlacement placement = PredictRestorePlacement(candidateCenterX, mode);
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

                if (!MatchesPlacementMode(mode, pylonTopLeft))
                    continue;

                if (!MatchesPylonBiome(pylonTopLeft))
                    continue;

                validCandidates.Add((placement, candidateCenterX));
            }

            if (validCandidates.Count == 0)
                return false;

            int bestScore = int.MinValue;
            var scoredCandidates = new List<(ZoneRestorePlacement placement, int score)>(validCandidates.Count);
            foreach (var candidate in validCandidates)
            {
                int score = ScoreBiomeCentrality(candidate.centerX, mode);
                scoredCandidates.Add((candidate.placement, score));
                bestScore = Math.Max(bestScore, score);
            }

            int scoreThreshold = bestScore <= 0
                ? bestScore
                : Math.Max(1, (int)Math.Ceiling(bestScore * 0.8f));

            var preferredCandidates = scoredCandidates
                .Where(candidate => candidate.score >= scoreThreshold)
                .ToList();

            int chosenIndex = (WorldGen.genRand ?? Main.rand).Next(preferredCandidates.Count);
            Zone.RestoreToPlacement(preferredCandidates[chosenIndex].placement, "[BiomeDowser]");
            return true;
        }

        private ZoneRestorePlacement PredictRestorePlacement(int targetCenterX, BiomeDowserPlacementMode mode)
        {
            return mode == BiomeDowserPlacementMode.Floating
                ? PredictFloatingPlacement(targetCenterX)
                : Zone.PredictRestorePlacement(targetCenterX, GetGroundSearchStartY(mode));
        }

        private ZoneRestorePlacement PredictFloatingPlacement(int targetCenterX)
        {
            int deltaX = targetCenterX - Zone.CenterX;
            int targetPylonY = Math.Clamp(
                (int)Math.Round(Main.worldSurface + FloatingYOffsetFromSurface),
                48,
                Math.Max(48, (int)Main.worldSurface - 24));
            Point16 currentPylon = GetPylonTopLeft();
            int deltaY = targetPylonY - currentPylon.Y;

            Point16 newTopLeft = new Point16(
                (short)(Zone.TopLeft.X + deltaX),
                (short)(Zone.TopLeft.Y + deltaY));
            Point16 newBottomRight = new Point16(
                (short)(Zone.BottomRight.X + deltaX),
                (short)(Zone.BottomRight.Y + deltaY));

            return new ZoneRestorePlacement(newTopLeft, newBottomRight, deltaX, deltaY, newBottomRight.Y);
        }

        private int ScoreBiomeCentrality(int candidateCenterX, BiomeDowserPlacementMode mode)
        {
            return ScoreBiomeSpanDirection(candidateCenterX, mode, -1)
                + ScoreBiomeSpanDirection(candidateCenterX, mode, 1);
        }

        private int ScoreBiomeSpanDirection(int candidateCenterX, BiomeDowserPlacementMode mode, int direction)
        {
            int sampleStep = GetCandidateStep();
            const int maxSamplesPerSide = 12;

            int score = 0;
            for (int sampleIndex = 1; sampleIndex <= maxSamplesPerSide; sampleIndex++)
            {
                int neighborCenterX = candidateCenterX + (direction * sampleStep * sampleIndex);
                ZoneRestorePlacement placement = PredictRestorePlacement(neighborCenterX, mode);
                if (!IsPlacementUsable(placement))
                    break;

                Point16 pylonTopLeft = new Point16(
                    (short)(placement.TopLeft.X + PylonOffset.X),
                    (short)(placement.TopLeft.Y + PylonOffset.Y));

                if (!MatchesPlacementMode(mode, pylonTopLeft) || !MatchesPylonBiome(pylonTopLeft))
                    break;

                score += maxSamplesPerSide - sampleIndex + 1;
            }

            return score;
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

        private int GetCandidateStep()
        {
            return PylonType == TeleportPylonType.GlowingMushroom ? 4 : 16;
        }

        private IEnumerable<int> EnumerateCandidateCenterXs()
        {
            int candidateStep = GetCandidateStep();
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

        private int GetGroundSearchStartY(BiomeDowserPlacementMode mode)
        {
            return mode == BiomeDowserPlacementMode.Underground
                ? Math.Max(10, (int)Main.worldSurface + 16)
                : 0;
        }

        private static bool MatchesPlacementMode(BiomeDowserPlacementMode mode, Point16 pylonTopLeft)
        {
            return mode switch
            {
                BiomeDowserPlacementMode.Underground => pylonTopLeft.Y > Main.worldSurface,
                BiomeDowserPlacementMode.Floating => pylonTopLeft.Y < Main.worldSurface - 24,
                _ => pylonTopLeft.Y <= Main.worldSurface,
            };
        }

        private static IEnumerable<Point16> EnumerateFootprintTiles(Point16 pylonTopLeft)
        {
            Tile tile = Framing.GetTileSafely(pylonTopLeft.X, pylonTopLeft.Y);
            TileObjectData data = TileObjectData.GetTileData(tile);
            if (data == null)
            {
                yield return pylonTopLeft;
                yield break;
            }

            for (int x = pylonTopLeft.X; x < pylonTopLeft.X + data.Width; x++)
            {
                for (int y = pylonTopLeft.Y; y < pylonTopLeft.Y + data.Height; y++)
                    yield return new Point16((short)x, (short)y);
            }
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

        private bool MatchesPylonBiome(Point16 pylonTopLeft)
        {
            if (PylonType == TeleportPylonType.GlowingMushroom)
                return MatchesGlowingMushroomBiome(pylonTopLeft);

            if (!TryScanSceneMetricsAt(pylonTopLeft, out SceneMetrics sceneMetrics))
                return false;

            return PylonType switch
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
                TeleportPylonType.Victory => true,
                _ => false,
            };
        }

        private bool MatchesGlowingMushroomBiome(Point16 pylonTopLeft)
        {
            if (Main.remixWorld && pylonTopLeft.Y >= Main.maxTilesY - 200)
                return false;

            (int dx, int dy)[] scanOffsets =
            {
                (0, 0),
                (1, 2),
                (-8, 2),
                (8, 2),
                (-14, 5),
                (14, 5),
                (0, 8),
            };

            foreach ((int dx, int dy) in scanOffsets)
            {
                var scanTile = new Point16(
                    (short)(pylonTopLeft.X + dx),
                    (short)(pylonTopLeft.Y + dy));

                if (TryScanSceneMetricsAt(scanTile, out SceneMetrics sceneMetrics) &&
                    sceneMetrics.EnoughTilesForGlowingMushroom)
                {
                    return true;
                }
            }

            Rectangle scanArea = GetSceneMetricsTileArea(pylonTopLeft);
            int nearbyMushroomTiles = CountNearbyGlowingMushroomTiles(scanArea);
            if (nearbyMushroomTiles >= GlowingMushroomTileThreshold)
                return true;

            int savedMushroomTiles = CountSavedGlowingMushroomTiles(scanArea, pylonTopLeft);
            return nearbyMushroomTiles + savedMushroomTiles >= GlowingMushroomTileThreshold;
        }

        private static bool TryScanSceneMetricsAt(Point16 scanTile, out SceneMetrics sceneMetrics)
        {
            sceneMetrics = null;
            if (!WorldGen.InWorld(scanTile.X, scanTile.Y, 10))
                return false;

            sceneMetrics = new SceneMetrics();
            sceneMetrics.ScanAndExportToMain(new SceneMetricsScanSettings
            {
                BiomeScanCenterPositionInWorld = scanTile.ToWorldCoordinates(),
            });
            return true;
        }

        private static Rectangle GetSceneMetricsTileArea(Point16 centerTile)
        {
            int halfWidth = ApproximateSceneMetricsZoneScanSize.X / 2;
            int halfHeight = ApproximateSceneMetricsZoneScanSize.Y / 2;

            int left = Math.Max(0, centerTile.X - halfWidth);
            int right = Math.Min(Main.maxTilesX - 1, centerTile.X + halfWidth);
            int top = Math.Max(0, centerTile.Y - halfHeight);
            int bottom = Math.Min(Main.maxTilesY - 1, centerTile.Y + halfHeight);

            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private static int CountNearbyGlowingMushroomTiles(Rectangle scanArea)
        {
            int count = 0;

            for (int x = scanArea.Left; x < scanArea.Right; x++)
            {
                for (int y = scanArea.Top; y < scanArea.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.IsActuated)
                        continue;

                    if (IsGlowingMushroomBiomeTile(tile.TileType))
                        count++;
                }
            }

            return count;
        }

        private int CountSavedGlowingMushroomTiles(Rectangle scanArea, Point16 restoredPylonTopLeft)
        {
            int count = 0;
            Point16 restoredZoneTopLeft = new Point16(
                (short)(restoredPylonTopLeft.X - PylonOffset.X),
                (short)(restoredPylonTopLeft.Y - PylonOffset.Y));

            foreach (var kv in Zone.Tiles)
            {
                AnchoredTileData tileData = kv.Value;
                if (!tileData.Active || !IsGlowingMushroomBiomeTile(tileData.TileType))
                    continue;

                Point16 relativeTile = new Point16(
                    (short)(kv.Key.X - Zone.TopLeft.X),
                    (short)(kv.Key.Y - Zone.TopLeft.Y));
                Point16 translatedTile = new Point16(
                    (short)(restoredZoneTopLeft.X + relativeTile.X),
                    (short)(restoredZoneTopLeft.Y + relativeTile.Y));

                if (scanArea.Contains(translatedTile.X, translatedTile.Y))
                    count++;
            }

            return count;
        }

        private static bool IsGlowingMushroomBiomeTile(ushort tileType)
        {
            return tileType == TileID.MushroomGrass
                || tileType == TileID.MushroomPlants
                || tileType == TileID.MushroomTrees
                || tileType == VanillaMushroomVinesTileId;
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

    public class BiomeDowserSystem : ModSystem
    {
        public static readonly Dictionary<int, BiomeDowserZone> Zones = new();
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
            foreach (BiomeDowserZone zone in Zones.Values)
                zone.RefreshChestSnapshots();
        }

        public static void RestoreAllZones(bool announce = true)
        {
            LastRestoreTransforms.Clear();
            if (Zones.Count == 0)
                return;

            var reservedBounds = new List<ZoneTileBounds>();
            foreach (BuildingZone structureZone in StructureAnchorSystem.Zones.Values)
                reservedBounds.Add(new ZoneTileBounds(structureZone.TopLeft, structureZone.BottomRight));

            int placedCount = 0;
            foreach (var kv in Zones.OrderBy(kv => kv.Key))
            {
                BiomeDowserZone zone = kv.Value;
                Point16 oldTopLeft = zone.Zone.TopLeft;
                Point16 oldBottomRight = zone.Zone.BottomRight;

                if (!zone.TryRestoreToMatchingBiome(reservedBounds, out string failureReason))
                {
                    ModContent.GetInstance<DynamicWorlds>().Logger.Warn($"[BiomeDowser] {failureReason} Falling back to the original X position.");
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
                    $"[BiomeDowser] Re-registered {restoredPylons} restored vanilla pylon(s).");
            }

            if (announce && Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText(
                    $"Restored {placedCount} Biome Dowser zone{(placedCount == 1 ? "" : "s")}.",
                    255,
                    215,
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
            foreach (BiomeDowserZone zone in Zones.Values)
                list.Add(zone.ToTag());

            tag["BiomeDowserZones"] = list;
            tag["BiomeDowserNextId"] = _nextId;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Zones.Clear();
            _nextId = 1;

            if (tag.ContainsKey("BiomeDowserZones"))
            {
                foreach (TagCompound zoneTag in tag.GetList<TagCompound>("BiomeDowserZones"))
                {
                    BiomeDowserZone zone = BiomeDowserZone.FromTag(zoneTag);
                    Zones[zone.Id] = zone;
                }
            }

            if (tag.ContainsKey("BiomeDowserNextId"))
                _nextId = tag.GetInt("BiomeDowserNextId");

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

            foreach (BiomeDowserZone zone in Zones.Values)
            {
                WorldToolOverlayHelper.DrawAreaOverlay(
                    spriteBatch,
                    zone.TopLeft,
                    zone.BottomRight,
                    screenPos,
                    new Color(255, 210, 90) * 0.32f,
                    new Color(255, 180, 70));
            }

            BiomeDowserPlayer modPlayer = player.GetModPlayer<BiomeDowserPlayer>();
            if (modPlayer.IsDragging)
            {
                int x0 = Math.Min(modPlayer.DragStart.X, modPlayer.DragEnd.X);
                int x1 = Math.Max(modPlayer.DragStart.X, modPlayer.DragEnd.X);
                int y0 = Math.Min(modPlayer.DragStart.Y, modPlayer.DragEnd.Y);
                int y1 = Math.Max(modPlayer.DragStart.Y, modPlayer.DragEnd.Y);

                WorldToolOverlayHelper.DrawAreaOverlay(
                    spriteBatch,
                    new Point16((short)x0, (short)y0),
                    new Point16((short)x1, (short)y1),
                    screenPos,
                    new Color(255, 225, 140) * 0.36f,
                    Color.Gold);
            }

            spriteBatch.End();
        }
    }

    public class BiomeDowserPlayer : ModPlayer
    {
        public bool IsDragging;
        public Point16 DragStart;
        public Point16 DragEnd;
        public BiomeDowserPlacementMode PlacementMode = BiomeDowserPlacementMode.Surface;

        private bool _wasLeftMouseHeldLastFrame;
        private bool _wasRightMouseHeldLastFrame;

        public override void PostUpdate()
        {
            if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
            {
                if (IsDragging)
                    CancelDrag();

                _wasLeftMouseHeldLastFrame = false;
                _wasRightMouseHeldLastFrame = false;
                return;
            }

            bool holding = Player.HeldItem?.type == ModContent.ItemType<BiomeDowser>();
            if (!holding)
            {
                if (IsDragging)
                    CancelDrag();

                _wasLeftMouseHeldLastFrame = false;
                _wasRightMouseHeldLastFrame = false;
                return;
            }

            bool leftMouseHeld = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
            bool rightMouseHeld = Main.mouseRight && !Main.LocalPlayer.mouseInterface;
            bool shiftHeld = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            int tileX = (int)(Main.MouseWorld.X / 16f);
            int tileY = (int)(Main.MouseWorld.Y / 16f);
            Point16 mouseTile = new Point16(tileX, tileY);

            if (rightMouseHeld && !_wasRightMouseHeldLastFrame && !IsDragging)
            {
                TogglePlacementMode();
                _wasRightMouseHeldLastFrame = true;
                return;
            }

            if (shiftHeld && leftMouseHeld && !_wasLeftMouseHeldLastFrame && !Main.LocalPlayer.mouseInterface)
            {
                RemoveZoneAtPosition(mouseTile);
                _wasLeftMouseHeldLastFrame = true;
                _wasRightMouseHeldLastFrame = rightMouseHeld;
                return;
            }

            if (leftMouseHeld)
            {
                if (!_wasLeftMouseHeldLastFrame)
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
            else if (_wasLeftMouseHeldLastFrame && IsDragging)
            {
                SoundEngine.PlaySound(SoundID.Item4, Player.position);
                IsDragging = false;
                CommitZone();
            }

            _wasLeftMouseHeldLastFrame = leftMouseHeld;
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
                Main.NewText("Drag a larger area so the full pylon structure is inside the Biome Dowser zone.", 255, 210, 100);
                return;
            }

            var topLeft = new Point16((short)x0, (short)y0);
            var bottomRight = new Point16((short)x1, (short)y1);

            foreach (var kv in StructureAnchorSystem.Zones)
            {
                ZoneTileBounds other = new ZoneTileBounds(kv.Value.TopLeft, kv.Value.BottomRight);
                if (new ZoneTileBounds(topLeft, bottomRight).Overlaps(other))
                {
                    Main.NewText($"Biome Dowser zone overlaps with structure zone #{kv.Key}. Zones cannot share tiles.", 255, 120, 120);
                    return;
                }
            }

            foreach (var kv in BiomeDowserSystem.Zones)
            {
                ZoneTileBounds other = new ZoneTileBounds(kv.Value.TopLeft, kv.Value.BottomRight);
                if (new ZoneTileBounds(topLeft, bottomRight).Overlaps(other))
                {
                    Main.NewText($"Biome Dowser zone overlaps with Biome Dowser zone #{kv.Key}. Zones cannot share tiles.", 255, 120, 120);
                    return;
                }
            }

            if (StructureAnchorSystem.TryFindOverlappingAnchoredTile(topLeft, bottomRight, out Point16 anchoredOverlap))
            {
                Main.NewText(
                    $"Biome Dowser zones cannot overlap individually anchored tiles. Remove the anchor at ({anchoredOverlap.X}, {anchoredOverlap.Y}) first.",
                    255,
                    120,
                    120);
                return;
            }

            int zoneId = BiomeDowserSystem.NextId();
            BiomeDowserPlacementMode requestedMode = PlacementMode;
            if (!BiomeDowserZone.TryCapture(topLeft, bottomRight, zoneId, requestedMode, out BiomeDowserZone zone, out string errorMessage))
            {
                Main.NewText(errorMessage, 255, 200, 80);
                return;
            }

            BiomeDowserSystem.Zones[zoneId] = zone;

            if (zone.PlacementMode != requestedMode)
            {
                Main.NewText(
                    $"{zone.PylonType} pylons don't support {BiomeDowserPlacementHelper.GetLabel(requestedMode)} placement. Using {BiomeDowserPlacementHelper.GetLabel(zone.PlacementMode)} instead.",
                    255,
                    215,
                    120);
            }

            Main.NewText(
                $"Biome Dowser zone #{zoneId} created for a {zone.PylonType} pylon ({BiomeDowserPlacementHelper.GetLabel(zone.PlacementMode)} preferred): {zone.Width}x{zone.Height}.",
                255,
                215,
                120);
        }

        private void RemoveZoneAtPosition(Point16 clickPos)
        {
            int zoneIdToRemove = -1;
            foreach (var kv in BiomeDowserSystem.Zones)
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
                if (BiomeDowserSystem.Zones.Remove(zoneIdToRemove))
                {
                    SoundEngine.PlaySound(SoundID.Item14, Player.position);
                    Main.NewText(
                        $"Biome Dowser zone #{zoneIdToRemove} removed. ({BiomeDowserSystem.Zones.Count} zones remain)",
                        255,
                        180,
                        120);
                }

                return;
            }

            Main.NewText("Shift+Click on a Biome Dowser zone to remove it.", 255, 220, 120);
        }

        public void CancelDrag()
        {
            IsDragging = false;
            _wasLeftMouseHeldLastFrame = false;
            _wasRightMouseHeldLastFrame = false;
        }

        private void TogglePlacementMode()
        {
            PlacementMode = PlacementMode switch
            {
                BiomeDowserPlacementMode.Surface => BiomeDowserPlacementMode.Underground,
                BiomeDowserPlacementMode.Underground => BiomeDowserPlacementMode.Floating,
                _ => BiomeDowserPlacementMode.Surface,
            };

            Main.NewText(
                $"Biome Dowser now prefers {BiomeDowserPlacementHelper.GetLabel(PlacementMode)} placement when the selected pylon supports it.",
                255,
                220,
                120);
        }
    }

    public class BiomeDowser : ModItem
    {
        public override string Texture => "DynamicWorlds/BiomeDowser";

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
            tooltips.Add(new TooltipLine(Mod, "BDInfo1",
                "Left-click and drag to project a biome-aware pylon structure zone.")
                { OverrideColor = Color.Gold });
            tooltips.Add(new TooltipLine(Mod, "BDInfo2",
                "Each zone must fully contain exactly one vanilla pylon.")
                { OverrideColor = Color.LightGoldenrodYellow });
            tooltips.Add(new TooltipLine(Mod, "BDInfo3",
                "Only tiles connected to that pylon inside the selection are saved and moved.")
                { OverrideColor = Color.LightSkyBlue });
            tooltips.Add(new TooltipLine(Mod, "BDInfo4",
                "Right-click while holding to cycle surface, underground, or floating placement preferences.")
                { OverrideColor = Color.LightGoldenrodYellow });
            tooltips.Add(new TooltipLine(Mod, "BDInfo5",
                "On regen, the saved structure is rebuilt in a biome where that pylon can function.")
                { OverrideColor = Color.LightSkyBlue });
            tooltips.Add(new TooltipLine(Mod, "BDInfo6",
                "Biome Dowser zones cannot overlap structure zones or individually anchored tiles.")
                { OverrideColor = Color.Orange });
            tooltips.Add(new TooltipLine(Mod, "BDInfo7",
                "Shift+Click inside a Biome Dowser zone to remove it.")
                { OverrideColor = Color.LightBlue });
            tooltips.Add(new TooltipLine(Mod, "BDInfo8",
                "Hold any world tool to see anchors, erasures, structure zones, and Biome Dowser zones.")
                { OverrideColor = Color.LightSkyBlue });

            BiomeDowserPlayer modPlayer = Main.LocalPlayer?.GetModPlayer<BiomeDowserPlayer>();
            string modeLabel = BiomeDowserPlacementHelper.GetLabel(
                modPlayer?.PlacementMode ?? BiomeDowserPlacementMode.Surface);
            tooltips.Add(new TooltipLine(Mod, "BDInfoPreference",
                $"Current placement preference: {modeLabel}")
                { OverrideColor = Color.Gold });

            int zoneCount = BiomeDowserSystem.Zones.Count;
            if (zoneCount > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "BDZoneCount",
                    $"World has {zoneCount} Biome Dowser zone{(zoneCount == 1 ? "" : "s")}")
                    { OverrideColor = Color.Gold });
            }
        }
    }
}
