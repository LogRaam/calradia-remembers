// Code written by Gabriel Mailhot, 03/10/2026.
// Fakade (Nexus, 03/10/2026): "an NPC wants to approach me to start a conversation, but I am in a fief preparing for
// a defensive siege. The NPC will wait right outside of the gate until the siege begins. If that unit is also at war
// with the sieging army, I am pulled into a land battle instead of a defensive siege." A pursuer within reach never
// gave up ("about to connect"), and one at the gate of the fief the player sits in is always within reach, so the
// day limit never came. If these rules are wrong, a lord camps at the player's gate for good, or drags him into a
// field battle in the middle of his own siege defence.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChaseAtTheGateTests
   {
      // Fakade's case: the player's fief is besieged; the lord at the gate leaves at once, however close he stands.
      [Test]
      public void GIVEN_the_players_fief_under_siege_WHEN_judging_the_chase_THEN_the_pursuer_stands_down_at_once()
      {
         InterceptionPolicy.ShouldAbandonChase(new InterceptionPolicy.ChaseFacts {
            Distance = 1f, DaysPursuing = 0, PlayerInsideSettlement = true, PlayerSettlementBesieged = true
         }).Should().BeTrue();
      }

      // Inside walls, "almost caught" means nothing: the day limit applies, so nobody camps at the gate for good.
      [Test]
      public void GIVEN_the_player_inside_a_fief_past_the_day_limit_WHEN_judging_THEN_the_pursuer_gives_up_even_at_the_gate()
      {
         InterceptionPolicy.ShouldAbandonChase(new InterceptionPolicy.ChaseFacts {
            Distance = 1f, DaysPursuing = InterceptionPolicy.MaxPursuitDays, PlayerInsideSettlement = true
         }).Should().BeTrue();
      }

      // Within the limit he may still wait a while for the player to come out: a short stop in town is no refusal.
      [Test]
      public void GIVEN_the_player_inside_a_fief_within_the_day_limit_WHEN_judging_THEN_the_pursuer_still_waits()
      {
         InterceptionPolicy.ShouldAbandonChase(new InterceptionPolicy.ChaseFacts {
            Distance = 1f, DaysPursuing = 1, PlayerInsideSettlement = true
         }).Should().BeFalse();
      }

      // A siege ending a meeting is still a missed meeting: the lord writes, as when he gives up on the road.
      [Test]
      public void GIVEN_a_chase_ended_by_a_siege_WHEN_the_lord_lives_THEN_he_writes_what_he_meant_to_say()
      {
         InterceptionPolicy.ShouldWriteMissedMeetingLetter(AbandonReason.Siege, true).Should().BeTrue();
      }
   }
}
