// Code written by Gabriel Mailhot, 08/10/2026.
// Whether closing a conversation on the map also finishes the player's encounter. Maifailun (Nexus bug report,
// 08/10/2026, 2.7.4): "an exception when trying to close the conversation ... i never had this before". His log: he spoke
// with Dhamshut, a lord whose party was in a fight on the map; on close CR finished the encounter, which settled that
// fight in the same instant ("[SELF-ESTEEM] Dhamshut: SkirmishWon" written during the close), and the game then drew its
// map conversation with a leader no longer there (NullReferenceException in MapConversationTableau.SpawnOpponentLeader).
// An encounter with a battle under way belongs to the game: it is left for the game's own menu to settle.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MapEncounterClosePolicyTests
   {
      private static MapEncounterCloseFacts Field() => new() {HasEncounter = true};

      // The ordinary talk on the open map: the encounter is finished, so no army menu lingers.
      [Test]
      public void GIVEN_a_plain_talk_on_the_open_map_WHEN_the_chat_closes_THEN_the_encounter_is_finished()
      {
         MapEncounterClosePolicy.MayFinish(Field()).Should().BeTrue();
      }

      // Maifailun's crash: a fight was under way in the encounter (not a raid). It is the game's to settle.
      [Test]
      public void GIVEN_a_battle_under_way_in_the_encounter_WHEN_the_chat_closes_THEN_the_encounter_is_left_to_the_game()
      {
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, BattleUnderWay = true}).Should().BeFalse();
      }

      // Zivkyn (Nexus, 10/10/2026), since 2.7.5: an ally spoke to him while he raided a village, and closing the chat
      // left him on "attack, surrender or try to escape". A raid is the player's own action, not a fight to settle: the
      // close finishes it as it did before 2.7.5 (the game's own raid branch ends it cleanly), Maifailun's fix untouched.
      [Test]
      public void GIVEN_the_player_raiding_a_village_WHEN_the_chat_closes_THEN_the_encounter_is_finished_as_before()
      {
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, BattleUnderWay = true, BattleIsRaid = true}).Should().BeTrue();
      }

      // The guards that were already there still hold: no encounter, a scene, a settlement, or a briefing that returns.
      [Test]
      public void GIVEN_no_map_encounter_of_its_own_WHEN_the_chat_closes_THEN_nothing_is_finished()
      {
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts()).Should().BeFalse();
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, InMission = true}).Should().BeFalse();
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, InsideSettlement = true}).Should().BeFalse();
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, BriefingReturnPending = true}).Should().BeFalse();
      }
   }
}
