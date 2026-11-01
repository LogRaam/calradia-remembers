// Code written by Gabriel Mailhot, 01/08/2026.
// This policy decides the base priority weight of a letter's subject. Since the letter box removed the mailbox budget
// (31/10/2026) it decides which of ONE sender's ready letters goes first (LetterMailboxPolicy.ReleasedToday, via
// LetterCandidate.Weight); before, it decided which single letter of the day reached the player at all. If the tiers
// are wrong here, a lord with both an intrigue and a birthday note to send sends the note, or an unmapped LetterReason
// scores zero and is never his first choice.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterWeightsTests
    {
        // The exhaustiveness anchor for Task 1's graceful degrade: BaseWeight no longer throws on an
        // unmapped reason (see the test below), so this dictionary is the ONLY thing left that pins each
        // LetterReason to its exact designed weight. If a future Sprint adds a value here without also
        // updating LetterWeights.BaseWeight's switch, the production code silently degrades to
        // UnmappedDefaultWeight (25) while this test keeps demanding the real tier, so the oubli is still
        // caught at dev time even though the live daily tick can never crash over it.
        private static readonly Dictionary<LetterReason, int> ExpectedWeights = new Dictionary<LetterReason, int>
        {
            { LetterReason.Blackmail, LetterWeights.WeightBlackmail },
            { LetterReason.ReinforcementRequest, LetterWeights.WeightReinforcementRequest },
            { LetterReason.JealousThreat, LetterWeights.WeightJealousThreat },
            { LetterReason.CorruptionAttempt, LetterWeights.WeightCorruptionAttempt },
            { LetterReason.LeakedCorrespondence, LetterWeights.WeightLeakedCorrespondence },
            { LetterReason.PrisonerOffer, LetterWeights.WeightPrisonerOffer },
            { LetterReason.ServiceOffer, LetterWeights.WeightServiceOffer },
            { LetterReason.MarriageProposal, LetterWeights.WeightMarriageProposal },
            { LetterReason.QuestUpdate, LetterWeights.WeightQuestUpdate },
            { LetterReason.StanceHostility, LetterWeights.WeightStanceHostility },
            { LetterReason.PoliticalAlliance, LetterWeights.WeightPoliticalAlliance },
            { LetterReason.GangFavor, LetterWeights.WeightGangFavor },
            { LetterReason.BastardMotherNote, LetterWeights.WeightBastardMotherNote },
            { LetterReason.InterceptionMissed, LetterWeights.WeightInterceptionMissed },

            { LetterReason.AwaitingReply, LetterWeights.WeightAwaitingReply },
            { LetterReason.RomanticCorrespondence, LetterWeights.WeightRomanticCorrespondence },
            { LetterReason.SpouseCorrespondence, LetterWeights.WeightSpouseCorrespondence },
            { LetterReason.StanceFavor, LetterWeights.WeightStanceFavor },
            { LetterReason.RealmTidings, LetterWeights.WeightRealmTidings },

            { LetterReason.BirthAnnouncement, LetterWeights.WeightBirthAnnouncement },
            { LetterReason.TournamentVictory, LetterWeights.WeightTournamentVictory },
            { LetterReason.TournamentResult, LetterWeights.WeightTournamentResult },
            { LetterReason.BattleVictory, LetterWeights.BattleBaseWeight },
            // A lord's own aim (CrAims, 28/10/2026): paced by the mailbox like a missed meeting.
            { LetterReason.StandingAim, LetterWeights.WeightStandingAim },
            { LetterReason.AbandonedReproach, LetterWeights.WeightAbandonedReproach },
            { LetterReason.VillagesSpared, LetterWeights.WeightVillagesSpared },

            { LetterReason.ChildSupportRequest, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.SpouseDivorceDemand, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.SpouseDivorceEscalation, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.CompanionFarewell, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.PlayerLetterReply, LetterWeights.WeightGuaranteedBypassSentinel },
            // The vassal war duty (15/10/2026): a summons and a levy carry a deadline the game holds the player to.
            { LetterReason.WarSummons, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.WarLevyDemand, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.PatrolReport, LetterWeights.WeightGuaranteedBypassSentinel },
            { LetterReason.FromAnotherMod, LetterWeights.WeightGuaranteedBypassSentinel }
        };

        // The exhaustiveness guarantee, moved here now that production degrades gracefully instead of
        // throwing: every LetterReason the SDK defines must appear in ExpectedWeights AND BaseWeight must
        // return exactly that pinned value, not the graceful default. A forgotten mapping fails THIS test
        // loudly at dev time, which is exactly where a forgotten mapping belongs.
        [Test]
        public void GIVEN_every_LetterReason_the_SDK_defines_WHEN_weighed_THEN_each_one_is_explicitly_mapped_to_its_pinned_tier()
        {
            foreach (LetterReason reason in Enum.GetValues(typeof(LetterReason)).Cast<LetterReason>())
            {
                ExpectedWeights.Should().ContainKey(reason,
                    because: $"{reason} must have an explicit expected weight pinned here or a forgotten " +
                              "production mapping would silently degrade and go unnoticed");
                LetterWeights.BaseWeight(reason).Should().Be(ExpectedWeights[reason]);
            }
        }

        // Task 1's other half: the live daily tick must never crash because a future Sprint forgot a
        // mapping. A genuinely unmapped reason (one no current enum value can produce) must degrade to the
        // low named default rather than throwing, so an oubli costs a quiet correspondent, not a crashed
        // save.
        [Test]
        public void GIVEN_a_LetterReason_nobody_has_mapped_yet_WHEN_weighed_THEN_it_degrades_to_the_low_default_instead_of_throwing()
        {
            var unmapped = (LetterReason)9999;

            Action act = () => LetterWeights.BaseWeight(unmapped);

            act.Should().NotThrow();
            LetterWeights.BaseWeight(unmapped).Should().Be(LetterWeights.UnmappedDefaultWeight);
        }

        // A blackmail letter losing the mailbox slot to a tournament congratulation would be the scheduler's
        // worst possible mistake: the player must be told about a threat to their secrets before they hear
        // about a joust. This is the load-bearing ordering the whole tier system exists to guarantee.
        [Test]
        public void GIVEN_an_intrigue_letter_and_a_flavor_letter_WHEN_weighed_THEN_intrigue_outranks_flavor()
        {
            LetterWeights.BaseWeight(LetterReason.Blackmail)
                .Should().BeGreaterThan(LetterWeights.BaseWeight(LetterReason.TournamentVictory));
            LetterWeights.BaseWeight(LetterReason.JealousThreat)
                .Should().BeGreaterThan(LetterWeights.BaseWeight(LetterReason.BirthAnnouncement));
        }

        // A palier a designer can no longer reason about is a palier nobody will tune correctly. Each of these
        // fixed points is the exact number the design brief specified; drifting from any of them changes how
        // remarkable a battle has to be before it earns a letter that can compete with real intrigue.
        [TestCase(0.4, 20)]
        [TestCase(1.0, 30)]
        [TestCase(1.5, 50)]
        [TestCase(2.0, 70)]
        [TestCase(3.0, 95)]
        public void GIVEN_a_disadvantage_ratio_WHEN_the_battle_victory_weight_is_computed_THEN_it_matches_the_agreed_palier(double ratio, int expectedWeight)
        {
            LetterWeights.BattleVictoryWeight(ratio).Should().Be(expectedWeight);
        }

        // If a harder-won fight could ever score LOWER than an easier one, "a remarkable deed breaks through"
        // would be a lie: the player could win against overwhelming odds and get a quieter letter than a
        // near-even skirmish. The bonus must never go backwards as the odds worsen.
        [Test]
        public void GIVEN_ratios_from_even_upward_WHEN_compared_THEN_the_weight_never_decreases()
        {
            double[] ratios = { 1.0, 1.2, 1.5, 1.8, 2.0, 2.4, 2.625, 3.0, 4.0 };

            int previous = int.MinValue;
            foreach (double ratio in ratios)
            {
                int weight = LetterWeights.BattleVictoryWeight(ratio);
                weight.Should().BeGreaterThanOrEqualTo(previous);
                previous = weight;
            }
        }

        // The stomp floor exists so that curb-stomping a much weaker enemy is never mistaken for the
        // "remarkable deed" this bonus rewards. Anything below the threshold must sit BELOW ordinary flavor,
        // not merely at the base battle weight, or a farmer-militia rout would read as a real accomplishment.
        [Test]
        public void GIVEN_a_lopsided_stomp_WHEN_weighed_THEN_it_scores_below_ordinary_flavor_not_merely_at_the_base()
        {
            LetterWeights.BattleVictoryWeight(0.1).Should().BeLessThan(LetterWeights.WeightTournamentVictory);
        }

        // The five bypass reasons never actually compete in LetterMailboxPolicy (the caller filters them out
        // before candidates are built), but if one ever slipped through by caller error, it must still win
        // outright rather than getting buried by an ordinary intrigue letter. The sentinel above every tier
        // guarantees that a mishandled bypass reason never gets starved instead.
        [TestCase(LetterReason.ChildSupportRequest)]
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        public void GIVEN_a_guaranteed_bypass_reason_WHEN_weighed_THEN_it_carries_the_sentinel_above_every_other_tier(LetterReason reason)
        {
            LetterWeights.BaseWeight(reason).Should().Be(LetterWeights.WeightGuaranteedBypassSentinel);
            LetterWeights.BaseWeight(reason).Should().BeGreaterThan(LetterWeights.WeightBlackmail);
        }

        // The whole point of the interest floor: a correspondent the player has completely soured on must
        // still get a sliver of a chance, never true zero. Three dampers stacked past the floor must still
        // land exactly on the floor, not below it.
        [Test]
        public void GIVEN_dampers_that_would_push_below_the_floor_WHEN_combined_with_a_base_weight_THEN_the_result_floors_at_ten()
        {
            LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.WeightBirthAnnouncement, SenderInterest = -40, SubjectInterest = -25, GlobalInterest = -30})
                .Should().Be(LetterWeights.EffectiveWeightFloor);
        }

        // A damper is exactly that: it must genuinely lower the weight it is applied to, or the whole
        // adaptive layer is a no-op wearing a name.
        [Test]
        public void GIVEN_a_negative_interest_damper_WHEN_applied_THEN_the_weight_drops_below_the_base()
        {
            int withoutDamper = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.WeightRomanticCorrespondence, SenderInterest = 0, SubjectInterest = 0, GlobalInterest = 0});
            int withDamper = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.WeightRomanticCorrespondence, SenderInterest = -10, SubjectInterest = -5, GlobalInterest = 0});

            withDamper.Should().BeLessThan(withoutDamper);
        }

        // The caller clamps the three interest arguments before calling this method; WeightWithInterest
        // itself must not re-clamp or reject a positive value, it only sums and floors. A caller who passes
        // a positive interest (a bug elsewhere) still gets an arithmetically honest sum back.
        [Test]
        public void GIVEN_an_interest_value_above_zero_WHEN_applied_THEN_it_still_simply_adds_no_extra_clamping_here()
        {
            LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.WeightTournamentVictory, SenderInterest = 10, SubjectInterest = 0, GlobalInterest = 0})
                .Should().Be(LetterWeights.WeightTournamentVictory + 10);
        }

        // With every interest neutral, the method must be a pure pass-through: the adaptive layer should
        // never nudge a weight the player has given no reason to move.
        [Test]
        public void GIVEN_all_interests_are_neutral_WHEN_applied_THEN_the_base_weight_passes_through_unchanged()
        {
            LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.WeightQuestUpdate, SenderInterest = 0, SubjectInterest = 0, GlobalInterest = 0})
                .Should().Be(LetterWeights.WeightQuestUpdate);
        }
    }
}
