// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08): the LLM proposes a price in-fiction, but the BRIDGE IS LAW here.
// BuyPrisonerVerb and SellPrisonerVerb both trust PrisonerTradePolicy.ClampPrice as the one place that keeps
// that price honest around the game's own ransom valuation. A floor set too low turns a captive into a
// giveaway; a ceiling set too high lets a silver tongue talk a lord into an absurd premium; either failure
// undermines the bounty-hunting loop this trade exists to open.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerTradePolicyTests
    {
        // A genuinely reasonable negotiation must pass through untouched: the clamp exists to catch abuse,
        // not to re-price every honest deal regardless of what was actually agreed.
        [Test]
        public void GIVEN_a_price_within_the_honest_band_WHEN_clamping_THEN_the_negotiated_figure_stands()
        {
            PrisonerTradePolicy.ClampPrice(800, ransomAnchor: 1000).Should().Be(800);
        }

        // The 40% floor stops a captive being haggled down to a giveaway; without it a player (or an
        // over-eager LLM) could talk a lord into selling a valuable hostage for pocket change.
        [Test]
        public void GIVEN_a_lowball_below_the_floor_WHEN_clamping_THEN_it_rises_to_the_floor()
        {
            // Floor = 40% of the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(10, ransomAnchor: 1000).Should().Be(400);
        }

        // The 3x ceiling is the mirror guard: the LLM negotiates in-fiction, but it must never be able to
        // agree its way into paying (or demanding) an absurd sum for a single captive.
        [Test]
        public void GIVEN_an_absurd_premium_WHEN_clamping_THEN_it_falls_to_the_ceiling()
        {
            // Ceiling = 3x the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(50000, ransomAnchor: 1000).Should().Be(3000);
        }

        // A missing price is a broken emission, not a broken promise: the exchange already promised a sale,
        // so it must land at the honest floor, never at zero or a free hand-over.
        [Test]
        public void GIVEN_no_price_emitted_WHEN_clamping_THEN_the_sale_lands_at_the_floor_not_zero()
        {
            PrisonerTradePolicy.ClampPrice(0, ransomAnchor: 1000).Should().Be(400);
        }

        // BuyPrisonerVerb.RansomAnchor falls back to FallbackAnchor whenever the game's own ransom model
        // cannot be read for a captive; a raw anchor of 0 would otherwise collapse floor and ceiling to
        // nothing and let any price through unclamped.
        [Test]
        public void GIVEN_an_unreadable_ransom_anchor_WHEN_clamping_THEN_the_fallback_anchor_rules()
        {
            // Fallback anchor 500: floor 200, ceiling 1500.
            PrisonerTradePolicy.ClampPrice(9999, ransomAnchor: 0).Should().Be(1500);
            PrisonerTradePolicy.ClampPrice(100, ransomAnchor: -5).Should().Be(200);
        }

        // The 40% floor alone is not enough for a very low-value captive (24 denars here); MinPrice is the
        // absolute backstop so no prisoner, however lowly, ever trades for a trivial sum.
        [Test]
        public void GIVEN_a_worthless_captive_WHEN_clamping_THEN_the_absolute_minimum_price_holds()
        {
            // Anchor 60: raw floor would be 24, held up to the MinPrice of 50.
            PrisonerTradePolicy.ClampPrice(1, ransomAnchor: 60).Should().Be(PrisonerTradePolicy.MinPrice);
        }

        // STAKE: GiveGoldAction self-clamps a payment to the payer's purse, so a broke NPC buyer would "buy" a
        // valuable captive for its last few denars while the player was told the full agreed price. buy_prisoner
        // refuses below this honest floor; it must match the same 40% band ClampPrice uses so a captive is never
        // given away for a pittance. Anchor 1000 -> floor 400.
        [Test]
        public void GIVEN_a_valuable_captive_WHEN_reading_the_minimum_acceptable_payment_THEN_it_is_the_forty_percent_floor()
        {
            PrisonerTradePolicy.MinAcceptablePayment(ransomAnchor: 1000).Should().Be(400);
        }

        // STAKE: for a very low-value captive the 40% floor collapses below MinPrice; the absolute backstop must
        // still hold so a buyer can never acquire even a lowly prisoner for a trivial sum. Anchor 60 -> raw 24,
        // held up to MinPrice.
        [Test]
        public void GIVEN_a_worthless_captive_WHEN_reading_the_minimum_acceptable_payment_THEN_the_absolute_minimum_holds()
        {
            PrisonerTradePolicy.MinAcceptablePayment(ransomAnchor: 60).Should().Be(PrisonerTradePolicy.MinPrice);
        }

        // STAKE: RansomAnchor falls back to FallbackAnchor when the game's ransom model is unreadable; a raw
        // anchor of 0 must not collapse the minimum acceptable payment to nothing (which would let a buyer take
        // the captive for a single denar). Fallback 500 -> floor 200.
        [Test]
        public void GIVEN_an_unreadable_ransom_anchor_WHEN_reading_the_minimum_acceptable_payment_THEN_the_fallback_rules()
        {
            PrisonerTradePolicy.MinAcceptablePayment(ransomAnchor: 0).Should().Be(200);
        }
    
      #region which way the trade actually runs (Lordfadooboo, 21/09/2026)

      // THE REPORT: he tried to BUY a Nord lord from a Khuzait one. The model heard the player's word "buy" and
      // emitted buy_prisoner, which is the mirror verb (the NPC buying FROM the player), so the bridge answered
      // "you hold no such captive to sell" while his gold had already gone. Both verbs were taught and each spec
      // already names the other as its anti-pattern: teaching harder was not going to settle it. Custody does.
      [Test]
      public void GIVEN_the_captive_stands_in_the_npcs_own_cage_WHEN_reading_the_trade_THEN_the_npc_can_only_be_selling()
      {
         PrisonerTradePolicy.DirectionFor(playerHoldsTarget: false, npcClanHoldsTarget: true)
                            .Should().Be(PrisonerTradePolicy.TradeDirection.NpcSellsToPlayer);
      }

      [Test]
      public void GIVEN_the_player_holds_the_captive_WHEN_reading_the_trade_THEN_the_npc_can_only_be_buying()
      {
         PrisonerTradePolicy.DirectionFor(playerHoldsTarget: true, npcClanHoldsTarget: false)
                            .Should().Be(PrisonerTradePolicy.TradeDirection.NpcBuysFromPlayer);
      }

      // A NAME NEITHER SIDE HOLDS is a hallucinated captive, and must stay a refusal: redirecting it would
      // invent a trade out of nothing, which is worse than the bug being fixed.
      [Test]
      public void GIVEN_nobody_here_holds_that_captive_WHEN_reading_the_trade_THEN_there_is_no_trade_to_run()
      {
         PrisonerTradePolicy.DirectionFor(playerHoldsTarget: false, npcClanHoldsTarget: false)
                            .Should().Be(PrisonerTradePolicy.TradeDirection.NoTrade);
      }

      // The player's own captive wins when both look true (a stale read on the other side): the player's party
      // roster is the one custody fact the mod reads directly rather than inferring.
      [Test]
      public void GIVEN_both_sides_appear_to_hold_them_WHEN_reading_the_trade_THEN_the_players_own_roster_decides()
      {
         PrisonerTradePolicy.DirectionFor(playerHoldsTarget: true, npcClanHoldsTarget: true)
                            .Should().Be(PrisonerTradePolicy.TradeDirection.NpcBuysFromPlayer);
      }

      #endregion

        // Lordfadooboo (Nexus, 04/10/2026): he offered Taslur 20000 for Rhagaea, Taslur agreed in words, and the game
        // answered "You hold no such captive to sell", which he was not trying to do. The refusal must say the truth of
        // it: the lord he spoke to does not hold her, and who does, so the player knows whom to treat with.
        [Test]
        public void GIVEN_a_captive_held_by_another_house_WHEN_the_wrong_lord_agrees_to_sell_THEN_the_player_is_told_who_holds_them()
        {
            string note = PrisonerTradePolicy.NotHisToSellNote(new NotHisToSellFacts {
                SellerName = "Taslur", CaptiveName = "Rhagaea", HolderName = "Mesui", PlaceName = "Makeb"
            });

            note.Should().Contain("Taslur does not hold Rhagaea").And.Contain("Mesui").And.Contain("Makeb");
            note.Should().NotContain("to sell.)", "the old wording read as though the player were the seller");
        }

        // A captive of the player's own house is never merchandise: the refusal says so, rather than "no such captive".
        [Test]
        public void GIVEN_a_captive_of_the_players_own_house_WHEN_a_lord_agrees_to_sell_THEN_the_player_is_told_why_not()
        {
            PrisonerTradePolicy.NotHisToSellNote(new NotHisToSellFacts {SellerName = "Taslur", CaptiveName = "Rhagaea", IsPlayersOwn = true})
                .Should().Contain("one of your own");
        }

        // Nobody by that name in any cell: the refusal names the lord, not the player, as the one without the captive.
        [Test]
        public void GIVEN_no_captive_by_that_name_WHEN_a_lord_agrees_to_sell_THEN_the_refusal_says_he_holds_none()
        {
            PrisonerTradePolicy.NotHisToSellNote(new NotHisToSellFacts {SellerName = "Taslur", CaptiveName = "Rhagaea"})
                .Should().Contain("Taslur holds no captive called Rhagaea");
        }
    }
}
