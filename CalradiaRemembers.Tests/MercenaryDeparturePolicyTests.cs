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
   
      // ── The game's own companies (Zivkyn, Nexus: "why does nobody resent the Skolderbrotva?") ─────────────────
      //
      // The grid judged only the PLAYER's company. The game's mercenary clans change sides in the middle of a war, and
      // no lord of the realm they left ever held it against them. The same grid now judges them, but word of it goes
      // about only when there is something to hold: companies change contracts often, and a peaceful end or a
      // departure with the war won is not news.

      [TestCase(MercenaryDepartureVerdict.Abandoned, true)]
      [TestCase(MercenaryDepartureVerdict.Betrayal, true)]
      [TestCase(MercenaryDepartureVerdict.AtPeace, false)]
      [TestCase(MercenaryDepartureVerdict.ReleasedInVictory, false)]
      public void GIVEN_a_company_departure_WHEN_asked_whether_it_is_news_THEN_only_abandonment_and_betrayal_are(
         MercenaryDepartureVerdict verdict, bool news)
      {
         MercenaryDeparturePolicy.CompanyDepartureIsNews(verdict).Should().Be(news);
      }

      // Zivkyn's case, in the words the realm's lords will hear.
      [Test]
      public void GIVEN_a_company_that_went_over_to_the_enemy_WHEN_worded_THEN_it_names_the_company_the_realm_and_the_enemy()
      {
         MercenaryDeparturePolicy.CompanySummary(MercenaryDepartureVerdict.Betrayal, "Skolderbrotva", "Vlandia", "Sturgia")
                                 .Should().Be("The Skolderbrotva left Vlandia's service in the middle of the war and went over to its enemy, Sturgia.");
         MercenaryDeparturePolicy.CompanySummary(MercenaryDepartureVerdict.Abandoned, "Skolderbrotva", "Vlandia")
                                 .Should().Be("The Skolderbrotva abandoned Vlandia in the middle of a losing war, and kept the pay.");
      }

      // A company whose name already carries its article is not given a second one.
      [Test]
      public void GIVEN_a_company_named_with_its_article_WHEN_worded_THEN_the_article_is_not_doubled()
      {
         MercenaryDeparturePolicy.CompanySummary(MercenaryDepartureVerdict.Abandoned, "The Brotherhood of the Woods", "Battania")
                                 .Should().StartWith("The Brotherhood of the Woods abandoned");
      }

      // ── A vassal with no fief (fkasad, Nexus, 24/09/2026) ──────────────────────────────────────────────────────
      //
      // "Should also apply when leaving as vassal with no fief. (The departure of the faction with its fief is final
      // and amounts to a declaration of war; therefore, no penalty is necessary.)" A vassal who leaves with fiefs takes
      // them to war against the realm, and that war is the consequence. One who leaves with nothing walks out as a
      // mercenary would, but had sworn an oath: the same grid, with the oath counting as much as kinship does.

      // fkasad's line: the grid judges only the vassal who holds no fief.
      [Test]
      public void GIVEN_a_vassal_leaving_WHEN_asked_whether_the_grid_judges_it_THEN_only_one_holding_no_fief_is_judged()
      {
         MercenaryDeparturePolicy.JudgesAVassalsDeparture(fiefsHeld: 0).Should().BeTrue();
         MercenaryDeparturePolicy.JudgesAVassalsDeparture(fiefsHeld: 1).Should().BeFalse();
      }

      // The oath weighs like kinship: a sworn vassal who abandons a losing war costs twice a hired company, and a
      // sworn vassal of the realm's own people three times. Never more: -60 is already near the bottom of the ledger.
      [TestCase(MercenaryDepartureVerdict.Abandoned, false, -16)]
      [TestCase(MercenaryDepartureVerdict.Abandoned, true, -24)]
      [TestCase(MercenaryDepartureVerdict.Betrayal, false, -40)]
      [TestCase(MercenaryDepartureVerdict.Betrayal, true, -60)]
      public void GIVEN_a_sworn_vassal_WHEN_the_departure_is_charged_THEN_the_oath_weighs_like_kinship(
         MercenaryDepartureVerdict verdict, bool sameCulture, int expected)
      {
         MercenaryDeparturePolicy.RegardShift(verdict, sameCulture, sworn: true).Should().Be(expected);
      }

      // A company let go with the war won is thanked; a vassal who renounces an oath while the war goes well is not.
      // No fault is charged (fkasad: winning, no penalty), but no thanks either.
      [Test]
      public void GIVEN_a_sworn_vassal_leaving_a_winning_war_WHEN_charged_THEN_there_is_neither_fault_nor_thanks()
      {
         MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.ReleasedInVictory, false, sworn: true).Should().Be(0);
         MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.AtPeace, true, sworn: true).Should().Be(0);
      }

      // Going over to the enemy after renouncing the oath is charged only the difference, as for a company.
      [Test]
      public void GIVEN_a_sworn_vassals_abandonment_upgraded_to_betrayal_WHEN_charged_THEN_only_the_difference_is_added()
      {
         int upgrade = MercenaryDeparturePolicy.BetrayalUpgradeShift(MercenaryDepartureVerdict.Abandoned, true, sworn: true);

         (MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Abandoned, true, sworn: true) + upgrade)
            .Should().Be(MercenaryDeparturePolicy.RegardShift(MercenaryDepartureVerdict.Betrayal, true, sworn: true));
      }

      // A vassal swore fealty: the words the world hears say an oath was renounced, never a contract ended (the very
      // confusion Zivkyn reported, the other way round).
      [Test]
      public void GIVEN_a_sworn_vassal_WHEN_the_departure_is_worded_THEN_it_speaks_of_the_oath_not_of_a_company()
      {
         MercenaryDeparturePolicy.VassalSummary(MercenaryDepartureVerdict.Abandoned, "Arwa", "Vlandia")
                                 .Should().Be("Arwa renounced fealty to Vlandia in the middle of a losing war.");
         MercenaryDeparturePolicy.VassalSummary(MercenaryDepartureVerdict.Betrayal, "Arwa", "Vlandia", "Sturgia")
                                 .Should().Be("Arwa renounced fealty to Vlandia in the middle of the war and went over to its enemy, Sturgia.");
         MercenaryDeparturePolicy.VassalLordMemory(MercenaryDepartureVerdict.Abandoned, "Arwa")
                                 .Should().Contain("swore to us").And.NotContain("company");
      }

      // Word of a sworn vassal's departure carries at least as far as the plain "left the realm" news it replaces.
      [TestCase(MercenaryDepartureVerdict.ReleasedInVictory)]
      [TestCase(MercenaryDepartureVerdict.Abandoned)]
      [TestCase(MercenaryDepartureVerdict.Betrayal)]
      public void GIVEN_a_sworn_vassals_departure_WHEN_its_reach_is_asked_THEN_it_is_never_less_than_a_plain_departure(MercenaryDepartureVerdict verdict)
      {
         MercenaryDeparturePolicy.VassalMagnitude(verdict).Should().BeGreaterThanOrEqualTo(MercenaryDeparturePolicy.PlainVassalDepartureMagnitude);
      }
}
}
