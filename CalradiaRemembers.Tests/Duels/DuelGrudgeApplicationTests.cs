// Code written by Gabriel Mailhot, 19/09/2026.
// What these pin: that a duel fought over a grievance actually DOES something to the grievance.
//
// Since the duels pillar shipped, DuelOutcomePolicy computed "settled" for a fought duel and "hardened" for a ducked
// one, and nothing ever applied either: the applier logged the verdict and left the grudge as it was, because there
// was no way to deepen a grudge and applying only the settle half would have taught that a duel can only ever help.
// A player who crossed blades with a lord over a grievance walked away with the grievance intact, and the lord went
// on nursing it as though the fight had never happened.

#region

using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelGrudgeApplicationTests
   {
      private const int Today = 300;

      // STEEL CLEARS THE AIR, whoever won: the pillar's promise, finally kept.
      [Test]
      public void GIVEN_a_duel_fought_over_a_grievance_WHEN_its_verdict_is_applied_THEN_the_grievance_is_resolved()
      {
         Grudge grudge = Mendable();

         DuelOutcomePolicy.ApplyGrudgeEffect(grudge, DuelGrudgeEffect.Settled, Today).Should().BeTrue();

         grudge.IsResolved.Should().BeTrue();
         grudge.ResolvedDay.Should().Be(Today);
      }

      // AND WALKING AWAY MAKES IT FESTER, the half that could not be applied before.
      [Test]
      public void GIVEN_a_duel_ducked_over_a_grievance_WHEN_its_verdict_is_applied_THEN_the_grievance_deepens()
      {
         Grudge grudge = Mendable();

         DuelOutcomePolicy.ApplyGrudgeEffect(grudge, DuelGrudgeEffect.Hardened, Today).Should().BeTrue();

         grudge.Magnitude.Should().Be(50 + DuelOutcomePolicy.GrudgeHardenedByForfeit);
         grudge.IsResolved.Should().BeFalse();
      }

      // The two halves end to end, from how the duel ended to what became of the grievance, through the real policy.
      [TestCase(DuelResolution.PlayerWon, true)]
      [TestCase(DuelResolution.NpcWon, true)]
      [TestCase(DuelResolution.PlayerForfeited, false)]
      [TestCase(DuelResolution.NpcForfeited, false)]
      public void GIVEN_how_a_grudge_duel_ended_WHEN_applied_THEN_fought_resolves_and_ducked_deepens(DuelResolution how, bool resolved)
      {
         Grudge grudge = Mendable();
         DuelOutcomeResult outcome = DuelOutcomePolicy.Resolve(new DuelResolutionContext {
            Resolution = how, AGrudgeDroveIt = true, Stake = DuelStakeKind.Honour
         });

         DuelOutcomePolicy.ApplyGrudgeEffect(grudge, outcome.GrudgeEffect, Today);

         grudge.IsResolved.Should().Be(resolved);
         if (!resolved) grudge.Magnitude.Should().BeGreaterThan(50);
      }

      // A dead kinsman is not answered by steel, and a vendetta cannot be made worse than unforgivable.
      [TestCase(DuelGrudgeEffect.Settled)]
      [TestCase(DuelGrudgeEffect.Hardened)]
      public void GIVEN_a_vendetta_WHEN_a_duel_verdict_is_applied_THEN_it_is_untouched(DuelGrudgeEffect effect)
      {
         Grudge grudge = Mendable();
         grudge.Rigidity = GrudgePolicy.VendettaRigidity;

         DuelOutcomePolicy.ApplyGrudgeEffect(grudge, effect, Today).Should().BeFalse();

         grudge.IsResolved.Should().BeFalse();
         grudge.Magnitude.Should().Be(50);
      }

      [Test]
      public void GIVEN_no_grudge_in_play_WHEN_applied_THEN_nothing_happens()
      {
         DuelOutcomePolicy.ApplyGrudgeEffect(null, DuelGrudgeEffect.Settled, Today).Should().BeFalse();
         DuelOutcomePolicy.ApplyGrudgeEffect(Mendable(), DuelGrudgeEffect.None, Today).Should().BeFalse();
      }

      private static Grudge Mendable() => new() {
         Id = "d", HolderId = "lord_1", TargetId = "main_hero", Source = GrudgeSource.Jealousy,
         CauseSummary = "an insult at the feast", Magnitude = 50, Rigidity = 40, KnownToPlayer = true, EventDay = 100
      };
   }
}
