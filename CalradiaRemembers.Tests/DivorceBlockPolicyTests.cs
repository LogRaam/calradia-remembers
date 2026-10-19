// Code written by Gabriel Mailhot, 17/07/2026.
// DIVORCE: classifying why a dissolution was refused at its deadline (romance audit M-D2/M-D6). The host used to
// treat EVERY refusal as the household resisting: a false "closed ranks" record plus a relation penalty on the
// player, and for the player's own walk-out an infinite silent retry. This policy splits the two real causes so
// the host reacts honestly. Get it wrong and either a pregnant couple's divorce punishes the player for biology
// (transient misread as resistance) or the "hand your spouse a party to freeze the divorce" exploit reopens
// (structural misread as none). Precedence matters: a pregnancy must win over a role, because while either party
// is pregnant the marriage cannot dissolve AT ALL, so releasing a party would be premature and pointless.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceBlockPolicyTests
   {
      // A clean pair with no pregnancy, no captivity, and a freely-relocatable leaving spouse: any refusal here
      // was genuine spousal resistance, which stays a real abort. This is the baseline the two harmful misreads
      // are measured against.
      [Test]
      public void GIVEN_no_pregnancy_captivity_or_role_WHEN_classifying_THEN_no_mechanical_block()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = false, EitherCaptive = false, LeavingIsClanLeader = false, LeavingLeadsParty = false, LeavingIsGovernor = false}).Should().Be(DivorceBlock.None);
      }

      // Pregnancy is transient: the host must DEFER, never abort-with-penalty. A conception early in a 15-30 day
      // process would otherwise guarantee a false "resisted" failure, since a pregnancy runs far longer (M-D6).
      [Test]
      public void GIVEN_a_pregnancy_WHEN_classifying_THEN_it_is_transient()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = true, EitherCaptive = false, LeavingIsClanLeader = false, LeavingLeadsParty = false, LeavingIsGovernor = false}).Should().Be(DivorceBlock.Transient);
      }

      // Captivity is transient for the same reason: it clears on release, ransom, or death, so it must defer, not
      // punish the player as if the household had closed ranks.
      [Test]
      public void GIVEN_a_captivity_WHEN_classifying_THEN_it_is_transient()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = false, EitherCaptive = true, LeavingIsClanLeader = false, LeavingLeadsParty = false, LeavingIsGovernor = false}).Should().Be(DivorceBlock.Transient);
      }

      // Each structural role, alone, is a STRUCTURAL block: none clears on its own, so a silent retry would loop
      // forever. The party-leader case is the exact exploit vector (M-D2: hand the spouse a party to freeze the
      // divorce), so it must be caught distinctly from clan-leadership and governorship.
      [Test]
      public void GIVEN_only_a_party_leadership_WHEN_classifying_THEN_it_is_structural()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = false, EitherCaptive = false, LeavingIsClanLeader = false, LeavingLeadsParty = true, LeavingIsGovernor = false}).Should().Be(DivorceBlock.Structural);
      }

      [Test]
      public void GIVEN_only_a_clan_leadership_WHEN_classifying_THEN_it_is_structural()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = false, EitherCaptive = false, LeavingIsClanLeader = true, LeavingLeadsParty = false, LeavingIsGovernor = false}).Should().Be(DivorceBlock.Structural);
      }

      [Test]
      public void GIVEN_only_a_governorship_WHEN_classifying_THEN_it_is_structural()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = false, EitherCaptive = false, LeavingIsClanLeader = false, LeavingLeadsParty = false, LeavingIsGovernor = true}).Should().Be(DivorceBlock.Structural);
      }

      // Precedence: a pregnancy AND a party leadership at once must read TRANSIENT, not structural. While the
      // couple is pregnant the marriage cannot dissolve at all, so disbanding the party now would be premature;
      // once the pregnancy clears the role is re-classified and released then. Getting this backwards would
      // destroy a spouse's party for a divorce that was never going to complete that day.
      [Test]
      public void GIVEN_both_a_pregnancy_and_a_role_WHEN_classifying_THEN_transient_wins()
      {
         DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = true, EitherCaptive = false, LeavingIsClanLeader = true, LeavingLeadsParty = true, LeavingIsGovernor = true}).Should().Be(DivorceBlock.Transient);
      }
   }
}
