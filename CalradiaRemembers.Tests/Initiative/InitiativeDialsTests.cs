// Code written by Gabriel Mailhot, 29/10/2026.
// The older pace dials under the world's initiative. Gabriel (28/10/2026) wanted one conductor, the old dials read into
// its weights. Before this, their own rates applied first and the director paced the rest, so the two multiplied and the
// world came far less often than the player set. If this is wrong, the old and new settings fight, a player's "Never"
// stops meaning never, or switching the director Off changes how the old dials behave.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Initiative;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Initiative
{
   [TestFixture]
   public sealed class InitiativeDialsTests
   {
      // A player who chose Never for lords riding out keeps never seeing one, whatever the director's own sliders say.
      [Test]
      public void GIVEN_lords_approaching_never_WHEN_read_as_a_weight_THEN_the_family_is_stopped()
      {
         InitiativeDials.Combined(100, InitiativeDials.WeightPercent(InterceptionPacing.Never)).Should().Be(0);
         InitiativeDials.Combined(200, InitiativeDials.WeightPercent(CompanionAudiencePacing.Never)).Should().Be(0);
      }

      // Rarely and Often keep their meaning as room in the director's draw; the shipped Occasionally changes nothing.
      [Test]
      public void GIVEN_the_old_dials_WHEN_read_as_weights_THEN_rarely_halves_often_adds_half_and_the_default_keeps()
      {
         InitiativeDials.Combined(100, InitiativeDials.WeightPercent(InterceptionPacing.Rarely)).Should().Be(50);
         InitiativeDials.Combined(100, InitiativeDials.WeightPercent(InterceptionPacing.Often)).Should().Be(150);
         InitiativeDials.Combined(120, InitiativeDials.WeightPercent(InterceptionPacing.Occasionally)).Should().Be(120);
      }

      // The director paces now: the old daily roll must not thin the world a second time, but an off dial stays off.
      [Test]
      public void GIVEN_the_director_pacing_WHEN_a_daily_chance_is_read_THEN_it_is_only_on_or_off()
      {
         InitiativeDials.ChancePercent(25, true).Should().Be(100);
         InitiativeDials.ChancePercent(0, true).Should().Be(0);
         InitiativeDials.KindCooldownDays(14, true).Should().Be(0);
      }

      // Off means every system paces itself exactly as before.
      [Test]
      public void GIVEN_the_director_off_WHEN_the_old_rates_are_read_THEN_they_are_untouched()
      {
         InitiativeDials.ChancePercent(25, false).Should().Be(25);
         InitiativeDials.KindCooldownDays(14, false).Should().Be(14);
      }
   }
}
