using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DynamicWorlds
{
    public sealed class CalamityProgressSnapshot
    {
        public bool loaded;

        public Dictionary<string, bool> downedFlags = new();
        public Dictionary<string, bool> recipeUnlocks = new();

        public bool revenge;
        public bool death;
        public bool armageddon;

        public bool spawnedBandit;
        public bool foundHomePermafrost;
        public bool catName;
        public bool dogName;
        public bool bunnyName;
        public bool talkedToDraedon;
        public bool draedonMechdusa;
        public bool isWorldAfterDraedonUpdate;

        public bool acidRainActive;
        public int acidRainKillPoints;
        public bool acidRainHasTriedToSummonOldDuke;
        public bool acidRainHasStartedAcidicDownpour;
        public bool acidRainHasBeenForceStartedByEoCDefeat;
        public bool acidRainOldDukeHasBeenEncountered;
        public int acidRainCountdownUntilForced;
        public int acidRainTimeSinceLastKill;
        public int acidRainTimeSinceEventStarted;

        public bool bossRushActive;
        public int bossRushStage;
        public int bossRushSpawnCountdown;
        public int bossRushStartTimer;
        public int bossRushEndTimer;
        public int bossRushHostileProjectileKillCounter;

        public int draedonSummonCountdown;
        public int draedonMechToSummon;
        public Vector2 draedonSummonPosition;

        public int moneyStolenByBandit;
        public int reforges;

        public bool hadAstralBiome;
        public bool hadLuminitePlanetoids;

        public CalamityProgressSnapshot Clone()
        {
            return new CalamityProgressSnapshot
            {
                loaded = loaded,
                downedFlags = new Dictionary<string, bool>(downedFlags),
                recipeUnlocks = new Dictionary<string, bool>(recipeUnlocks),
                revenge = revenge,
                death = death,
                armageddon = armageddon,
                spawnedBandit = spawnedBandit,
                foundHomePermafrost = foundHomePermafrost,
                catName = catName,
                dogName = dogName,
                bunnyName = bunnyName,
                talkedToDraedon = talkedToDraedon,
                draedonMechdusa = draedonMechdusa,
                isWorldAfterDraedonUpdate = isWorldAfterDraedonUpdate,
                acidRainActive = acidRainActive,
                acidRainKillPoints = acidRainKillPoints,
                acidRainHasTriedToSummonOldDuke = acidRainHasTriedToSummonOldDuke,
                acidRainHasStartedAcidicDownpour = acidRainHasStartedAcidicDownpour,
                acidRainHasBeenForceStartedByEoCDefeat = acidRainHasBeenForceStartedByEoCDefeat,
                acidRainOldDukeHasBeenEncountered = acidRainOldDukeHasBeenEncountered,
                acidRainCountdownUntilForced = acidRainCountdownUntilForced,
                acidRainTimeSinceLastKill = acidRainTimeSinceLastKill,
                acidRainTimeSinceEventStarted = acidRainTimeSinceEventStarted,
                bossRushActive = bossRushActive,
                bossRushStage = bossRushStage,
                bossRushSpawnCountdown = bossRushSpawnCountdown,
                bossRushStartTimer = bossRushStartTimer,
                bossRushEndTimer = bossRushEndTimer,
                bossRushHostileProjectileKillCounter = bossRushHostileProjectileKillCounter,
                draedonSummonCountdown = draedonSummonCountdown,
                draedonMechToSummon = draedonMechToSummon,
                draedonSummonPosition = draedonSummonPosition,
                moneyStolenByBandit = moneyStolenByBandit,
                reforges = reforges,
                hadAstralBiome = hadAstralBiome,
                hadLuminitePlanetoids = hadLuminitePlanetoids,
            };
        }
    }

    internal static class CalamityCompat
    {
        private const BindingFlags StaticMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly string[] DownedFlagNames =
        {
            "downedDesertScourge",
            "downedCrabulon",
            "downedHiveMind",
            "downedPerforator",
            "downedSlimeGod",
            "downedDreadnautilus",
            "downedCryogen",
            "downedAquaticScourge",
            "downedBrimstoneElemental",
            "downedCalamitasClone",
            "downedLeviathan",
            "downedAstrumAureus",
            "downedBetsy",
            "downedPlaguebringer",
            "downedRavager",
            "downedAstrumDeus",
            "downedGuardians",
            "downedDragonfolly",
            "downedProvidence",
            "downedCeaselessVoid",
            "downedStormWeaver",
            "downedSignus",
            "downedSecondSentinels",
            "downedPolterghast",
            "downedBoomerDuke",
            "downedDoG",
            "downedYharon",
            "downedAres",
            "downedThanatos",
            "downedArtemisAndApollo",
            "downedExoMechs",
            "downedCalamitas",
            "downedPrimordialWyrm",
            "downedGSS",
            "downedCLAM",
            "downedCLAMHardMode",
            "downedCragmawMire",
            "downedMauler",
            "downedNuclearTerror",
            "downedEoCAcidRain",
            "downedAquaticScourgeAcidRain",
            "startedBossRushAtLeastOnce",
            "downedBossRush",
        };

        private static readonly string[] RecipeUnlockNames =
        {
            "HasUnlockedT1ArsenalRecipes",
            "HasUnlockedT2ArsenalRecipes",
            "HasUnlockedT3ArsenalRecipes",
            "HasUnlockedT4ArsenalRecipes",
            "HasUnlockedT5ArsenalRecipes",
            "HasFoundSunkenSeaSchematic",
            "HasFoundPlanetoidSchematic",
            "HasFoundJungleSchematic",
            "HasFoundHellSchematic",
            "HasFoundIceSchematic",
        };

        private static readonly string[] AstralTileNames =
        {
            "AstralOre",
            "AstralSand",
            "AstralSandstone",
            "HardenedAstralSand",
            "AstralIce",
            "AstralDirt",
            "AstralStone",
            "AstralGrass",
            "NovaeSlag",
            "CelestialRemains",
            "AstralSnow",
            "AstralClay",
        };

        private static bool _resolved;
        private static bool _available;
        private static Mod _calamity;
        private static Type _downedBossSystemType;
        private static Type _calamityWorldType;
        private static Type _acidRainEventType;
        private static Type _bossRushEventType;
        private static Type _astralBiomeType;
        private static Type _luminitePlanetType;
        private static Type _recipeUnlockHandlerType;
        private static Type _globalSaveDataSystemType;
        private static HashSet<ushort> _astralTileTypes;
        private static ushort? _exodiumOreTileType;

        public static CalamityProgressSnapshot Capture()
        {
            if (!Resolve())
                return null;

            try
            {
                var snapshot = new CalamityProgressSnapshot
                {
                    loaded = true,
                    revenge = GetDifficultyActive("revengeance"),
                    death = GetDifficultyActive("death"),
                    armageddon = GetDifficultyActive("armageddon"),
                    spawnedBandit = GetStaticBool(_calamityWorldType, "spawnedBandit"),
                    foundHomePermafrost = GetStaticBool(_calamityWorldType, "foundHomePermafrost"),
                    catName = GetStaticBool(_calamityWorldType, "catName"),
                    dogName = GetStaticBool(_calamityWorldType, "dogName"),
                    bunnyName = GetStaticBool(_calamityWorldType, "bunnyName"),
                    talkedToDraedon = GetStaticBool(_calamityWorldType, "TalkedToDraedon"),
                    draedonMechdusa = GetStaticBool(_calamityWorldType, "DraedonMechdusa"),
                    isWorldAfterDraedonUpdate = GetStaticBool(_calamityWorldType, "IsWorldAfterDraedonUpdate"),
                    acidRainActive = GetStaticBool(_acidRainEventType, "AcidRainEventIsOngoing"),
                    acidRainKillPoints = GetStaticInt(_acidRainEventType, "AccumulatedKillPoints"),
                    acidRainHasTriedToSummonOldDuke = GetStaticBool(_acidRainEventType, "HasTriedToSummonOldDuke"),
                    acidRainHasStartedAcidicDownpour = GetStaticBool(_acidRainEventType, "HasStartedAcidicDownpour"),
                    acidRainHasBeenForceStartedByEoCDefeat = GetStaticBool(_acidRainEventType, "HasBeenForceStartedByEoCDefeat"),
                    acidRainOldDukeHasBeenEncountered = GetStaticBool(_acidRainEventType, "OldDukeHasBeenEncountered"),
                    acidRainCountdownUntilForced = GetStaticInt(_acidRainEventType, "CountdownUntilForcedAcidRain"),
                    acidRainTimeSinceLastKill = GetStaticInt(_acidRainEventType, "TimeSinceLastAcidRainKill"),
                    acidRainTimeSinceEventStarted = GetStaticInt(_acidRainEventType, "TimeSinceEventStarted"),
                    bossRushActive = GetStaticBool(_bossRushEventType, "BossRushActive"),
                    bossRushStage = GetStaticInt(_bossRushEventType, "BossRushStage"),
                    bossRushSpawnCountdown = GetStaticInt(_bossRushEventType, "BossRushSpawnCountdown"),
                    bossRushStartTimer = GetStaticInt(_bossRushEventType, "StartTimer"),
                    bossRushEndTimer = GetStaticInt(_bossRushEventType, "EndTimer"),
                    bossRushHostileProjectileKillCounter = GetStaticInt(_bossRushEventType, "HostileProjectileKillCounter"),
                    draedonSummonCountdown = GetStaticInt(_calamityWorldType, "DraedonSummonCountdown"),
                    draedonMechToSummon = GetStaticEnumValueAsInt(_calamityWorldType, "DraedonMechToSummon"),
                    draedonSummonPosition = GetStaticVector2(_calamityWorldType, "DraedonSummonPosition"),
                    moneyStolenByBandit = GetStaticInt(_calamityWorldType, "MoneyStolenByBandit"),
                    reforges = GetStaticInt(_calamityWorldType, "Reforges"),
                    hadAstralBiome = WorldHasAstralContent(),
                    hadLuminitePlanetoids = WorldHasLuminitePlanetoids(),
                };

                foreach (string flagName in DownedFlagNames)
                    snapshot.downedFlags[flagName] = GetStaticBool(_downedBossSystemType, flagName);

                foreach (string flagName in RecipeUnlockNames)
                    snapshot.recipeUnlocks[flagName] = GetStaticBool(_recipeUnlockHandlerType, flagName);

                return snapshot;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<DynamicWorlds>().Logger.Warn($"[CalamityCompat] Failed to capture Calamity state: {ex}");
                return null;
            }
        }

        public static void Apply(CalamityProgressSnapshot snapshot)
        {
            if (!Resolve() || snapshot == null || !snapshot.loaded)
                return;

            try
            {
                ApplyDownedFlags(snapshot);
                ApplyRecipeUnlocks(snapshot);
                ApplyModes(snapshot);
                ApplyPersistentWorldFlags(snapshot);
                ApplyOngoingEvents(snapshot);
                ReplayWorldChanges(snapshot);
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<DynamicWorlds>().Logger.Warn($"[CalamityCompat] Failed to apply Calamity state: {ex}");
            }
        }

        private static void ApplyDownedFlags(CalamityProgressSnapshot snapshot)
        {
            foreach (KeyValuePair<string, bool> kv in snapshot.downedFlags)
                SetStaticBool(_downedBossSystemType, kv.Key, kv.Value);
        }

        private static void ApplyRecipeUnlocks(CalamityProgressSnapshot snapshot)
        {
            foreach (KeyValuePair<string, bool> kv in snapshot.recipeUnlocks)
                SetStaticBool(_recipeUnlockHandlerType, kv.Key, kv.Value);
        }

        private static void ApplyModes(CalamityProgressSnapshot snapshot)
        {
            SetDifficultyActive("revengeance", snapshot.revenge);
            SetDifficultyActive("death", snapshot.death);
            SetDifficultyActive("armageddon", snapshot.armageddon);
        }

        private static void ApplyPersistentWorldFlags(CalamityProgressSnapshot snapshot)
        {
            // These are progression/meta flags that should survive regen.
            // Calamity's location metadata for the Abyss, Sulphurous Sea, labs, and Astral Y start
            // intentionally stays on the freshly generated world so it continues to match the new terrain.
            SetStaticBool(_calamityWorldType, "spawnedBandit", snapshot.spawnedBandit);
            SetStaticBool(_calamityWorldType, "foundHomePermafrost", snapshot.foundHomePermafrost);
            SetStaticBool(_calamityWorldType, "catName", snapshot.catName);
            SetStaticBool(_calamityWorldType, "dogName", snapshot.dogName);
            SetStaticBool(_calamityWorldType, "bunnyName", snapshot.bunnyName);
            SetStaticBool(_calamityWorldType, "TalkedToDraedon", snapshot.talkedToDraedon);
            SetStaticBool(_calamityWorldType, "DraedonMechdusa", snapshot.draedonMechdusa);
            SetStaticBool(_calamityWorldType, "IsWorldAfterDraedonUpdate", snapshot.isWorldAfterDraedonUpdate);
            SetStaticInt(_calamityWorldType, "MoneyStolenByBandit", snapshot.moneyStolenByBandit);
            SetStaticInt(_calamityWorldType, "Reforges", snapshot.reforges);
            SetStaticInt(_calamityWorldType, "DraedonSummonCountdown", snapshot.draedonSummonCountdown);
            SetStaticEnumValueFromInt(_calamityWorldType, "DraedonMechToSummon", snapshot.draedonMechToSummon);
            SetStaticVector2(_calamityWorldType, "DraedonSummonPosition", snapshot.draedonSummonPosition);
        }

        private static void ApplyOngoingEvents(CalamityProgressSnapshot snapshot)
        {
            if (snapshot.acidRainActive)
                EnsureAcidRainWeather();

            SetStaticBool(_acidRainEventType, "AcidRainEventIsOngoing", snapshot.acidRainActive);
            SetStaticInt(_acidRainEventType, "AccumulatedKillPoints", snapshot.acidRainKillPoints);
            SetStaticBool(_acidRainEventType, "HasTriedToSummonOldDuke", snapshot.acidRainHasTriedToSummonOldDuke);
            SetStaticBool(_acidRainEventType, "HasStartedAcidicDownpour", snapshot.acidRainHasStartedAcidicDownpour);
            SetStaticBool(_acidRainEventType, "HasBeenForceStartedByEoCDefeat", snapshot.acidRainHasBeenForceStartedByEoCDefeat);
            SetStaticBool(_acidRainEventType, "OldDukeHasBeenEncountered", snapshot.acidRainOldDukeHasBeenEncountered);
            SetStaticInt(_acidRainEventType, "CountdownUntilForcedAcidRain", snapshot.acidRainCountdownUntilForced);
            SetStaticInt(_acidRainEventType, "TimeSinceLastAcidRainKill", snapshot.acidRainTimeSinceLastKill);
            SetStaticInt(_acidRainEventType, "TimeSinceEventStarted", snapshot.acidRainTimeSinceEventStarted);

            SetStaticBool(_bossRushEventType, "DeactivateStupidFuckingBullshit", snapshot.bossRushActive);
            SetStaticBool(_bossRushEventType, "BossRushActive", snapshot.bossRushActive);
            SetStaticInt(_bossRushEventType, "BossRushStage", Math.Max(0, snapshot.bossRushStage));
            SetStaticInt(_bossRushEventType, "BossRushSpawnCountdown", Math.Max(0, snapshot.bossRushSpawnCountdown));
            SetStaticInt(_bossRushEventType, "StartTimer", Math.Max(0, snapshot.bossRushStartTimer));
            SetStaticInt(_bossRushEventType, "EndTimer", Math.Max(0, snapshot.bossRushEndTimer));
            SetStaticInt(_bossRushEventType, "HostileProjectileKillCounter", Math.Max(0, snapshot.bossRushHostileProjectileKillCounter));
        }

        private static void ReplayWorldChanges(CalamityProgressSnapshot snapshot)
        {
            if (snapshot.hadAstralBiome && !WorldHasAstralContent())
                InvokeStaticMethod(_astralBiomeType, "PlaceAstralMeteor");

            bool currentPlanetoidsFlag = GetStaticBool(_calamityWorldType, "HasGeneratedLuminitePlanetoids");
            bool currentPlanetoidsPresent = currentPlanetoidsFlag || WorldHasLuminitePlanetoids();
            if (snapshot.hadLuminitePlanetoids && !currentPlanetoidsPresent)
            {
                InvokeStaticMethod(_luminitePlanetType, "GenerateLuminitePlanetoids");
                SetStaticBool(_calamityWorldType, "HasGeneratedLuminitePlanetoids", true);
            }
            else if (snapshot.hadLuminitePlanetoids)
            {
                SetStaticBool(_calamityWorldType, "HasGeneratedLuminitePlanetoids", true);
            }
        }

        private static bool Resolve()
        {
            if (_resolved)
                return _available;

            _resolved = true;
            if (!ModLoader.TryGetMod("CalamityMod", out _calamity))
                return false;

            Assembly calamityAssembly = _calamity.GetType().Assembly;
            _downedBossSystemType = calamityAssembly.GetType("CalamityMod.DownedBossSystem");
            _calamityWorldType = calamityAssembly.GetType("CalamityMod.World.CalamityWorld");
            _acidRainEventType = calamityAssembly.GetType("CalamityMod.Events.AcidRainEvent");
            _bossRushEventType = calamityAssembly.GetType("CalamityMod.Events.BossRushEvent");
            _astralBiomeType = calamityAssembly.GetType("CalamityMod.World.AstralBiome");
            _luminitePlanetType = calamityAssembly.GetType("CalamityMod.World.Planets.LuminitePlanet");
            _recipeUnlockHandlerType = calamityAssembly.GetType("CalamityMod.CustomRecipes.RecipeUnlockHandler");
            _globalSaveDataSystemType = calamityAssembly.GetType("CalamityMod.Systems.GlobalSaveDataSystem");

            _available =
                _downedBossSystemType != null &&
                _calamityWorldType != null &&
                _acidRainEventType != null &&
                _bossRushEventType != null &&
                _astralBiomeType != null &&
                _luminitePlanetType != null &&
                _recipeUnlockHandlerType != null;

            if (_available)
                CacheTileTypes();

            return _available;
        }

        private static void CacheTileTypes()
        {
            _astralTileTypes = new HashSet<ushort>();
            foreach (string tileName in AstralTileNames)
            {
                if (_calamity.TryFind(tileName, out ModTile tile))
                    _astralTileTypes.Add((ushort)tile.Type);
            }

            if (_calamity.TryFind("ExodiumOre", out ModTile exodiumOreTile))
                _exodiumOreTileType = (ushort)exodiumOreTile.Type;
        }

        private static bool WorldHasAstralContent()
        {
            if (_astralTileTypes == null || _astralTileTypes.Count == 0)
                return false;

            return CountAnyTiles(_astralTileTypes, 1) > 0;
        }

        private static bool WorldHasLuminitePlanetoids()
        {
            if (!_exodiumOreTileType.HasValue)
                return false;

            return CountTile(_exodiumOreTileType.Value, 1) > 0;
        }

        private static int CountAnyTiles(HashSet<ushort> tileTypes, int stopAfter)
        {
            int count = 0;
            int threshold = Math.Max(1, stopAfter);

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile == null || !tile.HasTile || !tileTypes.Contains(tile.TileType))
                        continue;

                    count++;
                    if (count >= threshold)
                        return count;
                }
            }

            return count;
        }

        private static int CountTile(ushort tileType, int stopAfter)
        {
            int count = 0;
            int threshold = Math.Max(1, stopAfter);

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile == null || !tile.HasTile || tile.TileType != tileType)
                        continue;

                    count++;
                    if (count >= threshold)
                        return count;
                }
            }

            return count;
        }

        private static void EnsureAcidRainWeather()
        {
            Main.raining = true;
            Main.cloudBGActive = 1f;
            Main.numCloudsTemp = Main.maxClouds;
            Main.numClouds = Main.numCloudsTemp;
            Main.windSpeedCurrent = 0.72f;
            Main.windSpeedTarget = Main.windSpeedCurrent;
            Main.weatherCounter = 60 * 60 * 10;
            Main.rainTime = Main.weatherCounter;
            Main.maxRaining = 0.89f;
        }

        private static bool GetDifficultyActive(string difficulty)
        {
            try
            {
                object result = _calamity.Call("GetDifficultyActive", difficulty);
                return result is bool active && active;
            }
            catch
            {
                return difficulty switch
                {
                    "revengeance" => GetStaticBool(_calamityWorldType, "revenge"),
                    "death" => GetStaticBool(_calamityWorldType, "death"),
                    "armageddon" => GetStaticBool(_calamityWorldType, "armageddon"),
                    _ => false,
                };
            }
        }

        private static void SetDifficultyActive(string difficulty, bool enabled)
        {
            bool handled = false;
            try
            {
                object result = _calamity.Call("SetDifficultyActive", difficulty, enabled);
                handled = result is bool;
            }
            catch
            {
            }

            if (handled)
                return;

            switch (difficulty)
            {
                case "revengeance":
                    SetStaticBool(_calamityWorldType, "revenge", enabled);
                    break;
                case "death":
                    SetStaticBool(_calamityWorldType, "death", enabled);
                    break;
                case "armageddon":
                    SetStaticBool(_calamityWorldType, "armageddon", enabled);
                    break;
            }
        }

        private static bool GetStaticBool(Type type, string memberName)
        {
            object value = GetStaticValue(type, memberName);
            return value is bool boolValue && boolValue;
        }

        private static int GetStaticInt(Type type, string memberName)
        {
            object value = GetStaticValue(type, memberName);
            return value is int intValue ? intValue : 0;
        }

        private static int GetStaticEnumValueAsInt(Type type, string memberName)
        {
            object value = GetStaticValue(type, memberName);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static Vector2 GetStaticVector2(Type type, string memberName)
        {
            object value = GetStaticValue(type, memberName);
            return value is Vector2 vector ? vector : Vector2.Zero;
        }

        private static object GetStaticValue(Type type, string memberName)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            PropertyInfo property = type.GetProperty(memberName, StaticMemberFlags);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(null);

            FieldInfo field = type.GetField(memberName, StaticMemberFlags);
            return field?.GetValue(null);
        }

        private static void SetStaticBool(Type type, string memberName, bool value)
        {
            SetStaticValue(type, memberName, value);
        }

        private static void SetStaticInt(Type type, string memberName, int value)
        {
            SetStaticValue(type, memberName, value);
        }

        private static void SetStaticVector2(Type type, string memberName, Vector2 value)
        {
            SetStaticValue(type, memberName, value);
        }

        private static void SetStaticEnumValueFromInt(Type type, string memberName, int value)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
                return;

            PropertyInfo property = type.GetProperty(memberName, StaticMemberFlags);
            if (property != null && property.CanWrite)
            {
                object enumValue = Enum.ToObject(property.PropertyType, value);
                property.SetValue(null, enumValue);
                return;
            }

            FieldInfo field = type.GetField(memberName, StaticMemberFlags);
            if (field == null)
                return;

            object convertedValue = field.FieldType.IsEnum
                ? Enum.ToObject(field.FieldType, value)
                : value;
            field.SetValue(null, convertedValue);
        }

        private static void SetStaticValue(Type type, string memberName, object value)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
                return;

            PropertyInfo property = type.GetProperty(memberName, StaticMemberFlags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, value);
                return;
            }

            FieldInfo field = type.GetField(memberName, StaticMemberFlags);
            if (field != null)
                field.SetValue(null, value);
        }

        private static void InvokeStaticMethod(Type type, string methodName)
        {
            if (type == null || string.IsNullOrWhiteSpace(methodName))
                return;

            MethodInfo method = type.GetMethod(methodName, StaticMemberFlags, null, Type.EmptyTypes, null);
            method?.Invoke(null, null);
        }

        public static bool UsesGlobalSaveKeys()
        {
            return Resolve() && _globalSaveDataSystemType != null;
        }
    }
}
