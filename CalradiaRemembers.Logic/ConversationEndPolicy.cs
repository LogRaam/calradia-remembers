// Code written by Gabriel Mailhot, 03/09/2026.
// Player request: let the player decide when a chatroom conversation ends. When an NPC emits end_conversation (or
// a farewell), it currently locks the chat to the close button; some players want the last word, or to keep
// talking. This policy decides whether that NPC-driven end actually LOCKS the conversation: always in a captive
// or captor scene (those run on their own Continue/resume flow and must play out), and in the ordinary Chatroom
// only when the player's MCM toggle allows it. Pure and engine-free so the rule stays tested; the chat view model
// reads the live toggle and scene state and acts on the answer.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether an NPC's end_conversation should lock the chat shut, or leave the player in control.</summary>
    public static class ConversationEndPolicy
    {
        /// <summary>
        ///   True when an NPC's end_conversation should LOCK the conversation (input closed, only the Leave button
        ///   left). A captive or captor scene ALWAYS locks, whatever the setting: those have their own
        ///   Continue/resume flow and this must not interfere. An ordinary Chatroom obeys
        ///   <paramref name="npcCanEndSetting" />: on (default) locks as before, off leaves the text box open so
        ///   the player keeps the last word and closes the chat themselves.
        /// </summary>
        public static bool NpcEndLocks(bool npcCanEndSetting, bool isCaptiveOrCaptorScene)
            => isCaptiveOrCaptorScene || npcCanEndSetting;
    }
}
