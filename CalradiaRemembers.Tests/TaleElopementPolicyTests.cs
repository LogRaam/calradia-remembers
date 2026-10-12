// Code written by Gabriel Mailhot, 12/10/2026.
// The Troy-inspired tale (docs/design/tale-scenario-bridge; Gabriel, 12/10/2026): a queen leaves her husband with the
// player ("elope"), rides with him as a guest who may later be married ("Invitee et epousable"), and her return to her
// husband is the condition of peace ("Le retour de la Reine a son mari est la condition de paix"). These rules say when
// each can really happen. If they are wrong, a tale tells the realm she left while the game kept her married, a woman
// who leads her house is torn from it, a captive "rides away", or she is "returned" to a man already remarried, or
// after the player married her.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleElopementPolicyTests
   {
      private static ElopementFacts Free() => new() {SheIsMarriedToHim = true, BothInTheWorld = true};

      // The tale's turn: a married lady, free, leading neither house, party nor town, may leave with the player.
      [Test]
      public void GIVEN_a_married_lady_free_to_go_WHEN_she_would_elope_THEN_she_may()
      {
         TaleElopementPolicy.WhyNotElope(Free()).Should().BeNull();
      }

      // A divorce the game could not complete would leave her married while the tale says she left: never begun.
      [Test]
      public void GIVEN_what_keeps_a_marriage_from_ending_WHEN_she_would_elope_THEN_she_does_not()
      {
         TaleElopementPolicy.WhyNotElope(new ElopementFacts {BothInTheWorld = true}).Should().Contain("married");
         TaleElopementPolicy.WhyNotElope(new ElopementFacts {SheIsMarriedToHim = true}).Should().Contain("world");
         TaleElopementPolicy.WhyNotElope(new ElopementFacts {SheIsMarriedToHim = true, BothInTheWorld = true, EitherIsPrisoner = true}).Should().Contain("captive");
         TaleElopementPolicy.WhyNotElope(new ElopementFacts {SheIsMarriedToHim = true, BothInTheWorld = true, SheIsPregnant = true}).Should().Contain("child");
         TaleElopementPolicy.WhyNotElope(new ElopementFacts {SheIsMarriedToHim = true, BothInTheWorld = true, SheLeadsHerHouse = true}).Should().Contain("house");
      }

      // A party she leads or a town she governs is a post the game must release first, as for a Stolen Love divorce.
      [Test]
      public void GIVEN_she_leads_a_party_or_governs_WHEN_she_would_elope_THEN_her_post_is_released_first()
      {
         TaleElopementPolicy.MustReleasePost(new ElopementFacts {SheLeadsAParty = true}).Should().BeTrue();
         TaleElopementPolicy.MustReleasePost(new ElopementFacts {SheGoverns = true}).Should().BeTrue();
         TaleElopementPolicy.MustReleasePost(Free()).Should().BeFalse();
      }

      // ── Her return: the condition of peace ──

      private static ReturnFacts Returnable() => new() {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true};

      // Gabriel: her return to her husband ends the war. She and he both free and unwed, the marriage is restored.
      [Test]
      public void GIVEN_both_alive_and_unwed_WHEN_she_is_returned_THEN_the_marriage_is_restored()
      {
         TaleElopementPolicy.WhyNotReturn(Returnable()).Should().BeNull();
      }

      // Marrying her closes the way back (DESIGN.md); a husband wed again has no place for her; a captive goes nowhere.
      [Test]
      public void GIVEN_what_closes_the_way_back_WHEN_she_would_be_returned_THEN_she_is_not()
      {
         TaleElopementPolicy.WhyNotReturn(new ReturnFacts {BothInTheWorld = true, HeIsSingle = true}).Should().Contain("married");
         TaleElopementPolicy.WhyNotReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true}).Should().Contain("wed again");
         TaleElopementPolicy.WhyNotReturn(new ReturnFacts {SheIsSingle = true, HeIsSingle = true}).Should().Contain("world");
         TaleElopementPolicy.WhyNotReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true, EitherIsPrisoner = true}).Should().Contain("captive");
      }
   }
}
