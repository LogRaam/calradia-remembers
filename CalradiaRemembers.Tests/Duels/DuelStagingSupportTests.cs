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
   }
}
