// Code written by Gabriel Mailhot, 09/09/2026.
// Player report (Nexus, tashmetu, 2026-09-09, v2.5.6): "the chat overlay yields on NPC-initiated conversations and
// stays hidden for the rest of the session... the mod itself works end to end, profiles load, vanilla lines are
// seeded, the LLM returns a full response, but the overlay is never drawn." His own reading of his log found the
// fault before we did, and it was right:
//
//   [SCREENS] yield | top=MissionScreen | host=MissionScreen layers[13]: SceneLayer GauntletLayer x12
//   "top equals host, there is no actual separate screen above the overlay. The layer count is 13 here; in
//    sessions where the overlay behaves it is 12. No restore/reclaim line appears anywhere afterwards."
//
// The old arbiter decided "a screen is above us" by COUNTING the host screen's layers and comparing against the
// count taken the instant the chat opened. That is a proxy for the real question, and the proxy breaks on the one
// ordering he described. Verified against the game's own binaries: SandBox.GauntletUI's MissionGauntletConversationView
// adds a Gauntlet layer named "MissionConversation" (order 49) to the SAME MissionScreen while the conversation is
// being set up. When the PLAYER clicks an NPC, that layer already exists when we take our baseline, so the count
// never grows. When the NPC approaches the PLAYER, the mod opens first and vanilla's layer lands after, so the
// count grows by exactly one, we read that as a modal, we slide the chat off the right edge, and it never comes
// back: the vanilla conversation layer stays for the whole conversation, so the count never returns to baseline.
// From there the window is off screen, unclosable, IsOpen stays true, and every later conversation is refused.
// One arrival order costs the player the feature until they restart the game.
//
// So the question stops being "how many layers are there now" and becomes "does anything here actually want the
// input". The engine already answers that: a layer sets IsFocusLayer when it means to take focus. Sweeping every
// named Gauntlet layer in Native and SandBox shows the answer is clean. NOT ONE always-on mission HUD layer sets
// it (crosshair, nameplates, kill feed, objectives, order UI, markers); every layer that does is a real modal
// (MissionEscapeMenu, MissionBarter, MissionOptions, MissionTournament, MissionBoardGame, the map menus, the
// screens) plus exactly two exceptions: "MissionConversation" and "MapConversation", the vanilla conversation
// windows this mod exists to replace. Those two are named here and never yielded to.
//
// Gabriel's rule, 2026-09-09: a proven regression sensitivity earns a MECHANISM. There are two here. The decision
// is a pure function of named facts, so it can be tested without a game. And the decision is now the exact
// negation of its own reclaim, evaluated fresh every frame from the world as it stands, holding no memory of what
// the screen looked like when the chat opened: there is no longer a baseline to be wrong about, so a yield can no
// longer outlive the thing it yielded to.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What the chat overlay should be doing with the input this frame.</summary>
    public enum ChatFocusVerdict
    {
        /// <summary>Nothing above us wants the input: hold focus and stay where the player can read it.</summary>
        Hold,

        /// <summary>Something above us wants the input: drop focus and slide aside until it is gone.</summary>
        Yield
    }

    /// <summary>One layer beside the chat, as the arbiter needs to see it. Read off the live screen by the caller.</summary>
    public sealed class OverlayNeighbour
    {
        /// <summary>The engine's own layer name, which is how a modal is told from the window it opened over.</summary>
        public string Name { get; set; }

        /// <summary>True for the chat's own layer, which is never its own reason to yield.</summary>
        public bool IsTheChat { get; set; }

        /// <summary>A finalized layer is on its way out and speaks for nobody.</summary>
        public bool IsFinalized { get; set; }

        /// <summary>The engine's IsFocusLayer: the vanilla contract for "I mean to take the input".</summary>
        public bool WantsFocus { get; set; }
    }

    /// <summary>Whether the chat overlay should hold the input this frame, or stand aside for something that wants it.</summary>
    public static class ChatFocusArbitrationPolicy
    {
        /// <summary>
        ///   The vanilla conversation windows. Named rather than counted, and named rather than excluded by order:
        ///   both sit far below the chat's own order, so ordering cannot tell them from a modal, and both set
        ///   IsFocusLayer exactly as a modal does. They are the two layers the chat is deliberately drawn over,
        ///   because replacing them IS the feature.
        /// </summary>
        public static readonly string[] VanillaConversationLayerNames = {"MissionConversation", "MapConversation"};

        /// <summary>True for a vanilla conversation window, the one focus claim the chat must ignore.</summary>
        public static bool IsVanillaConversationLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;

            foreach (string known in VanillaConversationLayerNames)
                if (string.Equals(layerName, known, System.StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>
        ///   Judges the frame from what is actually on the screen right now. <paramref name="hostScreenIsOnTop" />
        ///   is false when a whole screen was pushed over the chat's host (the encyclopedia, a panel, the map
        ///   behind a state), which the engine already treats as above us. Everything else is decided by whether
        ///   any neighbouring layer means to take the input.
        /// </summary>
        public static ChatFocusVerdict Decide(bool hostScreenIsOnTop, IEnumerable<OverlayNeighbour> neighbours)
        {
            if (!hostScreenIsOnTop) return ChatFocusVerdict.Yield;
            if (neighbours == null) return ChatFocusVerdict.Hold;

            foreach (OverlayNeighbour layer in neighbours)
            {
                if (layer == null || layer.IsTheChat) continue;
                if (layer.IsFinalized || !layer.WantsFocus) continue;
                if (IsVanillaConversationLayer(layer.Name)) continue;

                return ChatFocusVerdict.Yield;
            }

            return ChatFocusVerdict.Hold;
        }

        /// <summary>
        ///   The name of the layer the verdict was reached on, for the log. Empty when holding. Knowing WHICH
        ///   modal took the screen is what turned tashmetu's report into a diagnosis instead of a guess.
        /// </summary>
        public static string ReasonFor(bool hostScreenIsOnTop, IEnumerable<OverlayNeighbour> neighbours)
        {
            if (!hostScreenIsOnTop) return "a screen was pushed over the chat's host";
            if (neighbours == null) return "";

            foreach (OverlayNeighbour layer in neighbours)
            {
                if (layer == null || layer.IsTheChat) continue;
                if (layer.IsFinalized || !layer.WantsFocus) continue;
                if (IsVanillaConversationLayer(layer.Name)) continue;

                return string.IsNullOrEmpty(layer.Name) ? "an unnamed layer claimed focus" : layer.Name;
            }

            return "";
        }
    }
}
