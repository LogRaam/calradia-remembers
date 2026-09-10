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
// A NAME LIST CANNOT BE COMPLETE, AND TWO RUNS PROVED IT. cr.testall caught the first miss:
// TaleWorlds.Engine.Screens.SceneLayer, the 3D world, is not a GauntletLayer at all and its constructor reads
//
//      : base("SceneLayer", -100) { ... base.IsFocusLayer = true; }
//
// so it claims focus on every screen a conversation can happen on, and unexempted it took the chat off screen on
// the first frame of every conversation. Gabriel's own play-test then caught the second: opening a chat from a
// TAVERN, where the vanilla settlement menu "MapMenuOverlay" is up and holding focus for the whole conversation.
// The chat yielded to the very backdrop it had just been opened over, so it never appeared, the vanilla dialogue
// ran on underneath, and the window only slid into view once the player left the menu.
//
// So the rule is no longer only "does anything here want the input". It is:
//
//      WHATEVER WAS ALREADY CLAIMING FOCUS WHEN THE CHAT OPENED IS THE BACKDROP, NOT A SCREEN ABOVE US.
//
// That is structural rather than enumerated, and it is what the failed layer-COUNT baseline was reaching for
// without getting right: the question was never how MANY layers there are, it is WHICH of them the chat was
// deliberately drawn over. A backdrop can now be anything, named or unnamed, in this game build or the next.
// Only two things remain by name, and both are unavoidable:
//
//   - the vanilla conversation windows, because they arrive AFTER the chat when the NPC starts the conversation
//     (tashmetu's report), so no baseline can contain them;
//   - SceneLayer, kept as a TYPE check by the caller purely as a second lock on the one mistake that would cost
//     every conversation in the game. The baseline already covers it.
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

        /// <summary>
        ///   The 3D world the chat is drawn over: TaleWorlds.Engine.Screens.SceneLayer, which every screen with
        ///   a scene carries (a mission, the campaign map, the naval map). Its constructor sets IsFocusLayer
        ///   unconditionally and gives it order -100, so it is ALWAYS claiming focus and always at the bottom.
        ///   Read from the layer's TYPE by the caller rather than from its name, because getting this one wrong
        ///   costs every conversation in the game (see the policy header).
        /// </summary>
        public bool IsTheWorldBeneath { get; set; }
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
        public static ChatFocusVerdict Decide(bool hostScreenIsOnTop,
                                              IEnumerable<OverlayNeighbour> neighbours,
                                              ICollection<string> backdropAtOpen = null)
        {
            if (!hostScreenIsOnTop) return ChatFocusVerdict.Yield;
            if (neighbours == null) return ChatFocusVerdict.Hold;

            foreach (OverlayNeighbour layer in neighbours)
                if (TakesTheScreen(layer, backdropAtOpen))
                    return ChatFocusVerdict.Yield;

            return ChatFocusVerdict.Hold;
        }

        /// <summary>
        ///   Whether this one layer is genuinely a screen above the chat. Every exclusion here is a way of
        ///   saying "this was already part of the picture the chat was drawn over".
        /// </summary>
        private static bool TakesTheScreen(OverlayNeighbour layer, ICollection<string> backdropAtOpen)
        {
            if (layer == null || layer.IsTheChat || layer.IsTheWorldBeneath) return false;
            if (layer.IsFinalized || !layer.WantsFocus) return false;
            if (IsVanillaConversationLayer(layer.Name)) return false;

            return !WasPartOfTheBackdrop(layer.Name, backdropAtOpen);
        }

        /// <summary>
        ///   True when this layer was already claiming focus at the moment the chat opened. A nameless layer can
        ///   never be matched against the backdrop, so it is treated as new: yielding to an unknown claimant is
        ///   recoverable (it goes away and the chat returns), while ignoring a real modal strands the player in
        ///   a menu that does not answer the mouse.
        /// </summary>
        public static bool WasPartOfTheBackdrop(string layerName, ICollection<string> backdropAtOpen)
        {
            if (backdropAtOpen == null || backdropAtOpen.Count == 0) return false;
            if (string.IsNullOrEmpty(layerName)) return false;

            foreach (string known in backdropAtOpen)
                if (string.Equals(layerName, known, System.StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>
        ///   The name of the layer the verdict was reached on, for the log. Empty when holding. Knowing WHICH
        ///   modal took the screen is what turned tashmetu's report into a diagnosis instead of a guess.
        /// </summary>
        public static string ReasonFor(bool hostScreenIsOnTop,
                                       IEnumerable<OverlayNeighbour> neighbours,
                                       ICollection<string> backdropAtOpen = null)
        {
            if (!hostScreenIsOnTop) return "a screen was pushed over the chat's host";
            if (neighbours == null) return "";

            foreach (OverlayNeighbour layer in neighbours)
                if (TakesTheScreen(layer, backdropAtOpen))
                    return string.IsNullOrEmpty(layer.Name) ? "an unnamed layer claimed focus" : layer.Name;

            return "";
        }
    }
}
