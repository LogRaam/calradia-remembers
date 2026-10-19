// Code written by Gabriel Mailhot, 08/10/2026.
// Whether closing a conversation on the map also finishes the player's encounter. Maifailun (Nexus bug report,
// 08/10/2026, 2.7.4): "an exception when trying to close the conversation ... i never had this before". His log: he spoke
// with Dhamshut, a lord whose party was in a fight on the map; on close CR finished the encounter, which settled that
// fight in the same instant ("[SELF-ESTEEM] Dhamshut: SkirmishWon" written during the close), and the game then drew its
// map conversation with a leader no longer there (NullReferenceException in MapConversationTableau.SpawnOpponentLeader).
// An encounter with a battle under way belongs to the game: it is left for the game's own menu to settle.
// Wallace1067 (Nexus, 13/10/2026): "I ride up to a friendly lord ... standing alone on open ground ... On End
// Conversation the encounter is not closed, and I'm left in the vanilla encounter menu ... It happens every time."
// A MapEvent that had already finalized, or already had a winner, was still read as "a battle under way", so a
// settled fight kept the encounter open forever. MapEventSighting.IsLive tells a genuinely live fight (Maifailun,
// still kept) from one that is merely attached but over (Wallace1067, must be finished).

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

      // Maifailun's crash, restated as a sighting: a fight genuinely still going (attached, not finalized, no
      // winner yet) is live, and the game must be left to settle it or the close settles it mid-way.
      [Test]
      public void GIVEN_a_fight_still_going_WHEN_sighted_THEN_the_event_is_live()
      {
         MapEncounterClosePolicy.IsLive(new MapEventSighting {Exists = true}).Should().BeTrue();
      }

      // Wallace1067: "On End Conversation the encounter is not closed ... It happens every time." The MapEvent had
      // reached WaitingRemoval (the state the game uses once a fight is done and only cleanup is pending), so an
      // event merely still ATTACHED was read as a battle under way and the encounter never finished.
      [Test]
      public void GIVEN_a_finalized_event_WHEN_sighted_THEN_it_is_not_live()
      {
         MapEncounterClosePolicy.IsLive(new MapEventSighting {Exists = true, IsFinalized = true}).Should().BeFalse();
      }

      // Wallace1067, the other shape of the same settled fight: a winner was already decided, so there is nothing
      // left for the game's own menu to settle, and the chat close must not wait on it either.
      [Test]
      public void GIVEN_an_event_with_a_winner_WHEN_sighted_THEN_it_is_not_live()
      {
         MapEncounterClosePolicy.IsLive(new MapEventSighting {Exists = true, HasWinner = true}).Should().BeFalse();
      }

      // No event at all (the ordinary friendly chat Wallace1067 describes, nobody fighting anywhere): nothing to
      // settle, so this reads as not live just the same as a settled one, never as a missing fact to guess at.
      [Test]
      public void GIVEN_no_event_at_all_WHEN_sighted_THEN_it_is_not_live()
      {
         MapEncounterClosePolicy.IsLive(new MapEventSighting()).Should().BeFalse();
         MapEncounterClosePolicy.IsLive(null).Should().BeFalse();
      }

      // ── Leaving the meeting (Wallace1067, 17/10/2026, on 2.7.12) ──────────────────────────────────────────────
      // The game starts a battle with the party met when its meeting menu comes back and nobody said the meeting was
      // over. Its own "leave" line says so; CR's close must too.

      private static MeetingLeaveFacts Meeting() => new() {HasEncounter = true, TalkedWithTheMetParty = true};

      // STAKE: Wallace's every-chat case: a friendly lord on the road, and the encounter menu stayed open after the talk.
      [Test]
      public void GIVEN_a_talk_with_a_friendly_party_met_on_the_road_WHEN_the_chat_closes_THEN_the_meeting_is_over()
      {
         MapEncounterClosePolicy.LeavesTheMeeting(Meeting()).Should().BeTrue();
      }

      // STAKE: Wallace's stuck case: the companion recalled from his errand, his party disbanded mid-talk. A battle with
      // a party that no longer exists has no one in it and no way out, at war or not.
      [Test]
      public void GIVEN_the_party_met_is_gone_WHEN_the_chat_closes_THEN_the_meeting_is_over()
      {
         MapEncounterClosePolicy.LeavesTheMeeting(new MeetingLeaveFacts {HasEncounter = true, MetPartyGone = true, AtWarWithTheMetParty = true}).Should().BeTrue();
      }

      // STAKE: an enemy lord met on the road: after the words, the player may still choose to attack from the encounter
      // menu, as before.
      [Test]
      public void GIVEN_a_talk_with_an_enemy_party_WHEN_the_chat_closes_THEN_the_encounter_menu_stays_for_the_player_to_choose()
      {
         MeetingLeaveFacts f = Meeting();
         MapEncounterClosePolicy.LeavesTheMeeting(new MeetingLeaveFacts {HasEncounter = f.HasEncounter, TalkedWithTheMetParty = true, AtWarWithTheMetParty = true})
                                .Should().BeFalse();
      }

      // STAKE: a town or castle visit is not a meeting on the road; ending it would throw the player out of the
      // settlement menus.
      [Test]
      public void GIVEN_a_talk_inside_a_settlement_WHEN_the_chat_closes_THEN_the_encounter_is_not_ended()
      {
         MapEncounterClosePolicy.LeavesTheMeeting(new MeetingLeaveFacts {HasEncounter = true, TalkedWithTheMetParty = true, AtASettlement = true}).Should().BeFalse();
      }

      // STAKE (Zivkyn, 18/10/2026: "I crash every time i end a Council meeting ... if we are preparing for a seige
      // defense"): a siege with no fight yet is invisible to the battle guard; finishing the encounter would tear down the
      // siege menu while the siege goes on. A siege is the game's to settle, whatever else the close sees.
      [Test]
      public void GIVEN_a_siege_under_way_WHEN_the_chat_closes_THEN_the_encounter_is_left_to_the_game()
      {
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, SiegeUnderWay = true}).Should().BeFalse();
         MapEncounterClosePolicy.MayFinish(new MapEncounterCloseFacts {HasEncounter = true, SiegeUnderWay = true, BattleUnderWay = true, BattleIsRaid = true})
                                .Should().BeFalse();
      }

      // STAKE: a briefing is a step inside a meeting (Gabriel, 12/09/2026): closing it must leave the meeting standing.
      [Test]
      public void GIVEN_a_briefing_WHEN_it_closes_THEN_the_meeting_goes_on()
      {
         MapEncounterClosePolicy.LeavesTheMeeting(new MeetingLeaveFacts {HasEncounter = true, TalkedWithTheMetParty = true, BriefingReturnPending = true}).Should().BeFalse();
      }

      // STAKE: a chat with someone who is not the party met (a companion of the player's own, on the road) is no
      // reason to end someone else's meeting.
      [Test]
      public void GIVEN_a_talk_with_someone_other_than_the_party_met_WHEN_the_chat_closes_THEN_the_meeting_is_not_ended()
      {
         MapEncounterClosePolicy.LeavesTheMeeting(new MeetingLeaveFacts {HasEncounter = true}).Should().BeFalse();
      }
   }
}
