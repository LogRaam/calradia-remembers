// Code written by Gabriel Mailhot, 03/08/2026.
// Player reports, 2026-08-03: a gifted item that never transferred, an NPC's "here's 1000 denars" that
// never arrived, an offered prisoner that stayed in the party. In every case a bridge executor hit a
// refusal branch (no parsable amount, an insolvent giver or player, no matching bargain) and returned a
// bare null instead of an ActionOutcome. A null reaches neither AppendSystemMessage (the player line) nor
// ActionRefusalNote (the model's out-of-character transcript note), so the LLM, having already narrated the
// deed as done, keeps replaying the phantom gift or payment into every later prompt while the player sees
// nothing at all. These strings are what the bridge now hands to ActionOutcome.Refused(...) on those
// branches instead, so both sides of the conversation learn the deed did not happen.
//
// The same silent-null class also covers a promised gift with no item named at all, a grant_blessing with
// no one named, an arrange_marriage with no kin named, and a decline_divorce whose two guard checks used to
// return null while accept_divorce's identical checks already surfaced a Refused: the model narrates the
// gift, blessing, betrothal, or refusal as done, and without a surfaced message the player never learns it
// silently did nothing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SilentRefusalMessagesTests
   {
      // STAKE: this is the branch a hallucinated or garbled give_gold amount used to hit silently. Without
      // a surfaced message the player never learns why no gold arrived even though the model just said it did.
      [Test]
      public void GIVEN_give_gold_had_no_valid_sum_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.GiveGoldNoValidSum();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: player report 2026-08-03, an NPC's "here's 1000 denars" never arrived. The giver was
      // insolvent, the executor used to just return null, and the model kept treating the promise as kept.
      // The giver's name must be present so the model can attribute the refusal to the right character.
      [Test]
      public void GIVEN_the_giver_has_no_gold_WHEN_building_the_message_THEN_it_names_the_giver_and_denies_the_gift()
      {
         string message = SilentRefusalMessages.GiveGoldGiverInsolvent("Derthert");

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
         message.Should().Contain("Derthert");
      }

      // STAKE: the take_gold twin of GiveGoldNoValidSum; a garbled or missing amount used to vanish
      // silently instead of telling either side that no coin moved.
      [Test]
      public void GIVEN_take_gold_had_no_valid_sum_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.TakeGoldNoValidSum();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: the demand is clamped to the player's real purse (never drives it negative); once that
      // clamp lands at zero the player has nothing to give, and that refusal must reach the model too, or
      // it keeps believing the payment landed.
      [Test]
      public void GIVEN_the_player_has_no_coin_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.TakeGoldPlayerInsolvent();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: player report 2026-08-03, an offered prisoner stayed in the party. The model emitted
      // give_prisoner with no matching outstanding DeliverPrisoner bargain, the executor used to just
      // return null, and the captive was never actually handed over while the model narrated the handoff.
      [Test]
      public void GIVEN_no_matching_prisoner_bargain_exists_WHEN_building_the_message_THEN_it_denies_the_handoff()
      {
         string message = SilentRefusalMessages.GivePrisonerNoBargain();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: the model narrates a gift with no item ever actually named (no picked bag item, no free-text
      // name). ExecuteGiveItem used to return null on this branch, so the player saw no effect while the
      // model kept believing the gift went through.
      [Test]
      public void GIVEN_give_item_named_no_item_at_all_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.GiveItemNoneNamed();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: the model narrates a blessing with no one actually named to bless. ExecuteGrantBlessing used
      // to return null on a missing or blank hero parameter, leaving the player with no idea the blessing
      // never happened while the model kept believing consent was given.
      [Test]
      public void GIVEN_grant_blessing_named_no_one_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.GrantBlessingNoOneNamed();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: the model narrates a betrothal with the player's or the target's kin left unnamed.
      // ExecuteArrangeMarriage used to return null when either parameter was missing or blank, so the
      // promised match silently never happened.
      [Test]
      public void GIVEN_arrange_marriage_is_missing_a_kin_name_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.ArrangeMarriageMissingKin();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: accept_divorce already surfaces this exact check (the named hero is not actually the
      // player's spouse) via ActionOutcome.Refused. decline_divorce used to return null on the identical
      // check, a real asymmetry: refusing a divorce demand that was never real silently did nothing.
      [Test]
      public void GIVEN_the_named_hero_is_not_the_player_spouse_WHEN_building_the_message_THEN_it_denies_a_marriage_exists()
      {
         string message = SilentRefusalMessages.DivorceNotFromSpouse();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: accept_divorce already surfaces this exact check (no divorce demand is actually pending)
      // via ActionOutcome.Refused, naming the spouse. decline_divorce used to return null on the identical
      // check; the spouse's name must be present so the model can attribute the refusal to the right person.
      [Test]
      public void GIVEN_no_divorce_demand_is_pending_WHEN_building_the_message_THEN_it_names_the_spouse_and_denies_a_demand()
      {
         string message = SilentRefusalMessages.DivorceNoDemandPending("Derthert");

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
         message.Should().Contain("Derthert");
      }

      // STAKE: the teach_skill mirror of GiveItemNoneNamed: a blank or missing skill parameter used to be
      // the kind of branch that returned null elsewhere, silently telling neither the player nor the model
      // that no lesson (and no XP) actually happened.
      [Test]
      public void GIVEN_teach_skill_named_no_skill_at_all_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.TeachSkillNoSkillNamed();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }
   }
}
