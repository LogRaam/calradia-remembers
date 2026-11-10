// Code written by Gabriel Mailhot, 10/11/2026.
// CredulityPolicy judges how readily a character believes the player: shrewd, ordinary or trusting, from their traits,
// station and age, and shrewd toward a player who has broken faith with them before. Gabriel (10/11/2026) asked for it
// after reading AI Influence 7.0.0's naivety; until now one rule ("to be guarded is to not be naive") served everyone.
// If this is wrong, a scheming merchant swallows every tale, a trusting girl of sixteen cross-examines the player like
// a magistrate, or a lord the player swore false to believes him again the next day.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CredulityPolicyTests
   {
      // STAKE: a calculating, dishonest merchant must be hard to fool; that is who he is.
      [Test]
      public void GIVEN_a_calculating_dishonest_merchant_WHEN_judged_THEN_the_leaning_is_shrewd()
      {
         CredulityPolicy.Score(new RomanticTraits {Calculating = 1, Honor = -1}, "merchant", 40).Should().BeLessThanOrEqualTo(-3);
      }

      // STAKE: an honourable, generous, impulsive young noble judges others by herself and believes them.
      [Test]
      public void GIVEN_an_honourable_generous_impulsive_youth_WHEN_judged_THEN_the_leaning_is_trusting()
      {
         CredulityPolicy.Score(new RomanticTraits {Honor = 1, Generosity = 1, Calculating = -1}, "noble", 17).Should().BeGreaterThanOrEqualTo(3);
      }

      // STAKE: a lean of one step either way is only a leaning; only two steps make a nature worth telling the model.
      [Test]
      public void GIVEN_scores_WHEN_turned_into_a_nature_THEN_two_steps_make_shrewd_or_trusting_and_less_is_ordinary()
      {
         CredulityPolicy.FromScore(-2).Should().Be(Credulity.Shrewd);
         CredulityPolicy.FromScore(-1).Should().Be(Credulity.Ordinary);
         CredulityPolicy.FromScore(1).Should().Be(Credulity.Ordinary);
         CredulityPolicy.FromScore(2).Should().Be(Credulity.Trusting);
      }

      // STAKE: once burned: a lord the player swore false to must not take his word again, however trusting by nature.
      [Test]
      public void GIVEN_a_trusting_character_the_player_broke_an_oath_to_WHEN_judged_THEN_they_are_shrewd_and_say_why()
      {
         var f = new CredulityFacts {
            HeroId = "lord_1_1", Station = "noble", Age = 17, Traits = new RomanticTraits {Honor = 2, Generosity = 2, Calculating = -2},
            GrudgesAgainstPlayer = new[] {GrudgeSource.BrokenOath}
         };

         CredulityPolicy.Judge(f).Should().Be(Credulity.Shrewd);
         CredulityPolicy.Render(Credulity.Shrewd, CredulityPolicy.Burned(f)).Should().Contain("broken faith with you before");
      }

      // STAKE: a grudge over jealousy or a divorce is a wound, not a lie; it must not make a character cross-examine every word.
      [Test]
      public void GIVEN_a_grudge_that_is_no_broken_faith_WHEN_judged_THEN_it_does_not_count_as_burned()
      {
         CredulityPolicy.Burned(new CredulityFacts {GrudgesAgainstPlayer = new[] {GrudgeSource.Jealousy, GrudgeSource.Divorce}}).Should().BeFalse();
      }

      // STAKE: nothing is saved, so the same character must believe the same way after a reload.
      [Test]
      public void GIVEN_the_same_character_WHEN_judged_twice_THEN_the_nature_is_the_same()
      {
         var f = new CredulityFacts {HeroId = "lord_7_3", Station = "lord", Age = 40, Traits = new RomanticTraits {Calculating = 1}};

         CredulityPolicy.Judge(f).Should().Be(CredulityPolicy.Judge(f));
      }

      // STAKE: the ordinary need no word: telling every character how credulous they are would only crowd the prompt.
      [Test]
      public void GIVEN_an_ordinary_character_WHEN_rendered_THEN_nothing_is_said()
      {
         CredulityPolicy.Render(Credulity.Ordinary, false).Should().BeNull();
         CredulityPolicy.Render(Credulity.Trusting, false).Should().Contain("trusting");
         CredulityPolicy.Render(Credulity.Shrewd, false).Should().Contain("hard to fool");
      }
   }
}
