using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DynamicWorlds
{
    /// <summary>
    /// Automatically triggers /regenworld on the configured in-game day interval.
    /// Broadcasts spooky countdown messages at 3 days, 1 day, and dawn of regen day.
    /// Persists the day counter in the world save so it survives restarts.
    /// </summary>
    public class WorldRegenScheduler : ModSystem
    {
        // Total in-game days elapsed since the last regen (or world creation).
        private static int _daysSinceRegen = 0;

        // Countdown warning thresholds (days remaining).
        private static readonly int[] WarnAtDays = { 3, 1 };

        // Track the last day we warned about, so we only message once per day.
        private static int _lastWarnDay = -1;

        // Whether we've shown the "dawn of regen day" warning this cycle.
        private static bool _dawnWarningShown = false;

        // ── Persistence ──────────────────────────────────────────────────

        public override void SaveWorldData(TagCompound tag)
        {
            tag["daysSinceRegen"]   = _daysSinceRegen;
            tag["lastWarnDay"]      = _lastWarnDay;
            tag["dawnWarningShown"] = _dawnWarningShown;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            _daysSinceRegen   = tag.GetInt("daysSinceRegen");
            _lastWarnDay      = tag.ContainsKey("lastWarnDay") ? tag.GetInt("lastWarnDay") : -1;
            _dawnWarningShown = tag.ContainsKey("dawnWarningShown") && tag.GetBool("dawnWarningShown");
        }

        public override void OnWorldLoad()
        {
            // Reset transient state when entering a world.
            _lastWarnDay = -1;
            _wasNight = !Main.dayTime;
            _wasDay = Main.dayTime;
        }

        // ── Time tracking ─────────────────────────────────────────────────

        // PostUpdateTime fires every game tick after Main.time advances.
        // Dawn (start of a new day) is when Main.dayTime becomes true and
        // Main.time is near 0. We detect the transition by watching for the
        // moment the sun rises.
        private bool _wasNight = false;

        public override void PostUpdateTime()
        {
            // Only run in single-player.
            if (Main.netMode != NetmodeID.SinglePlayer)
                return;

            bool isNight = !Main.dayTime;

            if (!IsSchedulerEnabled())
            {
                _wasNight = isNight;
                return;
            }

            // Detect dawn: we were in night, now it's daytime.
            if (_wasNight && !isNight)
                OnNewDay();

            _wasNight = isNight;
        }

        private void OnNewDay()
        {
            int regenEveryDays = GetRegenEveryDays();
            _daysSinceRegen++;
            _dawnWarningShown = false; // reset for the new day

            int daysRemaining = regenEveryDays - _daysSinceRegen;

            // ── Countdown warnings ────────────────────────────────────────
            foreach (int warnDay in WarnAtDays)
            {
                if (daysRemaining == warnDay && _lastWarnDay != _daysSinceRegen)
                {
                    _lastWarnDay = _daysSinceRegen;
                    BroadcastCountdown(daysRemaining);
                    return;
                }
            }

            // ── Dawn-of-regen-day warning ─────────────────────────────────
            if (daysRemaining == 0 && !_dawnWarningShown)
            {
                _dawnWarningShown = true;
                Main.NewText("☠ The world stirs... regeneration begins at midnight. ☠", 200, 80, 255);
                return;
            }

            // ── Trigger regen at the next midnight (start of night) ───────
            // We do this by setting a flag and actually triggering at nightfall.
            // See PostUpdateTime → OnMidnight below. Nothing else to do here.
        }

        // Detect midnight (dayTime → night transition) to fire the regen.
        private bool _wasDay = false;

        public override void PreUpdateTime()
        {
            if (Main.netMode != NetmodeID.SinglePlayer)
                return;

            bool isDay = Main.dayTime;

            if (!IsSchedulerEnabled())
            {
                _wasDay = isDay;
                return;
            }

            // Detect midnight: we were in daytime, now it's night.
            if (_wasDay && !isDay)
                OnMidnight();

            _wasDay = isDay;
        }

        private void OnMidnight()
        {
            int daysRemaining = GetRegenEveryDays() - _daysSinceRegen;

            if (daysRemaining <= 0)
            {
                // Fire the regen!
                Main.NewText("☠ The world tears itself apart and is reborn... ☠", 200, 80, 255);
                _daysSinceRegen = 0;
                _lastWarnDay    = -1;
                _dawnWarningShown = false;

                // Small delay isn't easily done here, so trigger immediately.
                // The regen itself will print "Before/After regen" to chat.
                SingleplayerRegenHelper.RegenerateWorldWithProgress();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static bool IsSchedulerEnabled()
        {
            return ModContent.GetInstance<DynamicWorldsConfig>().EnableRegenCounter;
        }

        private static int GetRegenEveryDays()
        {
            return Math.Max(1, ModContent.GetInstance<DynamicWorldsConfig>().ScheduledRegenIntervalDays);
        }

        private static void BroadcastCountdown(int daysRemaining)
        {
            string msg = daysRemaining switch
            {
                3 => "☠ The ground trembles. The world will regenerate in 3 days... ☠",
                1 => "☠ Something wicked approaches. The world regenerates TOMORROW. ☠",
                _ => $"☠ The world regenerates in {daysRemaining} days. ☠"
            };

            // Eerie purple colour.
            Main.NewText(msg, 200, 80, 255);
        }

        // ── Debug command ─────────────────────────────────────────────────

        /// <summary>Returns current scheduler state for the /snap command.</summary>
        public static string GetStatusText()
        {
            int regenEveryDays = GetRegenEveryDays();

            if (!IsSchedulerEnabled())
            {
                return $"Scheduled world regeneration is disabled. Progress is paused at day {_daysSinceRegen}/{regenEveryDays}.";
            }

            int daysRemaining = regenEveryDays - _daysSinceRegen;
            return $"World regen in {Math.Max(0, daysRemaining)} day(s) " +
                   $"(day {_daysSinceRegen}/{regenEveryDays})";
        }
    }
}
