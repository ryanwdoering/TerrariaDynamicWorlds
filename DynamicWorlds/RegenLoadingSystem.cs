using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.WorldBuilding;

namespace DynamicWorlds
{
    internal enum RegenLifecycleStage
    {
        PreparingToQuit,
        Generating,
        ReloadingWorld
    }

    internal sealed class PendingRegenContext
    {
        public WorldProgressSnapshot Snapshot;
        public Dictionary<Point16, AnchoredTileData> AnchoredTiles = new();
        public Dictionary<Point16, SavedChestContents> AnchoredChests = new();
        public HashSet<Point16> ErasedTiles = new();
        public Dictionary<int, BuildingZone> BuildingZones = new();
        public int NewSeed;
        public string SeedLabel = string.Empty;
        public int SavedSpawnX = -1;
        public int SavedSpawnY = -1;
        public GenerationProgress Progress;
        public Task GenerationTask;
        public RegenLifecycleStage Stage;
        public string StatusMessage = "Preparing regeneration...";
        public bool GenerationSucceeded;
        public RegenExecutionResult ExecutionResult = new RegenExecutionResult();
    }

    public class DynamicWorldRegenSystem : ModSystem
    {
        private static PendingRegenContext _pending;
        private static RegenLoadingUI _loadingUi;

        internal static PendingRegenContext CurrentContext => _pending;

        public static bool IsBusy => _pending != null;

        public static bool SuppressPlayerPositionSave => _pending != null;

        public static bool ShouldSuppressWorldLoadSnapshot =>
            _pending != null && _pending.Stage == RegenLifecycleStage.ReloadingWorld;

        internal static void QueueRegen(PendingRegenContext pending)
        {
            if (pending == null)
                return;

            _pending = pending;
            _pending.Stage = RegenLifecycleStage.PreparingToQuit;
            _pending.StatusMessage = "Saving current world...";

            WorldGen.SaveAndQuit(() => Main.QueueMainThreadAction(BeginGenerationFromMenu));
        }

        public override void PostWorldGen()
        {
            if (_pending == null || _pending.Stage != RegenLifecycleStage.Generating)
                return;

            _pending.ExecutionResult = SingleplayerRegenHelper.ExecutePendingRegen(_pending);
            _pending.GenerationSucceeded = true;

            if (_pending.Progress != null)
            {
                _pending.StatusMessage = "Saving regenerated world...";
                _pending.Progress.Message = _pending.StatusMessage;
                _pending.Progress.Start(1d);
                _pending.Progress.Set(1d);
            }
        }

        public override void PostWorldLoad()
        {
            if (_pending == null || _pending.Stage != RegenLifecycleStage.ReloadingWorld)
                return;

            int restoredPylons = PylonRestoreHelper.RestoreTrackedVanillaPylons(forceRefresh: true);
            if (restoredPylons > 0)
            {
                ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                    $"[Regen] Re-registered {restoredPylons} restored vanilla pylon(s) after reloading the regenerated world.");
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (_pending == null)
                return;

            if (_pending.Stage != RegenLifecycleStage.Generating)
                return;

            EnsureLoadingUi();
        }

        public static bool TryHandlePostRegenEnter(Player player)
        {
            if (_pending == null || _pending.Stage != RegenLifecycleStage.ReloadingWorld)
                return false;

            RegenExecutionResult result = _pending.ExecutionResult ?? new RegenExecutionResult();
            Vector2 spawnPos;

            if (result.UsePersonalSpawn)
            {
                player.SpawnX = result.SpawnTileX;
                player.SpawnY = result.SpawnTileY;
                spawnPos = new Vector2(result.SpawnTileX * 16f, result.SpawnTileY * 16f - 48f);
                Main.NewText("Your bed survived — spawning there.", 180, 255, 180);
            }
            else
            {
                player.SpawnX = -1;
                player.SpawnY = -1;
                spawnPos = new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f - 48f);

                if (result.HadSavedSpawn)
                    Main.NewText("Your bed was not preserved — spawning at world spawn.", 255, 200, 100);
            }

            player.Teleport(spawnPos, 1);
            player.fallStart = (int)(player.position.Y / 16f);
            player.GetModPlayer<DynamicWorldsPlayer>().ClearSavedPosition();
            player.AddBuff(BuffID.Featherfall, 60 * 10);

            if (result.RespawnedNpcCount > 0)
            {
                Main.NewText($"Respawned {result.RespawnedNpcCount} town NPC{(result.RespawnedNpcCount == 1 ? "" : "s")}.", 150, 200, 255);

                if (result.RestoredHousingCount > 0)
                {
                    Main.NewText(
                        $"Reassigned {result.RestoredHousingCount} preserved home{(result.RestoredHousingCount == 1 ? "" : "s")}.",
                        180, 255, 180);
                }
            }

            WorldProgressUtil.PrintSnapshotToChat("After regen", WorldProgressUtil.Capture());
            WorldProgressUtil.SaveToFile();
            Main.NewText("World regeneration complete!", 80, 255, 80);

            WorldGenerator.CurrentGenerationProgress = null;
            Main.statusText = string.Empty;
            _pending = null;
            return true;
        }

