// Code written by Gabriel Mailhot, 25/09/2026.
// Documents how an envoy is held to his lord's mandate (prisoner exchange, increment 3; fkasad, Nexus, 25/09/2026;
// Gabriel: always an anonymous envoy, never the lord). The envoy is a model, and a model can write any deal. Whatever
// it writes is rebound here before the bridge runs it in the lord's name, so a stray action is the difference between
// an envoy who negotiates and one who spends his lord's gold on a captive nobody sent him for.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class EnvoyMandatePolicyTests
   {
      private static EnvoyMandate Mandate(string exchange = "")
         => new() {WantedCaptiveName = "Lord Garios", RansomCeiling = 1800, ExchangeCaptiveName = exchange};

      // Gabriel's ruling: anonymous. The envoy is shown only as whose man he is.
      [Test]
      public void GIVEN_a_lord_WHEN_his_envoy_is_named_THEN_he_has_no_name_of_his_own()
      {
         EnvoyMandatePolicy.DisplayName("Derthert").Should().Be("Envoy of Derthert");
         EnvoyMandatePolicy.DisplayName(null).Should().Be("An envoy");
      }

      // The envoy agreed a ransom within his authority: it runs as written, on the captive he was sent for.
      [Test]
      public void GIVEN_a_ransom_within_the_ceiling_WHEN_bound_THEN_it_runs_for_the_wanted_captive_at_the_agreed_price()
      {
         EnvoyDeal deal = EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["target"] = "Garios", ["price"] = "1200"}, Mandate());

         deal.Type.Should().Be("buy_prisoner");
         deal.Parameters["target"].Should().Be("Lord Garios");
         deal.Parameters["price"].Should().Be("1200");
      }

      // A model talked above its ceiling: the lord never authorised it, so the price is held to the ceiling.
      [Test]
      public void GIVEN_a_ransom_above_the_ceiling_WHEN_bound_THEN_it_is_held_to_the_ceiling()
      {
         EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["price"] = "5000"}, Mandate())
                           .Parameters["price"].Should().Be("1800");
      }

      // The envoy said it was agreed but wrote no usable figure: offered at the ceiling rather than dropped, since a
      // deal announced and then silently lost reads as the model breaking its word.
      [Test]
      public void GIVEN_a_ransom_with_no_usable_price_WHEN_bound_THEN_it_is_offered_at_the_ceiling()
      {
         EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["price"] = "a fair sum"}, Mandate())
                           .Parameters["price"].Should().Be("1800");
      }

      // fkasad (Nexus, 26/09/2026): "Of course the prisoner is yours for 0 denars", and 20,000 came in all the same. A
      // player who gives the captive away has agreed a price, and it is nothing: the lord pays nothing, and the deal is
      // marked a gift so the sale verb does not lift it to its floor.
      [TestCase("0")]
      [TestCase("free")]
      [TestCase("nothing")]
      public void GIVEN_a_captive_given_for_nothing_WHEN_bound_THEN_it_is_a_gift_and_no_gold_moves(string price)
      {
         EnvoyDeal deal = EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["price"] = price}, Mandate());

         deal.Parameters["price"].Should().Be("0");
         deal.Parameters["gift"].Should().Be("true");
      }

      // A paid ransom is never marked a gift, so the sale's honest floor still protects the player.
      [Test]
      public void GIVEN_a_paid_ransom_WHEN_bound_THEN_it_is_not_a_gift()
      {
         EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["price"] = "900"}, Mandate())
                           .Parameters.Should().NotContainKey("gift");
      }

      // fkasad: "the NPC should kick off negotiations by making an offer... maybe start at 50% of the budget". The
      // envoy opens at half his ceiling, never below the price a sale may honestly go for, never above the ceiling.
      [TestCase(1800, 400, 900)]
      [TestCase(1800, 1200, 1200)]
      [TestCase(1000, 1500, 1000)]
      public void GIVEN_a_ceiling_and_an_honest_floor_WHEN_the_envoy_opens_THEN_he_offers_half_the_ceiling_within_both(int ceiling, int floor, int expected)
      {
         EnvoyMandatePolicy.OpeningOffer(ceiling, floor).Should().Be(expected);
      }

      // A model naming another captive cannot redirect the lord's gold: the target is always the one he was sent for.
      [Test]
      public void GIVEN_a_ransom_naming_another_captive_WHEN_bound_THEN_it_is_for_the_wanted_captive_all_the_same()
      {
         EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["target"] = "Someone Else", ["price"] = "500"}, Mandate())
                           .Parameters["target"].Should().Be("Lord Garios");
      }

      // An exchange the lord authorised runs with the two captives of the mandate, whatever names the model wrote.
      [Test]
      public void GIVEN_an_authorised_exchange_WHEN_bound_THEN_it_trades_the_wanted_captive_for_the_one_offered()
      {
         EnvoyDeal deal = EnvoyMandatePolicy.Bind("exchange_prisoners", new Dictionary<string, string> {["give"] = "x", ["receive"] = "y"}, Mandate("Borcha"));

         deal.Parameters["give"].Should().Be("Lord Garios");
         deal.Parameters["receive"].Should().Be("Borcha");
      }

      // The lord holds nobody to give: an exchange the model invented has nothing behind it and never runs.
      [Test]
      public void GIVEN_no_one_to_offer_in_exchange_WHEN_an_exchange_is_written_THEN_nothing_runs()
      {
         EnvoyMandatePolicy.Bind("exchange_prisoners", new Dictionary<string, string>(), Mandate("")).Should().BeNull();
      }

      // An envoy runs two actions and no others: gold on the side, a relation change, a quest, all ignored.
      [Test]
      public void GIVEN_any_other_action_WHEN_bound_THEN_nothing_runs()
      {
         EnvoyMandatePolicy.Bind("give_gold", new Dictionary<string, string> {["amount"] = "500"}, Mandate()).Should().BeNull();
         EnvoyMandatePolicy.Bind("change_relation", null, Mandate()).Should().BeNull();
      }

      // "regards the player neutral" is not a sentence; the regard line reads as one whatever the band.
      [Test]
      public void GIVEN_a_regard_band_WHEN_worded_for_the_envoy_THEN_it_reads_after_regards()
      {
         EnvoyMandatePolicy.RegardWords("deep affection").Should().Be("with deep affection");
         EnvoyMandatePolicy.RegardWords("neutral").Should().Be("without warmth or rancour");
      }
   }
}
