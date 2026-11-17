// Code written by Gabriel Mailhot, 17/11/2026.
// What a mother remembers of a birth (BirthMemoryText). bdionysis80 (Nexus, 17/11/2026): pregnant a second time, his wife
// "kept going back to the first pregnancy, which had already been completed". A birth wrote no memory, so nothing ended an
// old "I am with child". If this text is wrong, she either keeps speaking of a pregnancy long over, misnames her own child,
// or names a father the bastard pillar keeps secret.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BirthMemoryTextTests
   {
      // STAKE: the mother must remember the child by name and know the pregnancy is over, or she keeps speaking of it.
      [Test]
      public void GIVEN_one_child_born_alive_WHEN_composed_THEN_she_names_the_child_and_the_pregnancy_is_over()
      {
         string text = BirthMemoryText.Compose(new BirthFacts {Alive = new List<BornChild> {new() {Name = "Aldric"}}});

         text.Should().Be("I gave birth to my son Aldric. That pregnancy is over.");
      }

      // STAKE: twins are two children; naming one would make her forget the other.
      [Test]
      public void GIVEN_twins_WHEN_composed_THEN_both_are_named()
      {
         string text = BirthMemoryText.Compose(new BirthFacts {
            Alive = new List<BornChild> {new() {Name = "Ira", IsFemale = true}, new() {Name = "Joren"}}
         });

         text.Should().Be("I gave birth to twins: my daughter Ira and my son Joren. That pregnancy is over.");
      }

      // STAKE: a child born dead still ends the pregnancy; without a memory she would speak of carrying it still.
      [Test]
      public void GIVEN_only_a_stillbirth_WHEN_composed_THEN_she_remembers_it_and_the_pregnancy_is_over()
      {
         BirthMemoryText.Compose(new BirthFacts {Stillborn = 1}).Should().Be("My child was born dead. That pregnancy is over.");
      }

      // STAKE: a living child and a stillborn twin are both remembered.
      [Test]
      public void GIVEN_one_alive_and_one_stillborn_WHEN_composed_THEN_both_are_told()
      {
         BirthMemoryText.Compose(new BirthFacts {Alive = new List<BornChild> {new() {Name = "Lia", IsFemale = true}}, Stillborn = 1})
                        .Should().Be("I gave birth to my daughter Lia. Another was born dead. That pregnancy is over.");
      }

      // STAKE: nothing born is nothing to remember; an empty memory would clutter her history.
      [Test]
      public void GIVEN_nothing_born_WHEN_composed_THEN_there_is_no_memory()
      {
         BirthMemoryText.Compose(new BirthFacts()).Should().BeNull();
         BirthMemoryText.Compose(null).Should().BeNull();
      }
   }
}
