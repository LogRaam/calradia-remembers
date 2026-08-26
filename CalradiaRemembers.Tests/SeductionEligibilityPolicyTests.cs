// Code written by Gabriel Mailhot, 26/08/2026.
// This policy is the first gate of the Stolen Love pillar: whether a married NPC can be seduced by the
// player at all, before anything about breaking the marriage begins. Increment 1's seduction entry point
// consumes the Seducible/Reason verdict directly; Increment 2's estrangement pacing is meant to read the
// Difficulty to decide how long an affair takes to surface. Get the hard regard floor wrong and the path
// becomes a cheat that can steal ANY spouse regardless of personality; get the honour/devotion weighting
// wrong and either the feature never fires for anyone whose spouse isn't already loathed, or it turns every
// marriage in Calradia into a lock waiting for a big enough regard number.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SeductionEligibilityPolicyTests
   {
      // Without a real caller, Evaluate(null) must degrade gracefully rather than throw: a missing-facts call
      // should read as the hardest possible refusal, never crash the seduction attempt outright.
      [Test]
      public void GIVEN_no_facts_WHEN_evaluating_THEN_it_refuses_without_throwing()
      {
         SeductionEligibilityPolicy.Evaluate(null).Seducible.Should().BeFalse();
         SeductionEligibilityPolicy.Evaluate(null).Difficulty.Should().Be(SeductionDifficulty.VeryHard);
      }

      // The hard precondition: without this, a player could talk a total stranger's spouse into bed on the
      // strength of a happy-go-lucky personality alone. Nothing else in the formula is allowed to buy past a
      // regard this low; the shortfall must be blamed on regard and nothing else, however unhappy the marriage.
      [Test]
      public void GIVEN_regard_below_the_floor_WHEN_evaluating_THEN_it_is_refused_for_regard_alone()
      {
         var facts = new SeductionFacts {
            RegardWithPlayer = 20,
            NpcSpouseRelation = -100,
            NpcHonor = -2,
            NpcCalculating = 2,
            HasChildrenTogether = true,
            MarriageDurationDays = 5000
         };

         var verdict = SeductionEligibilityPolicy.Evaluate(facts);

         verdict.Seducible.Should().BeFalse();
         verdict.Reason.Should().Contain("regard");
      }

      // The boundary that defines the hard gate: one regard point below RegardFloor is refused SOLELY for
      // regard, while sitting exactly at the floor must clear the hard gate (even if the softer weighing below
      // still refuses it for another reason). If this drifted, the floor would either block a fully-qualified
      // NPC or silently admit one a point short.
      [Test]
      public void GIVEN_regard_one_point_short_of_the_floor_WHEN_evaluating_THEN_only_the_floor_case_names_regard()
      {
         var belowFloor = new SeductionFacts { RegardWithPlayer = SeductionEligibilityPolicy.RegardFloor - 1 };
         var atFloor = new SeductionFacts { RegardWithPlayer = SeductionEligibilityPolicy.RegardFloor };

         SeductionEligibilityPolicy.Evaluate(belowFloor).Reason.Should().Be("does not regard you nearly enough yet");
         SeductionEligibilityPolicy.Evaluate(atFloor).Reason.Should().NotBe("does not regard you nearly enough yet");
      }

      // The dominant resistor named: a devout, happily married NPC must refuse even a well-regarded player, or
      // the honour/devotion weighting would mean nothing for the exact personalities it exists to protect. The
      // Reason must point at the marriage or the character, never quietly fail with an unhelpful blank.
      [Test]
      public void GIVEN_a_devoted_and_honorable_spouse_WHEN_evaluating_at_high_regard_THEN_it_is_refused_for_devotion_or_honor()
      {
         var facts = new SeductionFacts {
            RegardWithPlayer = 90,
            NpcSpouseRelation = 80,
            NpcHonor = 2,
            NpcCalculating = 0
         };

         var verdict = SeductionEligibilityPolicy.Evaluate(facts);

         verdict.Seducible.Should().BeFalse();
         (verdict.Reason.Contains("devoted") || verdict.Reason.Contains("honorable")).Should().BeTrue();
      }

      // Isolates the OTHER resistance branch from the previous test: with the marriage itself neutral (no
      // devotion pull) but Honor maxed, the refusal must name honour specifically, proving the dominant-resistor
      // pick isn't accidentally always landing on "devoted" regardless of which force actually did the work.
      [Test]
      public void GIVEN_a_neutral_marriage_but_maximum_honor_WHEN_evaluating_THEN_it_names_honor_as_the_blocker()
      {
         var facts = new SeductionFacts {
            RegardWithPlayer = 90,
            NpcSpouseRelation = 0,
            NpcHonor = 2,
            NpcCalculating = 0
         };

         SeductionEligibilityPolicy.Evaluate(facts).Reason.Should().Be("too honorable to stray");
      }

      // The other side of the gate has to actually open: an unhappy marriage plus real regard plus a spouse
      // with no honour to speak of is precisely the case the whole pillar exists to unlock. If this refused,
      // the feature would never fire for anyone except spouses already publicly despised.
      [Test]
      public void GIVEN_an_unhappy_marriage_and_high_regard_and_low_honor_WHEN_evaluating_THEN_it_is_seducible()
      {
         var facts = new SeductionFacts {
            RegardWithPlayer = 80,
            NpcSpouseRelation = -60,
            NpcHonor = 0,
            NpcCalculating = 0
         };

         SeductionEligibilityPolicy.Evaluate(facts).Seducible.Should().BeTrue();
         SeductionEligibilityPolicy.IsSeducible(facts).Should().BeTrue();
      }

      // Children must raise the bar, not merely flavour text: a couple bound by a child is a different, harder
      // case than a childless one at the same regard and marriage temperature. Without this, "HasChildrenTogether"
      // would be a fact the policy reads and then ignores, and the affair would cost a real family nothing extra.
      [Test]
      public void GIVEN_a_borderline_case_WHEN_children_are_shared_THEN_a_seducible_case_becomes_refused()
      {
         var childless = new SeductionFacts { RegardWithPlayer = 50, NpcSpouseRelation = -10 };
         var withChildren = new SeductionFacts { RegardWithPlayer = 50, NpcSpouseRelation = -10, HasChildrenTogether = true };

         SeductionEligibilityPolicy.Evaluate(childless).Seducible.Should().BeTrue();
         SeductionEligibilityPolicy.Evaluate(withChildren).Seducible.Should().BeFalse();
      }

      // A long marriage should mildly raise the bar too, per the same "raises the bar, not flavour text" logic
      // as children: a decade-old bond has to weigh SOMETHING, or MarriageDurationDays is a fact the policy
      // reads and never actually uses.
      [Test]
      public void GIVEN_a_borderline_case_WHEN_the_marriage_is_a_decade_old_THEN_a_seducible_case_becomes_refused()
      {
         var freshMarriage = new SeductionFacts { RegardWithPlayer = 48, MarriageDurationDays = 0 };
         var oldMarriage = new SeductionFacts { RegardWithPlayer = 48, MarriageDurationDays = 3650 };

         SeductionEligibilityPolicy.Evaluate(freshMarriage).Seducible.Should().BeTrue();
         SeductionEligibilityPolicy.Evaluate(oldMarriage).Seducible.Should().BeFalse();
      }

      // A schemer must be more willing to betray, not merely equally reluctant: without Calculating actually
      // moving the needle, a personality trait the design calls out by name would have no mechanical weight at
      // all, and every NPC would resist identically regardless of character.
      [Test]
      public void GIVEN_a_borderline_case_WHEN_the_npc_is_highly_calculating_THEN_a_refused_case_becomes_seducible()
      {
         var plainNpc = new SeductionFacts { RegardWithPlayer = 45, NpcSpouseRelation = 20 };
         var calculatingNpc = new SeductionFacts { RegardWithPlayer = 45, NpcSpouseRelation = 20, NpcCalculating = 2 };

         SeductionEligibilityPolicy.Evaluate(plainNpc).Seducible.Should().BeFalse();
         SeductionEligibilityPolicy.Evaluate(calculatingNpc).Seducible.Should().BeTrue();
      }

      // Difficulty is the signal Inc 2's estrangement pacing is meant to read, so it must actually track how
      // narrowly the case passed. If a razor-thin pass and a wide-open one reported the same difficulty, the
      // later pacing would have nothing honest to key off and every affair would surface at the same speed.
      [Test]
      public void GIVEN_a_barely_passing_case_and_a_wide_open_one_WHEN_evaluating_THEN_the_barely_passing_case_is_harder()
      {
         var barelyPassing = new SeductionFacts { RegardWithPlayer = 45 };
         var wideOpen = new SeductionFacts { RegardWithPlayer = 100, NpcSpouseRelation = -100, NpcCalculating = 2 };

         SeductionEligibilityPolicy.Evaluate(barelyPassing).Difficulty.Should().Be(SeductionDifficulty.VeryHard);
         SeductionEligibilityPolicy.Evaluate(wideOpen).Difficulty.Should().Be(SeductionDifficulty.Easy);
      }
   }
}
