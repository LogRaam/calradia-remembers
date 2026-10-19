// Code written by Gabriel Mailhot, 05/09/2026.
// SecretLoverAdmissionPolicy decides whether the player confessing a secret-lover bond to a given NPC reveals
// anything, and about whom. It gates the admit_secret_lover verb: the LLM is taught the confession ONLY when there
// is a genuine secret the NPC does not already embody, and on execution the knowledge ledger records exactly the
// subjects this NPC newly learns. Get it wrong and the mystery-lover pillar breaks its core promise: either the verb
// is offered when there is nothing to confess (the LLM invents a bond), or a confession made to the lover themselves
// pointlessly records them as "knowing" a secret they ARE.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SecretLoverAdmissionPolicyTests
   {
      // With no secret bond at all, the player has nothing to confess: the verb must never be taught, or the LLM is
      // handed a confession vocabulary for a bond that does not exist and will fabricate one.
      [Test]
      public void GIVEN_the_player_has_no_secret_lover_WHEN_checking_admissibility_THEN_nothing_is_revealed()
      {
         SecretLoverAdmissionPolicy.CanAdmitTo(new List<string>(), "lord_a").Should().BeFalse();
         SecretLoverAdmissionPolicy.SubjectsRevealedTo(new List<string>(), "lord_a").Should().BeEmpty();
      }

      // A secret bond with someone the NPC is NOT: the confession genuinely teaches the NPC an identity no one can
      // deduce, so it is admissible and the subject is recorded.
      [Test]
      public void GIVEN_a_secret_lover_who_is_not_the_listener_WHEN_checking_THEN_that_subject_is_revealed()
      {
         var subjects = new List<string> {"companion_x"};

         SecretLoverAdmissionPolicy.CanAdmitTo(subjects, "lord_a").Should().BeTrue();
         SecretLoverAdmissionPolicy.SubjectsRevealedTo(subjects, "lord_a").Should().ContainSingle().Which.Should().Be("companion_x");
      }

      // Confessing your bond to the very person you secretly love tells them nothing: they ARE the secret. Teaching it
      // there would let the NPC record themselves as "knowing" a fact about their own bond, muddying propagation.
      [Test]
      public void GIVEN_the_only_secret_lover_is_the_listener_WHEN_checking_THEN_nothing_is_revealed()
      {
         var subjects = new List<string> {"companion_x"};

         SecretLoverAdmissionPolicy.CanAdmitTo(subjects, "companion_x").Should().BeFalse();
         SecretLoverAdmissionPolicy.SubjectsRevealedTo(subjects, "companion_x").Should().BeEmpty();
      }

      // Multiple bonds, one of them being the listener: only the OTHER subjects are newly revealed to this NPC; the
      // listener is filtered out so a lord is never recorded as knowing a secret he himself embodies.
      [Test]
      public void GIVEN_several_secret_lovers_one_being_the_listener_WHEN_checking_THEN_only_the_others_are_revealed()
      {
         var subjects = new List<string> {"companion_x", "companion_y", "lord_a"};

         var revealed = SecretLoverAdmissionPolicy.SubjectsRevealedTo(subjects, "lord_a");

         revealed.Should().BeEquivalentTo(new[] {"companion_x", "companion_y"});
      }

      // A null or empty subject id in the list is not a real bond and must never be recorded as knowledge, or the
      // ledger fills with meaningless "lord|" keys.
      [Test]
      public void GIVEN_a_blank_subject_id_WHEN_checking_THEN_it_is_ignored()
      {
         var subjects = new List<string> {"", null, "companion_x"};

         SecretLoverAdmissionPolicy.SubjectsRevealedTo(subjects, "lord_a").Should().ContainSingle().Which.Should().Be("companion_x");
      }

      // A null subject collection (no store, no clan yet) must degrade to "nothing revealed", never throw: eligibility
      // is checked on every prompt build and a throw there would break the whole conversation.
      [Test]
      public void GIVEN_a_null_subject_collection_WHEN_checking_THEN_nothing_is_revealed_and_it_does_not_throw()
      {
         SecretLoverAdmissionPolicy.CanAdmitTo(null, "lord_a").Should().BeFalse();
         SecretLoverAdmissionPolicy.SubjectsRevealedTo(null, "lord_a").Should().BeEmpty();
      }
   }
}
