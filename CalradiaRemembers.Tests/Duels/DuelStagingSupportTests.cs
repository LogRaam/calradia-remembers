// Code written by Gabriel Mailhot, 22/07/2026.
// Duels pillar: this policy decides whether the VENUE DuelVenuePolicy already chose can actually be OPENED by
// the game right now. It feeds DuelEligibilityResolver.CanDuelNow (the one shared gate the prompt-time teaching
// and the commit-time bridge both consult): a true here means challenge_duel is safe to teach or to honour; a
// false means the game has no mission for it yet. Both real venues are built (Wilderness through
// DuelMissionLauncher, TownGates through TownDuelBehavior's town-centre staging); get either wrong and a duel
// path that works stops being taught, or one that does not work gets promised, which is the empty-promise bug
// this codebase keeps closing.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelStagingSupportTests
   {
      // The open-map duel: DuelMissionLauncher.StartWildernessDuel. If this ever returned false, the first duel
      // path ever built would stop being taught or honoured.
      [Test]
      public void GIVEN_the_venue_is_wilderness_WHEN_checking_staging_THEN_it_can_be_staged()
      {
         DuelStagingSupport.CanStage(DuelVenue.Wilderness).Should().BeTrue();
      }

      // The town duel (TownDuelBehavior, on the street ambush's proven town-centre recipe). This one carries
      // most of the feature's reach: the majority of conversations happen inside a settlement, so while this
      // returned false the verb was withheld there and most players would never meet duels at all.
      [Test]
      public void GIVEN_the_venue_is_town_gates_WHEN_checking_staging_THEN_it_can_be_staged()
      {
         DuelStagingSupport.CanStage(DuelVenue.TownGates).Should().BeTrue();
      }

      // NotNow is not "not built yet": it means the player is at sea and there is no ground to fight on, so no
      // amount of future engine work may flip it. Staging must never override that verdict.
      [Test]
      public void GIVEN_the_venue_is_not_now_WHEN_checking_staging_THEN_it_cannot_be_staged()
      {
         DuelStagingSupport.CanStage(DuelVenue.NotNow).Should().BeFalse();
      }

      // yozakura12 (Nexus, 17 and 18/09/2026, the second time on the version meant to have fixed it): the game
      // crashed loading the battlefield when he called a lord out. His log reads venue=TownGates, then "opening
      // the town centre", then nothing, and the conversation before it ended with "returned to campaign map".
      // He was AT a town, on the map, not inside it.
      //
      // Standing on a settlement answers YES to both of the questions that were being asked: a settlement is
      // there, and it owns a "center" location. Neither of them asked the one that decides whether the engine
      // can open an interior mission, which is whether the player is inside the walls.

      [Test]
      public void GIVEN_the_player_is_inside_a_town_with_a_centre_WHEN_asked_THEN_a_town_duel_can_be_held()
      {
         DuelStagingSupport.CanHoldATownDuel(true, true).Should().BeTrue();
      }

      // THE REPORTED CASE. Everything about the settlement is right and the player is still outside it.
      [Test]
      public void GIVEN_the_player_is_at_a_town_but_not_inside_it_WHEN_asked_THEN_no_town_duel_can_be_held()
      {
         DuelStagingSupport.CanHoldATownDuel(false, true).Should()
                           .BeFalse("a settlement the party is merely standing on owns a centre just the same");
      }

      // The half that WAS being asked, kept: a settlement with no duelling ground cannot host one even from
      // inside. Without this the fix above could pass by answering the wrong question the other way round.
      [Test]
      public void GIVEN_a_settlement_with_no_duelling_ground_WHEN_asked_THEN_no_town_duel_can_be_held()
      {
         DuelStagingSupport.CanHoldATownDuel(true, false).Should().BeFalse();
      }

      // A CASTLE COURTYARD IS A DUELLING GROUND, and this rule has always said so: it asks whether there is
      // ground to fight on, never what KIND of settlement owns it. The staging path disagreed for two months.
      // DuelEligibilityResolver stopped demanding IsTown on 10/09/2026, on FloatingMammal's report that
      // castles never offered the duel at all ("none of the lords I 'challenged' to a duel went anywhere other
      // than a dialogue driven event"), but TownDuelBehavior.Arm kept demanding it, so in a castle the duel
      // was offered, armed, and then silently refused. The game's own data settles it: castle_complex in
      // SandBox/ModuleData/location_complex_templates.xml owns a location with id "center", the Castle
      // Courtyard. Gabriel ratified the castle venue on 21/09/2026.
      [Test]
      public void GIVEN_the_player_inside_a_castle_whose_courtyard_is_its_centre_WHEN_asked_THEN_the_duel_can_be_held_there()
      {
         DuelStagingSupport.CanHoldATownDuel(true, true).Should()
                           .BeTrue("a castle courtyard is as good a place to cross blades as a town square, and the "
                                 + "engine opens it by the same location id");
      }

      // AND A VILLAGE STILL CANNOT, which is what makes dropping the settlement-kind test safe rather than
      // merely simpler. A village's walkable location is "village_center", not "center", so asking the engine
      // for a duelling ground already excludes it: there is no kind test left to forget to update, and no
      // village duel opened by omission.
      [Test]
      public void GIVEN_a_village_whose_centre_is_not_the_one_a_duel_opens_WHEN_asked_THEN_no_duel_can_be_held_there()
      {
         DuelStagingSupport.CanHoldATownDuel(true, false).Should()
                           .BeFalse("a village answers to 'village_center' and a hideout to 'hideout_center', so "
                                  + "neither owns the ground this duel opens");
      }

      // And a refusal here is never the end of the duel: the wilderness is still a venue that can be staged,
      // which is what makes routing the outside case to the open field the right answer rather than a fallback.
      [Test]
      public void GIVEN_a_town_duel_is_refused_WHEN_the_open_field_is_chosen_instead_THEN_it_can_still_be_staged()
      {
         DuelStagingSupport.CanHoldATownDuel(false, true).Should().BeFalse();
         DuelStagingSupport.CanStage(DuelVenue.Wilderness).Should().BeTrue();
      }
   
      // ── Waiting for the town to come back (Zivkyn 23/09, Maifailun 29/09/2026) ──────────────────────
      // A duel called from a keep or a lord's hall ends that walkabout mission, then opens the town square. Opened while
      // the game is still on its way back to the town menu, the square crashed in some settlements and not others (a
      // duel with a lady in a Khuzait market worked). The square waits for the menu and a short settling.

      // Not before the town menu is back: the walkabout mission is gone but the game is still between states.
      [Test]
      public void GIVEN_the_walkabout_gone_but_the_town_menu_not_back_WHEN_asked_THEN_the_square_is_not_opened_yet()
      {
         DuelStagingSupport.ReadyToOpenTheSquare(Settled(townMenuActive: false)).Should().BeFalse();
      }

      // Not on the very frame the menu returns either: a short settling first.
      [Test]
      public void GIVEN_the_town_menu_just_back_WHEN_asked_THEN_it_waits_a_few_frames()
      {
         DuelStagingSupport.ReadyToOpenTheSquare(Settled(framesSinceMissionEnded: 1)).Should().BeFalse();
         DuelStagingSupport.ReadyToOpenTheSquare(Settled()).Should().BeTrue();
      }

      // Thoragoros1 (Nexus, 08/10/2026), reproduced by Gabriel with cr.duel_take_field the same day: a duel taken up in a
      // conversation held over the town menu crashed the engine loading the square (0xC0000005, right after
      // "Scene_view::clear_all(Main_map)"), opened in the very second the chat closed. The settling was counted from the
      // last MISSION, which had ended long before the chat, so it was already "over". It is counted from the chat's close
      // too, and the game's own conversation must be over: the square never opens while the map conversation unwinds.
      [Test]
      public void GIVEN_a_chat_just_closed_or_the_games_conversation_still_open_WHEN_asked_THEN_the_square_waits()
      {
         DuelStagingSupport.ReadyToOpenTheSquare(Settled(framesSinceChatClosed: 1)).Should().BeFalse();
         DuelStagingSupport.ReadyToOpenTheSquare(Settled(gameConversationOpen: true)).Should().BeFalse();
      }

      private static SquareReadinessFacts Settled(bool townMenuActive = true, int framesSinceMissionEnded = DuelStagingSupport.SettleFramesBeforeTheSquare,
                                                  int framesSinceChatClosed = DuelStagingSupport.SettleFramesBeforeTheSquare, bool gameConversationOpen = false)
         => new() {
            TownMenuActive = townMenuActive, FramesSinceMissionEnded = framesSinceMissionEnded,
            FramesSinceChatClosed = framesSinceChatClosed, GameConversationOpen = gameConversationOpen
         };

      // ── Who stands aside (Maifailun 29/09/2026: "my companions were also in the fight") ──────────────
      // The game's fight handler makes the whole PLAYER TEAM an enemy of the opponent's, and companions following the
      // player in the street are on that team. Everyone on it but the player stands aside for a duel.

      [Test]
      public void GIVEN_a_companion_following_the_player_WHEN_the_duel_starts_THEN_they_stand_aside()
      {
         DuelStagingSupport.StandsAsideFromTheDuel(isHuman: true, isThePlayer: false, isTheOpponent: false, onThePlayersTeam: true).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_two_duellists_or_a_townsman_WHEN_the_duel_starts_THEN_none_is_moved()
      {
         DuelStagingSupport.StandsAsideFromTheDuel(true, true, false, true).Should().BeFalse();
         DuelStagingSupport.StandsAsideFromTheDuel(true, false, true, false).Should().BeFalse();
         DuelStagingSupport.StandsAsideFromTheDuel(true, false, false, false).Should().BeFalse();
      }
   }
}
