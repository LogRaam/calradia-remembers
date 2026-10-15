// Code written by Gabriel Mailhot, 15/10/2026.
// What WarServiceLedger remembers for the vassal war duty (Maifailun, Gabriel, 15/10/2026): since when the player has
// been sworn to his realm, since when the realm has been at war, when he last served it, and whether this stretch of
// war already had its reckoning. The game keeps none of it. If it is wrong, a vassal is summoned the day he loads an
// old save, the day he swears, twice in one war, or never at all.

#region

using CalradiaRemembers.Logic.WarDuty;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WarServiceLedgerTests
   {
      private static readonly WarDutyLive Vassal = new WarDutyLive {IsSwornVassal = true, WindowDays = 30};

      // STAKE: save-migration rule. An old save loaded in the middle of a long war must not summon anyone on day one:
      // the ledger counts the war and the oath from the day it first sees them.
      [Test]
      public void GIVEN_an_old_save_loaded_mid_war_WHEN_the_first_day_is_judged_THEN_no_one_owes_yet()
      {
         WarServiceLedger ledger = WarServiceLedger.Deserialize(null);
         ledger.NoteSworn("empire", 500);
         ledger.NoteWars(new[] {"battania"}, 500);

         WarDutyPolicy.Decide(ledger.Facts(500, Vassal)).Should().NotBe(WarDutyStanding.Owes);
      }

      // STAKE: Maifailun's case, end to end in the ledger: sworn, a war runs a month and more, he never served.
      [Test]
      public void GIVEN_a_month_of_war_without_service_WHEN_judged_THEN_he_owes()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);

         WarDutyPolicy.Decide(ledger.Facts(140, Vassal)).Should().Be(WarDutyStanding.Owes);
      }

      // STAKE: a battle fought for the realm is service; he must not be summoned the week after it.
      [Test]
      public void GIVEN_a_battle_fought_for_the_realm_last_week_WHEN_judged_THEN_he_has_served()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);
         ledger.NoteServed(133);

         WarDutyPolicy.Decide(ledger.Facts(140, Vassal)).Should().Be(WarDutyStanding.Served);
      }

      // STAKE: his duty is to the realm he swore to, from the day he swore; a lord who changes realms starts afresh.
      [Test]
      public void GIVEN_a_vassal_who_swore_to_a_new_realm_WHEN_judged_THEN_his_count_starts_again()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);
         ledger.NoteSworn("vlandia", 200);
         ledger.NoteWars(new[] {"sturgia"}, 200);

         WarDutyPolicy.Decide(ledger.Facts(210, Vassal)).Should().Be(WarDutyStanding.WarTooYoung);
      }

      // STAKE: one reckoning per stretch of war, so he is not summoned again for the same war once it opened.
      [Test]
      public void GIVEN_a_reckoning_opened_in_this_war_WHEN_judged_later_in_it_THEN_he_is_not_summoned_again()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);
         ledger.OpenCase("lucon", 3000, 140);
         ledger.Case.Stage = WarDutyStage.Settled;

         WarDutyPolicy.Decide(ledger.Facts(200, Vassal)).Should().Be(WarDutyStanding.AlreadyCalledThisWar);
      }

      // STAKE: once peace has come and a new war begins, the new war may call him to account again.
      [Test]
      public void GIVEN_peace_then_a_new_war_WHEN_the_new_war_runs_a_month_unserved_THEN_he_owes_again()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);
         ledger.OpenCase("lucon", 3000, 140);
         ledger.Case.Stage = WarDutyStage.Settled;
         ledger.NoteWars(new string[0], 160);
         ledger.NoteWars(new[] {"aserai"}, 170);

         WarDutyPolicy.Decide(ledger.Facts(205, Vassal)).Should().Be(WarDutyStanding.Owes);
      }

      // STAKE: what the ledger remembers must survive a save and a load, the reckoning in progress included.
      [Test]
      public void GIVEN_a_ledger_with_a_reckoning_WHEN_saved_and_loaded_THEN_it_reads_the_same()
      {
         var ledger = new WarServiceLedger();
         ledger.NoteSworn("empire", 100);
         ledger.NoteWars(new[] {"battania"}, 100);
         ledger.NoteServed(110);
         ledger.OpenCase("lucon", 3000, 150);

         WarServiceLedger back = WarServiceLedger.Deserialize(ledger.Serialize());

         back.SwornKingdomId.Should().Be("empire");
         back.WarStretchSince.Should().Be(100);
         back.LastServedDay.Should().Be(110);
         back.Case.RulerId.Should().Be("lucon");
         back.Case.LevyAmount.Should().Be(3000);
         back.Case.IsOpen.Should().BeTrue();
      }

      // STAKE: a corrupt blob must never brick a load; it reads as an empty ledger.
      [Test]
      public void GIVEN_a_corrupt_saved_ledger_WHEN_loaded_THEN_an_empty_ledger_is_read()
      {
         WarServiceLedger.Deserialize("{not json").Case.Should().BeNull();
      }
   }
}
