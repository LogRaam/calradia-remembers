// Code written by Gabriel Mailhot, 13/09/2026.
// take_into_service, built on Gabriel's ruling "un seigneur doit pouvoir recruter" after Ira agreed in prose
// to take his companion Nabb under her banner and the game had nothing to execute.
//
// What these pin is the DIRECTION and the household gates. What no pure test can pin is that
// RemoveCompanionAction/AddCompanionAction leave the hero in a coherent state - that is the in-game
// self-test's job, and it is why both exist.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Tests.Companions
{
   [TestFixture]
   public sealed class CompanionServiceTransferPolicyTests
   {
      // Everything passing, so the refusals below cannot be satisfied by refusing everything.
      private static CompanionServiceTransferFacts Fine() => new() {
         LordIsAlive = true,
         LordHasClan = true,
         CompanionResolved = true,
         CompanionIsAlive = true,
         CompanionIsInYourHousehold = true
      };

      [Test]
      public void GIVEN_a_free_lord_and_a_free_companion_WHEN_evaluated_THEN_the_transfer_is_allowed()
      {
         CompanionServiceTransferPolicy.Evaluate(Fine()).Should().Be(CompanionServiceTransferBlock.None);
      }

      // ── the household gates: what the PLAYER cannot be parted from ──

      // A marriage is not a term of service. This one can never be resolved by waiting, unlike the errand and
      // the governorship, so it must refuse outright rather than tell the player to come back later.
      [Test]
      public void GIVEN_the_companion_is_the_players_spouse_WHEN_evaluated_THEN_no_lord_may_take_them()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionIsPlayerSpouse = true};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.CompanionIsYourSpouse);
      }

      // Letting a sitting governor ride off would leave a fief ungoverned as a side effect nobody in the
      // conversation agreed to.
      [Test]
      public void GIVEN_the_companion_governs_a_fief_WHEN_evaluated_THEN_the_fief_is_not_quietly_left_empty()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionGovernsPlayerFief = true};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.CompanionGovernsAFief);
      }

      [Test]
      public void GIVEN_the_companion_is_away_on_an_errand_WHEN_evaluated_THEN_they_must_be_recalled_first()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionAwayOnErrand = true};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.CompanionAwayOnErrand);
      }

      [Test]
      public void GIVEN_the_hero_is_not_of_the_players_household_WHEN_evaluated_THEN_there_is_nothing_to_give()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionIsInYourHousehold = false};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.NotYourCompanion);
      }

      // THE ONE GABRIEL'S PLAY-TEST FOUND. He presented Sley the Grim to Ira as "my Master of Arms" - a nemesis
      // he had spared and sworn into his clan - and the verb was never taught, so she could not have accepted
      // him however the scene had gone. turn_nemesis and recruit_prisoner join a hero by assigning Hero.Clan
      // directly, which never sets CompanionOf: IsPlayerCompanion stays FALSE for exactly the retainers a
      // player is proudest of. The question is "is this person in your service", never "which door did they
      // come through", and this pins that the fact is about the household.
      [Test]
      public void GIVEN_a_retainer_sworn_in_without_the_companion_door_WHEN_evaluated_THEN_they_can_still_be_offered()
      {
         CompanionServiceTransferFacts sworn = Fine(); // in the household, never a formal companion

         CompanionServiceTransferPolicy.Evaluate(sworn).Should().Be(CompanionServiceTransferBlock.None);
      }

      // Your own son is not a retainer. Placing kin in another house is fosterage or hostage-taking - a
      // different act, with different consequences, that this verb must not quietly perform.
      [Test]
      public void GIVEN_the_hero_is_the_players_own_blood_WHEN_evaluated_THEN_they_are_not_a_retainer_to_place()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionIsPlayerFamily = true};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.CompanionIsYourFamily);
      }

      // Widening the candidate set from Companions to the whole clan lets the player and their clan leader in,
      // so they are refused explicitly rather than relied upon never to be asked for.
      [Test]
      public void GIVEN_the_hero_leads_the_players_house_WHEN_evaluated_THEN_they_are_nobodys_to_give()
      {
         CompanionServiceTransferFacts f = Fine() with {CompanionLeadsThePlayersClan = true};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.CompanionLeadsYourHouse);
      }

      // ── the lord's own fitness, which comes first because it makes everything after moot ──

      [Test]
      public void GIVEN_a_lord_who_is_dead_captive_or_the_player_WHEN_evaluated_THEN_nobody_changes_hands()
      {
         foreach (CompanionServiceTransferFacts f in new[] {
                     Fine() with {LordIsAlive = false},
                     Fine() with {LordIsPrisoner = true},
                     Fine() with {LordIsThePlayer = true}
                  })
            CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.LordUnavailable);
      }

      // THE ONE THAT KEEPS THE DIRECTION HONEST. If the "lord" is of the player's own clan, nobody actually
      // changes hands - and letting it through would make this verb a confusing no-op alias for the inward
      // ones (join_clan, recruit_notable) rather than their mirror.
      [Test]
      public void GIVEN_the_lord_is_of_the_players_own_clan_WHEN_evaluated_THEN_it_refuses_rather_than_no_op()
      {
         CompanionServiceTransferFacts f = Fine() with {LordClanIsPlayerClan = true};

         CompanionServiceTransferPolicy.Evaluate(f)
                                       .Should()
                                       .Be(CompanionServiceTransferBlock.LordIsOfThePlayersOwnClan);
      }

      [Test]
      public void GIVEN_a_lord_with_no_house_of_their_own_WHEN_evaluated_THEN_there_is_nowhere_to_take_them()
      {
         CompanionServiceTransferFacts f = Fine() with {LordHasClan = false};

         CompanionServiceTransferPolicy.Evaluate(f).Should().Be(CompanionServiceTransferBlock.LordHasNoHouse);
      }

      [Test]
      public void GIVEN_no_facts_at_all_WHEN_evaluated_THEN_it_refuses_rather_than_throwing()
      {
         CompanionServiceTransferPolicy.Evaluate(null).Should().Be(CompanionServiceTransferBlock.LordUnavailable);
      }

      // ── picking who was meant ──

      private static List<CompanionCandidate> Cast(params (string id, string name)[] people)
      {
         var list = new List<CompanionCandidate>();
         foreach ((string id, string name) in people) list.Add(new CompanionCandidate {HeroId = id, Name = name});

         return list;
      }

      // THE COMMON CASE, and the reason a blank target is allowed at all: a lord taking the man standing in
      // front of her does not announce his name to the room.
      [Test]
      public void GIVEN_one_companion_and_no_name_spoken_WHEN_picking_THEN_it_is_the_one_standing_there()
      {
         CompanionServiceTransferPolicy.PickCompanion(Cast(("n", "Nabb the Bloody Handed")), null)
                                       .Should()
                                       .Be("n");
      }

      // THE ONE THAT MATTERS MOST. Guessing which of three companions was given away is not a guess this code
      // is entitled to make - and unlike every other refusal here, being wrong would hand the WRONG PERSON to
      // another clan, which the player cannot undo.
      [Test]
      public void GIVEN_several_companions_and_no_name_spoken_WHEN_picking_THEN_it_refuses_to_guess()
      {
         CompanionServiceTransferPolicy.PickCompanion(Cast(("n", "Nabb"), ("c", "Coll"), ("a", "Ajin")), " ")
                                       .Should()
                                       .BeNull();
      }

      // A model writes "Nabb" for "Nabb the Bloody Handed".
      [Test]
      public void GIVEN_a_short_name_spoken_WHEN_picking_THEN_the_long_named_companion_is_found()
      {
         CompanionServiceTransferPolicy
            .PickCompanion(Cast(("n", "Nabb the Bloody Handed"), ("c", "Coll the Unbroken")), "Nabb")
            .Should()
            .Be("n");
      }

      [Test]
      public void GIVEN_the_full_name_spoken_WHEN_picking_THEN_it_is_matched_exactly()
      {
         CompanionServiceTransferPolicy
            .PickCompanion(Cast(("n", "Nabb the Bloody Handed"), ("c", "Coll the Unbroken")), "Coll the Unbroken")
            .Should()
            .Be("c");
      }

      // Two companions who share a first word: a crowd by another route, and it refuses for the same reason.
      [Test]
      public void GIVEN_two_companions_sharing_a_first_name_WHEN_only_that_name_is_spoken_THEN_it_refuses_to_guess()
      {
         CompanionServiceTransferPolicy
            .PickCompanion(Cast(("a", "Nabb the Bloody Handed"), ("b", "Nabb of Pravend")), "Nabb")
            .Should()
            .BeNull();
      }

      // A full-name match still wins even when another candidate shares the first word, because it is the
      // more specific evidence of who was meant.
      [Test]
      public void GIVEN_a_full_name_that_another_shares_a_first_word_with_WHEN_picking_THEN_the_exact_one_wins()
      {
         CompanionServiceTransferPolicy
            .PickCompanion(Cast(("a", "Nabb the Bloody Handed"), ("b", "Nabb of Pravend")), "Nabb of Pravend")
            .Should()
            .Be("b");
      }

      [Test]
      public void GIVEN_a_name_nobody_answers_to_WHEN_picking_THEN_nobody_is_handed_over()
      {
         CompanionServiceTransferPolicy.PickCompanion(Cast(("n", "Nabb")), "Ira").Should().BeNull();
         CompanionServiceTransferPolicy.PickCompanion(null, "Nabb").Should().BeNull();
         CompanionServiceTransferPolicy.PickCompanion(new List<CompanionCandidate>(), "Nabb").Should().BeNull();
      }
   }
}
