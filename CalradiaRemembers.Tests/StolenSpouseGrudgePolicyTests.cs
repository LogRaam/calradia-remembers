// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 3: sizes the jilted spouse's grudge when the player's affair ends their
// marriage. The behavior plants this as a GrudgeSource.StolenSpouse grudge against the player; it feeds nemesis
// and the vanilla Blood Feuds. If it drifts, being left for the player either barely registers (no consequence
// to the betrayal, so the pillar has no bite) or plants a life-sentence no amends could outlive (the grudge
// system is meant to let deeds win someone round in time, which the cap protects).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StolenSpouseGrudgePolicyTests
   {
      // A neutral marriage (no special love, no forgiveness) must still land a real, baseline sting: being left
      // for another is a wound in itself, or the whole consequence of the pillar is toothless.
      [Test]
      public void GIVEN_a_neutral_marriage_WHEN_sizing_the_grudge_THEN_it_is_the_baseline_sting()
      {
         StolenSpouseGrudgePolicy.Magnitude(0, 0).Should().Be(StolenSpouseGrudgePolicy.BaseMagnitude);
      }

      // The love-wound: a spouse who dearly loved the partner who left is more deeply wounded than one who barely
      // did, so the grudge grows with the relation to the departed. Without this, losing a beloved and losing a
      // stranger-spouse would sting the same.
      [Test]
      public void GIVEN_a_dearly_loved_partner_lost_WHEN_sizing_the_grudge_THEN_it_cuts_deeper()
      {
         int loved = StolenSpouseGrudgePolicy.Magnitude(60, 0);
         int barely = StolenSpouseGrudgePolicy.Magnitude(0, 0);

         loved.Should().BeGreaterThan(barely);
      }

      // A forgiving nature nurses a lighter grudge: high Mercy must soften the sting, so a merciful spouse can be
      // won round more readily than a bitter one. This is what keeps the consequence personality-true.
      [Test]
      public void GIVEN_a_merciful_spouse_WHEN_sizing_the_grudge_THEN_it_is_softened()
      {
         int merciful = StolenSpouseGrudgePolicy.Magnitude(0, 2);
         int hardhearted = StolenSpouseGrudgePolicy.Magnitude(0, 0);

         merciful.Should().BeLessThan(hardhearted);
      }

      // The floor: even a mild, forgiving spouse must feel SOMETHING at being left for another, or a merciful
      // temperament could zero out the betrayal entirely and the jilted spouse would react as if nothing happened.
      [Test]
      public void GIVEN_a_very_merciful_spouse_WHEN_sizing_the_grudge_THEN_it_never_falls_below_the_floor()
      {
         StolenSpouseGrudgePolicy.Magnitude(0, 100).Should().Be(StolenSpouseGrudgePolicy.MinMagnitude);
      }

      // The cap keeps the arc winnable: even a devoted spouse losing a beloved must not plant a grudge so large
      // no amends could outlive it, or the grudge system's redemption path is a dead letter for this source.
      [Test]
      public void GIVEN_a_devoted_spouse_losing_a_beloved_WHEN_sizing_the_grudge_THEN_it_stops_at_the_winnable_cap()
      {
         StolenSpouseGrudgePolicy.Magnitude(1000, 0).Should().Be(StolenSpouseGrudgePolicy.MaxMagnitude);
      }
   }
}