        private static void BeginGenerationFromMenu()
        {
            if (_pending == null)
                return;

            _pending.Stage = RegenLifecycleStage.Generating;
            _pending.GenerationSucceeded = false;
            _pending.StatusMessage = "Generating world terrain...";
            _pending.Progress = new GenerationProgress();

            if (Main.ActiveWorldFileData != null)
                Main.ActiveWorldFileData.SetSeed(_pending.NewSeed.ToString());

            EnsureLoadingUi();
            _pending.GenerationTask = WorldGen.CreateNewWorld(_pending.Progress);
            _pending.GenerationTask.ContinueWith(task =>
            {
                Main.QueueMainThreadAction(() => HandleGenerationCompleted(task));
            }, TaskScheduler.Default);
            EnsureLoadingUi();
        }

        private static void HandleGenerationCompleted(Task completedTask)
        {
            if (_pending == null || _pending.Stage != RegenLifecycleStage.Generating)
                return;

            if (!ReferenceEquals(completedTask, _pending.GenerationTask))
                return;

            if (completedTask.IsFaulted || !_pending.GenerationSucceeded)
            {
                if (completedTask.Exception != null)
                {
                    ModContent.GetInstance<DynamicWorlds>().Logger.Error(
                        "World regeneration failed before the world could be reloaded.",
                        completedTask.Exception);
                }

                FailPendingRegen();
                return;
            }

            PersistPlayerForReload(_pending.ExecutionResult);
            _pending.StatusMessage = "Loading regenerated world...";
            _pending.Stage = RegenLifecycleStage.ReloadingWorld;
            _pending.GenerationTask = null;
            WorldGenerator.CurrentGenerationProgress = null;
            Main.statusText = _pending.StatusMessage;
            WorldGen.playWorld();
            Main.menuMode = 10;
        }

        private static void EnsureLoadingUi()
        {
            _loadingUi ??= new RegenLoadingUI();

            Main.menuMode = 888;
            if (Main.MenuUI.CurrentState != _loadingUi)
                Main.MenuUI.SetState(_loadingUi);
        }

        private static void PersistPlayerForReload(RegenExecutionResult result)
        {
            Player player = Main.ActivePlayerFileData?.Player ?? Main.LocalPlayer;
            if (player == null)
                return;

            if (result.UsePersonalSpawn)
            {
                player.SpawnX = result.SpawnTileX;
                player.SpawnY = result.SpawnTileY;
            }
            else
            {
                player.SpawnX = -1;
                player.SpawnY = -1;
            }

            player.GetModPlayer<DynamicWorldsPlayer>().ClearSavedPosition();

            if (Main.ActivePlayerFileData != null)
            {
                Main.ActivePlayerFileData.Player = player;
                Player.SavePlayer(Main.ActivePlayerFileData);
            }
        }

        private static void FailPendingRegen()
        {
            WorldGenerator.CurrentGenerationProgress = null;
            Main.statusText = "World regeneration failed.";
            Main.LoadWorlds();
            Main.GoToWorldSelect();
            _pending = null;
        }
    }

    internal sealed class RegenLoadingUI : UIState
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            PendingRegenContext pending = DynamicWorldRegenSystem.CurrentContext;
            if (pending == null)
                return;

            GenerationProgress progress = pending.Progress;
            float overallProgress = progress != null ? MathHelper.Clamp((float)progress.TotalProgress, 0f, 1f) : 0f;
            string message = !string.IsNullOrWhiteSpace(progress?.Message) ? progress.Message : pending.StatusMessage;
            string detail = string.IsNullOrWhiteSpace(pending.SeedLabel)
                ? "Preserving anchored tiles, housing, and progression"
                : $"Seed: {pending.SeedLabel}";

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int panelWidth = Math.Min(620, Main.screenWidth - 80);
            int panelHeight = 190;
            Rectangle panel = new Rectangle(
                (Main.screenWidth - panelWidth) / 2,
                (Main.screenHeight - panelHeight) / 2,
                panelWidth,
                panelHeight);
            Rectangle border = new Rectangle(panel.X - 3, panel.Y - 3, panel.Width + 6, panel.Height + 6);
            Rectangle bar = new Rectangle(panel.X + 36, panel.Bottom - 64, panel.Width - 72, 24);
            Rectangle fill = new Rectangle(bar.X + 4, bar.Y + 4, (int)((bar.Width - 8) * overallProgress), bar.Height - 8);

            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 1.6f);

            spriteBatch.Draw(pixel, border, new Color(18, 28, 52, 255));
            spriteBatch.Draw(pixel, panel, new Color(8, 12, 28, 235));
            spriteBatch.Draw(pixel, bar, new Color(24, 34, 58, 255));

            if (fill.Width > 0)
                spriteBatch.Draw(pixel, fill, Color.Lerp(new Color(80, 170, 255), new Color(165, 235, 255), pulse));

            Utils.DrawBorderStringBig(
                spriteBatch,
                "Regenerating World",
                new Vector2(panel.Center.X, panel.Y + 38),
                Color.White,
                0.7f,
                0.5f,
                0.5f);

            Utils.DrawBorderString(
                spriteBatch,
                message,
                new Vector2(panel.Center.X, panel.Y + 94),
                new Color(200, 230, 255),
                1f,
                0.5f,
                0.5f);

            Utils.DrawBorderString(
                spriteBatch,
                detail,
                new Vector2(panel.Center.X, panel.Y + 126),
                new Color(150, 185, 220),
                0.9f,
                0.5f,
                0.5f);

            Utils.DrawBorderString(
                spriteBatch,
                $"{overallProgress:P1}",
                new Vector2(bar.Right, bar.Y - 26),
                Color.White,
                0.95f,
                1f,
                0f);
        }
    }
}
