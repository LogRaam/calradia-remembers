// Code written by Gabriel Mailhot, 27/10/2026.
// IntimateActPolicy rolls, once per character, the gestures they particularly love and the ones they dislike (Gabriel,
// 26/10/2026: "chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois des gestes qu'il n'aime
// pas"). If it is wrong, a character's tastes change from one game to the next, a gesture is both loved and disliked,
// everyone dislikes something and every scene turns into a negotiation, or no one ever does.

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
         var first = IntimateActPolicy.Roll("lord_1_14", true);
         var second = IntimateActPolicy.Roll("lord_1_14", true);

         second.Loved.Should().Equal(first.Loved);
         second.Disliked.Should().Equal(first.Disliked);
         IntimateActPolicy.StableSeed("lord_1_14").Should().Be(IntimateActPolicy.StableSeed("lord_1_14"));
      }

      // STAKE: "particulierement aimer": everyone has one to three favourites, and no gesture is both loved and disliked.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_each_loves_one_to_three_and_never_dislikes_what_they_love()
      {
         foreach (string id in Heroes)
         {
            var (loved, disliked) = IntimateActPolicy.Roll(id, false);
            loved.Count.Should().BeInRange(1, 3);
            disliked.Count.Should().BeInRange(0, 2);
            loved.Intersect(disliked).Should().BeEmpty();
            loved.Concat(disliked).Should().NotContain(IntimateAct.None);
         }
      }

      // STAKE: "meme parfois": about one character in two dislikes something, rarely two things; otherwise every scene
      // becomes a negotiation, or none ever does.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_about_half_dislike_something_and_few_dislike_two()
      {
         List<int> counts = Heroes.Select(id => IntimateActPolicy.Roll(id, false).Disliked.Count).ToList();

         (counts.Count(c => c > 0) / (double) counts.Count).Should().BeInRange(0.4, 0.6);
         (counts.Count(c => c == 2) / (double) counts.Count).Should().BeInRange(0.03, 0.15);
      }

      // STAKE: a woman of Calradia is never given taking her partner anally to love or to dislike.
      [Test]
      public void GIVEN_a_woman_WHEN_rolled_THEN_taking_her_partner_anally_is_never_among_her_tastes()
      {
         foreach (string id in Heroes)
         {
            var (loved, disliked) = IntimateActPolicy.Roll(id, true);
            loved.Concat(disliked).Should().NotContain(IntimateAct.AnalGiving);
         }
      }
   }
}
