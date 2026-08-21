// Code written by Gabriel Mailhot, 20/08/2026.
// Player wish (Fakade, roadmap): a lord can only ride out to meet the player on the ROAD
// (InterceptionBehavior/InterceptionPolicy), which needs a field party to move -- so everyone WITHOUT one
// (ladies, notables, a garrisoned lord standing around in a fief) never gets a turn. A fief audience answers
// the SAME "does this NPC have a live reason to speak right now" question InterceptionPolicy already answers,
// reused via ChooseFiefValid below rather than reimplemented; this file only narrows WHICH of its triggers make
// sense for someone already standing in the settlement (never intercepting anyone), and gates how often the
// offer itself may fire (a per-NPC cooldown mirroring InterceptionPacingRules' own dial), so entering a busy
// town can never open a queue of "may I have a word" popups.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One partyless hero standing in the settlement who qualifies for a fief audience, and why.</summary>
    public sealed class FiefAudienceCandidate
    {
        public FiefAudienceCandidate(string heroId, InterceptionTrigger trigger)
        {
            HeroId = heroId;
            Trigger = trigger;
        }

        /// <summary>The candidate's StringId.</summary>
        public string HeroId { get; }

        /// <summary>The fief-valid reason this candidate qualifies. Never <see cref="InterceptionTrigger.None" /> in a returned candidate.</summary>
        public InterceptionTrigger Trigger { get; }
    }

    /// <summary>
    ///   Decides which of InterceptionPolicy's triggers make sense for someone with no field party, standing
    ///   where the player just walked in, and how often the fief-audience offer itself may fire for the same NPC.
    /// </summary>
    public static class FiefAudiencePolicy
    {
        /// <summary>No prior offer recorded for a candidate.</summary>
        public const int NeverOffered = -1;

        /// <summary>
        ///   Whether a trigger reads believably for someone already standing in the settlement.
        ///   <see cref="InterceptionTrigger.TerritorialPatrol" /> is written around a lord riding out to
        ///   challenge a traveller CROSSING their land (its own doc: "a hostile-realm lord challenges a
        ///   notorious traveller crossing their land") -- a fief-bound NPC standing beside the player is not
        ///   intercepting a crossing, so that trigger stays road-only. <see cref="InterceptionTrigger.HeardOfDeed" />
        ///   ("a lord who has heard of a recent deed... rides out to react while it is fresh") reads just as
        ///   well from someone who simply asks for a word where they already stand.
        /// </summary>
        public static bool IsFiefValid(InterceptionTrigger trigger) => trigger == InterceptionTrigger.HeardOfDeed;

        /// <summary>The trigger <see cref="InterceptionPolicy.Choose" /> would pick for these facts, narrowed to the fief-valid subset, or None.</summary>
        public static InterceptionTrigger ChooseFiefValid(InterceptionFacts facts)
        {
            InterceptionTrigger trigger = InterceptionPolicy.Choose(facts);

            return IsFiefValid(trigger) ? trigger : InterceptionTrigger.None;
        }

        /// <summary>
        ///   True while the SAME NPC's last offer is still inside the cooldown window. Deliberately reuses
        ///   <see cref="InterceptionPacingRules.CooldownDays" /> rather than a second number, so "how readily an
        ///   NPC seeks the player out" reads as one dial whether the NPC rides out or is simply found standing
        ///   where the player walked in.
        /// </summary>
        public static bool IsOnCooldown(int lastOfferedDay, int today, InterceptionPacing pacing)
            => lastOfferedDay != NeverOffered && today - lastOfferedDay < InterceptionPacingRules.CooldownDays(pacing);

        /// <summary>
        ///   The single strongest fief-valid candidate, or null when none qualify. Deterministic tie-break by
        ///   HeroId so the SAME roster always yields the SAME winner regardless of scan order -- mirrors
        ///   CompanionAudienceTopicBehavior's own StringId ordering, for the same reason (a settlement's Notables
        ///   enumeration order is not guaranteed stable).
        /// </summary>
        public static FiefAudienceCandidate SelectCandidate(IEnumerable<FiefAudienceCandidate> candidates)
            => candidates?
               .Where(c => c != null && c.Trigger != InterceptionTrigger.None && !string.IsNullOrEmpty(c.HeroId))
               .OrderBy(c => c.HeroId, StringComparer.Ordinal)
               .FirstOrDefault();
    }
}
