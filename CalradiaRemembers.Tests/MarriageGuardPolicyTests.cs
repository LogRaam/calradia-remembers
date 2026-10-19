// Code written by Gabriel Mailhot, 17/07/2026.
// Marriage & blessing guards (romance audit M-B3/M-B4/M-B5). These are the "bridge is law" re-validations for the
// chat marry and blessing actions: without them a hallucinated (or stale-quest) emission could wed a captive or
// an enemy across an active war, or record a blessing from a dead, imprisoned, at-war, or openly hostile lord,
// none of which vanilla would ever permit. Each test pins one forbidden state so a future loosening cannot
// silently reopen a marriage or a blessing that should be impossible.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
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
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = false, AtWarWithCandidate = false})
            .Should().BeTrue();
      }

      // Each blocked state alone must forbid the marriage: a captive candidate, a captive player, or an active
      // war. Vanilla forbids all three at its own screen, and the M-B5 guard must match that exactly.
      [Test]
      public void GIVEN_any_of_captivity_or_war_WHEN_checking_a_marriage_THEN_it_is_forbidden()
      {
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = true, PlayerPrisoner = false, AtWarWithCandidate = false}).Should().BeFalse();  // candidate captive
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = true, AtWarWithCandidate = false}).Should().BeFalse();  // player captive
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = false, AtWarWithCandidate = true}).Should().BeFalse();  // at war
      }

      // v1.34.2 finding 1: arrange_marriage (CourtActionResolver.TryArrangeMarriage) now re-applies THIS same
      // predicate to BOTH kin, having delegated all legality to the engine's IsCoupleSuitableForMarriage before,
      // which does not bar a captive kin or kin whose houses are at war. Mapping the two prospective spouses onto
      // the predicate's args, a captive on either side or a war between their houses must forbid the match exactly
      // as the direct marry action already does, so the arranged path cannot wed a captive or across a war.
      [Test]
      public void GIVEN_an_arranged_match_with_a_captive_kin_or_warring_houses_WHEN_checking_THEN_it_is_forbidden()
      {
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = true, PlayerPrisoner = false, AtWarWithCandidate = false}).Should().BeFalse();  // player-kin captive
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = true, AtWarWithCandidate = false}).Should().BeFalse();  // target-kin captive
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = false, AtWarWithCandidate = true}).Should().BeFalse();  // houses at war
         MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = false, PlayerPrisoner = false, AtWarWithCandidate = false}).Should().BeTrue();  // clean match still allowed
      }

      // A blessing from a lord who is alive, free, at peace, and holds the player in at least neutral regard is
      // the one valid case, so an earned blessing still lands.
      [Test]
      public void GIVEN_a_living_free_peaceful_regarding_lord_WHEN_checking_a_blessing_THEN_it_is_allowed()
      {
         MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = true, BlesserPrisoner = false, AtWarWithBlesser = false, BlesserRegard = MarriageGuardPolicy.MinBlessingRegard})
            .Should().BeTrue();
      }

      // Each disqualifying state alone kills the blessing: dead, imprisoned, at war, or holding the player in
      // negative regard. The negative-regard case is the M-B3 headline (an enemy at -100 could otherwise bless).
      [Test]
      public void GIVEN_any_disqualifying_state_WHEN_checking_a_blessing_THEN_it_is_forbidden()
      {
         MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = false, BlesserPrisoner = false, AtWarWithBlesser = false, BlesserRegard = 20}).Should().BeFalse();   // dead
         MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = true, BlesserPrisoner = true, AtWarWithBlesser = false, BlesserRegard = 20}).Should().BeFalse();     // imprisoned
         MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = true, BlesserPrisoner = false, AtWarWithBlesser = true, BlesserRegard = 20}).Should().BeFalse();     // at war
         MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = true, BlesserPrisoner = false, AtWarWithBlesser = false, BlesserRegard = -1}).Should().BeFalse();    // negative regard
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

      // Gabriel, 25/09/2026 (zhenja1's report): a marriage the base game made always had the family's consent, the
      // clan head bargained it or the family offered it. Treating it as unblessed chilled the very family that
      // proposed the match by 40 and planted grudges on its kin.
      [Test]
      public void GIVEN_a_marriage_the_base_game_arranged_WHEN_the_family_reacts_THEN_it_approves_without_any_blessing_record()
      {
         MarriageGuardPolicy.FamilyApproves(madeThroughCr: false, hasRecord: false, blessedClanId: null, spouseFamilyClanId: "clan_A")
            .Should().BeTrue();
      }

      // The case the blessing exists for: a match sealed in conversation, going around the family. It is still
      // judged by whether that family blessed it.
      [Test]
      public void GIVEN_a_marriage_made_in_conversation_WHEN_the_family_never_blessed_it_THEN_it_does_not_approve()
      {
         MarriageGuardPolicy.FamilyApproves(true, false, null, "clan_A").Should().BeFalse();
         MarriageGuardPolicy.FamilyApproves(true, true, "clan_A", "clan_A").Should().BeTrue();
      }
   }
}
