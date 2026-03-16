using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ObjectData;

namespace DynamicWorlds
{
    internal static class PylonRestoreHelper
    {
        public static int RestoreTrackedVanillaPylons(bool forceRefresh = false)
        {
            var candidateTiles = AnchoredTileSystem.AnchoredTiles.Keys
                .Concat(StructureAnchorSystem.Zones.Values.SelectMany(zone => zone.Tiles.Keys));

            return RestoreVanillaPylons(candidateTiles, forceRefresh);
        }

        public static int RestoreVanillaPylons(IEnumerable<Point16> candidateTiles)
        {
            return RestoreVanillaPylons(candidateTiles, forceRefresh: false);
        }

        public static int RestoreVanillaPylons(IEnumerable<Point16> candidateTiles, bool forceRefresh)
        {
            if (candidateTiles == null)
            {
                if (forceRefresh)
                    Main.PylonSystem.RequestImmediateUpdate();

                return 0;
            }

            var processedTopLefts = new HashSet<Point16>();
            int restoredCount = 0;

            foreach (var pos in candidateTiles)
            {
                if (!WorldGen.InWorld(pos.X, pos.Y, 1))
                    continue;

                Tile tile = Framing.GetTileSafely(pos.X, pos.Y);
                if (!tile.HasTile || tile.TileType != TileID.TeleportationPylon)
                    continue;

                Point16 topLeft = TileObjectData.TopLeft(pos.X, pos.Y);
                if (!processedTopLefts.Add(topLeft))
                    continue;

                if (!HasCompletePylonFootprint(topLeft))
                    continue;

                if (TileEntity.ByPosition.TryGetValue(topLeft, out var existingEntity))
                {
                    if (existingEntity is TETeleportationPylon)
                        continue;

                    lock (TileEntity.EntityCreationLock)
                    {
                        TileEntity.ByID.Remove(existingEntity.ID);
                        TileEntity.ByPosition.Remove(topLeft);
                    }
                }

                TETeleportationPylon.Place(topLeft.X, topLeft.Y);
                restoredCount++;
            }

            if (restoredCount > 0 || forceRefresh)
                Main.PylonSystem.RequestImmediateUpdate();

            return restoredCount;
        }

        private static bool HasCompletePylonFootprint(Point16 topLeft)
        {
            if (!WorldGen.InWorld(topLeft.X, topLeft.Y, 1))
                return false;

            Tile topLeftTile = Framing.GetTileSafely(topLeft.X, topLeft.Y);
            if (!topLeftTile.HasTile || topLeftTile.TileType != TileID.TeleportationPylon)
                return false;

            TileObjectData tileData = TileObjectData.GetTileData(topLeftTile);
            if (tileData == null)
                return false;

            for (int x = topLeft.X; x < topLeft.X + tileData.Width; x++)
            {
                for (int y = topLeft.Y; y < topLeft.Y + tileData.Height; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        return false;

                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != TileID.TeleportationPylon)
                        return false;
                }
            }

            return true;
        }
    }
}
