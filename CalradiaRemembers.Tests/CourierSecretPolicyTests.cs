// Code written by Gabriel Mailhot, 26/09/2026.
// Documents what a lost courier becomes. raphareish (Nexus, 24/09/2026): an intercepted letter simply died, and the
// sender, asked about it, said he had "been busy", because nothing of the letter was left anywhere. Gabriel's rulings
// the same day: a lost letter is sent again until it arrives, and an INTERCEPTED one with something to say becomes a
// secret that travels in the world. The band that took it carries it; whoever defeats that band sends it up to the
// lord who owns the troop (a Pravend patrol, Pravend's owner); a band that goes home brings it to its hideout; and a
// secret is only worth something for a while. These tests pin each of those decisions.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CourierSecretPolicyTests
   {
      private const string Substantive = "I would see your brother removed from the council before the spring muster.";

      // ── What a loss is ─────────────────────────────────────────────────────

      // Bandits who take a courier have taken its letter: an interception.
      [Test]
      public void GIVEN_bandits_destroy_a_courier_WHEN_classified_THEN_the_letter_was_intercepted_by_bandits()
      {
         CourierSecretPolicy.Classify(new CourierLossFacts {DestroyerIsBandit = true}).Should().Be(CourierLoss.InterceptedByBandits);
      }

      // Gabriel: a troop at war with the courier's own clan intercepts it too, and its lord reads it.
      [Test]
      public void GIVEN_a_troop_at_war_with_the_couriers_clan_destroys_it_WHEN_classified_THEN_the_enemy_intercepted_it()
      {
         CourierSecretPolicy.Classify(new CourierLossFacts {DestroyerAtWarWithCourierClan = true}).Should().Be(CourierLoss.InterceptedByEnemy);
      }

      // Starvation, a battle it wandered into, a despawn: the letter is gone, and nobody read it.
      [Test]
      public void GIVEN_anything_else_destroys_a_courier_WHEN_classified_THEN_the_letter_is_merely_lost()
      {
         CourierSecretPolicy.Classify(new CourierLossFacts()).Should().Be(CourierLoss.Lost);
         CourierSecretPolicy.Classify(null).Should().Be(CourierLoss.Lost);
      }

      // Gabriel: the letter is sent again until it arrives, whatever took the first courier, so no dialogue breaks.
      [TestCase(CourierLoss.Lost)]
      [TestCase(CourierLoss.InterceptedByBandits)]
      [TestCase(CourierLoss.InterceptedByEnemy)]
      public void GIVEN_any_loss_WHEN_decided_THEN_the_letter_is_sent_again(CourierLoss loss)
      {
         CourierSecretPolicy.IsResent(loss).Should().BeTrue();
      }

      // ── What becomes a secret ──────────────────────────────────────────────

      // Gabriel: an intercepted letter with substance ALWAYS becomes a secret; no roll, no toggle.
      [TestCase(CourierLoss.InterceptedByBandits)]
      [TestCase(CourierLoss.InterceptedByEnemy)]
      public void GIVEN_an_intercepted_letter_with_substance_WHEN_decided_THEN_it_becomes_a_secret(CourierLoss loss)
      {
         CourierSecretPolicy.BecomesSecret(loss, Substantive).Should().BeTrue();
      }

      // Gabriel: substance required. "Well met, I hope you are well" is nobody's secret.
      [Test]
      public void GIVEN_an_intercepted_letter_with_nothing_in_it_WHEN_decided_THEN_it_is_no_secret()
      {
         CourierSecretPolicy.BecomesSecret(CourierLoss.InterceptedByBandits, "Greetings, friend.").Should().BeFalse();
      }

      // A letter nobody took was read by nobody.
      [Test]
      public void GIVEN_a_letter_merely_lost_WHEN_decided_THEN_it_is_no_secret()
      {
         CourierSecretPolicy.BecomesSecret(CourierLoss.Lost, Substantive).Should().BeFalse();
      }

      // ── Where a carried secret goes when its carrier is beaten ─────────────

      // The report's picture: a lord's own party beats the band, and the lord reads the letter.
      [Test]
      public void GIVEN_a_lords_party_defeats_the_band_WHEN_the_secret_changes_hands_THEN_that_lord_learns_it()
      {
         SecretHandoff h = CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {LeaderHeroId = "lord_derthert"});

         h.Kind.Should().Be(SecretHandoffKind.Lord);
         h.HeroId.Should().Be("lord_derthert");
      }

      // Gabriel's example: a Pravend patrol, no lord at its head, sends it up to Pravend's owner.
      [Test]
      public void GIVEN_a_town_patrol_defeats_the_band_WHEN_the_secret_changes_hands_THEN_the_towns_owner_learns_it()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {SettlementOwnerHeroId = "lord_pravend"})
                            .HeroId.Should().Be("lord_pravend");
      }

      // A caravan answers to its owner.
      [Test]
      public void GIVEN_a_caravan_defeats_the_band_WHEN_the_secret_changes_hands_THEN_its_owner_learns_it()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {OwnerHeroId = "merchant_lord"})
                            .HeroId.Should().Be("merchant_lord");
      }

      // A lord at the head of a party is the one who reads it, even when the party belongs to another's holding.
      [Test]
      public void GIVEN_a_party_with_both_a_leader_and_an_owner_WHEN_the_secret_changes_hands_THEN_the_leader_reads_it()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {LeaderHeroId = "leader", OwnerHeroId = "owner", SettlementOwnerHeroId = "town"})
                            .HeroId.Should().Be("leader");
      }

      // One band beats another: the letter is loot like any other, and the winners carry it on.
      [Test]
      public void GIVEN_another_band_defeats_the_carrier_WHEN_the_secret_changes_hands_THEN_the_winners_carry_it_on()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {IsBandit = true}).Kind.Should().Be(SecretHandoffKind.CarriedOn);
      }

      // The player hunts the band down (his own party or his clan's): the letter is recovered, and nobody reads it.
      [Test]
      public void GIVEN_the_players_side_defeats_the_band_WHEN_the_secret_changes_hands_THEN_the_letter_is_recovered()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {IsPlayerSide = true, LeaderHeroId = "a_companion"})
                            .Kind.Should().Be(SecretHandoffKind.RecoveredByPlayer);
      }

      // The lord the letter was written TO beats the band: he reads what was meant for him. A delivery, not a leak.
      [Test]
      public void GIVEN_the_intended_recipient_ends_up_with_it_WHEN_the_secret_changes_hands_THEN_it_is_a_delivery_not_a_leak()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {LeaderHeroId = "lord_x", IntendedRecipientId = "lord_x"})
                            .Kind.Should().Be(SecretHandoffKind.ReachedItsRecipient);
      }

      // The sender's own men recover his own letter: nothing he did not already know.
      [Test]
      public void GIVEN_the_sender_ends_up_with_his_own_letter_WHEN_the_secret_changes_hands_THEN_it_goes_nowhere()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts {LeaderHeroId = "lord_s", SenderId = "lord_s"})
                            .Kind.Should().Be(SecretHandoffKind.Nobody);
      }

      // A troop with no lord behind it at all: the secret is lost with the band.
      [Test]
      public void GIVEN_no_one_answers_for_the_defeating_troop_WHEN_the_secret_changes_hands_THEN_it_goes_nowhere()
      {
         CourierSecretPolicy.WhoGetsTheSecret(new SecretDefeaterFacts()).Kind.Should().Be(SecretHandoffKind.Nobody);
         CourierSecretPolicy.WhoGetsTheSecret(null).Kind.Should().Be(SecretHandoffKind.Nobody);
      }

      // ── How long a secret matters ──────────────────────────────────────────

      // Gabriel: limited in time. Within its shelf life a secret still has weight...
      [Test]
      public void GIVEN_a_fresh_secret_WHEN_checked_THEN_it_still_matters()
      {
         CourierSecretPolicy.IsStale(capturedDay: 100, today: 100 + CourierSecretPolicy.SecretShelfLifeDays - 1).Should().BeFalse();
      }

      // ...and past it, nobody cares any more what a year-old letter said.
      [Test]
      public void GIVEN_a_secret_past_its_shelf_life_WHEN_checked_THEN_it_no_longer_matters()
      {
         CourierSecretPolicy.IsStale(capturedDay: 100, today: 100 + CourierSecretPolicy.SecretShelfLifeDays).Should().BeTrue();
      }
   }
}
