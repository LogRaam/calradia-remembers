// Code written by Gabriel Mailhot, 18/07/2026.
// Adult-prompt audit M3. This maps the player's Relationship Pacing dial onto the intimacy consent bar,
// which until now was the one gate in the mod that ignored the dial entirely. The answer feeds BOTH the
// verdict printed in the prompt and the mechanical gate in the bridge, so a wrong value here does not just
// reword a paragraph: it changes how hard intimacy is in a live campaign. The Grounded zero matters most
// of all, because it is what keeps the whole feature invisible for a player who never touches the setting.

#region

using CalradiaRemembers;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class IntimacyPacingRulesTests
   {
      // The load-bearing case: at the level the mod was balanced around, the bar must not move at all.
      // Any non-zero here would silently retune every existing campaign the moment this shipped.
      [Test]
      public void GIVEN_the_grounded_dial_WHEN_asking_the_relief_THEN_the_bar_is_left_exactly_where_it_shipped()
      {
         IntimacyPacingRules.Relief(RelationWarmth.Normal).Should().Be(0);
      }

      // Warm and Storyteller must actually differ, and in the right direction: a dial whose two upper
      // settings resolved to the same relief would read to the player as a setting that does nothing.
      [Test]
      public void GIVEN_the_warmer_dials_WHEN_asking_the_relief_THEN_each_step_lowers_the_bar_further()
      {
         int easier = IntimacyPacingRules.Relief(RelationWarmth.Easier);
         int veryEasy = IntimacyPacingRules.Relief(RelationWarmth.VeryEasy);

         easier.Should().BeGreaterThan(0);
         veryEasy.Should().BeGreaterThan(easier);
      }

      // The dial only ever LOOSENS, exactly as it does for positive relation gains: turning it up can never
      // produce a campaign harsher than the one the mod was balanced around.
      [Test]
      public void GIVEN_any_dial_setting_WHEN_asking_the_relief_THEN_it_never_raises_the_bar()
      {
         foreach (RelationWarmth warmth in new[] {RelationWarmth.Normal, RelationWarmth.Easier, RelationWarmth.VeryEasy})
            IntimacyPacingRules.Relief(warmth).Should().BeGreaterThanOrEqualTo(0);
      }

      // Fail safe against a value persisted by a future build (or a corrupted setting): an unknown dial
      // must fall back to the balanced level rather than to the most permissive one.
      [Test]
      public void GIVEN_an_unknown_dial_value_WHEN_asking_the_relief_THEN_it_falls_back_to_the_balanced_level()
      {
         IntimacyPacingRules.Relief((RelationWarmth) 99).Should().Be(IntimacyPacingRules.NormalRelief);
      }
   }
}
