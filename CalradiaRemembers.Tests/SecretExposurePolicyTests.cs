// Code written by Gabriel Mailhot, 01/11/2026.
// SecretExposurePolicy and the ledger's exposures decide who can be told a secret, what telling it costs, how the scandal reads,
// and whether coin taken for silence was betrayed (Gabriel's "go", 01/11/2026). If they are wrong, a secret can be spent twice, a
// paid silence can be broken for free, or a rumour hides who told it, and the world then blames nobody for the player's deed.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Extortion;
using CalradiaRemembers.Logic.Trauma;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class SecretExposurePolicyTests
   {
      private static ExtortionRecord Paid(string victim, ExtortionLever lever, int day)
         => new() {VictimId = victim, ClanId = "clan", Lever = lever, Day = day, Amount = 500};

      [Test]
      public void GIVEN_the_two_lovers_WHEN_asked_who_may_hear_THEN_neither_can_be_told()
      {
         // STAKE: telling someone their own affair is no exposure: it would spend the secret on the only two who already know it.
         SecretExposurePolicy.MayHear("ira", "ira", "aldric").Should().BeFalse();
         SecretExposurePolicy.MayHear("aldric", "ira", "aldric").Should().BeFalse();
      }

      [Test]
      public void GIVEN_anyone_else_WHEN_asked_who_may_hear_THEN_they_can_be_told()
      {
         // STAKE: the betrayed spouse is the listener a player most wants to reach.
         SecretExposurePolicy.MayHear("orthes", "ira", "aldric").Should().BeTrue();
      }

      [Test]
      public void GIVEN_someone_who_paid_for_silence_WHEN_the_secret_is_told_THEN_it_costs_more()
      {
         // STAKE: without the dearer price, taking the coin and telling anyway would be the best move on the board.
         ExposureCost plain = SecretExposurePolicy.Cost(false);
         ExposureCost broken = SecretExposurePolicy.Cost(true);

         plain.RelationHit.Should().Be(DeedRegardPolicy.SecretExposedVictim);
         broken.RelationHit.Should().BeLessThan(plain.RelationHit);
         broken.GrudgeMagnitude.Should().BeGreaterThan(plain.GrudgeMagnitude);
      }

      [Test]
      public void GIVEN_another_affair_WHEN_the_rumour_is_written_THEN_it_names_both_lovers_and_the_teller()
      {
         // STAKE: a character can only say a name the summary holds, and the teller's name is what makes this the player's misdeed.
         string rumour = SecretExposurePolicy.Rumour(new ExposureNames {Subject = "Ira", Lover = "Aldric", Teller = "Gabriel"});

         rumour.Should().Contain("Ira").And.Contain("Aldric").And.Contain("Gabriel");
      }

      [Test]
      public void GIVEN_the_players_own_affair_WHEN_the_rumour_is_written_THEN_it_says_they_told_it_themselves()
      {
         // STAKE: Gabriel's go on mutual ruin: the player's own name is in the scandal, as the lover and as the one who spoke.
         string rumour = SecretExposurePolicy.Rumour(new ExposureNames {Subject = "Ira", Lover = "Gabriel", Teller = "Gabriel", TellerIsLover = true});

         rumour.Should().Contain("lying with Gabriel").And.Contain("said so aloud");
      }

      [Test]
      public void GIVEN_a_told_secret_WHEN_the_ledger_is_asked_THEN_it_is_spent_whichever_way_it_is_read()
      {
         // STAKE: a secret told once is no secret: it cannot be sold, or told a second time for a second scandal.
         var ledger = new ExtortionLedger();
         ledger.Exposures.Add(new ExposureRecord {SubjectId = "ira", LoverId = "aldric", ListenerId = "orthes", Day = 10});

         ledger.IsExposed("ira", "aldric").Should().BeTrue();
         ledger.IsExposed("ira", "orthes").Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_payment_for_a_secret_WHEN_the_bargain_is_read_inside_the_month_THEN_it_holds()
      {
         // STAKE: the same thirty days as a bargain for lands: the player's word has one length.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(Paid("ira", ExtortionLever.Secret, 100));

         ledger.SecretBargainWith("ira", 100 + ExtortionPolicy.DaysOfTheBargain).Should().NotBeNull();
         ledger.SecretBargainWith("ira", 101 + ExtortionPolicy.DaysOfTheBargain).Should().BeNull();
      }

      [Test]
      public void GIVEN_coin_paid_for_lands_or_a_refusal_WHEN_a_secret_bargain_is_read_THEN_there_is_none()
      {
         // STAKE: nobody bought silence here, so telling the secret must not write a broken promise on the page.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(Paid("ira", ExtortionLever.Lands, 100));
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "ira", Lever = ExtortionLever.Secret, Day = 100, Amount = 0});

         ledger.SecretBargainWith("ira", 100).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_ledger_with_exposures_WHEN_saved_and_read_THEN_they_survive()
      {
         // STAKE: a reload must not hand a spent secret back.
         var ledger = new ExtortionLedger();
         ledger.Exposures.Add(new ExposureRecord {SubjectId = "ira", LoverId = "aldric", ListenerId = "orthes", Day = 10});

         ExtortionLedger.Deserialize(ledger.Serialize()).IsExposed("ira", "aldric").Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_ledger_saved_before_exposures_WHEN_read_THEN_it_has_none()
      {
         // STAKE: the save-migration rule: extortions saved by an earlier build of this version must load.
         ExtortionLedger.Deserialize("{\"Attempts\":[]}").Exposures.Should().BeEmpty();
         ExtortionLedger.Deserialize("{\"Attempts\":[],\"Exposures\":null}").Exposures.Should().BeEmpty();
      }

      [Test]
      public void GIVEN_a_told_secret_WHEN_the_page_tells_it_THEN_the_two_deeds_read_differently()
      {
         // STAKE: telling a secret and betraying the one who paid for silence are two different stains.
         var told = new MisdeedFacts {Kind = MisdeedKind.ExposedSecret};
         var broke = new MisdeedFacts {Kind = MisdeedKind.BrokeSecretBargain};

         MisdeedsPagePolicy.Line(told, "Ira", true, d => "").Should().Contain("Ira's hidden affair");
         MisdeedsPagePolicy.Line(broke, "Ira", true, d => "").Should().Contain("coin for your silence");
      }
   }
}
