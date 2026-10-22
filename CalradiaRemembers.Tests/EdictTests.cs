// Code written by Gabriel Mailhot, 22/10/2026.
// Edicts: a ruler's lasting, bounded decision on a town or a realm (Gabriel, 21/10/2026, after reading another mod whose
// rulers write policies: "2 et 3 m'interesse", then "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre
// conteste."). These tests hold what an edict may do, who may decree it, which one holds on a town, when it ends, how a
// town contests it, and what the world's lords decree on their own. If they are wrong, a model invents an edict with no
// price, a lord decrees for a town he does not hold, two edicts stack on one town, an edict outlives the lord who lost
// the town, a content town riots, or a starving town's lord decrees a feast.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Edicts;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class EdictTests
   {
      // STAKE: an edict is a choice, not a gift: every one buys its gain with a cost, so a ruler is never handed a free
      // boon and a contest always has something to push against.
      [Test]
      public void GIVEN_the_catalogue_WHEN_each_edict_is_read_THEN_each_has_a_gain_and_a_cost()
      {
         foreach (EdictEffect e in EdictCatalog.All)
         {
            float[] channels = {e.TaxFactor * 10f, e.Loyalty, e.Security, e.Prosperity, e.Militia, e.Food / 4f};
            channels.Should().Contain(v => v > 0f, $"{e.Title} gains something");
            channels.Should().Contain(v => v < 0f, $"{e.Title} costs something");
         }
      }

      // STAKE: the model names the edict, the catalogue decides what it does: a name it invents ("Edict of Free Gold")
      // decrees nothing.
      [Test]
      public void GIVEN_a_name_the_model_writes_WHEN_looked_up_THEN_only_the_catalogues_edicts_are_found()
      {
         EdictCatalog.Find("Tax relief").Kind.Should().Be(EdictKind.TaxRelief);
         EdictCatalog.Find("GRAIN_REQUISITION").Kind.Should().Be(EdictKind.GrainRequisition);
         EdictCatalog.Find("Edict of Free Gold").Should().BeNull();
      }

      // STAKE: an edict is bounded in time: "forever" or "two days" are brought within the term, never past it.
      [Test]
      public void GIVEN_a_term_asked_WHEN_decreed_THEN_it_stays_within_the_bounds()
      {
         EdictCatalog.Term(0).Should().Be(EdictCatalog.DefaultDays);
         EdictCatalog.Term(2).Should().Be(EdictCatalog.MinDays);
         EdictCatalog.Term(10000).Should().Be(EdictCatalog.MaxDays);
      }

      // STAKE: the player confirms what he decrees: the question names what the edict really does, gains and costs.
      [Test]
      public void GIVEN_an_edict_WHEN_described_THEN_its_gains_and_costs_are_said()
      {
         EdictCatalog.Of(EdictKind.TaxRelief).Describe().Should().Be("taxes -30%, loyalty +1 a day");
         EdictCatalog.Of(EdictKind.GrainRequisition).Describe().Should().Be("loyalty -1 a day, food +4 a day");
      }

      // STAKE: one edict per town at a time, so effects never stack: the town's own edict holds over the realm's, and a
      // town that forced the realm's edict off is free of it.
      [Test]
      public void GIVEN_a_town_and_a_realm_edict_WHEN_asked_which_holds_THEN_the_towns_own_first_and_a_repealed_one_never()
      {
         var ledger = new EdictLedger();
         Edict realm = ledger.Decree(new Edict {Kind = EdictKind.MilitiaMuster, Scope = EdictScope.Realm, TargetId = "empire"});
         ledger.HoldingOn("town_a", "empire").Should().BeSameAs(realm);

         Edict own = ledger.Decree(new Edict {Kind = EdictKind.TaxRelief, Scope = EdictScope.Town, TargetId = "town_a"});
         ledger.HoldingOn("town_a", "empire").Should().BeSameAs(own);
         ledger.HoldingOn("town_b", "empire").Should().BeSameAs(realm);

         realm.RepealedIn.Add("town_b");
         ledger.HoldingOn("town_b", "empire").Should().BeNull();
         ledger.HoldingOn("town_c", "vlandia").Should().BeNull();
      }

      // STAKE: only the head of the house that holds a town decrees for it, and only the ruler for the realm; a second
      // edict waits for the first to end and the town to rest; a decree costs influence. Refused BEFORE anyone announces it.
      [Test]
      public void GIVEN_a_decree_WHEN_checked_THEN_the_right_one_edict_at_a_time_rest_and_influence_are_required()
      {
         var fine = new EdictDecreeFacts {Scope = EdictScope.Town, HoldsTheRight = true, IsATown = true, Influence = 100};
         EdictPolicy.WhyNot(fine).Should().BeNull();
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Town, IsATown = true, Influence = 100}).Should().Contain("head of the house");
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Realm, Influence = 500}).Should().Contain("ruler");
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Town, HoldsTheRight = true, IsATown = false, Influence = 100}).Should().Contain("town");
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Town, HoldsTheRight = true, IsATown = true, OneAlreadyHolds = true, Influence = 100}).Should().Contain("already");
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Town, HoldsTheRight = true, IsATown = true, DaysSinceLastEnded = 3, Influence = 100}).Should().Contain("3 days");
         EdictPolicy.WhyNot(new EdictDecreeFacts {Scope = EdictScope.Realm, HoldsTheRight = true, Influence = 50}).Should().Contain($"{EdictCatalog.RealmInfluenceCost} influence");
      }

      // STAKE: an edict is the decree of whoever holds the town or the throne: when he loses it, or dies, it lapses;
      // otherwise it runs its term.
      [Test]
      public void GIVEN_an_edict_in_force_WHEN_its_enactor_loses_the_right_or_its_term_ends_THEN_it_lapses_or_expires()
      {
         EdictPolicy.Standing(new EdictStandingFacts {EnactorPresent = true, EnactorStillHoldsTheRight = true, Today = 10, EndDay = 40}).Should().Be(EdictStatus.Active);
         EdictPolicy.Standing(new EdictStandingFacts {EnactorPresent = true, EnactorStillHoldsTheRight = false, Today = 10, EndDay = 40}).Should().Be(EdictStatus.Lapsed);
         EdictPolicy.Standing(new EdictStandingFacts {EnactorPresent = false, EnactorStillHoldsTheRight = true, Today = 10, EndDay = 40}).Should().Be(EdictStatus.Lapsed);
         EdictPolicy.Standing(new EdictStandingFacts {EnactorPresent = true, EnactorStillHoldsTheRight = true, Today = 40, EndDay = 40}).Should().Be(EdictStatus.Expired);
      }

      // STAKE: Gabriel, "un edit peut etre conteste": a burdened, restless town grows a grievance, contests the edict
      // openly once, and at last forces it off; a content town, or one under an edict that costs it nothing, never does.
      [Test]
      public void GIVEN_a_town_under_an_edict_WHEN_days_pass_THEN_a_burdened_restless_town_contests_it_then_forces_it_off()
      {
         var g = 0;
         var contested = false;
         var days = 0;
         EdictUnrest last = EdictUnrest.Quiet;
         var said = new List<EdictUnrest>();
         while (last != EdictUnrest.Repealed && days < 100)
         {
            EdictGrievanceStep step = EdictPolicy.Grieve(new EdictGrievanceFacts {BurdensThePeople = true, Loyalty = 20, Grievance = g, AlreadyContested = contested});
            g = step.Grievance;
            last = step.Unrest;
            if (last == EdictUnrest.Contested) contested = true;
            said.Add(last);
            days++;
         }

         said.Count(u => u == EdictUnrest.Contested).Should().Be(1);
         said.IndexOf(EdictUnrest.Contested).Should().Be(EdictPolicy.ContestedAfterDays - 1);
         days.Should().Be(EdictPolicy.RepealedAfterDays);

         EdictPolicy.Grieve(new EdictGrievanceFacts {BurdensThePeople = true, Loyalty = 70, Grievance = 5}).Grievance.Should().Be(4);
         EdictPolicy.Grieve(new EdictGrievanceFacts {BurdensThePeople = false, Loyalty = 10, Grievance = 0}).Grievance.Should().Be(0);
      }

      // STAKE: a lord's own edict answers his town's sorest need, so what the player hears makes sense: a hungry town
      // requisitions grain, a restless one lightens taxes, a lawless one doubles the watch; a content town needs none.
      [Test]
      public void GIVEN_a_town_in_need_WHEN_its_lord_weighs_an_edict_THEN_he_answers_the_sorest_need()
      {
         var calm = new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 50, Security = 50, Prosperity = 4000, Militia = 200};
         EdictCounselPolicy.Answer(calm).Should().BeNull();
         EdictCounselPolicy.Answer(new EdictTownFacts {FoodDaysLeft = 5, Loyalty = 10, Security = 10}).Should().Be(EdictKind.GrainRequisition);
         EdictCounselPolicy.Answer(new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 10, Security = 10}).Should().Be(EdictKind.TaxRelief);
         EdictCounselPolicy.Answer(new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 50, Security = 10}).Should().Be(EdictKind.DoubledWatch);
         EdictCounselPolicy.Answer(new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 50, Security = 50, AtWar = true, Militia = 40}).Should().Be(EdictKind.MilitiaMuster);
         EdictCounselPolicy.Answer(new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 80, Security = 80, Prosperity = 4000, HolderShortOfCoin = true}).Should().Be(EdictKind.HeavyLevy);
      }

      // STAKE: a ruler decrees for the whole realm only for a need most of its towns share; a single hungry town is its
      // own lord's business.
      [Test]
      public void GIVEN_a_realms_towns_WHEN_its_ruler_weighs_an_edict_THEN_only_a_shared_need_is_answered_for_all()
      {
         var hungry = new EdictTownFacts {FoodDaysLeft = 5, Loyalty = 50, Security = 50, Prosperity = 4000, Militia = 200};
         var calm = new EdictTownFacts {FoodDaysLeft = 99, Loyalty = 50, Security = 50, Prosperity = 4000, Militia = 200};
         EdictCounselPolicy.AnswerForRealm(new[] {hungry, hungry, calm}).Should().Be(EdictKind.GrainRequisition);
         EdictCounselPolicy.AnswerForRealm(new[] {hungry, calm, calm}).Should().BeNull();
      }

      // STAKE: the world's lords decree seldom and a realm carries few at once, so edicts stay news the player meets,
      // not a simulation of every town he never visits.
      [Test]
      public void GIVEN_a_lord_with_a_sore_town_WHEN_the_week_turns_THEN_he_decrees_seldom_and_never_past_the_realms_room()
      {
         EdictCounselPolicy.DecreesThisWeek(0.05f, 0).Should().BeTrue();
         EdictCounselPolicy.DecreesThisWeek(0.5f, 0).Should().BeFalse();
         EdictCounselPolicy.DecreesThisWeek(0.05f, EdictCounselPolicy.MostInARealm).Should().BeFalse();
      }
   }
}
