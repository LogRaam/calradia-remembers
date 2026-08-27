// Code written by Gabriel Mailhot, 27/08/2026.
// Spotlight pillar. A promoted participant can be ordered back to silence, and whether that lands is what keeps
// the scene believable: a lord's own vassal falling quiet on command reads true, and so does a proud rival
// refusing to be shushed by an equal. Gabriel's ruling is that STANDING (rank and clan influence) dominates
// deterministically, and temper only decides between near-equals. These pin exactly that: the liege and the
// wide-gap cases ignore temper, the extremes never depend on a die of personality, and inside the band an
// agreeable heart yields while a bold, proud one holds the floor. If standing did not dominate, an influential
// lord's command would be at the mercy of the target's mood, which is the opposite of what power means here.

#region

using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class SilenceOrderPolicyTests
   {
      // The clearest deterministic case: a vassal obeys their liege's order to be quiet, however bold or proud
      // they are. If temper could override the liege bond, the whole feudal hierarchy of the scene would be theatre.
      [Test]
      public void GIVEN_the_commander_is_the_targets_liege_WHEN_ordered_silent_THEN_they_obey_whatever_their_temper()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            CommanderIsLiege = true,
            TargetTraits = Defiant()
         }).Should().Be(SilenceResponse.Comply);
      }

      // Gabriel's headline: an influential lord who tells a lesser one to be silent is obeyed, temper aside. This
      // is the wide-standing-gap override, the deterministic role of influence he insisted on.
      [Test]
      public void GIVEN_the_commander_far_outweighs_the_target_WHEN_ordered_silent_THEN_they_obey_whatever_their_temper()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = SilenceOrderPolicy.HardComplyGap,
            TargetTraits = Defiant()
         }).Should().Be(SilenceResponse.Comply);
      }

      // The mirror: a nobody cannot silence a great lord, however meekly or forcefully they try. The order is
      // laughed off by the wide gap the OTHER way, before temper is even weighed.
      [Test]
      public void GIVEN_the_commander_is_far_beneath_the_target_WHEN_ordering_silence_THEN_it_is_defied_whatever_their_temper()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = SilenceOrderPolicy.HardDefyGap,
            TargetTraits = Deferential() // even a meek great lord does not take the order from a nobody
         }).Should().Be(SilenceResponse.Defy);
      }

      // Influence beats temper, pinned head-on: a defiant target under a wide standing gap still complies. This is
      // the exact "influence dominates personality" guarantee, so a bold rival cannot shrug off a powerful command.
      [Test]
      public void GIVEN_a_defiant_target_WHEN_the_standing_gap_is_wide_THEN_influence_wins_and_they_comply()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = SilenceOrderPolicy.HardComplyGap + 2,
            TargetTraits = Defiant()
         }).Should().Be(SilenceResponse.Comply);
      }

      // Between equals of neutral temper the order does NOT land: people do not simply fall silent because a peer
      // told them to. The threshold sits above zero precisely so a flat, equal exchange defaults to holding the floor.
      [Test]
      public void GIVEN_equal_standing_and_a_neutral_temper_WHEN_ordered_silent_THEN_they_hold_the_floor()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = 0,
            TargetTraits = new TraitFacts()
         }).Should().Be(SilenceResponse.Defy);
      }

      // Within the near-equal band, temper decides: a deferential, calculating soul yields to a peer's command.
      // This is the space Gabriel left to personality once standing is not decisive.
      [Test]
      public void GIVEN_equal_standing_and_a_deferential_temper_WHEN_ordered_silent_THEN_they_yield()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = 0,
            TargetTraits = Deferential()
         }).Should().Be(SilenceResponse.Comply);
      }

      // The other half of the band: a bold, proud equal refuses to be shushed. If this yielded, "be silent" would
      // cow even a defiant rival between equals, flattening the personalities the pillar exists to surface.
      [Test]
      public void GIVEN_equal_standing_and_a_proud_defiant_temper_WHEN_ordered_silent_THEN_they_refuse()
      {
         SilenceOrderPolicy.Resolve(new SilenceOrderContext {
            StandingGap = 0,
            TargetTraits = Defiant()
         }).Should().Be(SilenceResponse.Defy);
      }

      // A missing context must never silence anyone: the safe default is to keep the voice, so a bug upstream
      // cannot quietly drop a participant out of the conversation.
      [Test]
      public void GIVEN_no_context_WHEN_resolving_THEN_the_voice_is_kept()
      {
         SilenceOrderPolicy.Resolve(null).Should().Be(SilenceResponse.Defy);
      }

      #region private

      // Bold and proud, with no give: high Valor and Honor, low Mercy. Holds the floor against an equal.
      private static TraitFacts Defiant() => new TraitFacts {Valor = 2, Honor = 2, Mercy = -2};

      // Agreeable and calculating, unassertive: high Mercy and Calculating, low Valor. Yields to an equal.
      private static TraitFacts Deferential() => new TraitFacts {Mercy = 2, Calculating = 2, Valor = -2};

      #endregion
   }
}
