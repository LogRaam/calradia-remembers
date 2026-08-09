// Code written by Gabriel Mailhot, 09/08/2026.
// Guards OpenPartnerLoverPolicy (Inc 3a): whether an OPEN partner takes a lover of her own this week, and
// whether a scanned candidate is a plausible one. This is the symmetry of the Inc 2 jealousy suppression:
// an open partner who accepts the player's other loves eventually exercises the same freedom herself, the
// likelier the more she is neglected. The weekly-chance curve feeds the mod's autonomy roll; the eligibility
// predicate pins WHO she may take. Get the curve wrong and a well-attended open partner strays constantly
// (drama the open pact was meant to defuse) or a neglected one never does (the feature is dead); get the
// predicate wrong and she takes the player, a married lord, a child, or a stranger, seeding cascade drama
// this increment deliberately keeps out.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OpenPartnerLoverPolicyTests
    {
        // The whole design intent: a well-attended open partner should rarely stray, a long-neglected one much
        // more often. If the chance did not rise with neglect, neglect would carry no consequence and the
        // feature would not model "she exercises the same freedom when the player ignores her" at all.
        [Test]
        public void GIVEN_more_neglect_WHEN_computing_the_weekly_chance_THEN_it_rises()
        {
            int attended = OpenPartnerLoverPolicy.ChancePercent(neglectDays: 0);
            int slighted = OpenPartnerLoverPolicy.ChancePercent(neglectDays: 20);
            int abandoned = OpenPartnerLoverPolicy.ChancePercent(neglectDays: 40);

            attended.Should().Be(OpenPartnerLoverPolicy.BaseChancePercent, "within the grace week she is not neglected, so only the floor applies");
            slighted.Should().BeGreaterThan(attended, "neglect past the grace week must raise the chance");
            abandoned.Should().BeGreaterThan(slighted, "the longer the neglect, the higher the chance");
        }

        // The cap keeps straying a CHANCE and never a certainty: even an utterly abandoned open partner has a
        // real weekly probability of NOT taking a lover. Without the clamp a long neglect would drive the
        // chance past 100 and she would stray every single week, which reads as a scripted punishment.
        [Test]
        public void GIVEN_extreme_neglect_WHEN_computing_the_weekly_chance_THEN_it_clamps_to_the_max()
        {
            OpenPartnerLoverPolicy.ChancePercent(neglectDays: 100000)
                                  .Should().Be(OpenPartnerLoverPolicy.MaxChancePercent, "the chance is capped so straying is never a certainty");
        }

        // A roll UNDER the neglect-scaled chance means she strays; a roll AT or ABOVE it means she does not.
        // Pins the boundary the mod's MBRandom roll is compared against, so the tuning knobs actually govern
        // the outcome rather than an off-by-one letting everyone (or no one) through.
        [Test]
        public void GIVEN_an_open_partner_with_no_lover_WHEN_the_roll_lands_under_the_chance_THEN_she_takes_one()
        {
            int chance = OpenPartnerLoverPolicy.ChancePercent(neglectDays: 30);

            OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: true, alreadyHasLover: false, neglectDays: 30, rollPercent: chance - 1)
                                  .Should().BeTrue("a roll below the chance means she strays this week");
            OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: true, alreadyHasLover: false, neglectDays: 30, rollPercent: chance)
                                  .Should().BeFalse("a roll at the chance is outside it (strictly-less comparison)");
        }

        // Inc 3a forms ONE lover link per open partner: once she has taken a lover, the weekly roll must never
        // give her a second. Without this she would keep re-rolling every week and accumulate lovers, and the
        // later increments (trysts, pregnancy) would have no single, stable lover to build on.
        [Test]
        public void GIVEN_a_partner_who_already_has_a_lover_WHEN_asked_THEN_she_takes_no_other()
        {
            OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: true, alreadyHasLover: true, neglectDays: 100, rollPercent: 0)
                                  .Should().BeFalse("she already has a lover; Inc 3a forms only one link");
        }

        // The gate that keeps this feature confined to open relationships: an ordinary (non-open) partner never
        // takes a lover, no matter how neglected. If suppression leaked here, a monogamous partner would start
        // an affair the player never consented to, which is a wholly different (and unwanted) mechanic.
        [Test]
        public void GIVEN_a_partner_not_on_open_terms_WHEN_asked_THEN_she_never_strays()
        {
            OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: false, alreadyHasLover: false, neglectDays: 100, rollPercent: 0)
                                  .Should().BeFalse("only an open committed partner exercises this freedom");
        }

        // The happy path for the eligibility bar: an unwed, orientation-compatible adult hero she is acquainted
        // with, who is neither the player nor of the player's household, is a plausible lover. This is the exact
        // candidate the mod's scan is meant to accept, so the predicate must let it through.
        [Test]
        public void GIVEN_an_unwed_acquainted_compatible_adult_WHEN_judged_THEN_he_is_a_plausible_lover()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: true, candidateAdult: true, acquainted: true,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: false)
                                  .Should().BeTrue("a valid, acquainted, unwed compatible adult is a believable lover");
        }

        // The player is never her lover-of-her-own: this link exists precisely to model a THIRD party she loves
        // under the open arrangement. Picking the player would collapse the whole point of the increment and
        // would later try to parent her child by "him" to the player, which is the opposite of the design.
        [Test]
        public void GIVEN_the_candidate_is_the_player_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: true, candidateAdult: true, acquainted: true,
                                      isThePlayer: true, isSameClanAsPlayerHousehold: false)
                                  .Should().BeFalse("her own lover is a third party, never the player");
        }

        // A married candidate is barred: taking a wedded lord as a lover would spawn exactly the jealousy
        // cascade (his spouse) this increment is scoped to avoid. She prefers the unwed for that reason.
        [Test]
        public void GIVEN_a_married_candidate_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: false, candidateAdult: true, acquainted: true,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: false)
                                  .Should().BeFalse("a married lord's spouse would be a jealous party, out of scope this increment");
        }

        // A child is never a lover: the adult gate is non-negotiable and independent of every other condition.
        [Test]
        public void GIVEN_a_child_candidate_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: true, candidateAdult: false, acquainted: true,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: false)
                                  .Should().BeFalse("a lover must be an adult");
        }

        // A total stranger is rejected: she takes a lover from among those she actually knows, so a lover link
        // reads as grounded rather than a name plucked at random from the whole world. The mod prefers the
        // best-acquainted eligible candidate, and this is the floor beneath that preference.
        [Test]
        public void GIVEN_a_stranger_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: true, candidateAdult: true, acquainted: false,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: false)
                                  .Should().BeFalse("she takes a lover from among those she is at least acquainted with");
        }

        // A member of the player's own household is barred even when otherwise perfect: a lover inside the
        // player's clan would seed intra-household drama (a companion, a clan spouse) the increment excludes.
        [Test]
        public void GIVEN_a_member_of_the_players_household_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: true,
                                      candidateUnwed: true, candidateAdult: true, acquainted: true,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: true)
                                  .Should().BeFalse("a lover inside the player's own household would cascade drama, out of scope");
        }

        // An orientation-incompatible candidate is rejected: a female hetero partner does not take a woman as a
        // lover. The host computes compatibility from her orientation and the candidate's sex; this pins that
        // the predicate honours it rather than pairing anyone with anyone.
        [Test]
        public void GIVEN_an_orientation_incompatible_candidate_WHEN_judged_THEN_he_is_rejected()
        {
            OpenPartnerLoverPolicy.IsPlausibleLover(
                                      candidateAlive: true, candidateIsHero: true, orientationCompatible: false,
                                      candidateUnwed: true, candidateAdult: true, acquainted: true,
                                      isThePlayer: false, isSameClanAsPlayerHousehold: false)
                                  .Should().BeFalse("she takes a lover she is actually drawn to");
        }

        // ── Culture branch (Inc 3a): WHO may stray, and whether her lover is open or secret ─────────────────

        // An egalitarian wife with NO open pact stays faithful: her culture does not accept a wife straying
        // uninvited, so the ONLY way she takes a lover is a reciprocal open arrangement the player granted.
        // If this leaked, a monogamous-culture wife would start an affair the player never consented to, the
        // exact non-consensual mechanic the design keeps out (she would instead negotiate openness in chat).
        [Test]
        public void GIVEN_an_egalitarian_partner_with_no_open_arrangement_WHEN_asked_THEN_she_may_not_take_a_lover()
        {
            OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement: false, isHaremCulture: false, unsolicitedInfidelityAllowed: true)
                                  .Should().BeFalse("an egalitarian wife strays only under an open arrangement, never uninvited");
        }

        // An egalitarian wife WITH an open pact may take a lover, and that lover is OPEN (known), never secret:
        // the pact is mutual, so she has nothing to hide. Pins that the egalitarian branch keeps Inc 3a's
        // original visible-lover behaviour (a faint public rumour, later surfaced as an already-known lover).
        [Test]
        public void GIVEN_an_egalitarian_partner_with_an_open_arrangement_WHEN_asked_THEN_she_may_take_an_open_lover()
        {
            OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement: true, isHaremCulture: false, unsolicitedInfidelityAllowed: false)
                                  .Should().BeTrue("an open arrangement lets either culture take a lover, and it is not gated by the jealousy toggle");
            OpenPartnerLoverPolicy.LoverIsSecret(isHaremCulture: false)
                                  .Should().BeFalse("an egalitarian wife under an open pact takes an OPEN lover, nothing to hide");
        }

        // A harem-culture wife WITHIN an open arrangement may take a lover, but it is SECRET despite the
        // openness: cultural submission means she hides it from her husband. Pins case (A) of the design, and
        // that the open-arrangement path is not gated by the jealousy toggle (the player opted into openness).
        [Test]
        public void GIVEN_a_harem_partner_with_an_open_arrangement_WHEN_asked_THEN_she_takes_a_secret_lover()
        {
            OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement: true, isHaremCulture: true, unsolicitedInfidelityAllowed: false)
                                  .Should().BeTrue("the open arrangement makes her eligible regardless of the jealousy toggle");
            OpenPartnerLoverPolicy.LoverIsSecret(isHaremCulture: true)
                                  .Should().BeTrue("a harem/patriarchal wife hides her lover even within an open pact");
        }

        // A harem-culture wife with NO open pact, neglected, DOES stray when the jealousy toggle is on, and her
        // affair is SECRET: case (B), the unsolicited-infidelity path. This is the one path the design gates by
        // EnableJealousy; here it is ON, so a neglected harem wife may take a hidden lover the player never sanctioned.
        [Test]
        public void GIVEN_a_harem_partner_with_no_pact_and_the_toggle_on_WHEN_asked_THEN_she_takes_a_secret_affair()
        {
            OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement: false, isHaremCulture: true, unsolicitedInfidelityAllowed: true)
                                  .Should().BeTrue("a neglected harem wife strays even without a pact when the jealousy toggle allows drama");
            OpenPartnerLoverPolicy.LoverIsSecret(isHaremCulture: true)
                                  .Should().BeTrue("an uninvited harem affair is hidden, never an open lover");
        }

        // The safety gate: the SAME harem wife with no pact stays faithful when the jealousy toggle is OFF. A
        // player who turned jealousy off asked for a drama-free household, so no wife starts an unsolicited
        // affair; only the open-arrangement paths (which the player granted) survive the toggle being off.
        [Test]
        public void GIVEN_a_harem_partner_with_no_pact_and_the_toggle_off_WHEN_asked_THEN_she_stays_faithful()
        {
            OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement: false, isHaremCulture: true, unsolicitedInfidelityAllowed: false)
                                  .Should().BeFalse("jealousy off means a drama-free household: no unsolicited infidelity");
        }
    }
}
