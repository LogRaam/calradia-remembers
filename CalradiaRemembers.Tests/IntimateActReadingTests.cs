// Code written by Gabriel Mailhot, 27/10/2026.
// IntimateActReading teaches whoever reads a turn's actions the gestures a character loves or dislikes, and judges a
// reported one (Gabriel, 26/10/2026: "un geste impose que le NPC n'aime pas devrait reduire le personal regard"; the
// rest is felt in the roleplay, "sentir la temperature de la relation"). If it is wrong, imposing what they hate costs
// nothing, or merely asking for it is punished, a single night drains their regard reply after reply, a forced scene is
// read as a taste, or the reader is taught gestures at an adult level that never asked for them.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class IntimateActReadingTests
   {
      private static readonly List<IntimateAct> Loved = new() {IntimateAct.OralGiving};
      private static readonly List<IntimateAct> Disliked = new() {IntimateAct.AnalReceiving};

      private static IntimateActJudgeFacts Facts(IntimateAct act, bool performed, bool known = false, bool charged = false)
         => new() {
            Act = act, Performed = performed, Hardcore = true, IntimacyPermitted = true, Loved = Loved, Disliked = Disliked,
            AlreadyKnown = known, AlreadyChargedToday = charged
         };

      // STAKE: Gabriel's ruling: a gesture the character dislikes, imposed anyway, lowers their personal regard and they
      // remember it.
      [Test]
      public void GIVEN_a_disliked_gesture_the_player_did_WHEN_judged_THEN_regard_falls_and_it_is_remembered()
      {
         IntimateActVerdict v = IntimateActReading.Judge(Facts(IntimateAct.AnalReceiving, true));

         v.RegardDelta.Should().Be(DeedRegardPolicy.ImposedDislikedGestureVictim).And.BeNegative();
         v.Imposed.Should().BeTrue();
         v.Discovered.Should().BeTrue();
      }

      // STAKE: "un geste impose": only asking is not imposing; the character refused or showed unease, and the player
      // learned something, at no cost.
      [Test]
      public void GIVEN_a_disliked_gesture_only_asked_for_WHEN_judged_THEN_nothing_is_charged_but_the_taste_is_learned()
      {
         IntimateActVerdict v = IntimateActReading.Judge(Facts(IntimateAct.AnalReceiving, false));

         v.RegardDelta.Should().Be(0);
         v.Imposed.Should().BeFalse();
         v.Discovered.Should().BeTrue();
      }

      // STAKE: "une chose qu'elle aime moins": a gesture they are less fond of is met without eagerness, never an offence;
      // the player learns it all the same.
      [Test]
      public void GIVEN_a_gesture_they_are_less_fond_of_WHEN_the_player_does_it_THEN_it_costs_nothing_and_is_learned()
      {
         IntimateActVerdict v = IntimateActReading.Judge(new IntimateActJudgeFacts {
            Act = IntimateAct.DirtyTalk, Performed = true, Hardcore = true, IntimacyPermitted = true, Loved = Loved, Disliked = Disliked,
            LessFond = new List<IntimateAct> {IntimateAct.DirtyTalk}
         });

         v.RegardDelta.Should().Be(0);
         v.Imposed.Should().BeFalse();
         v.Discovered.Should().BeTrue();
      }

      // STAKE: one night is one offence: the same gesture reported reply after reply is charged once a day.
      [Test]
      public void GIVEN_a_disliked_gesture_already_charged_today_WHEN_reported_again_THEN_it_costs_nothing_more()
      {
         IntimateActVerdict v = IntimateActReading.Judge(Facts(IntimateAct.AnalReceiving, true, known: true, charged: true));

         v.RegardDelta.Should().Be(0);
         v.Imposed.Should().BeFalse();
      }

      // STAKE: finding what they love warms them a little, once; it cannot be farmed by repeating it.
      [Test]
      public void GIVEN_a_loved_gesture_WHEN_found_the_first_time_and_again_THEN_only_the_first_warms_them()
      {
         IntimateActReading.Judge(Facts(IntimateAct.OralGiving, true)).RegardDelta.Should().Be(DeedRegardPolicy.FoundLovedGestureVictim);
         IntimateActReading.Judge(Facts(IntimateAct.OralGiving, true, known: true)).RegardDelta.Should().Be(0);
      }

      // STAKE: a gesture they neither love nor dislike changes nothing: not every gesture is a test.
      [Test]
      public void GIVEN_an_ordinary_gesture_WHEN_judged_THEN_nothing_happens()
      {
         IntimateActReading.Judge(Facts(IntimateAct.Kissing, true)).Should().BeSameAs(IntimateActVerdict.Nothing);
      }

      // STAKE: below Hardcore, or where the game does not permit intimacy, a reported gesture moves nothing.
      [Test]
      public void GIVEN_a_game_below_hardcore_or_intimacy_not_permitted_WHEN_judged_THEN_nothing_happens()
      {
         IntimateActJudgeFacts f = Facts(IntimateAct.AnalReceiving, true);
         IntimateActReading.Judge(new IntimateActJudgeFacts {Act = f.Act, Performed = true, Hardcore = false, IntimacyPermitted = true, Loved = Loved, Disliked = Disliked})
                           .Should().BeSameAs(IntimateActVerdict.Nothing);
         IntimateActReading.Judge(new IntimateActJudgeFacts {Act = f.Act, Performed = true, Hardcore = true, IntimacyPermitted = false, Loved = Loved, Disliked = Disliked})
                           .Should().BeSameAs(IntimateActVerdict.Nothing);
      }

      // STAKE: the reader is told only this character's loved and disliked gestures, named from their side, and how to
      // report one; a forced or captive scene is never read for tastes.
      [Test]
      public void GIVEN_a_permitted_hardcore_lovemaking_WHEN_the_reader_is_taught_THEN_only_their_tastes_are_watched()
      {
         string block = IntimateActReading.ForReader(new IntimateActReaderFacts {
            Hardcore = true, IntimacyPermitted = true, NpcName = "Rhagaea", Loved = Loved, Disliked = Disliked
         });

         block.Should().Contain("- oral_giving: Rhagaea's mouth on the player");
         block.Should().Contain("- anal_receiving: the player takes Rhagaea anally");
         block.Should().NotContain("kissing");
         IntimateActReading.ForReader(new IntimateActReaderFacts {
            Hardcore = true, IntimacyPermitted = true, NpcName = "Rhagaea", Loved = Loved, Disliked = Disliked, LessFond = new List<IntimateAct> {IntimateAct.DirtyTalk}
         }).Should().Contain("- dirty_talk:");
         block.Should().Contain("type: " + IntimateActReading.Verb);

         IntimateActReading.ForReader(new IntimateActReaderFacts {Hardcore = true, IntimacyPermitted = true, ForcedScene = true, Loved = Loved, Disliked = Disliked})
                           .Should().BeNull();
         IntimateActReading.ForReader(new IntimateActReaderFacts {Hardcore = false, IntimacyPermitted = true, Loved = Loved, Disliked = Disliked})
                           .Should().BeNull();
      }

      // STAKE: an author who writes a character's tastes in character_overrides.json gets exactly them; a typo is named,
      // not swallowed, and an entry holding only tastes is still authored content (the 2.5.0 "no kinks entry" bug).
      [Test]
      public void GIVEN_authored_loves_and_dislikes_WHEN_read_THEN_they_replace_the_roll_and_a_typo_is_named()
      {
         var o = new CharacterOverride {
            Hero = "Rhagaea", Loves = new List<string> {"oral_giving", "kising"}, Dislikes = new List<string> {"anal_receiving", "oral_giving"},
            LessFond = new List<string> {"dirty_talk", "anal_receiving"}
         };

         o.ParsedLoves.Should().Equal(IntimateAct.OralGiving);
         o.ParsedDislikes.Should().Equal(IntimateAct.AnalReceiving);
         o.ParsedLessFond.Should().Equal(IntimateAct.DirtyTalk);
         o.HasAnyAuthoredContent.Should().BeTrue();
         o.AuthoringProblems().Should().Contain(p => p.Contains("kising"));
         new CharacterOverride {Hero = "Rhagaea"}.ParsedLoves.Should().BeNull();
      }

      // STAKE: a doubt never costs the character's regard: only a plain "performed" is an act done; a missing value or the
      // template copied as written ("performed | proposed") reads as only asked for.
      [Test]
      public void GIVEN_an_unclear_report_WHEN_read_THEN_it_counts_as_only_asked_for()
      {
         IntimateActReading.IsPerformed("performed").Should().BeTrue();
         IntimateActReading.IsPerformed(" Performed ").Should().BeTrue();
         IntimateActReading.IsPerformed("performed | proposed").Should().BeFalse();
         IntimateActReading.IsPerformed(null).Should().BeFalse();
         IntimateActReading.IsPerformed("proposed").Should().BeFalse();
      }

      // STAKE: the id the reader writes is the one the game reads back, whatever its spelling.
      [Test]
      public void GIVEN_a_gesture_id_WHEN_written_and_read_back_THEN_it_is_the_same_gesture()
      {
         foreach (IntimateAct a in IntimateActPolicy.All) IntimateActReading.Parse(IntimateActReading.IdOf(a)).Should().Be(a);
         IntimateActReading.Parse("OralGiving").Should().Be(IntimateAct.OralGiving);
         IntimateActReading.Parse("nonsense").Should().Be(IntimateAct.None);
      }

      // STAKE: a gesture appended without its words would reach the reader as a bare id it cannot match, and the memory
      // of an imposed one would read "pressed something on me".
      [Test]
      public void GIVEN_every_gesture_WHEN_watched_or_remembered_THEN_it_is_told_in_words()
      {
         foreach (IntimateAct act in IntimateActPolicy.All)
         {
            string block = IntimateActReading.ForReader(new IntimateActReaderFacts {
               Hardcore = true, IntimacyPermitted = true, NpcName = "Rhagaea", Loved = new[] {act}
            });
            string prefix = "- " + IntimateActReading.IdOf(act) + ": ";
            string line = block.Split('\n').Single(l => l.StartsWith(prefix)).Trim();
            line.Length.Should().BeGreaterThan(prefix.Length + 3, act.ToString());
            IntimateActReading.ImposedMemory(act, "Aldric").Should().NotContain("something", act.ToString());
            IntimateActReading.Parse(IntimateActReading.IdOf(act)).Should().Be(act);
         }
      }
   }
}
