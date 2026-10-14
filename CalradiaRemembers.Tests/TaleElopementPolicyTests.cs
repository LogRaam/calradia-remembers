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

      // Gabriel's cr.testall on a new game (13/10/2026): Susada was not wed to Arcor again, yet the tale said she had
      // gone back to him. The game weds nobody whose party is in an army or a battle, and refused in silence. A husband
      // away on campaign (or a captive) is a wait, not an end: the return is tried again until it can happen.
      [Test]
      public void GIVEN_a_husband_away_on_campaign_or_a_captive_WHEN_she_would_be_returned_THEN_the_return_waits_and_is_tried_again()
      {
         ReturnDecision campaign = TaleElopementPolicy.DecideReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true, HeIsOnCampaign = true});
         ReturnDecision captive = TaleElopementPolicy.DecideReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true, EitherIsPrisoner = true});

         campaign.WaitAndRetry.Should().BeTrue();
         campaign.Why.Should().Contain("campaign");
         captive.WaitAndRetry.Should().BeTrue();
      }

      // What closes the way back for good is not waited on.
      [Test]
      public void GIVEN_the_way_back_closed_WHEN_decided_THEN_it_ends_and_says_why()
      {
         ReturnDecision remarried = TaleElopementPolicy.DecideReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true});

         remarried.WaitAndRetry.Should().BeFalse();
         remarried.Why.Should().Contain("wed again");
         TaleElopementPolicy.DecideReturn(Returnable()).Why.Should().BeNull();
      }

      // Gabriel's second cr.testall run (13/10/2026): with Arcor free of any campaign, the game still refused to wed
      // Susada back. Its rule is for NEW matches (two clan heads, a courtship elsewhere, a minor house); this couple's
      // marriage was ended by the tale itself, and her return is the condition of peace. When only that rule stands in
      // the way, the marriage is restored directly, unless she now heads a house of her own, which cannot be left.
      [Test]
      public void GIVEN_only_the_games_matchmaking_rule_refusing_WHEN_she_is_returned_THEN_the_marriage_is_restored_directly()
      {
         ReturnDecision refusedByTheGame = TaleElopementPolicy.DecideReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true, TheGameWouldWedThem = false});
         ReturnDecision sheLeadsAHouse = TaleElopementPolicy.DecideReturn(new ReturnFacts {BothInTheWorld = true, SheIsSingle = true, HeIsSingle = true, TheGameWouldWedThem = false, SheLeadsHerHouse = true});

         refusedByTheGame.Why.Should().BeNull();
         refusedByTheGame.RestoreDirectly.Should().BeTrue();
         sheLeadsAHouse.Why.Should().Contain("heads");
         sheLeadsAHouse.WaitAndRetry.Should().BeFalse();
      }
   }
}
