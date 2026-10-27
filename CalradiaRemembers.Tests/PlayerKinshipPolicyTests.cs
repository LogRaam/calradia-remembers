// Code written by Gabriel Mailhot, 27/10/2026.
// PlayerKinshipPolicy names what a character is to the player by blood, for a family line that stays in every prompt.
// DepaHN (Nexus, 26/10/2026): children, grandchildren, siblings and relatives "feel very cold". If this is wrong, a
// grandchild or a nephew is spoken to as a stranger again, or a kinsman is given the wrong bond (a grandson called son).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerKinshipPolicyTests
   {
      // STAKE: DepaHN's grandchildren and relatives were never counted as family; each must be named by their bond.
      [Test]
      public void GIVEN_kin_beyond_the_household_WHEN_named_THEN_each_bond_has_its_word()
      {
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsGrandchild = true, IsFemale = true}).Should().Be("granddaughter");
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsNephewOrNiece = true}).Should().Be("nephew");
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsUncleOrAunt = true, IsFemale = true}).Should().Be("aunt");
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsCousin = true}).Should().Be("cousin");
      }

      // STAKE: the closest bond is the one that matters: a child of the player is a son, never "a nephew" by another path.
      [Test]
      public void GIVEN_several_bonds_WHEN_named_THEN_the_closest_wins()
      {
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsChild = true, IsNephewOrNiece = true}).Should().Be("son");
         PlayerKinshipPolicy.Label(new PlayerKinFacts {IsSibling = true, IsCousin = true, IsFemale = true}).Should().Be("sister");
      }

      // STAKE: no bond of blood, no family line: a stranger or a hired companion is never told they are kin.
      [Test]
      public void GIVEN_no_bond_of_blood_WHEN_named_THEN_there_is_no_word()
      {
         PlayerKinshipPolicy.Label(new PlayerKinFacts()).Should().BeNull();
      }
   }
}
