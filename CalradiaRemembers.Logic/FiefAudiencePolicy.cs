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
        public FiefAudienceCandidate(string heroId, InterceptionTrigger trigger, string detail = null)
        {
            HeroId = heroId;
            Trigger = trigger;
            Detail = detail;
        }

        /// <summary>The candidate's StringId.</summary>
        public string HeroId { get; }

        /// <summary>The fief-valid reason this candidate qualifies. Never <see cref="InterceptionTrigger.None" /> in a returned candidate.</summary>
        public InterceptionTrigger Trigger { get; }

        /// <summary>
        ///   The specific grievance or kindness this candidate's <see cref="Trigger" /> would voice
        ///   (<see cref="InterceptionTrigger.Grudge" />/<see cref="InterceptionTrigger.AppreciationOwed" />
        ///   only, a cause summary from the underlying ledger entry). Null for every trigger that carries no
        ///   such specific detail (<see cref="InterceptionTrigger.HeardOfDeed" /> today).
        /// </summary>
        public string Detail { get; }
    }

    /// <summary>Who a person standing in the settlement is, for whether they may ask for an audience at all.</summary>
    public sealed class FiefAudienceSeekerFacts
    {
        /// <summary>They stand in the settlement the player just entered.</summary>
        public bool StandsInThisSettlement { get; set; }

        /// <summary>They are a prisoner.</summary>
        public bool IsPrisoner { get; set; }

        /// <summary>They lead or ride in a field party (such a one meets the player on the road instead).</summary>
        public bool HasFieldParty { get; set; }

        /// <summary>They are a child (Hero.IsChild): a child never asks for an audience, as a child never writes first.</summary>
        public bool IsChild { get; set; }

        /// <summary>
        ///   They are the realm's ruler calling the player in over the war duty. A ruler sitting in his own town still
        ///   has a field party, but a summoned vassal who walks into his town is called to the hall all the same.
        /// </summary>
        public bool SummonsThePlayer { get; set; }
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
        ///   Whether this person may ask the player for an audience here: standing in this settlement, free, with no
        ///   field party, and not a child (Nexusrrr, 28/09/2026: a baby son asked to discuss warfare).
        /// </summary>
        public static bool MaySeekAudience(FiefAudienceSeekerFacts f)
            => f != null && f.StandsInThisSettlement && (!f.HasFieldParty || f.SummonsThePlayer)
               && NpcActionGate.Allows(new ActorFacts {IsChild = f.IsChild, IsPrisoner = f.IsPrisoner});

        /// <summary>
        ///   Whether a trigger reads believably for someone already standing in the settlement.
        ///   <see cref="InterceptionTrigger.TerritorialPatrol" /> is written around a lord riding out to
        ///   challenge a traveller CROSSING their land (its own doc: "a hostile-realm lord challenges a
        ///   notorious traveller crossing their land") -- a fief-bound NPC standing beside the player is not
        ///   intercepting a crossing, so that trigger stays road-only. <see cref="InterceptionTrigger.HeardOfDeed" />
        ///   ("a lord who has heard of a recent deed... rides out to react while it is fresh") reads just as
        ///   well from someone who simply asks for a word where they already stand. So do
        ///   <see cref="InterceptionTrigger.Grudge" /> and <see cref="InterceptionTrigger.AppreciationOwed" />:
        ///   a grievance or a felt kindness is place-independent, unlike a claim about crossing a border, so
        ///   both are just as believable voiced by someone who never moved. <see cref="InterceptionTrigger.NemesisBounty" />
        ///   is the same: a lord raising a nemesis plaguing his region is just as plausible standing in his
        ///   own fief as riding out to say it. <see cref="InterceptionTrigger.WarDutySummons" /> above all: a summoned
        ///   vassal who walks into his liege's town is called to the hall.
        /// </summary>
        public static bool IsFiefValid(InterceptionTrigger trigger)
            => trigger == InterceptionTrigger.HeardOfDeed
               || trigger == InterceptionTrigger.Grudge
               || trigger == InterceptionTrigger.AppreciationOwed
               || trigger == InterceptionTrigger.NemesisBounty
               || trigger == InterceptionTrigger.WarDutySummons
               || trigger == InterceptionTrigger.StandingAim;

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
        ///   enumeration order is not guaranteed stable). A liege calling in his summoned vassal comes before anyone
        ///   else in the town: the one meeting the vassal is bound to answer.
        /// </summary>
        public static FiefAudienceCandidate SelectCandidate(IEnumerable<FiefAudienceCandidate> candidates)
            => candidates?
               .Where(c => c != null && c.Trigger != InterceptionTrigger.None && !string.IsNullOrEmpty(c.HeroId))
               .OrderByDescending(c => c.Trigger == InterceptionTrigger.WarDutySummons)
               .ThenBy(c => c.HeroId, StringComparer.Ordinal)
               .FirstOrDefault();
    }
}
