using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DynamicWorlds
{
    /// <summary>
    /// Hooks into the Guide NPC's chat so the player can ask about the Reality Anchor.
    /// Button2 ("Crafting" normally) is repurposed while on our info page to show "Back".
    /// We use PreChatButtonClicked to intercept button2 and drive our page logic, and
    /// GetChat to swap in our description text.
    /// Note: SetChatButtons does not exist on GlobalNPC in this tModLoader version.
    /// </summary>
    public class GuideGlobalNPC : GlobalNPC
    {
        // 0 = normal Guide chat, 1 = Reality Anchor info page.
        private static int _page = 0;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
            => npc.type == NPCID.Guide;

        // ----------------------------------------------------------------
        // Swap in our dialogue text when on the info page.
        // ----------------------------------------------------------------
        public override void GetChat(NPC npc, ref string chat)
        {
            if (_page == 1)
            {
                chat = Main.rand.Next(4) switch
                {
                    0 => "The Reality Anchor lets you mark tiles you want to keep. " +
                         "After the world regenerates, every anchored tile and its " +
                         "contents will be restored exactly as you left it.",
                    1 => "Left-click any tile while holding the Reality Anchor to " +
                         "anchor or unanchor it. Anchored tiles glow so you can " +
                         "spot them at a glance.",
                    2 => "Chests and dressers remember their contents too — even " +
                         "after a full world regeneration. Just make sure they're " +
                         "anchored before you run /regenworld!",
                    _ => "Right-click the Reality Anchor in your inventory at any " +
                         "time to instantly restore all of your anchored tiles " +
                         "without waiting for a full regen."
                };
            }
        }

        // ----------------------------------------------------------------
        // Intercept button2 to drive our page transitions.
        // Returns false to suppress vanilla's Crafting UI when on our page.
        // ----------------------------------------------------------------
        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        {
            if (firstButton)
            {
                // Left button (Housing) — reset our page, let vanilla handle it.
                _page = 0;
                return true;
            }

            // Right button (normally "Crafting")
            if (_page == 0)
            {
                // Enter Reality Anchor info page
                _page = 1;
                string chat = "";
                GetChat(npc, ref chat);
                Main.npcChatText = chat;
                return false; // suppress vanilla Crafting UI
            }
            else
            {
                // Back to normal Guide chat
                _page = 0;
                Main.npcChatText = npc.GetChat();
                return false;
            }
        }

        // Reset page when the Guide dies.
        public override void OnKill(NPC npc) => _page = 0;
    }
}
