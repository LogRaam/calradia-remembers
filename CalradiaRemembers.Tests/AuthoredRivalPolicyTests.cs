// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales: who may be bound as a tale's {rival} (smoke_over_the_hills: riders of another house of the giver's
// own realm burned the farms). The answer feeds the binding made when the tale is offered: every {rival} in the text,
// the "said" choice spoken to him and his knower lines. If it is wrong, the player is sent to confront the giver
// himself, their own house, a lord of another realm, or a dead man, and a path of the tale can never be taken.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredRivalPolicyTests
   {
      private static RivalCandidate Lord(string id) => new() {HeroId = id, LeadsHisClan = true, OfTheGiversRealm = true, InTheWorld = true};

      // The one who fits: the head of another house of the same realm, someone the player can reach.
      [Test]
      public void GIVEN_the_head_of_another_house_of_the_givers_realm_WHEN_a_rival_is_chosen_THEN_he_is_bound()
      {
         AuthoredRivalPolicy.Choose(new[] {Lord("lord_rival")}, 0.5, out string whyNot).Should().Be("lord_rival");
         whyNot.Should().BeNull();
      }

      // Never the giver's own house, the player's house, a lord of another realm, a younger son, or someone gone from the
      // world: each would send the player somewhere the story cannot happen.
      [Test]
      public void GIVEN_lords_who_cannot_be_the_rival_WHEN_a_rival_is_chosen_THEN_none_of_them_is_and_the_reason_is_said()
      {
         var unfit = new List<RivalCandidate> {
            new() {HeroId = "giver_brother", LeadsHisClan = true, OfTheGiversRealm = true, InTheWorld = true, OfTheGiversHouse = true},
            new() {HeroId = "player_kin", LeadsHisClan = true, OfTheGiversRealm = true, InTheWorld = true, OfThePlayersHouse = true},
            new() {HeroId = "foreign_lord", LeadsHisClan = true, OfTheGiversRealm = false, InTheWorld = true},
            new() {HeroId = "younger_son", LeadsHisClan = false, OfTheGiversRealm = true, InTheWorld = true},
            new() {HeroId = "dead_lord", LeadsHisClan = true, OfTheGiversRealm = true, InTheWorld = false}
         };

         AuthoredRivalPolicy.Choose(unfit, 0.3, out string whyNot).Should().BeNull();
         whyNot.Should().Be(AuthoredRivalPolicy.NoneEligible);
      }

      // Among several who fit, the dice choose, and an edge roll never falls outside the list.
      [Test]
      public void GIVEN_several_fitting_lords_WHEN_the_dice_fall_THEN_one_of_them_is_chosen_by_the_roll()
      {
         RivalCandidate[] lords = {Lord("a"), new() {HeroId = "skip", LeadsHisClan = false, OfTheGiversRealm = true, InTheWorld = true}, Lord("b")};

         AuthoredRivalPolicy.Choose(lords, 0.0, out _).Should().Be("a");
         AuthoredRivalPolicy.Choose(lords, 0.99, out _).Should().Be("b");
         AuthoredRivalPolicy.Choose(lords, 1.0, out _).Should().Be("b");
      }
   }
}
