// Code written by Gabriel Mailhot, 19/09/2026.
// What these pin: that a lord can be offered one of the player's retainers, and never one of the player's family.
//
// reminensce (Nexus, 19/09/2026): "it's possible for extended family members or married-in nobles to be given
// to and/or taken by other lords. Is this the intended behavior or an actual semantic error?"
//
// It was an error. The 2.5.9 changelog promised "your spouse cannot be given, nor your own blood", and "own
// blood" had been implemented as parents, children and siblings only. A clan that has run for a generation
// holds grandchildren, nephews, cousins, and the husbands and wives of all of them, and every one of those
// passed every test and could be handed to a lord as a retainer.
//
// The pool itself stays wide on purpose (Clan.Heroes, not Clan.Companions): a turned nemesis or a recruited
// prisoner is sworn in without ever becoming a "companion" to the engine, and those are the retainers a player
// is proudest of. So the question these answer is not "who is a companion" but "who is FAMILY".

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HouseholdKinPolicyTests
   {
      //  grandsire ─┬─ grandam
      //             │
      //     ┌───────┴────────┐
      //   player ═ wife     uncle
      //     │                 │
      //   son ═ daughterInLaw cousin
      //     │
      //   grandson
      //
      //   brother (shares the player's parents) ─ nephew
      //   companion (joined; no shared forebear)
      private static readonly List<KinRecord> House = new() {
         new() {Id = "grandsire"},
         new() {Id = "grandam"},
         new() {Id = "father", FatherId = "grandsire", MotherId = "grandam"},
         new() {Id = "uncle", FatherId = "grandsire", MotherId = "grandam"},
         new() {Id = "player", FatherId = "father", SpouseId = "wife"},
         new() {Id = "wife"},
         new() {Id = "brother", FatherId = "father"},
         new() {Id = "nephew", FatherId = "brother"},
         new() {Id = "cousin", FatherId = "uncle"},
         new() {Id = "son", FatherId = "player", MotherId = "wife", SpouseId = "daughterInLaw"},
         new() {Id = "daughterInLaw"},
         new() {Id = "grandson", FatherId = "son", MotherId = "daughterInLaw"},
         new() {Id = "companion"}
      };

      private static ISet<string> Family => HouseholdKinPolicy.FamilyOf("player", House);

      // THE REPORTED CASE, the married-in noble. Not the player's blood and not the player's spouse, which is
      // exactly how she slipped through: every test the old rule had, she passed.
      [Test]
      public void GIVEN_a_daughter_in_law_WHEN_asked_THEN_she_is_family_and_cannot_be_given_away()
      {
         Family.Should().Contain("daughterInLaw");
      }

      // The other half of his report: extended family. Grandchildren, nephews and cousins are all in a clan
      // that has run for a generation, and none of them were parents, children or siblings.
      [TestCase("grandson")]
      [TestCase("nephew")]
      [TestCase("cousin")]
      [TestCase("uncle")]
      public void GIVEN_extended_blood_family_WHEN_asked_THEN_they_are_family(string relative)
      {
         Family.Should().Contain(relative);
      }

      // What the old rule already covered must stay covered: this is a widening, never a swap.
      [TestCase("father")]
      [TestCase("brother")]
      [TestCase("son")]
      [TestCase("wife")]
      public void GIVEN_close_family_the_old_rule_already_protected_WHEN_asked_THEN_they_still_are(string relative)
      {
         Family.Should().Contain(relative);
      }

      // THE CONTROL, and it is what makes the widening safe: a companion who JOINED shares no forebear with the
      // player and married nobody in the family, so he is still a retainer a lord may be offered. If this goes
      // red the verb has been quietly disabled for everyone it was built for.
      [Test]
      public void GIVEN_a_companion_who_joined_the_clan_WHEN_asked_THEN_he_is_not_family()
      {
         Family.Should().NotContain("companion");
      }

      // A marriage may be recorded on only ONE of the two people. The rule must find it from either side, or a
      // spouse whose own record carries no SpouseId would drop out of the family.
      [Test]
      public void GIVEN_a_marriage_recorded_only_on_the_other_spouse_WHEN_asked_THEN_it_still_counts()
      {
         var house = new List<KinRecord> {
            new() {Id = "player"},
            new() {Id = "daughter", FatherId = "player"},
            new() {Id = "sonInLaw", SpouseId = "daughter"}
         };

         HouseholdKinPolicy.FamilyOf("player", house).Should().Contain("sonInLaw");
      }

      // An in-law of an in-law is not family. His brother-in-law's brother is a stranger to the player, and
      // counting him would keep widening the circle until nobody could ever be offered.
      [Test]
      public void GIVEN_the_sibling_of_a_married_in_spouse_WHEN_asked_THEN_he_is_not_family()
      {
         var house = new List<KinRecord> {
            new() {Id = "player"},
            new() {Id = "inLawsFather"},
            new() {Id = "son", FatherId = "player", SpouseId = "daughterInLaw"},
            new() {Id = "daughterInLaw", FatherId = "inLawsFather"},
            new() {Id = "herBrother", FatherId = "inLawsFather"}
         };

         HouseholdKinPolicy.FamilyOf("player", house).Should().NotContain("herBrother");
      }

      // An ancestor missing from the records is UNKNOWN, not shared. Two people with no parents on file must
      // never be made kin by both having nothing.
      [Test]
      public void GIVEN_people_with_no_parents_on_record_WHEN_asked_THEN_they_are_not_made_kin_by_it()
      {
         var house = new List<KinRecord> {new() {Id = "player"}, new() {Id = "stranger"}};

         HouseholdKinPolicy.FamilyOf("player", house).Should().NotContain("stranger");
      }

      // No player, no family: nothing to protect, and nothing thrown on a background call.
      [Test]
      public void GIVEN_no_player_WHEN_asked_THEN_nobody_is_family()
      {
         HouseholdKinPolicy.FamilyOf(null, House).Should().BeEmpty();
         HouseholdKinPolicy.FamilyOf("player", null).Should().BeEmpty();
      }
   }
}
