// Code written by Gabriel Mailhot, 19/09/2026.
// What these pin: that the lord who asked for a prisoner, and receives one, has had his bargain kept.
//
// Lordfadooboo (Nexus, 18/09/2026): a Khuzait lord asked him to deliver a Nord lord in chains. He brought one,
// typed out the handover, "the character spoke as if i completed the quest, i even got paid, but the quest still
// remained on my board, unable to be completed."
//
// Two verbs hand a captive to an NPC. The delivery closes the bargain; the SALE pays a price and touches no
// quest. Which one fired depended on a word the model chose, and the only prisoner option in the offer bag was
// worded as a sale. These pin the one question every path now asks: does handing THIS captive to THIS person
// settle something he asked for.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DeliveryQuestMatcherTests
   {
      private const string Nords = "kingdom_nord";
      private const string Vlandia = "kingdom_vlandia";

      // THE REPORTED CASE: "bring me any Nord lord", and a Nord lord is handed over. That settles it, however the
      // handover was phrased.
      [Test]
      public void GIVEN_a_bargain_for_any_lord_of_a_realm_WHEN_one_of_its_lords_is_handed_over_THEN_it_is_settled()
      {
         InformalQuest bargain = RealmBargain(Nords);

         DeliveryQuestMatcher.QuestSettledBy(new[] {bargain}, Captive("jarl", Nords)).Should().BeSameAs(bargain);
      }

      // A lord of the WRONG realm settles nothing: the Khuzait asked for a Nord, not for whoever was to hand.
      [Test]
      public void GIVEN_a_bargain_for_one_realm_WHEN_a_lord_of_another_is_handed_over_THEN_nothing_is_settled()
      {
         DeliveryQuestMatcher.QuestSettledBy(new[] {RealmBargain(Nords)}, Captive("knight", Vlandia)).Should().BeNull();
      }

      // A bargain that NAMED a man is kept only by that man, whatever his realm.
      [Test]
      public void GIVEN_a_bargain_naming_one_lord_WHEN_a_different_lord_of_his_realm_is_handed_over_THEN_nothing_is_settled()
      {
         InformalQuest bargain = NamedBargain("jarl_ulfr", Nords);

         DeliveryQuestMatcher.QuestSettledBy(new[] {bargain}, Captive("jarl_sigurd", Nords)).Should().BeNull();
         DeliveryQuestMatcher.QuestSettledBy(new[] {bargain}, Captive("jarl_ulfr", Nords)).Should().BeSameAs(bargain);
      }

      // A bargain already kept is not kept twice: a second captive handed to the same lord is a gift or a sale,
      // not the delivery he has already been paid for.
      [Test]
      public void GIVEN_a_bargain_already_settled_WHEN_another_captive_is_handed_over_THEN_it_is_not_settled_again()
      {
         InformalQuest done = RealmBargain(Nords);
         done.Status = QuestStatus.Completed;

         DeliveryQuestMatcher.QuestSettledBy(new[] {done}, Captive("jarl", Nords)).Should().BeNull();
      }

      // Only DELIVERY bargains count. A lord who wants the Nord KILLED in battle has not asked for him in chains,
      // and handing him over does not answer that.
      [Test]
      public void GIVEN_an_outstanding_bargain_of_another_kind_WHEN_a_matching_captive_is_handed_over_THEN_nothing_is_settled()
      {
         var other = new InformalQuest {Type = QuestType.AttackLord, TargetFactionId = Nords, Status = QuestStatus.Active};

         DeliveryQuestMatcher.QuestSettledBy(new[] {other}, Captive("jarl", Nords)).Should().BeNull();
      }

      // THE PLAYER'S CHOICE WINS. With a realm-keyed bargain and two of that realm's lords in the cells, "the one
      // the player named" and "the first one found" are different men, and handing over the wrong one is its own
      // small betrayal of whoever the player meant to keep.
      [Test]
      public void GIVEN_two_captives_who_would_both_settle_it_WHEN_the_player_named_one_THEN_that_one_is_delivered()
      {
         DeliveryMatch match = DeliveryQuestMatcher.FirstDelivery(
            new[] {RealmBargain(Nords)},
            new[] {Captive("jarl_a", Nords), Captive("jarl_b", Nords)},
            "jarl_b");

         match.CaptiveId.Should().Be("jarl_b");
      }

      // And when the player named nobody, or named someone who settles nothing, a delivery is still found.
      [Test]
      public void GIVEN_no_usable_preference_WHEN_a_delivery_is_sought_THEN_the_first_captive_who_settles_it_is_chosen()
      {
         DeliveryMatch match = DeliveryQuestMatcher.FirstDelivery(
            new[] {RealmBargain(Nords)},
            new[] {Captive("knight", Vlandia), Captive("jarl", Nords)},
            "knight");

         match.CaptiveId.Should().Be("jarl");
      }

      // Nothing to deliver: the verb must be told plainly, not handed a captive who settles nothing.
      [Test]
      public void GIVEN_no_captive_settles_anything_WHEN_a_delivery_is_sought_THEN_there_is_none()
      {
         DeliveryQuestMatcher.FirstDelivery(new[] {RealmBargain(Nords)}, new[] {Captive("knight", Vlandia)})
                             .Should().BeNull();
         DeliveryQuestMatcher.FirstDelivery(null, new[] {Captive("jarl", Nords)}).Should().BeNull();
      }

      #region private

      private static HeldCaptive Captive(string id, string faction) => new() {Id = id, FactionId = faction};

      private static InformalQuest RealmBargain(string faction)
         => new() {Type = QuestType.DeliverPrisoner, TargetFactionId = faction, Status = QuestStatus.Active};

      private static InformalQuest NamedBargain(string heroId, string faction)
         => new() {Type = QuestType.DeliverPrisoner, TargetHeroId = heroId, TargetFactionId = faction, Status = QuestStatus.Active};

      #endregion
   }
}
