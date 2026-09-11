// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A: is there a real triangle worth a duel? All three facts (a real courtship, a real rival
// desire, and a rival bold enough to force it) must hold at once. Without the triangle actually existing, a
// duel over a woman nobody is courting is nonsense; without rival boldness, a timid rival would never force it
// to steel in the first place.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class CourtshipRivalryEligibilityTests
   {
      // The all-true case is the only "yes": a real triangle, a real rival, and a rival with the nerve to act.
      [Test]
      public void GIVEN_all_three_facts_hold_WHEN_asked_THEN_the_rival_should_challenge()
      {
         CourtshipRivalryEligibility.ShouldChallenge(new CourtshipRivalryEligibility.RivalryFacts {PlayerIsCourtingHer = true, RivalDesiresHer = true, RivalIsBoldEnough = true}).Should().BeTrue();
      }

      // Without the player actually courting her, there is no triangle at all: a duel over a woman nobody is
      // courting is nonsense, so this alone must block the challenge.
      [Test]
      public void GIVEN_the_player_is_not_courting_her_WHEN_asked_THEN_the_rival_should_not_challenge()
      {
         CourtshipRivalryEligibility.ShouldChallenge(new CourtshipRivalryEligibility.RivalryFacts {PlayerIsCourtingHer = false, RivalDesiresHer = true, RivalIsBoldEnough = true}).Should().BeFalse();
      }

      // Without the rival genuinely wanting her too, there is no rival: nobody duels over a woman they do not
      // themselves desire.
      [Test]
      public void GIVEN_the_rival_does_not_desire_her_WHEN_asked_THEN_the_rival_should_not_challenge()
      {
         CourtshipRivalryEligibility.ShouldChallenge(new CourtshipRivalryEligibility.RivalryFacts {PlayerIsCourtingHer = true, RivalDesiresHer = false, RivalIsBoldEnough = true}).Should().BeFalse();
      }

      // Without boldness, a timid rival would never force the matter with steel, however jealous he is.
      [Test]
      public void GIVEN_the_rival_is_not_bold_enough_WHEN_asked_THEN_the_rival_should_not_challenge()
      {
         CourtshipRivalryEligibility.ShouldChallenge(new CourtshipRivalryEligibility.RivalryFacts {PlayerIsCourtingHer = true, RivalDesiresHer = true, RivalIsBoldEnough = false}).Should().BeFalse();
      }
   }
}
