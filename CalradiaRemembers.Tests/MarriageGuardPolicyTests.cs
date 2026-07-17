// Code written by Gabriel Mailhot, 17/07/2026.
// Marriage & blessing guards (romance audit M-B3/M-B4/M-B5). These are the "bridge is law" re-validations for the
// chat marry and blessing actions: without them a hallucinated (or stale-quest) emission could wed a captive or
// an enemy across an active war, or record a blessing from a dead, imprisoned, at-war, or openly hostile lord,
// none of which vanilla would ever permit. Each test pins one forbidden state so a future loosening cannot
// silently reopen a marriage or a blessing that should be impossible.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MarriageGuardPolicyTests
   {
      // The clean case: neither party captive, no war. A marriage the player genuinely earned must still go
      // through, so the guard never blocks a legitimate union.
      [Test]
      public void GIVEN_no_captivity_and_no_war_WHEN_checking_a_marriage_THEN_it_is_allowed()
      {
         MarriageGuardPolicy.MarriageAllowed(candidatePrisoner: false, playerPrisoner: false, atWarWithCandidate: false)
            .Should().BeTrue();
      }

      // Each blocked state alone must forbid the marriage: a captive candidate, a captive player, or an active
      // war. Vanilla forbids all three at its own screen, and the M-B5 guard must match that exactly.
      [Test]
      public void GIVEN_any_of_captivity_or_war_WHEN_checking_a_marriage_THEN_it_is_forbidden()
      {
         MarriageGuardPolicy.MarriageAllowed(true, false, false).Should().BeFalse();  // candidate captive
         MarriageGuardPolicy.MarriageAllowed(false, true, false).Should().BeFalse();  // player captive
         MarriageGuardPolicy.MarriageAllowed(false, false, true).Should().BeFalse();  // at war
      }

      // A blessing from a lord who is alive, free, at peace, and holds the player in at least neutral regard is
      // the one valid case, so an earned blessing still lands.
      [Test]
      public void GIVEN_a_living_free_peaceful_regarding_lord_WHEN_checking_a_blessing_THEN_it_is_allowed()
      {
         MarriageGuardPolicy.BlessingAllowed(blesserAlive: true, blesserPrisoner: false, atWarWithBlesser: false,
               blesserRegard: MarriageGuardPolicy.MinBlessingRegard)
            .Should().BeTrue();
      }

      // Each disqualifying state alone kills the blessing: dead, imprisoned, at war, or holding the player in
      // negative regard. The negative-regard case is the M-B3 headline (an enemy at -100 could otherwise bless).
      [Test]
      public void GIVEN_any_disqualifying_state_WHEN_checking_a_blessing_THEN_it_is_forbidden()
      {
         MarriageGuardPolicy.BlessingAllowed(false, false, false, 20).Should().BeFalse();   // dead
         MarriageGuardPolicy.BlessingAllowed(true, true, false, 20).Should().BeFalse();     // imprisoned
         MarriageGuardPolicy.BlessingAllowed(true, false, true, 20).Should().BeFalse();     // at war
         MarriageGuardPolicy.BlessingAllowed(true, false, false, -1).Should().BeFalse();    // negative regard
      }

      // M-B1: the +40 warm swing may only land on the clan that actually blessed. When the recorded blessing
      // clan still matches the spouse's family clan, the blessing is honoured.
      [Test]
      public void GIVEN_a_blessing_for_the_spouses_current_family_WHEN_marrying_THEN_it_is_honoured()
      {
         MarriageGuardPolicy.BlessingHonoursFamily(hasRecord: true, blessedClanId: "clan_A", spouseFamilyClanId: "clan_A")
            .Should().BeTrue();
      }

      // The M-B1 core: a spouse who changed clans since the blessing carries a record for a DIFFERENT clan than
      // the family the swing lands on. That family never blessed, so the marriage is unblessed, no unearned +40.
      [Test]
      public void GIVEN_a_blessing_for_a_clan_the_spouse_has_left_WHEN_marrying_THEN_it_is_not_honoured()
      {
         MarriageGuardPolicy.BlessingHonoursFamily(true, "clan_A", "clan_B").Should().BeFalse();
      }

      // No record at all, or a missing clan id on either side, can never honour a blessing (and must not throw).
      [Test]
      public void GIVEN_no_record_or_a_missing_clan_id_WHEN_marrying_THEN_it_is_not_honoured()
      {
         MarriageGuardPolicy.BlessingHonoursFamily(false, "clan_A", "clan_A").Should().BeFalse();
         MarriageGuardPolicy.BlessingHonoursFamily(true, null, "clan_A").Should().BeFalse();
         MarriageGuardPolicy.BlessingHonoursFamily(true, "clan_A", null).Should().BeFalse();
      }
   }
}
