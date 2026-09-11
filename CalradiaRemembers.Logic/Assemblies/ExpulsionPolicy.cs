// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "expel_from_clan"): the pure gate
// behind the council's harshest INTERNAL sanction, the player-chief casting a seated COMPANION out of their own
// clan entirely. Gabriel's own words on the sibling action revoke_fief (COUNCIL_ACTIONS.md) apply just as much
// here ("une des pires punitions qui puisse être faite"), so the guard shape and the grudge/relation cost mirror
// FiefTransferPolicy's own discipline (constants named and tested here, never a magic number in the lift
// executor).
//
// SCOPE DEVIATION (documented, not silently followed): this build's own brief named CouncilScope.Family and
// CouncilScope.ClanLords as expel_from_clan's home, mirroring COUNCIL_ACTIONS.md's own text ("conseil de famille
// ou de clan"). CouncilRosterResolver seats NEITHER scope with a companion in practice, though: Family is
// spouse/siblings/adult children by blood or marriage (ImmediateFamily), ClanLords is Hero.IsLord clan members
// (CandidatePool), and Hero.IsPlayerCompanion (CompanionOf == Clan.PlayerClan) is disjoint from both in the
// ordinary case (decompiled TaleWorlds.CampaignSystem.Hero: IsLord => Occupation == Occupation.Lord, a hired
// companion's occupation is never Lord). COUNCIL_ACTIONS.md's own Kimi review is explicit, too, that a
// fief-holding LORD cannot be "expelled" this way at all (a lord IS effectively their own clan once they hold a
// fief; they secede instead, a wholly different mechanic) - so ClanLords is the wrong home for this kind even
// setting the companions-only restriction aside. A companions-only expel_from_clan offered at Family/ClanLords
// would therefore be permanently unofferable dead code (the offering fact could never turn true). This build
// instead offers it at CouncilScope.Companions, the one roster that actually seats a live companion, mirroring
// every other companion-fate kind already scoped there (AssignPartyRole, AppointGovernor, DispatchOnMission,
// GrantStipend, RejoinParty). Family-member expulsion (the other "non-chef" case COUNCIL_ACTIONS.md allows) is
// left as a documented follow-up, exactly as this build's own brief anticipated ("a family member's expulsion is
// murky").
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for expel_from_clan: whether the player may cast a seated companion out of their own clan right now.</summary>
    public static class ExpulsionPolicy
    {
        /// <summary>
        ///   Tuning: the grudge magnitude expel_from_clan plants on the expelled companion. COUNCIL_ACTIONS.md's
        ///   Kimi review proposes 50-70 ("la pire humiliation sociale après la confiscation [de fief]"); this
        ///   sits at the middle of that range, deliberately below FiefTransferPolicy.RevokeGrudgeMagnitude (100,
        ///   the harshest deed in the catalogue): Gabriel's own words reserve that ceiling for stripping a
        ///   vassal's own fief specifically, and this build keeps expulsion one notch below it.
        /// </summary>
        public const int GrudgeMagnitude = 60;

        /// <summary>
        ///   Tuning: the relation hit expel_from_clan lands on the expelled companion. COUNCIL_ACTIONS.md's own
        ///   expel_from_clan section names no figure directly; this scales FiefTransferPolicy.RevokeRelationHit's
        ///   own middle-of-range choice (-50) down in the same proportion as GrudgeMagnitude sits below
        ///   RevokeGrudgeMagnitude (60/100), landing at -30: a real, felt cost without pretending a hired
        ///   companion's dismissal carries the exact same weight as stripping a vassal's own house of its fief.
        /// </summary>
        public const int RelationHit = -30;

        /// <summary>
        ///   expel_from_clan's gate: legal only when the player actually LEADS their own clan
        ///   (Clan.PlayerClan.Leader == Hero.MainHero - a companion's hire/fire is a genuine CLAN-internal
        ///   decision, unlike the cross-clan sovereignty FiefTransferPolicy needs), the target is a live
        ///   COMPANION of that same clan (never a family member or a fief-holding lord, both out of scope for
        ///   this build, see this file's own header), is not the player's own spouse (that bond has its own
        ///   dissolution channel, end_marriage/divorce, never this one), and is alive.
        /// </summary>
        public static bool CanExpel(ExpulsionFacts facts)
        {
           if (facts == null) return false;

           bool playerIsClanLeader = facts.PlayerIsClanLeader;
           bool targetIsCompanion = facts.TargetIsCompanion;
           bool targetIsSpouse = facts.TargetIsSpouse;
           bool targetAlive = facts.TargetAlive;

           return playerIsClanLeader && targetIsCompanion && !targetIsSpouse && targetAlive;
        }

        /// <summary>
        ///   What the gate on expel_from_clan weighs, read off the campaign by the caller. Shared by
        ///   <see cref="CanExpel" /> (the deed's own legality) and <see cref="CanOfferExpel" /> (whether
        ///   the LLM is even shown it), so the two can never drift into disagreeing about a fact.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ExpulsionFacts
        {
           /// <summary>The player heads the clan. A companion's hire and fire is the head's decision.</summary>
           public bool PlayerIsClanLeader { get; set; }

           /// <summary>The target is a companion of that clan, never a family member or a fief-holding lord.</summary>
           public bool TargetIsCompanion { get; set; }

           /// <summary>
           ///   The target is the player's own spouse. That bond dissolves through divorce, never through this.
           /// </summary>
           public bool TargetIsSpouse { get; set; }

           /// <summary>The target is alive.</summary>
           public bool TargetAlive { get; set; }

           /// <summary>
           ///   They are not away on an errand. A companion cannot be cast out from under an in-flight mission.
           /// </summary>
           public bool TargetAvailable { get; set; }
        }

        /// <summary>
        ///   expel_from_clan's 1:1 conversation TEACHING gate (2026-08-05, the suzerain-command batch brought to BOTH
        ///   the council and 1:1 pipelines). It is the whole <see cref="CanExpel" /> conjunction (the player still
        ///   leads their clan, the conversation NPC is a live COMPANION of it, and is not the player's own spouse)
        ///   PLUS the one mechanical fact the council executor also re-checks before firing: the companion is not
        ///   AWAY on a companion-mission errand (<c>TargetAvailable</c>), since a companion cannot be
        ///   cast out from under an in-flight errand. Pure and total, mirroring
        ///   <see cref="GovernorAppointmentPolicy.CanOfferAppointment" />: the bridge re-checks every one of these
        ///   again at commit time (<c>CourtActionResolver.TryExpelFromClan</c>), so a true answer here only ever
        ///   OFFERS a teaching, it never seals a deed.
        /// </summary>
        public static bool CanOfferExpel(ExpulsionFacts facts)
        {
           if (facts == null) return false;

           bool playerIsClanLeader = facts.PlayerIsClanLeader;
           bool targetIsCompanion = facts.TargetIsCompanion;
           bool targetIsSpouse = facts.TargetIsSpouse;
           bool targetAlive = facts.TargetAlive;
           bool targetAvailable = facts.TargetAvailable;

           return ExpulsionPolicy.CanExpel(new ExpulsionPolicy.ExpulsionFacts {PlayerIsClanLeader = playerIsClanLeader, TargetIsCompanion = targetIsCompanion, TargetIsSpouse = targetIsSpouse, TargetAlive = targetAlive}) && targetAvailable;
        }
    }
}
