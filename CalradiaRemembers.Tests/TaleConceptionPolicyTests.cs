// Code written by Gabriel Mailhot, 11/10/2026.
// A tale of intimacy may end on a chance of a child (Gabriel, 10/10/2026). If these rules are wrong, the player is made
// pregnant by his wife, two women "conceive", the chance a tale names is ignored, or a tale with no chance written gets
// none at all instead of the game's own.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleConceptionPolicyTests
   {
      // Gabriel's case: a man and his wife by the falls. She would carry the child.
      [Test]
      public void GIVEN_a_man_and_his_wife_WHEN_the_tale_ends_THEN_the_wife_would_carry_the_child()
      {
         TaleConceptionPolicy.Mother(new ConceptionFacts {PlayerIsFemale = false, PartnerIsFemale = true}).Should().Be(ConceptionMother.Partner);
      }

      // A woman player with her husband: she would carry it.
      [Test]
      public void GIVEN_a_woman_player_and_her_husband_WHEN_the_tale_ends_THEN_the_player_would_carry_the_child()
      {
         TaleConceptionPolicy.Mother(new ConceptionFacts {PlayerIsFemale = true, PartnerIsFemale = false}).Should().Be(ConceptionMother.Player);
      }

      // Gabriel: the tale is the same for a woman player married to a woman, without the chance of a child.
      [Test]
      public void GIVEN_two_women_or_two_men_WHEN_the_tale_ends_THEN_no_child_is_conceived()
      {
         TaleConceptionPolicy.Mother(new ConceptionFacts {PlayerIsFemale = true, PartnerIsFemale = true}).Should().Be(ConceptionMother.None);
         TaleConceptionPolicy.Mother(new ConceptionFacts {PlayerIsFemale = false, PartnerIsFemale = false}).Should().Be(ConceptionMother.None);
      }

      // A tale that names no chance takes the game's own 15%; one that names a chance gets that one.
      [Test]
      public void GIVEN_a_tale_that_names_a_chance_or_none_WHEN_read_THEN_its_own_or_the_games_applies()
      {
         new AuthoredConsequence {Type = "conception"}.Chance.Should().Be(0.15);
         TaleConceptionPolicy.Conceives(0.4, 0.39).Should().BeTrue();
         TaleConceptionPolicy.Conceives(0.4, 0.4).Should().BeFalse();
      }

      // A chance is between 0 and 1: anything else is refused at load.
      [Test]
      public void GIVEN_a_chance_outside_zero_to_one_WHEN_checked_THEN_it_is_no_chance()
      {
         TaleConceptionPolicy.IsAChance(1.5).Should().BeFalse();
         TaleConceptionPolicy.IsAChance(-0.1).Should().BeFalse();
         TaleConceptionPolicy.IsAChance(1).Should().BeTrue();
      }
   }
}
