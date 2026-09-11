// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: whether a prospective governor ACCEPTS the post the council pledged them.
// This is the FIRST governance executor's own decision seam, kept pure and deterministic (the mod's test harness
// forbids randomness, and a dice roll dressed up as "their judgment" could never be told apart from a bug by a
// test). Author's condition: the NPC MAY refuse, but MECHANICALLY, from their own standing toward the player,
// decided BEFORE the appointment is ever applied at the lift - never narrated as a refusal after the post
// already took effect. That ordering is enforced by the caller (CouncilLift.SettleAppointGovernor calls this
// BEFORE ChangeGovernorAction.Apply); this policy only supplies the verdict.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Whether a governorship pledge is accepted, and a short factual reason when it is not.</summary>
    public readonly struct GovernorAppointmentVerdict
    {
        public GovernorAppointmentVerdict(bool accepted, string refusalReason)
        {
            Accepted = accepted;
            RefusalReason = refusalReason;
        }

        public bool Accepted { get; }

        /// <summary>Null when accepted; a short factual reason when refused.</summary>
        public string RefusalReason { get; }
    }

    /// <summary>Decides whether the prospective governor accepts the post, purely from their standing toward the player.</summary>
    public static class GovernorAppointmentPolicy
    {
        /// <summary>
        ///   A regard at/above this marks a companion who holds the player in HIGH esteem. No longer the
        ///   governor-acceptance bar (a governorship is an honour accepted by default, see <see cref="Decide" />);
        ///   kept because other surfaces read it as a "high standing" marker (e.g. the companion-audience topic
        ///   resolver deciding whether to surface ambition-level matters).
        /// </summary>
        public const int StandingFloor = 20;

        /// <summary>
        ///   Tuning: a governorship is a PROMOTION offered to a member of the player's own clan, so it is accepted
        ///   by default; only genuine resentment refuses. Standing at/below this (the "resentment" regard band) is
        ///   the sole refusal: a clan member who actively despises the player will not take an honour from their hand.
        /// </summary>
        public const int ResentmentFloor = -30;

        /// <summary>Read when a verdict is refused for deep resentment (the only refusal case).</summary>
        public const string ResentfulRefusalReason = "resents the player too deeply to accept the honour";

        /// <summary>
        ///   Accept/refuse verdict for a governorship. It is an HONOUR, a promotion within the player's own clan,
        ///   not a favour the appointee grants the player (Gabriel 2026-08-27), so a member takes the post gladly
        ///   whatever their ordinary standing; ONLY active resentment (standing at/below <see cref="ResentmentFloor" />)
        ///   refuses. Deterministic (no randomness): the SAME pledge always resolves the SAME way at the lift, so a
        ///   test can pin the boundary exactly rather than chase a flaky roll.
        /// </summary>
        public static GovernorAppointmentVerdict Decide(int standingTowardPlayer)
            => standingTowardPlayer > ResentmentFloor
                ? new GovernorAppointmentVerdict(true, null)
                : new GovernorAppointmentVerdict(false, ResentfulRefusalReason);

        /// <summary>
        ///   The six facts the 1:1 grounding reads before the LLM is even shown appoint_governor.
        ///   SIX booleans in a row, of which three were about the appointee and three about the seat: a
        ///   swap between "is of the player's clan" and "is not already governing elsewhere" reads the
        ///   same to the compiler and double-posts a hero over two fiefs.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class AppointmentOfferFacts
        {
           /// <summary>The player still heads their own clan. Only its head hands out its governorships.</summary>
           public bool PlayerLeadsClan { get; set; }

           /// <summary>
           ///   The prospective governor belongs to that same clan, since a governor holds one of its OWN fiefs.
           /// </summary>
           public bool AppointeeInPlayerClan { get; set; }

           /// <summary>They are alive and free, not held prisoner somewhere.</summary>
           public bool AppointeeAvailable { get; set; }

           /// <summary>The clan actually holds a town or castle with no governor seated.</summary>
           public bool HasVacantGovernableFief { get; set; }

           /// <summary>They are not already seated over another fief. One governor, one seat.</summary>
           public bool AppointeeNotGoverningElsewhere { get; set; }

           /// <summary>They are not away on a companion errand, so they cannot be seated in absentia.</summary>
           public bool AppointeeNotAway { get; set; }
        }

        /// <summary>
        ///   Whether the 1:1 conversation path should TEACH this NPC the appoint_governor action this turn
        ///   (2026-08-05, the reference personal-command verb brought to BOTH the council and 1:1 pipelines). It
        ///   reduces the SAME eligibility the council's own offering proves before teaching it there
        ///   (ResolutionOfferingResolver: a member is available AND the player's clan holds a fief with no
        ///   governor) to the four facts the 1:1 grounding reads: the player must still lead their clan (only its
        ///   head hands out its governorships), the prospective governor must be an available member of that same
        ///   clan (a companion or clan lord, alive and free, since a governor holds one of the clan's OWN fiefs),
        ///   and that clan must actually hold a town or castle with no governor seated. Pure and total, mirroring
        ///   VassalageOfferPolicy.CanJoin / EscortEligibilityPolicy.CanEscort: the bridge re-checks the leadership,
        ///   the fief, and the standing floor again at commit time (CourtActionResolver.TryAppointGovernor), so a
        ///   true answer here only ever OFFERS a teaching, it never seals a deed.
        ///   <para>
        ///   v1.34.2 governance hardening: two more appointee facts join the conjunction so the offer is withheld
        ///   whenever the bridge would refuse for them. <c>AppointeeNotGoverningElsewhere</c> keeps a
        ///   hero already seated over one fief from being double-posted to a second (one governor, one seat), and
        ///   <c>AppointeeNotAway</c> keeps a companion currently away on an errand from being seated
        ///   in absentia. Both are re-checked at commit (CourtActionResolver.TryAppointGovernor), the same
        ///   prompt-is-advice, executor-is-law discipline every other fact here follows.
        ///   </para>
        /// </summary>
        public static bool CanOfferAppointment(AppointmentOfferFacts facts)
        {
           if (facts == null) return false;

           bool playerLeadsClan = facts.PlayerLeadsClan;
           bool appointeeInPlayerClan = facts.AppointeeInPlayerClan;
           bool appointeeAvailable = facts.AppointeeAvailable;
           bool hasVacantGovernableFief = facts.HasVacantGovernableFief;
           bool appointeeNotGoverningElsewhere = facts.AppointeeNotGoverningElsewhere;
           bool appointeeNotAway = facts.AppointeeNotAway;

           return playerLeadsClan && appointeeInPlayerClan && appointeeAvailable && hasVacantGovernableFief
              && appointeeNotGoverningElsewhere && appointeeNotAway;
        }
    }
}
