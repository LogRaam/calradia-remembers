// Code written by Gabriel Mailhot, 10/11/2026.
// CharacterVoicePolicy draws, from a hero's id, station, age and traits, how that character speaks: one habit of speech,
// how freely they curse, how their piety shows. Gabriel (10/11/2026) asked for a voice per character after reading AI
// Influence 7.0.0; kurushimi00 (Nexus, 01/11/2026) heard characters recite alike. If this is wrong, a character's habit
// changes between two conversations (nothing is saved, so the draw IS the memory), a preacher swears like a mercenary,
// or every character of a station sounds the same again.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CharacterVoicePolicyTests
   {
      private static CharacterVoiceFacts Facts(string id, string station = "lord", int age = 35, RomanticTraits traits = null)
         => new() {HeroId = id, Station = station, Age = age, Traits = traits ?? new RomanticTraits()};

      // STAKE: nothing is saved, so a lord who answered with questions yesterday must still do so after a reload.
      [Test]
      public void GIVEN_the_same_hero_WHEN_the_voice_is_drawn_twice_THEN_it_is_the_same_voice()
      {
         CharacterVoice first = CharacterVoicePolicy.Draw(Facts("lord_1_1"));
         CharacterVoice second = CharacterVoicePolicy.Draw(Facts("lord_1_1"));

         second.Habit.Should().Be(first.Habit);
         second.Coarseness.Should().Be(first.Coarseness);
         second.Piety.Should().Be(first.Piety);
      }

      // STAKE: a lord who grows more honourable keeps the way he talks; only how he curses may soften.
      [Test]
      public void GIVEN_a_hero_whose_traits_move_WHEN_the_voice_is_drawn_THEN_the_habit_stays()
      {
         CharacterVoice before = CharacterVoicePolicy.Draw(Facts("lord_4_2", traits: new RomanticTraits {Honor = -1}));
         CharacterVoice after = CharacterVoicePolicy.Draw(Facts("lord_4_2", traits: new RomanticTraits {Honor = 2, Mercy = 1}));

         after.Habit.Should().Be(before.Habit);
      }

      // STAKE: the whole point: a hall of lords must not all speak with one habit.
      [Test]
      public void GIVEN_many_lords_WHEN_their_voices_are_drawn_THEN_they_do_not_share_one_habit()
      {
         int distinct = Enumerable.Range(0, 40).Select(i => CharacterVoicePolicy.Draw(Facts($"lord_{i}_1")).Habit).Distinct().Count();

         distinct.Should().BeGreaterThan(15);
      }

      // STAKE: a merchant talking of his house and forebears, or a gang leader haggling like a guildsman, breaks the station.
      [Test]
      public void GIVEN_a_station_WHEN_its_habits_are_listed_THEN_habits_of_other_stations_are_not_among_them()
      {
         CharacterVoicePolicy.HabitsFor("merchant", 35).Should().NotContain(h => h.Contains("forebears"));
         CharacterVoicePolicy.HabitsFor("lord", 35).Should().Contain(h => h.Contains("forebears"));
         CharacterVoicePolicy.HabitsFor("lord", 35).Should().NotContain(h => h.Contains("haggle"));
      }

      // STAKE: a girl of twenty calling a grown lord "child" is absurd; an old man doing it is a character.
      [Test]
      public void GIVEN_a_young_or_an_old_character_WHEN_habits_are_listed_THEN_the_elder_habits_belong_to_the_old()
      {
         CharacterVoicePolicy.HabitsFor("lord", 20).Should().NotContain(h => h.Contains("\"child\""));
         CharacterVoicePolicy.HabitsFor("lord", 60).Should().Contain(h => h.Contains("\"child\""));
         CharacterVoicePolicy.HabitsFor("lord", 60).Should().NotContain(h => h.Contains("older and harder than you are"));
      }

      // STAKE: the station pack makes a preacher deferential and holy; a cursing, faithless preacher contradicts it.
      [Test]
      public void GIVEN_a_preacher_WHEN_the_voice_is_drawn_THEN_they_never_curse_and_are_devout()
      {
         var sinner = new RomanticTraits {Honor = -2, Mercy = -2, Calculating = -2};

         CharacterVoicePolicy.CoarsenessOf("preacher", sinner, 40, 1).Should().Be(Coarseness.Never);
         CharacterVoicePolicy.PietyOf("preacher", sinner, 9).Should().Be(Piety.Devout);
      }

      // STAKE: a hot-headed, merciless gang leader who never swears reads false; an honourable lord who swears like one too.
      [Test]
      public void GIVEN_a_merciless_impulsive_gang_leader_and_an_honourable_lord_WHEN_cursing_is_judged_THEN_one_curses_often_and_the_other_never()
      {
         CharacterVoicePolicy.CoarsenessOf("gangleader", new RomanticTraits {Mercy = -1, Calculating = -1}, 35, 0).Should().Be(Coarseness.Often);
         CharacterVoicePolicy.CoarsenessOf("lord", new RomanticTraits {Honor = 1}, 35, 1).Should().Be(Coarseness.Never);
      }

      // STAKE: a child hero who curses like a soldier.
      [Test]
      public void GIVEN_a_child_WHEN_cursing_is_judged_THEN_they_never_curse()
      {
         CharacterVoicePolicy.CoarsenessOf("gangleader", new RomanticTraits {Mercy = -2, Calculating = -2}, 12, 1).Should().Be(Coarseness.Never);
      }

      // STAKE: honour and mercy make believers, dishonour and scheming make hypocrites; the same roll reads differently.
      [Test]
      public void GIVEN_one_roll_WHEN_an_honourable_or_a_scheming_character_is_judged_THEN_the_first_is_devout_and_the_second_believes_for_show()
      {
         CharacterVoicePolicy.PietyOf("lord", new RomanticTraits {Honor = 1, Mercy = 1}, 4).Should().Be(Piety.Devout);
         CharacterVoicePolicy.PietyOf("lord", new RomanticTraits(), 4).Should().Be(Piety.Ordinary);
         CharacterVoicePolicy.PietyOf("lord", new RomanticTraits {Honor = -1, Calculating = 1}, 6).Should().Be(Piety.ForShow);
      }

      // STAKE: Calradia has no named faith of its own; a voice that names a god invents one (STAY WITHIN WHAT YOU KNOW).
      [Test]
      public void GIVEN_any_voice_WHEN_rendered_THEN_it_names_no_god_and_holds_the_habit_to_now_and_then()
      {
         foreach (Piety piety in new[] {Piety.Devout, Piety.Ordinary, Piety.ForShow})
         {
            string text = CharacterVoicePolicy.Render(new CharacterVoice {Habit = "You speak in short sentences.", Piety = piety, Coarseness = Coarseness.Rarely});

            text.Should().StartWith("You speak in short sentences.");
            text.Should().Contain("never in every line");
            text.Should().NotContainEquivalentOf(" god");
         }
      }

      // STAKE: a hero with no habit (nothing to say) must render nothing rather than a dangling rule.
      [Test]
      public void GIVEN_no_voice_WHEN_rendered_THEN_nothing_is_said()
      {
         CharacterVoicePolicy.Render(null).Should().BeNull();
      }
   }
}
