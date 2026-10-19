// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. CompanionLifecyclePolicy gates two lifecycle transitions the engine drives:
//   - MayDepartNow feeds CompanionHappinessBehavior / WarWearinessBehavior: a companion AWAY on an errand must
//     be excluded from the organic quit/retire loops, or one abroad on the player's business departs mid-errand.
//   - MayReintegrate feeds CompanionMissionBehavior.Reunite (and the AwaitingReunion force-close): a returning
//     errand-runner is re-added to the party ONLY if still a player companion of the player's clan, or someone
//     EXPELLED / who left the clan while away silently reappears in the party.
// A wrong answer here reintroduces exactly the two MAJEUR bugs this release closes.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionLifecyclePolicyTests
   {
      // The departure-guard fix: a companion away on an errand cannot organically quit or retire, or the
      // happiness/weariness loop would remove them from the clan while they are still abroad on your business.
      [Test]
      public void GIVEN_a_companion_away_on_an_errand_WHEN_asked_if_they_may_depart_now_THEN_no()
      {
         CompanionLifecyclePolicy.MayDepartNow(true).Should().BeFalse();
      }

      // A companion at home is fair game for the organic quit/retire loops, exactly as before the guard.
      [Test]
      public void GIVEN_a_companion_at_home_WHEN_asked_if_they_may_depart_now_THEN_yes()
      {
         CompanionLifecyclePolicy.MayDepartNow(false).Should().BeTrue();
      }

      // The reappearance fix: a returning errand-runner who is still both a player companion AND of the player's
      // clan may rejoin the party, the normal case.
      [Test]
      public void GIVEN_a_still_seated_clan_companion_WHEN_asked_if_they_may_reintegrate_THEN_yes()
      {
         CompanionLifecyclePolicy.MayReintegrate(true, true).Should().BeTrue();
      }

      // Expelled while away (no longer a player companion): must NOT be re-added, or an ejected companion
      // silently reappears in the party on return (the audit bug).
      [Test]
      public void GIVEN_a_companion_expelled_while_away_WHEN_asked_if_they_may_reintegrate_THEN_no()
      {
         CompanionLifecyclePolicy.MayReintegrate(false, true).Should().BeFalse();
      }

      // Left the player's clan while away: the same guard blocks the reappearance from the clan-membership side.
      [Test]
      public void GIVEN_a_companion_no_longer_of_the_players_clan_WHEN_asked_if_they_may_reintegrate_THEN_no()
      {
         CompanionLifecyclePolicy.MayReintegrate(true, false).Should().BeFalse();
      }
   }
}
