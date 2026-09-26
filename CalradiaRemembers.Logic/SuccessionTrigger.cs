// Code written by Gabriel Mailhot, 25/09/2026.
// What the engine's "the player character changed" event actually meant. CR read every such change as a death,
// reframed every NPC's memory (dropping the predecessor's private history for good) and chronicled "X died, and Y
// took up the succession". Wallace1067, 25/09/2026, with The Original Player Switcher: switching the active character
// back and forth logged a death each way and reframed 24 profiles twice, though nobody had died.
//
// Vanilla, decompiled (ApplyHeirSelectionAction.ApplyInternal): on a death the old hero is KILLED before
// ChangePlayerCharacterAction runs; on a retirement he is DISABLED before it runs. So by the time the event fires,
// the predecessor's own state says which it was, and a predecessor who is neither is a player who simply changed
// hands, which is no succession at all.

namespace CalradiaRemembers.Logic
{
    /// <summary>What a change of player character was.</summary>
    public enum PlayerChangeKind
    {
        /// <summary>The old character is alive and active: another mod switched who the player controls. Not a succession.</summary>
        Switch,

        /// <summary>The old character died and an heir took over.</summary>
        Death,

        /// <summary>The old character retired (vanilla disables him) and an heir took over.</summary>
        Retirement
    }

    /// <summary>The predecessor's state when the change fired.</summary>
    public sealed class PlayerChangeFacts
    {
        /// <summary>The old player character is dead.</summary>
        public bool PredecessorIsDead { get; set; }

        /// <summary>The old player character is disabled, which is how vanilla retires him.</summary>
        public bool PredecessorIsRetired { get; set; }
    }

    /// <summary>Classifies a change of player character, so only a real succession reframes memories.</summary>
    public static class SuccessionTrigger
    {
        /// <summary>Death wins over retirement if both somehow hold; anything else is a switch.</summary>
        public static PlayerChangeKind Classify(PlayerChangeFacts facts)
        {
            if (facts == null) return PlayerChangeKind.Switch;
            if (facts.PredecessorIsDead) return PlayerChangeKind.Death;
            if (facts.PredecessorIsRetired) return PlayerChangeKind.Retirement;

            return PlayerChangeKind.Switch;
        }

        /// <summary>True for the two kinds that are a real succession and so reframe what NPCs remember.</summary>
        public static bool IsSuccession(PlayerChangeKind kind) => kind != PlayerChangeKind.Switch;
    }
}
