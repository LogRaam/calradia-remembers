// Code written by Gabriel Mailhot, 29/10/2026.
// The children on a character's family line, said as a whole list. fearente (Nexus, 29/10/2026): a brother spoke of
// his son "Lorgand" when he has a daughter. If the list is not closed, or a childless lord is told nothing, the model
// invents heirs, and the player is told of people who do not exist in the world.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChildrenListTextTests
   {
      // The report itself: he has a daughter, and must know she is his only child, not one among others to be invented.
      [Test]
      public void GIVEN_a_lord_with_one_daughter_WHEN_his_family_is_written_THEN_he_is_told_she_is_his_only_child()
      {
         ChildrenListText.Part(new[] {"Ira (female, age 14)"}, true)
                         .Should().Be("Children: Ira (female, age 14), and no other children");
      }

      // Silence was the gap: a grown lord with no children was told nothing, and a son could be made up.
      [Test]
      public void GIVEN_a_grown_lord_with_no_children_WHEN_his_family_is_written_THEN_he_is_told_he_has_none()
      {
         ChildrenListText.Part(new string[0], true).Should().Be("Children: none");
      }

      // A child is not told they have no children: that line would be noise in a young character's prompt.
      [Test]
      public void GIVEN_a_child_with_no_children_WHEN_their_family_is_written_THEN_nothing_is_said()
      {
         ChildrenListText.Part(null, false).Should().BeNull();
      }
   }
}
