// Code written by Gabriel Mailhot, 03/11/2026.
// AuthoredTastesPolicy decides what a character's tastes in bed become once an author locks them in
// character_overrides.json ("loves", "lessFond", "dislikes"), and whether turning the lock off should give them their
// roll back. Its answer is written onto the profile that cr.acts, the Heart line of the encyclopedia, the prompt and the
// gesture judge all read. Tene21 (Nexus, 03/11/2026) found the lock invisible to cr.acts after cr.reload_lore. If this
// rule is wrong, an author's lock is ignored, a deliberate "none" is refilled, a gesture sits in two lists and the model
// is told a character both loves and is less fond of it, or a character the player knows has their tastes re-rolled.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredTastesPolicyTests
   {
      private static IntimateTastes Rolled() => new() {
         Loved = new List<IntimateAct> {IntimateAct.Massage, IntimateAct.OnTop},
         LessFond = new List<IntimateAct> {IntimateAct.Kissing},
         Disliked = new List<IntimateAct> {IntimateAct.Spanking}
      };

      // STAKE: Tene21's case: the author writes all three lists and the character carries exactly those, not the roll.
      [Test]
      public void GIVEN_an_author_who_wrote_all_three_lists_WHEN_the_lock_lands_THEN_the_character_has_the_authored_tastes()
      {
         IntimateTastes t = AuthoredTastesPolicy.Apply(new AuthoredTastesFacts {
            Current = Rolled(), Loves = new[] {IntimateAct.Kissing, IntimateAct.Beneath}, LessFond = new[] {IntimateAct.DirtyTalk},
            Dislikes = new[] {IntimateAct.RoughHandling}
         });

         t.Loved.Should().Equal(IntimateAct.Kissing, IntimateAct.Beneath);
         t.LessFond.Should().Equal(IntimateAct.DirtyTalk);
         t.Disliked.Should().Equal(IntimateAct.RoughHandling);
      }

      // STAKE: an author who locks only "loves" with kissing, on a character who rolled kissing as less loved, must not
      // leave kissing in both lists: the model would be told she loves it and is less fond of it.
      [Test]
      public void GIVEN_a_lock_on_loves_alone_WHEN_the_roll_had_that_gesture_elsewhere_THEN_it_sits_in_one_list_only()
      {
         IntimateTastes t = AuthoredTastesPolicy.Apply(new AuthoredTastesFacts {Current = Rolled(), Loves = new[] {IntimateAct.Kissing, IntimateAct.Spanking}});

         t.Loved.Should().Equal(IntimateAct.Kissing, IntimateAct.Spanking);
         t.LessFond.Should().BeEmpty();
         t.Disliked.Should().BeEmpty();
      }

      // STAKE: the lists the author left out keep the character's own roll (the file's doc: "omit to keep the roll").
      [Test]
      public void GIVEN_a_lock_on_dislikes_alone_WHEN_the_lock_lands_THEN_the_rolled_loves_and_less_loved_stay()
      {
         IntimateTastes t = AuthoredTastesPolicy.Apply(new AuthoredTastesFacts {Current = Rolled(), Dislikes = new[] {IntimateAct.Urgency}});

         t.Loved.Should().Equal(IntimateAct.Massage, IntimateAct.OnTop);
         t.LessFond.Should().Equal(IntimateAct.Kissing);
         t.Disliked.Should().Equal(IntimateAct.Urgency);
      }

      // STAKE: "An empty list is a deliberate lock to none" (character_overrides.json): an author who wants a character
      // to dislike nothing gets exactly that, not the rolled dislike back.
      [Test]
      public void GIVEN_an_empty_authored_list_WHEN_the_lock_lands_THEN_that_list_is_empty()
      {
         IntimateTastes t = AuthoredTastesPolicy.Apply(new AuthoredTastesFacts {Current = Rolled(), Dislikes = new IntimateAct[0]});

         t.Disliked.Should().BeEmpty();
         t.Loved.Should().Equal(IntimateAct.Massage, IntimateAct.OnTop);
      }

      // STAKE: a character nobody wrote tastes for keeps their profile untouched, so a learned taste is never moved.
      [Test]
      public void GIVEN_no_authored_list_WHEN_asked_THEN_the_character_keeps_what_they_have()
      {
         var f = new AuthoredTastesFacts {Current = Rolled()};

         AuthoredTastesPolicy.Apply(f).Should().BeNull();
         AuthoredTastesPolicy.LockIsInPlace(f).Should().BeFalse();
      }

      // STAKE: turning "Use custom traits" off gives back the roll only where the lock had actually landed; a character
      // whose tastes are still their own roll is never re-rolled, so nothing the player learned is taken back.
      [Test]
      public void GIVEN_the_authored_lists_WHEN_the_profile_carries_them_or_not_THEN_only_a_landed_lock_is_seen_in_place()
      {
         var authored = new[] {IntimateAct.Kissing, IntimateAct.Beneath};
         var landed = new AuthoredTastesFacts {
            Current = new IntimateTastes {Loved = new List<IntimateAct> {IntimateAct.Beneath, IntimateAct.Kissing}, LessFond = Rolled().LessFond}, Loves = authored
         };

         AuthoredTastesPolicy.LockIsInPlace(landed).Should().BeTrue();
         AuthoredTastesPolicy.LockIsInPlace(new AuthoredTastesFacts {Current = Rolled(), Loves = authored}).Should().BeFalse();
      }
   }
}
