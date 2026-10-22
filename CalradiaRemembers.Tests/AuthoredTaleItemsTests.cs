// Code written by Gabriel Mailhot, 21/10/2026.
// Items in tales, the second phase tashmetu asked for (20/10/2026): "A tale that asks for things ('bring 30 grain to
// {giver}'), and a thing given in an ending", approved by Gabriel for a second phase. These tests hold how a tale names
// its goods, when a hand-over counts, who receives it, and what the loader refuses. If they are wrong, a tale asks for
// goods nobody can bring, takes the player's food for less than it asked and moves on as if it had all, hands the goods
// to the wrong person, or promises an item the game cannot give.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredTaleItemsTests
   {
      private const string Grain = """
      {
        "id": "cr_test_grain",
        "title": "The Hungry Winter",
        "binds": { "giver": "a lord", "village": "a village" },
        "stages": [
          { "id": "ask", "journal": "{giver} asks for grain for {village}.",
            "objective": { "type": "talk_to", "hero": "{giver}" }, "on_complete": { "next": "bring_it" } },
          { "id": "bring_it", "journal": "Bring 30 grain to {giver}.",
            "objective": { "type": "bring", "item": "grain", "count": 30 },
            "choices": [ { "id": "give_up", "label": "Let the winter come", "objective": { "type": "wait", "days": 20 }, "next": "end:hungry" } ],
            "on_complete": { "next": "end:fed" } }
        ],
        "endings": {
          "fed": { "regard": 8, "memory": "The player brought grain to {village} before the snows.",
                   "consequences": [ { "type": "item", "item": "wine", "count": 5 } ] },
          "hungry": { "regard": -4, "memory": "No grain came." }
        }
      }
      """;

      private static readonly HashSet<string> Categories = new() {"wine", "iron", "grain"};
      private static readonly HashSet<string> Items = new() {"wine", "iron", "grain", "sumpter_horse"};

      private static string Key(string raw) => TaleItemPolicy.BringKey(raw, Categories.Contains, Items.Contains);

      // STAKE: "bring 30 grain", "bring horses", "bring wine": the author writes what a player would say, and every grain,
      // horse or wine the party carries counts, not one exact item the author would have to guess.
      [Test]
      public void GIVEN_goods_named_as_a_kind_a_category_or_an_item_WHEN_read_THEN_each_finds_the_key_the_bag_matches()
      {
         Key("Grain").Should().Be(ProvideGoodsPolicy.Grain);
         Key("horses").Should().Be(ProvideGoodsPolicy.Horses);
         Key("wine").Should().Be(ProvideGoodsPolicy.MarketKey("wine"));
         Key("sumpter_horse").Should().Be(ProvideGoodsPolicy.ItemKey("sumpter_horse"));
         ProvideGoodsPolicy.ItemIdOf(Key("sumpter_horse")).Should().Be("sumpter_horse");
      }

      // STAKE: a misspelt good ("wyne") or an item of a mod not loaded would ask the player for something nobody can bring.
      [Test]
      public void GIVEN_goods_the_game_does_not_know_WHEN_read_THEN_no_key_and_the_tale_is_reported_and_never_offered()
      {
         Key("wyne").Should().BeNull();

         AuthoredQuest q = AuthoredTales.Load(Grain.Replace("\"item\": \"grain\"", "\"item\": \"wyne\"").Replace("\"item\": \"wine\"", "\"item\": \"a_mods_sword\""));

         IReadOnlyList<string> unknown = TaleItemPolicy.UnknownItems(q, Categories.Contains, Items.Contains);
         unknown.Should().HaveCount(2);
         unknown.Should().Contain(p => p.Contains("wyne")).And.Contain(p => p.Contains("a_mods_sword"));
         TaleItemPolicy.UnknownItems(AuthoredTales.Load(Grain), Categories.Contains, Items.Contains).Should().BeEmpty();
      }

      // STAKE: an item given when the player loses to a tale's band is checked as an ending's is, or the tale would be offered
      // and promise, on a defeat, an item the game cannot give.
      [Test]
      public void GIVEN_an_unknown_item_given_on_a_defeat_WHEN_checked_THEN_it_is_reported_as_an_endings_would_be()
      {
         var q = new AuthoredQuest {Id = "cr_test_defeat", Stages = {
            new AuthoredStage {Id = "fight", Objective = new AuthoredObjective {Type = "defeat_party", Spawn = new AuthoredSpawn {
               Id = "band", OnDefeat = new AuthoredDefeat {Consequences = {new AuthoredConsequence {Type = "item", Item = "a_mods_sword"}}}}}}}};

         TaleItemPolicy.UnknownItems(q, Categories.Contains, Items.Contains).Should().ContainSingle(p => p.Contains("a_mods_sword"));
      }

      // STAKE: an ending that gives "a horse" would give whichever horse loads first, a different one on every install:
      // what is given is one item the file names.
      [Test]
      public void GIVEN_an_item_consequence_WHEN_read_THEN_only_an_exact_item_id_is_given()
      {
         TaleItemPolicy.GiftItemId(" wine ", Items.Contains).Should().Be("wine");
         TaleItemPolicy.GiftItemId("horses", Items.Contains).Should().BeNull();
      }

      // STAKE: gating before declaration: 20 grain handed over for 30 asked would take the party's food and the tale would
      // say the village was fed. Short, nothing is taken; enough, exactly the count leaves, never the surplus.
      [Test]
      public void GIVEN_a_hand_over_WHEN_short_or_with_more_than_asked_THEN_nothing_or_exactly_the_count_is_taken()
      {
         TaleItemPolicy.Taken(20, 30).Should().Be(0);
         TaleItemPolicy.Taken(30, 30).Should().Be(30);
         TaleItemPolicy.Taken(45, 30).Should().Be(30);
      }

      // STAKE: the grain is for the giver; the bag of anyone else must not offer to hand it over, and the giver's must.
      [Test]
      public void GIVEN_a_bring_with_no_receiver_named_WHEN_the_giver_or_someone_else_speaks_THEN_only_the_giver_is_handed_the_goods()
      {
         AuthoredQuest q = AuthoredTales.Load(Grain);
         AuthoredQuestRun run = AuthoredTales.At(q, "bring_it");

         AuthoredQuestEngine.DeliveryFor(run, q, AuthoredTales.Giver).Should().NotBeNull();
         AuthoredQuestEngine.DeliveryFor(run, q, "lord_someone_else").Should().BeNull();
         AuthoredQuestEngine.DeliveryFor(run, q, AuthoredTales.Giver).Objective.Count.Should().Be(30);
      }

      // STAKE: merely talking to the giver while carrying grain for the march must never take it: only the bag's
      // confirmed hand-over moves the tale.
      [Test]
      public void GIVEN_a_bring_stage_WHEN_the_player_opens_a_conversation_with_the_giver_THEN_the_tale_does_not_move()
      {
         AuthoredQuest q = AuthoredTales.Load(Grain);
         AuthoredQuestRun run = AuthoredTales.At(q, "bring_it");

         AuthoredQuestEngine.ConversationOpened(run, q, AuthoredTales.Giver, null, 101).Kind.Should().Be(AuthoredStepKind.None);
         run.StageId.Should().Be("bring_it");
      }

      // STAKE: the goods handed over end the stage the way the author wrote it, and the giver speaks from the ending.
      [Test]
      public void GIVEN_the_goods_handed_to_the_giver_WHEN_delivered_THEN_the_tale_reaches_the_ending_the_stage_leads_to()
      {
         AuthoredQuest q = AuthoredTales.Load(Grain);
         AuthoredQuestRun run = AuthoredTales.At(q, "bring_it");

         AuthoredStep step = AuthoredQuestEngine.Delivered(run, q, AuthoredTales.Giver, 101);

         step.EndingId.Should().Be("fed");
         run.Status.Should().Be(AuthoredQuestStatus.Completed);
         AuthoredQuestEngine.Delivered(run, q, AuthoredTales.Giver, 102).Kind.Should().Be(AuthoredStepKind.None);
      }

      // STAKE: "bring 30 grain to {giver}" where the grain is for someone else of the tale: the receiver named is the one.
      [Test]
      public void GIVEN_a_bring_to_another_role_WHEN_that_person_or_the_giver_speaks_THEN_only_the_named_receiver_takes_the_goods()
      {
         AuthoredQuest q = AuthoredTales.Load(Grain.Replace("\"item\": \"grain\", \"count\": 30", "\"item\": \"grain\", \"count\": 30, \"to\": \"{village}\""));
         AuthoredQuestRun run = AuthoredTales.At(q, "bring_it");

         AuthoredQuestEngine.DeliveryFor(run, q, AuthoredTales.Village).Should().NotBeNull();
         AuthoredQuestEngine.DeliveryFor(run, q, AuthoredTales.Giver).Should().BeNull();
      }

      // STAKE: a bring with no goods named or a count out of reach (0, or a herd of a thousand) is refused at load, where
      // the author sees it, not found out by a player who can never finish the tale.
      [Test]
      public void GIVEN_a_bring_or_an_item_with_no_goods_or_a_count_out_of_range_WHEN_loaded_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(Grain.Replace("\"item\": \"grain\", ", "")).Problems.Should().Contain(p => p.Contains("bring needs \"item\""));
         AuthoredTales.Read(Grain.Replace("\"count\": 30", "\"count\": 1000")).Problems.Should().Contain(p => p.Contains("bring needs a \"count\""));
         AuthoredTales.Read(Grain.Replace("\"count\": 5", "\"count\": 0")).Problems.Should().Contain(p => p.Contains("item needs a \"count\""));
         AuthoredTales.Read(Grain).Problems.Should().BeEmpty();
      }

      // STAKE: the bag settles what is owed first, as it always did: a tale's goods never hide a bargain or a lord's supply
      // task, and come before a plain gift or a prisoner when the tale asks them here.
      [Test]
      public void GIVEN_a_tale_asking_goods_here_WHEN_the_bag_routes_THEN_bargains_and_supply_tasks_still_come_first()
      {
         OfferFlowGate.WithTale(OfferRoute.ItemBargain, true).Should().Be(OfferRoute.ItemBargain);
         OfferFlowGate.WithTale(OfferRoute.SupplyQuest, true).Should().Be(OfferRoute.SupplyQuest);
         OfferFlowGate.WithTale(OfferRoute.InventoryDirect, true).Should().Be(OfferRoute.TaleDelivery);
         OfferFlowGate.WithTale(OfferRoute.CategoryChoice, true).Should().Be(OfferRoute.TaleDelivery);
         OfferFlowGate.WithTale(OfferRoute.InventoryDirect, false).Should().Be(OfferRoute.InventoryDirect);
      }
   }
}
