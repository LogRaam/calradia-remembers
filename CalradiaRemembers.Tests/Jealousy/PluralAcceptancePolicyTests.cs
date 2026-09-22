// Code written by Gabriel Mailhot, 22/09/2026.
// What these pin: who can be asked to accept a plural household, and who cannot be asked at all.
//
// This is the second half of Gabriel's ruling of 22/09/2026. The first half (AcceptedPluralJealousyTests) says
// what her acceptance COSTS her once given. This says whether she can give it, and it exists because of the
// objection he raised against the first draft: "une Vlandienne qui culturellement n'accepte pas le harem
// pourrait l'accepter sans consequence?"
//
// Without a gate here, one sentence from the model would dissolve a culture. With a gate that refuses outright,
// every Vlandian becomes the same woman. So the culture sets a PRICE: a people who already share partners ask
// nothing extra, and a people who call it betrayal ask for devotion, because that is what agreeing to bear it
// actually costs.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Jealousy
{
   [TestFixture]
   public sealed class PluralAcceptancePolicyTests
   {
      private static PluralAcceptanceFacts AVlandianWife(int regard) => new() {
         IsCommittedPartner = true,
         RegardWithPlayer = regard
      };

      // A DEVOTED WIFE OF AN EXCLUSIVE PEOPLE may be asked. yozakura12's Corein is exactly this: her people hold
      // marriage to one alone, and she says she accepts the harem as long as she remains senior. Refusing to let
      // her say it would make every Vlandian the same woman, which is the opposite of what this mod is for.
      [Test]
      public void GIVEN_a_devoted_wife_of_an_exclusive_people_WHEN_the_question_arises_THEN_she_may_say_she_accepts()
         => PluralAcceptancePolicy.CanBeOffered(AVlandianWife(Regard.DevotedTier)).Should().BeTrue();

      // AND A MERELY FOND ONE MAY NOT. This is the gate that keeps the cultures meaning something: a woman
      // agrees to bear what her own people condemn for someone she is devoted to, not for a husband she is
      // simply on good terms with. The model is never taught the verb below the rung, so it cannot obtain in
      // one sentence what her culture spends a lifetime refusing.
      [Test]
      public void GIVEN_a_wife_of_an_exclusive_people_who_is_merely_fond_WHEN_the_question_arises_THEN_she_cannot_be_asked()
      {
         PluralAcceptancePolicy.CanBeOffered(AVlandianWife(Regard.DevotedTier - 1)).Should().BeFalse();
         PluralAcceptancePolicy.WhyNot(AVlandianWife(Regard.DevotedTier - 1))
                               .Should().Contain("not yet devoted enough",
                                  "the prompt says the closed door out loud rather than being refuted after the fact");
      }

      // A PEOPLE WHO ALREADY SHARE ask nothing extra, because she has nothing to overcome: her own custom keeps
      // plural households. Requiring devotion of her too would be charging her for a thing that is free.
      [Test]
      public void GIVEN_a_wife_whose_people_already_share_partners_WHEN_the_question_arises_THEN_no_devotion_is_required()
         => PluralAcceptancePolicy.CanBeOffered(new PluralAcceptanceFacts {
               IsCommittedPartner = true,
               CultureToleratesSharing = true,
               RegardWithPlayer = 0
            }).Should().BeTrue();

      // A FLIRTATION HAS NOTHING TO ACCEPT WITH. The same guard open_relationship keeps, for the same reason:
      // there is no household to be part of until there is a bond.
      [Test]
      public void GIVEN_a_woman_who_is_not_a_committed_partner_WHEN_the_question_arises_THEN_there_is_nothing_for_her_to_accept()
      {
         var stranger = new PluralAcceptanceFacts {RegardWithPlayer = 100, CultureToleratesSharing = true};

         PluralAcceptancePolicy.CanBeOffered(stranger).Should().BeFalse();
         PluralAcceptancePolicy.WhyNot(stranger).Should().Contain("not a committed partner");
      }

      // SAYING IT TWICE IS NOT A THING TO TEACH. Without this the model would be offered the verb every turn
      // after she had already agreed, and the bridge would refuse it every turn: the empty-promise shape.
      [Test]
      public void GIVEN_a_partner_who_has_already_said_it_WHEN_the_question_arises_THEN_she_is_not_asked_again()
      {
         PluralAcceptanceFacts already = AVlandianWife(80);
         already.AlreadyAccepted = true;

         PluralAcceptancePolicy.CanBeOffered(already).Should().BeFalse();
         PluralAcceptancePolicy.WhyNot(already).Should().Contain("already said it");
      }

      // AND OPEN TERMS ARE WIDER THAN THIS. A couple who hold reciprocal open terms have already settled a
      // larger question; offering her the narrower one would read as the mod not knowing what it had agreed to.
      [Test]
      public void GIVEN_a_couple_who_already_hold_open_terms_WHEN_the_question_arises_THEN_the_narrower_one_is_not_offered()
      {
         PluralAcceptanceFacts open = AVlandianWife(80);
         open.AlreadyOnOpenTerms = true;

         PluralAcceptancePolicy.CanBeOffered(open).Should().BeFalse();
         PluralAcceptancePolicy.WhyNot(open).Should().Contain("already hold open terms");
      }

      // THE RUNG IS A NAMED ONE, not a number chosen here. If devotion ever moves, this moves with it, and the
      // decision table keeps its meaning.
      [Test]
      public void GIVEN_the_bar_for_accepting_against_your_own_culture_WHEN_read_THEN_it_is_devotion_itself()
         => PluralAcceptancePolicy.DevotionRequiredAgainstCulture.Should().Be(Regard.DevotedTier);
   }
}
