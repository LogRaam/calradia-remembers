// Code written by Gabriel Mailhot, 26/09/2026.
// Documents fkasad's grid for a mercenary's departure (Nexus, 25/09/2026; ratified by Gabriel). The stakes: a company
// that leaves a realm at peace must never be treated as a traitor (the report that started this: Zivkyn was told he
// broke an oath he never swore), while one that walks out of a losing war, or straight over to the enemy, must be.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MercenaryDeparturePolicyTests
   {
      // fkasad: employer at peace, no penalty. That is how a mercenary works.
      [Test]
      public void GIVEN_a_realm_at_peace_WHEN_the_contract_ends_THEN_there_is_no_fault()
      {
         MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = false}).Should().Be(MercenaryDepartureVerdict.AtPeace);
         MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.AtPeace, sameCulture: true).Should().Be(0);
      }

      // fkasad: at war and winning, "you're not needed anymore, thanks for your help".
      [Test]
      public void GIVEN_a_realm_winning_its_war_WHEN_the_contract_ends_THEN_the_company_leaves_with_thanks()
      {
         MercenaryDepartureVerdict v = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = true, EmployerStrength = 1000, EnemiesStrength = 800});

         v.Should().Be(MercenaryDepartureVerdict.ReleasedInVictory);
         MercenaryDeparturePolicy.RegardShift(v, false).Should().BeGreaterThan(0);
      }

      // fkasad: at war and losing, "you're a coward saving his coffers".
      [Test]
      public void GIVEN_a_realm_losing_its_war_WHEN_the_contract_ends_THEN_the_company_abandoned_it()
      {
         MercenaryDepartureVerdict v = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = true, EmployerStrength = 500, EnemiesStrength = 1000});

         v.Should().Be(MercenaryDepartureVerdict.Abandoned);
         MercenaryDeparturePolicy.RegardShift(v, false).Should().BeLessThan(0);
      }

      // A war on a near-even footing is not a losing one.
      [Test]
      public void GIVEN_a_near_even_war_WHEN_judged_THEN_the_realm_is_not_losing()
      {
         MercenaryDeparturePolicy.IsLosing(ownStrength: 950, enemiesStrength: 1000).Should().BeFalse();
         MercenaryDeparturePolicy.IsLosing(ownStrength: 850, enemiesStrength: 1000).Should().BeTrue();
      }

      // fkasad: joining the enemy is worse than leaving; a two-step event, judged when the next contract is signed.
      [Test]
      public void GIVEN_a_company_that_joins_the_former_employers_enemy_soon_after_WHEN_judged_THEN_it_is_a_betrayal()
      {
         MercenaryDeparturePolicy.IsBetrayal(departedDay: 100, today: 110, newRealmAtWarWithFormer: true).Should().BeTrue();
         MercenaryDeparturePolicy.IsBetrayal(100, 100 + MercenaryDeparturePolicy.BetrayalWindowDays + 1, true).Should().BeFalse();
         MercenaryDeparturePolicy.IsBetrayal(100, 110, newRealmAtWarWithFormer: false).Should().BeFalse();
      }

      // The upgrade costs only the difference: leaving a losing war and then going over is not charged twice.
      [Test]
      public void GIVEN_an_abandonment_upgraded_to_betrayal_WHEN_charged_THEN_only_the_difference_is_added()
      {
         int upgrade = MercenaryDeparturePolicy.BetrayalUpgradeShift(MercenaryDepartureVerdict.Abandoned, false);

         (MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Abandoned, false) + upgrade)
            .Should().Be(MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Betrayal, false));
      }

      // fkasad: "same culture = penalty x2, a mercenary shouldn't leave family". Thanks are never doubled.
      [Test]
      public void GIVEN_a_company_of_the_realms_own_people_WHEN_it_abandons_or_betrays_THEN_the_penalty_doubles()
      {
         MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Abandoned, true)
                                 .Should().Be(2 * MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Abandoned, false));
         MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.ReleasedInVictory, true)
                                 .Should().Be(MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.ReleasedInVictory, false));
      }

      // fkasad: a company of one's own people come to a losing realm's aid is remembered well.
      [Test]
      public void GIVEN_a_company_joining_its_own_people_in_a_losing_war_WHEN_judged_THEN_it_is_aid_of_kin()
      {
         MercenaryDeparturePolicy.IsAidOfKin(sameCulture: true, realmLosing: true).Should().BeTrue();
         MercenaryDeparturePolicy.IsAidOfKin(sameCulture: true, realmLosing: false).Should().BeFalse();
         MercenaryDeparturePolicy.IsAidOfKin(sameCulture: false, realmLosing: true).Should().BeFalse();
      }

      // Zivkyn's report: a contract that ends in peace says so, never "oath" or "fealty".
      [Test]
      public void GIVEN_a_peaceful_departure_WHEN_worded_THEN_it_is_a_contract_that_ended()
      {
         string summary = MercenaryDeparturePolicy.Summary(MercenaryDepartureVerdict.AtPeace, "Brennoc", "Vlandia");

         summary.Should().Be("Brennoc's mercenary contract with Vlandia came to an end.");
         summary.Should().NotContain("oath").And.NotContain("fealty");
         MercenaryDeparturePolicy.LordMemory(MercenaryDepartureVerdict.AtPeace, "Brennoc").Should().BeNull();
      }
   }
}
