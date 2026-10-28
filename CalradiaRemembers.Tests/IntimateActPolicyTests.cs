// Code written by Gabriel Mailhot, 27/10/2026.
// IntimateActPolicy rolls, once per character, the gestures they love, the ones they are less fond of and the ones they
// sometimes dislike (Gabriel, 26/10/2026: "chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois
// des gestes qu'il n'aime pas"; 27/10/2026: "au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle
// aime moins"; then "Je trouve interessant d'en mettre beaucoup" and "les traits influencent les kinks qui eux
// influencent les gestes"). If it is wrong, a character's tastes change from one game to the next, a gesture falls in two
// lists, someone has nothing to love or nothing less loved, everyone dislikes something and every scene turns into a
// negotiation, a masochist dislikes rough handling and the model is told two opposite things, or a character the player
// already knows has a learned taste taken back.

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

      private static IntimateTasteFacts With(string id, Kink[] kinks = null, RomanticPreference[] preferences = null)
         => new() {HeroId = id, IsFemale = true, Kinks = kinks ?? new Kink[0], Preferences = preferences ?? new RomanticPreference[0]};

      private static double Share(System.Func<IntimateTastes, bool> holds, Kink[] kinks = null, RomanticPreference[] preferences = null)
         => Heroes.Count(id => holds(IntimateActPolicy.Roll(With(id, kinks, preferences)))) / (double) Heroes.Count();

      // STAKE: Rhagaea loves tonight what she loved last time: the same id rolls the same tastes on every run.
      [Test]
      public void GIVEN_the_same_hero_WHEN_rolled_twice_THEN_the_tastes_are_the_same()
      {
         IntimateTastes first = IntimateActPolicy.Roll(With("lord_1_14", new[] {Kink.Masochism}));
         IntimateTastes second = IntimateActPolicy.Roll(With("lord_1_14", new[] {Kink.Masochism}));

         second.Loved.Should().Equal(first.Loved);
         second.LessFond.Should().Equal(first.LessFond);
         second.Disliked.Should().Equal(first.Disliked);
         IntimateActPolicy.StableSeed("lord_1_14").Should().Be(IntimateActPolicy.StableSeed("lord_1_14"));
      }

      // STAKE: "au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle aime moins", over a wider list:
      // two or three loved, one or two less loved, at most two disliked, and no gesture in two lists.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_each_loves_two_or_three_and_is_less_fond_of_one_or_two()
      {
         foreach (string id in Heroes)
         {
            IntimateTastes t = IntimateActPolicy.Roll(id, false);
            t.Loved.Count.Should().BeInRange(2, IntimateActPolicy.MaxLoved);
            t.LessFond.Count.Should().BeInRange(1, IntimateActPolicy.MaxLessFond);
            t.Disliked.Count.Should().BeInRange(0, IntimateActPolicy.MaxDisliked);
            t.Loved.Concat(t.LessFond).Concat(t.Disliked).Should().OnlyHaveUniqueItems().And.NotContain(IntimateAct.None);
         }
      }

      // STAKE: a gesture disliked outright stays a sharper note than a less loved one: about one character in two, so not
      // every scene becomes a negotiation.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_about_one_in_two_dislike_something()
      {
         Share(t => t.Disliked.Count > 0).Should().BeInRange(0.42, 0.58);
      }

      // STAKE: "Ca peut amener des scenes plus subtiles et complexes": every gesture of the widened list can be someone's
      // love, so the new ones are not dead names.
      [Test]
      public void GIVEN_many_heroes_WHEN_rolled_THEN_every_gesture_is_loved_by_someone()
      {
         HashSet<IntimateAct> loved = Heroes.SelectMany(id => IntimateActPolicy.Roll(id, false).Loved).ToHashSet();

         loved.Should().BeEquivalentTo(IntimateActPolicy.All);
      }

      // STAKE: "les kinks pourraient fortement qualifier ou disqualifier certains gestes": a masochist never dislikes nor
      // merely tolerates rough handling or being spanked, which would tell the model two opposite things.
      [Test]
      public void GIVEN_a_masochist_WHEN_rolled_THEN_rough_handling_and_spanking_are_never_disliked_nor_less_loved()
      {
         foreach (string id in Heroes)
         {
            IntimateTastes t = IntimateActPolicy.Roll(With(id, new[] {Kink.Masochism}));
            t.Disliked.Concat(t.LessFond).Should().NotContain(new[] {IntimateAct.RoughHandling, IntimateAct.Spanking});
         }
      }

      // STAKE: a kink weighs, it does not dictate: one who longs to be bound loves being held down far more often than
      // others, and still not always, so the Heart line does not merely repeat the kink.
      [Test]
      public void GIVEN_a_love_of_being_bound_WHEN_rolled_THEN_being_restrained_is_far_likelier_loved_but_not_always()
      {
         double plain = Share(t => t.Loved.Contains(IntimateAct.Restrained));
         double bound = Share(t => t.Loved.Contains(IntimateAct.Restrained), new[] {Kink.BondageReceiving});

         bound.Should().BeGreaterThan(plain * 2.5).And.BeLessThan(0.9);
      }

      // STAKE: a preference weighs too, for the many characters who have no kink: the slow-burning love being teased more
      // often than others.
      [Test]
      public void GIVEN_a_slow_burner_WHEN_rolled_THEN_teasing_is_likelier_loved()
      {
         double plain = Share(t => t.Loved.Contains(IntimateAct.Teasing));
         double slow = Share(t => t.Loved.Contains(IntimateAct.Teasing), preferences: new[] {RomanticPreference.SlowBurn});

         slow.Should().BeGreaterThan(plain * 1.5);
      }

      // STAKE: what runs against a nature is rarer to love and likelier to dislike, never impossible: a dominant may have
      // their flaw, and that is the subtlety Gabriel asked for.
      [Test]
      public void GIVEN_a_dominant_WHEN_rolled_THEN_being_led_is_rarer_loved_likelier_disliked_and_still_possible()
      {
         double lovedPlain = Share(t => t.Loved.Contains(IntimateAct.BeingLed));
         double lovedDominant = Share(t => t.Loved.Contains(IntimateAct.BeingLed), new[] {Kink.Dominance});
         double dislikedPlain = Share(t => t.Disliked.Contains(IntimateAct.BeingLed));
         double dislikedDominant = Share(t => t.Disliked.Contains(IntimateAct.BeingLed), new[] {Kink.Dominance});

         lovedDominant.Should().BeLessThan(lovedPlain).And.BeGreaterThan(0);
         dislikedDominant.Should().BeGreaterThan(dislikedPlain * 1.5);
      }

      // STAKE: a character met before the list widened keeps what they loved, were less fond of and disliked, gains only
      // new gestures, and loses a dislike their kink contradicts, unless the Heart already named it to the player.
      [Test]
      public void GIVEN_a_character_rolled_before_the_list_widened_WHEN_completed_THEN_old_tastes_stay_and_only_new_gestures_are_added()
      {
         var old = new IntimateTastes {
            Loved = new List<IntimateAct> {IntimateAct.Kissing}, LessFond = new List<IntimateAct> {IntimateAct.DirtyTalk},
            Disliked = new List<IntimateAct> {IntimateAct.RoughHandling}
         };

         foreach (string id in Heroes.Take(300))
         {
            IntimateTastes unknown = IntimateActPolicy.Complete(old, new IntimateAct[0], With(id, new[] {Kink.Masochism}));
            unknown.Disliked.Should().NotContain(IntimateAct.RoughHandling);
            unknown.Loved.Should().StartWith(IntimateAct.Kissing);
            unknown.LessFond.Should().StartWith(IntimateAct.DirtyTalk);
            unknown.Loved.Concat(unknown.LessFond).Concat(unknown.Disliked).Except(new[] {IntimateAct.Kissing, IntimateAct.DirtyTalk})
                   .Should().NotIntersectWith(IntimateActPolicy.FirstEleven);
            unknown.Loved.Count.Should().BeLessThanOrEqualTo(IntimateActPolicy.MaxLoved);
            unknown.Loved.Concat(unknown.LessFond).Concat(unknown.Disliked).Should().OnlyHaveUniqueItems();

            IntimateTastes learned = IntimateActPolicy.Complete(old, new[] {IntimateAct.RoughHandling}, With(id, new[] {Kink.Masochism}));
            learned.Disliked.Should().StartWith(IntimateAct.RoughHandling);
         }
      }

      // STAKE: a character met before 27/10/2026 is given the less loved gesture the first roll would have given, among the
      // first eleven, never one they already love or dislike.
      [Test]
      public void GIVEN_a_character_rolled_before_the_less_loved_gesture_WHEN_given_it_THEN_it_is_among_the_first_eleven_and_avoids_their_tastes()
      {
         foreach (string id in Heroes.Take(300))
         {
            var taken = new[] {IntimateAct.Kissing, IntimateAct.OnTop};
            List<IntimateAct> lessFond = IntimateActPolicy.LessFondFor(id, true, taken);
            lessFond.Should().ContainSingle().Which.Should().BeOneOf(IntimateActPolicy.FirstEleven);
            lessFond.Should().NotIntersectWith(taken).And.NotContain(IntimateAct.AnalGiving);
            IntimateActPolicy.LessFondFor(id, true, taken).Should().Equal(lessFond);
         }

         List<IntimateAct> allButKissing = IntimateActPolicy.FirstEleven.Where(a => a != IntimateAct.Kissing).ToList();
         IntimateActPolicy.LessFondFor("lord_7", false, allButKissing).Should().Equal(IntimateAct.Kissing);
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
