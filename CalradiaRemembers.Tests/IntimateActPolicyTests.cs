// Code written by Gabriel Mailhot, 27/10/2026.
// IntimateActPolicy rolls, once per character, the gestures they love, the one they are less fond of and the one they
// sometimes dislike (Gabriel, 26/10/2026: "chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois
// des gestes qu'il n'aime pas"; 27/10/2026: "au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle
// aime moins"). If it is wrong, a character's tastes change from one game to the next, a gesture falls in two lists,
// someone has nothing to love or nothing less loved, or everyone dislikes something and every scene turns into a
// negotiation.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class IntimateActPolicyTests
   {
      private static IEnumerable<string> Heroes => Enumerable.Range(0, 2000).Select(i => "lord_" + i);

      // STAKE: Rhagaea loves tonight what she loved last time: the same id rolls the same tastes on every run.
      [Test]
      public void GIVEN_the_same_hero_WHEN_rolled_twice_THEN_the_tastes_are_the_same()
      {
         IntimateTastes first = IntimateActPolicy.Roll("lord_1_14", true);
         IntimateTastes second = IntimateActPolicy.Roll("lord_1_14", true);

         second.Loved.Should().Equal(first.Loved);
         second.LessFond.Should().Equal(first.LessFond);
         second.Disliked.Should().Equal(first.Disliked);
         IntimateActPolicy.StableSeed("lord_1_14").Should().Be(IntimateActPolicy.StableSeed("lord_1_14"));
      }

      // STAKE: "au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle aime moins": one or two loved,
      // exactly one less loved, at most one disliked, and no gesture in two lists.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_each_loves_one_or_two_and_is_less_fond_of_one()
      {
         foreach (string id in Heroes)
         {
            IntimateTastes t = IntimateActPolicy.Roll(id, false);
            t.Loved.Count.Should().BeInRange(1, 2);
            t.LessFond.Should().HaveCount(1);
            t.Disliked.Count.Should().BeInRange(0, 1);
            t.Loved.Concat(t.LessFond).Concat(t.Disliked).Should().OnlyHaveUniqueItems().And.NotContain(IntimateAct.None);
         }
      }

      // STAKE: a gesture disliked outright stays the rarer, sharper note: about two characters in five, so not every scene
      // becomes a negotiation.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_about_two_in_five_dislike_something()
      {
         List<int> counts = Heroes.Select(id => IntimateActPolicy.Roll(id, false).Disliked.Count).ToList();

         (counts.Count(c => c > 0) / (double) counts.Count).Should().BeInRange(0.32, 0.48);
      }

      // STAKE: a character met before 27/10/2026 is given the same less loved gesture a fresh roll gives, never one they
      // already love or dislike.
      [Test]
      public void GIVEN_a_character_rolled_before_WHEN_given_their_less_loved_gesture_THEN_it_matches_the_roll_and_avoids_their_tastes()
      {
         foreach (string id in Heroes.Take(300))
         {
            IntimateTastes t = IntimateActPolicy.Roll(id, true);
            IntimateActPolicy.LessFondFor(id, true, t.Loved.Concat(t.Disliked)).Should().Equal(t.LessFond);
         }

         List<IntimateAct> taken = IntimateActPolicy.All.Where(a => a != IntimateAct.Kissing).ToList();
         IntimateActPolicy.LessFondFor("lord_7", false, taken).Should().Equal(IntimateAct.Kissing);
      }

      // STAKE: a woman of Calradia is never given taking her partner anally among her tastes.
      [Test]
      public void GIVEN_a_woman_WHEN_rolled_THEN_taking_her_partner_anally_is_never_among_her_tastes()
      {
         foreach (string id in Heroes)
         {
            IntimateTastes t = IntimateActPolicy.Roll(id, true);
            t.Loved.Concat(t.LessFond).Concat(t.Disliked).Should().NotContain(IntimateAct.AnalGiving);
         }
      }
   }
}
