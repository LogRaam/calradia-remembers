// Code written by Gabriel Mailhot, 18/07/2026.
// Turns the engine's facts about the player into the ONE value the prompt speaks from. Extracted from
// BannerlordGameStateBridge.GetPlayerStatus after a shipped bug (JoshGray700, Nexus 2026-07-18): the
// vassal/liege pair was inverted there, so the Aserai Sultan was told "The player is your liege lord."
//
// The trap that produced it is worth naming, because it is a naming trap and it will recur. The enum is
// PlayerStatusVsNpc: every value describes THE PLAYER's standing, not the NPC's. Read from the call site's
// own point of view ("this NPC is my liege") the two values look swapped, and that is exactly the mistake
// that shipped: the bridge's comment said "Player is a vassal" and the line under it returned Liege.
//
// Which is why the tests for this policy assert the SENTENCE the SDK renders, not the enum value. Pinning
// the enum alone would restate the convention rather than check it, and would have declared the bug correct.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Places the player beside one NPC, from facts the engine can answer directly.</summary>
    public static class PlayerStandingPolicy
    {
        /// <summary>
        ///   The player's standing. Order matters: captivity outranks everything (it is the immediate,
        ///   physical situation and it overrides any oath), then shared blood, then the feudal chain.
        /// </summary>
        public static PlayerStatusVsNpc Resolve(PlayerStandingFacts facts)
        {
            if (facts == null) return PlayerStatusVsNpc.Unknown;

            if (facts.PlayerIsCaptiveOfNpc) return PlayerStatusVsNpc.Captive;
            if (facts.NpcIsCaptiveOfPlayer) return PlayerStatusVsNpc.NpcIsCaptive;
            if (facts.SharesPlayerClan) return PlayerStatusVsNpc.ClanMember;

            // BEFORE the feudal pair, and the order is load-bearing: the engine gives a mercenary clan the
            // same Kingdom as a sworn one, so asking the feudal questions first would report a hired sword as
            // an oath-bound vassal (and CR's own join_as_mercenary action produces exactly that state).
            if (facts.PlayerIsMercenaryOfNpcRealm) return PlayerStatusVsNpc.Mercenary;

            // The mirror, and it comes before Liege for the same reason: the player's crown RENTS this NPC's
            // sword rather than owning its fealty, so the player is a paymaster, not a liege.
            if (facts.NpcIsMercenaryOfSharedRealm && facts.PlayerRulesNpcRealm) return PlayerStatusVsNpc.Employer;

            // The NPC rules the realm the player serves, so the PLAYER is the sworn vassal.
            if (facts.NpcRulesPlayerRealm) return PlayerStatusVsNpc.Vassal;

            // The player rules the realm this NPC serves, so the PLAYER is the liege.
            if (facts.PlayerRulesNpcRealm) return PlayerStatusVsNpc.Liege;

            // Neither commands the other, but one crown claims them both. A hired band is deliberately NOT
            // called a fellow vassal: they took the same realm's coin, not the same oath, and no bond of
            // brotherhood follows from a contract. Saying nothing is the honest answer here.
            if (facts.SharesRealm && !facts.NpcIsMercenaryOfSharedRealm) return PlayerStatusVsNpc.SameRealm;

            return PlayerStatusVsNpc.Free;
        }
    }
}
