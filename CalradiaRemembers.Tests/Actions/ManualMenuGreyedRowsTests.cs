// Code written by Gabriel Mailhot, 13/11/2026.
// What these pin: the Actions menu shows what is out of reach as well as what is open, each closed row with its
// reason, and a free lord's recruitment is one of its rows.
//
// Fakade, Nexus, 12/11/2026, after learning that free lords can be recruited: "I did not realize, probably because
// the option does not appear if not available. Is it possible to see all possible Action/Options rather than only
// the ones we have access to? Keep an option greyed out, perhaps with some hint as to why it is not available."
// Gabriel, 13/11/2026: "go". If these fail, an action the player cannot take yet vanishes from the menu again, and
// with it the only way a player learns that it exists and what it would take.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
   [TestFixture]
   public sealed class ManualMenuGreyedRowsTests
   {
      private static ManualActionStatus LordJoin(ManualActionFacts facts)
         => ManualActionAvailability.Describe(facts).Single(s => s.Entry.Type == ManualActionCatalog.JoinClan.Type);

      // THE REPORT: an action blocked as things stand is listed, greyed, with what it would take.
      [Test]
      public void GIVEN_an_action_out_of_reach_today_WHEN_the_menu_is_built_THEN_it_is_listed_with_its_reason()
      {
         ManualActionStatus row = ManualActionAvailability.MenuRows(new ManualActionFacts {PlayerHasGiveableItems = false})
                                                          .Single(s => s.Entry.Type == ManualActionCatalog.GiveItem.Type);

         row.State.Should().Be(ManualActionState.Blocked);
         row.Reason.Should().NotBeEmpty();
      }

      // STAKE: grey rows above the open ones would push what the player can do now below what they cannot.
      [Test]
      public void GIVEN_open_and_blocked_actions_WHEN_the_menu_is_built_THEN_every_open_one_comes_first()
      {
         var states = ManualActionAvailability.MenuRows(new ManualActionFacts {CanSpendInfluence = true}).Select(s => s.State).ToList();

         states.SkipWhile(s => s == ManualActionState.Open).Should().OnlyContain(s => s == ManualActionState.Blocked);
      }

      // STAKE: fiefs, prisoners and companions listed grey for a tavern keeper would bury the few rows that matter.
      [Test]
      public void GIVEN_actions_that_never_apply_to_this_person_WHEN_the_menu_is_built_THEN_they_stay_out_of_it()
      {
         ManualActionAvailability.MenuRows(new ManualActionFacts()).Should().NotContain(s => s.State == ManualActionState.NotForThisPerson);
      }

      // STAKE: Fakade never knew a free lord could be recruited, because the menu had no row for it at all.
      [Test]
      public void GIVEN_a_free_lord_who_would_join_WHEN_the_menu_is_built_THEN_asking_them_to_join_the_clan_is_open()
      {
         var facts = new ManualActionFacts {LordJoinBlock = LordRecruitmentBlock.None, LordRespectsPlayerEnough = true};

         LordJoin(facts).State.Should().Be(ManualActionState.Open);
      }

      // STAKE: "not yet" without the reason sends the player guessing; the regard named is the bar the bridge applies.
      [Test]
      public void GIVEN_a_free_lord_whose_regard_is_too_low_WHEN_the_menu_is_built_THEN_the_row_names_the_regard_needed()
      {
         ManualActionStatus row = LordJoin(new ManualActionFacts {LordJoinBlock = LordRecruitmentBlock.TrustTooLow});

         row.State.Should().Be(ManualActionState.Blocked);
         row.Reason.Should().Contain(LordRecruitment.MinRelation.ToString());
      }

      // STAKE: each gate has its own remedy (more regard, a higher clan, a lord with a reason to leave, respect).
      [TestCase(LordRecruitmentBlock.PlayerRankTooLow, true, "your clan must stand")]
      [TestCase(LordRecruitmentBlock.LordNotVulnerable, true, "too settled to leave")]
      [TestCase(LordRecruitmentBlock.None, false, "respect you enough")]
      public void GIVEN_a_free_lord_held_by_one_gate_WHEN_the_menu_is_built_THEN_the_row_names_that_gate(LordRecruitmentBlock block, bool respects, string reason)
      {
         ManualActionStatus row = LordJoin(new ManualActionFacts {LordJoinBlock = block, LordRespectsPlayerEnough = respects});

         row.State.Should().Be(ManualActionState.Blocked);
         row.Reason.Should().Contain(reason);
      }

      // STAKE: a clan leader can never fold their house into the player's (the bridge refuses it); no grey row may promise it.
      [Test]
      public void GIVEN_a_lord_who_leads_their_own_house_WHEN_the_menu_is_built_THEN_joining_never_applies_to_them()
      {
         LordJoin(new ManualActionFacts {LordJoinBlock = LordRecruitmentBlock.LordLeadsOwnClan}).State.Should().Be(ManualActionState.NotForThisPerson);
      }

      // STAKE: a commoner or a companion shown "ask them to join your clan" greyed would read as a goal to chase.
      [Test]
      public void GIVEN_someone_who_is_not_a_free_lord_of_another_house_WHEN_the_menu_is_built_THEN_joining_never_applies_to_them()
      {
         LordJoin(new ManualActionFacts()).State.Should().Be(ManualActionState.NotForThisPerson);
      }
   }
}
