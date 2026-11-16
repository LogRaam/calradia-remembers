// Code written by Gabriel Mailhot, 16/11/2026.
// CellDoorPolicy decides the last day of news a character knows (today, or for a prisoner the day he was taken) and the
// wars his realm fought that day. Schmidt (Discord, 16/11/2026): "even if they were locked up the same day, they have no
// idea what happened outside or what other lords of their faction were doing when they were captured." Gabriel
// (16/11/2026): news stops at the cell door instead of vanishing. Wrong one way, a captive talks of a war declared while
// he sat in the dungeon; the other, a lord taken this morning has never heard of the war he was taken in.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CellDoorPolicyTests
   {
      // STAKE: a free character's news runs to today; nothing about the cell door touches him.
      [Test]
      public void GIVEN_a_free_character_WHEN_his_last_news_day_is_read_THEN_it_is_today()
      {
         CellDoorPolicy.LastNewsDay(new CellDoorFacts {IsPrisoner = false, CaptureDay = 40, Today = 100}).Should().Be(100);
      }

      // STAKE: Schmidt's lord: taken on day 90, talked to on day 100, he knows what happened up to day 90.
      [Test]
      public void GIVEN_a_prisoner_taken_ten_days_ago_WHEN_his_last_news_day_is_read_THEN_it_is_the_day_he_was_taken()
      {
         CellDoorPolicy.LastNewsDay(new CellDoorFacts {IsPrisoner = true, CaptureDay = 90, Today = 100}).Should().Be(90);
      }

      // STAKE: a prisoner whose capture day the game does not give knows no dated news, as the rule stood before.
      [Test]
      public void GIVEN_a_prisoner_with_no_known_capture_day_WHEN_his_last_news_day_is_read_THEN_he_has_none()
      {
         CellDoorPolicy.LastNewsDay(new CellDoorFacts {IsPrisoner = true, CaptureDay = null, Today = 100}).Should().BeNull();
      }

      // STAKE: the battle of his capture day is his; a siege the day after is not.
      [Test]
      public void GIVEN_news_stopped_on_day_90_WHEN_events_are_weighed_THEN_day_90_is_known_and_day_91_is_not()
      {
         CellDoorPolicy.Admits(90, 90).Should().BeTrue();
         CellDoorPolicy.Admits(91, 90).Should().BeFalse();
         CellDoorPolicy.Admits(10, null).Should().BeFalse();
      }

      // STAKE: a war declared while he sat in the cell was not his realm's war the day he was taken.
      [Test]
      public void GIVEN_a_war_declared_after_his_capture_WHEN_his_wars_are_read_THEN_it_is_not_among_them()
      {
         CellDoorPolicy.AtWarWhenTaken(new[] {"Sturgia", "Battania"},
                                       new List<RealmChangeSinceCapture> {new() {Day = 95, OtherRealm = "Battania", WarBegan = true}})
                       .Should().BeEquivalentTo("Sturgia");
      }

      // STAKE: a peace struck while he sat in the cell: the day he was taken, that war was still on.
      [Test]
      public void GIVEN_a_peace_struck_after_his_capture_WHEN_his_wars_are_read_THEN_that_war_is_among_them()
      {
         CellDoorPolicy.AtWarWhenTaken(new[] {"Sturgia"},
                                       new List<RealmChangeSinceCapture> {new() {Day = 92, OtherRealm = "Aserai", WarBegan = false}})
                       .Should().BeEquivalentTo("Sturgia", "Aserai");
      }

      // STAKE: a war declared then ended after his capture: the first change tells the day he was taken (peace), not the last.
      [Test]
      public void GIVEN_a_war_begun_and_ended_after_his_capture_WHEN_his_wars_are_read_THEN_it_is_not_among_them()
      {
         CellDoorPolicy.AtWarWhenTaken(new string[0],
                                       new List<RealmChangeSinceCapture> {
                                          new() {Day = 99, OtherRealm = "Khuzait", WarBegan = false},
                                          new() {Day = 93, OtherRealm = "Khuzait", WarBegan = true}
                                       })
                       .Should().BeEmpty();
      }
   }
}
