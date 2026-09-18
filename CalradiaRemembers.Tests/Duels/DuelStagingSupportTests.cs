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

      // And a refusal here is never the end of the duel: the wilderness is still a venue that can be staged,
      // which is what makes routing the outside case to the open field the right answer rather than a fallback.
      [Test]
      public void GIVEN_a_town_duel_is_refused_WHEN_the_open_field_is_chosen_instead_THEN_it_can_still_be_staged()
      {
         DuelStagingSupport.CanHoldATownDuel(false, true).Should().BeFalse();
         DuelStagingSupport.CanStage(DuelVenue.Wilderness).Should().BeTrue();
      }
   }
}
