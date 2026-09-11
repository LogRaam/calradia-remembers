// Code written by Gabriel Mailhot, 06/09/2026.
// TournamentRivalryPolicy decides when repeated arena meetings become a standing rivalry and of which flavor. It is
// what makes Gabriel's "both, by traits" real at the rivalry level: the same tally of head-to-heads becomes a warm
// worthy-rivalry or a sour grudge purely from the lord's honour. Get the threshold wrong and rivalries form on a
// single meeting (every opponent a rival) or never (the feature is dead); get the flavor wrong and a gracious knight
// nurses a grudge, or a bitter cheat is recorded as a friend.

#region

using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Tournaments;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Tournaments
{
   [TestFixture]
   public sealed class TournamentRivalryPolicyTests
   {
      // Below the threshold there is no rivalry: a lord faced once or twice is not yet a rival, or the world would be
      // thick with "rivalries" from a single bout.
      [Test]
      public void GIVEN_too_few_meetings_WHEN_evaluated_THEN_there_is_no_rivalry()
         => TournamentRivalryPolicy.Evaluate(1, 1, new TournamentTraitFacts {Valor = 0, Honor = 0, Mercy = 0}).Should().Be(TournamentRivalryFlavor.None);

      // At the threshold, an honourable competitor becomes a WORTHY (friendly) rival, whatever the win/loss split.
      [Test]
      public void GIVEN_enough_meetings_and_an_honourable_lord_WHEN_evaluated_THEN_the_rivalry_is_friendly()
         => TournamentRivalryPolicy.Evaluate(2, 1, new TournamentTraitFacts {Valor = 1, Honor = 1, Mercy = 0})
            .Should().Be(TournamentRivalryFlavor.Friendly);

      // At the threshold, a low-honour competitor turns the rivalry BITTER (a light grudge). Character, not the
      // scoreboard, decides: a lord who has WON more can still be bitter if his honour is low.
      [Test]
      public void GIVEN_enough_meetings_and_a_low_honour_lord_WHEN_evaluated_THEN_the_rivalry_is_bitter()
         => TournamentRivalryPolicy.Evaluate(1, 2, new TournamentTraitFacts {Valor = 2, Honor = -1, Mercy = 0})
            .Should().Be(TournamentRivalryFlavor.Bitter);

      // The threshold boundary is inclusive: exactly RivalryThreshold total meetings forms a rivalry, so an
      // off-by-one never delays it by a whole extra tournament.
      [Test]
      public void GIVEN_exactly_the_threshold_WHEN_evaluated_THEN_a_rivalry_forms()
      {
         int total = TournamentRivalryPolicy.RivalryThreshold;

         TournamentRivalryPolicy.Evaluate(total, 0, new TournamentTraitFacts {Valor = 0, Honor = 0, Mercy = 0})
            .Should().NotBe(TournamentRivalryFlavor.None);
      }

      // Null traits must degrade to a friendly rivalry (honour defaults to 0, not bitter), never throw.
      [Test]
      public void GIVEN_null_traits_at_threshold_WHEN_evaluated_THEN_it_is_friendly_and_does_not_throw()
         => TournamentRivalryPolicy.Evaluate(2, 1, null).Should().Be(TournamentRivalryFlavor.Friendly);
      // The number 18 is not free: a bitter rivalry only leaves a mark if it clears the grudge system's OWN
      // formation floor, which is trait-modulated. cr.testall failed on 2026-09-06 because the campaign happened to
      // offer a merciful lord first, and the same silent refusal was swallowing the leaked-letter consequence too.
      // This pins the reading: an unforgiving rival carries the grudge.
      [Test]
      public void GIVEN_an_unforgiving_rival_WHEN_a_bitter_rivalry_plants_its_grudge_THEN_it_clears_the_formation_floor()
      {
         var facts = new GrudgeCandidateFacts {
            IsAttributable = true,
            Magnitude = TournamentRivalryPolicy.BitterMagnitude,
            Traits = new TraitFacts {Mercy = 0, Honor = -2}
         };

         GrudgePolicy.ShouldForm(facts).Should().BeTrue();
      }

      // The other half of the same reading, deliberately: mercy declines to nurse an arena sourness as a grievance.
      // The rivalry still forms and still colours what they say; only the grudge is forgiven. If this ever flips,
      // every merciful lord in Calradia starts carrying tournament grudges, which is not what was designed.
      [Test]
      public void GIVEN_a_merciful_rival_WHEN_a_bitter_rivalry_plants_its_grudge_THEN_mercy_forgives_it()
      {
         var facts = new GrudgeCandidateFacts {
            IsAttributable = true,
            Magnitude = TournamentRivalryPolicy.BitterMagnitude,
            Traits = new TraitFacts {Mercy = 1, Honor = -2}
         };

         GrudgePolicy.ShouldForm(facts).Should().BeFalse();
         TournamentRivalryPolicy.Evaluate(2, 1, new TournamentTraitFacts {Valor = 0, Honor = -2, Mercy = 1})
            .Should().Be(TournamentRivalryFlavor.Bitter); // the rivalry itself is unaffected
      }
   }
}
