// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0: once a challenge is admissible, does the NPC actually take the field? This is where the
// feature lives or dies as characterisation. The ratified character (ROADMAP): Valour and Honour lean in, a
// fearful Calculating slips out, an affronted grudge-holder demands it, and, crucially, a duel is NEVER
// casual, so with no real push the answer is no. If the default drifted to "accept", every idle chat would
// end in a brawl; if a burning grudge could not overcome caution, the single most dramatic duel (settling a
// vendetta with steel) would never happen. These tests pin the named forces and, at the seams, the exact
// score so the curve cannot silently drift under a later tweak.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelAcceptancePolicyTests
   {
      // The default is the whole safety valve: a plain lord with no grudge, no fear, no crowd, and no strong
      // trait either way must REFUSE. Without this, an admissible challenge would be accepted by anyone at any
      // time and the map would dissolve into constant duels.
      [Test]
      public void GIVEN_no_reason_to_fight_WHEN_deciding_THEN_the_default_is_to_refuse()
      {
         DuelAcceptancePolicy.Decide(Ctx()).Should().Be(DuelResponse.Refuse);
      }

      // A brave, honourable lord called out before a crowd is the archetype who MUST fight: bravery and honour
      // both lean in, and the public setting turns honour into near-compulsion. If this refused, the most
      // obviously correct duel in the game would not happen.
      [Test]
      public void GIVEN_a_brave_honourable_lord_challenged_before_witnesses_WHEN_deciding_THEN_he_accepts()
      {
         DuelAcceptanceContext ctx = Ctx(honor: 2, valor: 2);
         ctx.ChallengedBeforeWitnesses = true;

         DuelAcceptancePolicy.Decide(ctx).Should().Be(DuelResponse.Accept);
      }

      // A live grudge is the strongest single pull, enough to carry a merely cautious lord onto the field: this
      // is "let us settle this with steel", the duel the Grudges pillar most wants as a resolution verb. If a
      // grudge could not overcome ordinary caution, the pillar's marquee moment would be unreachable.
      [Test]
      public void GIVEN_a_live_grudge_drives_it_WHEN_deciding_THEN_even_a_cautious_lord_takes_the_field()
      {
         DuelAcceptanceContext ctx = Ctx();
         ctx.AGrudgeDrivesIt = true;

         DuelAcceptancePolicy.Decide(ctx).Should().Be(DuelResponse.Accept);
      }

      // The signature refusal: a calculating man who genuinely fears the player's prowess, with no grudge and no
      // crowd to shame him, slips out of a fight he would lose. This is the "fearful Calculating slips out" rule,
      // and it must beat any faint inclination to fight, or cowards would be indistinguishable from the brave.
      [Test]
      public void GIVEN_a_calculating_coward_who_fears_the_player_WHEN_deciding_privately_THEN_he_refuses()
      {
         DuelAcceptanceContext ctx = Ctx(calculating: 2, honor: -1, valor: -1);
         ctx.Stance.Fear = 70;

         DuelAcceptancePolicy.Decide(ctx).Should().Be(DuelResponse.Refuse);
      }

      // Calculating only becomes cowardice when there is something real to fear: with low fear it must have NO
      // effect, so a calculating lord and a plain one score identically below the threshold. At the threshold
      // the calculating one drops below the plain one. Comparing the two natures at the same fear pins the exact
      // boundary without depending on any rounding of the fear term. Without this, the trait would read as
      // blanket timidity when its whole point is cold self-interest that only avoids a fight it would lose.
      [Test]
      public void GIVEN_a_calculating_lord_with_little_fear_WHEN_deciding_THEN_the_slip_rule_does_not_bite_until_the_threshold()
      {
         int threshold = DuelAcceptancePolicy.FearCowardiceThreshold;

         DuelAcceptanceContext calcBelow = Ctx(calculating: 2);
         calcBelow.Stance.Fear = threshold - 1;
         DuelAcceptanceContext plainBelow = Ctx(calculating: 0);
         plainBelow.Stance.Fear = threshold - 1;
         DuelAcceptancePolicy.Score(calcBelow).Should().Be(DuelAcceptancePolicy.Score(plainBelow),
            "below the fear threshold, a calculating nature is dormant and changes nothing");

         DuelAcceptanceContext calcAt = Ctx(calculating: 2);
         calcAt.Stance.Fear = threshold;
         DuelAcceptanceContext plainAt = Ctx(calculating: 0);
         plainAt.Stance.Fear = threshold;
         DuelAcceptancePolicy.Score(calcAt).Should().BeLessThan(DuelAcceptancePolicy.Score(plainAt),
            "at the fear threshold, the calculating nature turns real fear into a calculated exit");
      }

      // Being called out in PUBLIC must move an honourable lord who would have ducked in private: the witness
      // bonus is the social pressure that makes a challenge before a crowd meaningfully different from a quiet
      // one. If public and private challenges scored the same, "shame him into it before witnesses" would be empty.
      [Test]
      public void GIVEN_the_same_honourable_lord_WHEN_challenged_publicly_versus_privately_THEN_the_public_score_is_higher()
      {
         DuelAcceptanceContext priv = Ctx(honor: 2);
         DuelAcceptanceContext pub = Ctx(honor: 2);
         pub.ChallengedBeforeWitnesses = true;

         DuelAcceptancePolicy.Score(pub).Should().BeGreaterThan(DuelAcceptancePolicy.Score(priv));
      }

      // A dishonourable man before a crowd ducks EVEN HARDER, not less: the witness term multiplies his negative
      // honour, so public exposure deepens a coward's refusal. Without a signed witness term, publicity would
      // wrongly push everyone toward fighting regardless of character.
      [Test]
      public void GIVEN_a_dishonourable_lord_WHEN_challenged_publicly_THEN_the_crowd_pushes_him_further_from_the_field()
      {
         DuelAcceptanceContext priv = Ctx(honor: -2);
         DuelAcceptanceContext pub = Ctx(honor: -2);
         pub.ChallengedBeforeWitnesses = true;

         DuelAcceptancePolicy.Score(pub).Should().BeLessThan(DuelAcceptancePolicy.Score(priv));
      }

      // A null context must lean nowhere and score zero (a refusal): a missing situation can never be read as a
      // reason to fight.
      [Test]
      public void GIVEN_no_context_WHEN_scoring_THEN_it_is_zero_and_refuses()
      {
         DuelAcceptancePolicy.Score(null).Should().Be(0);
         DuelAcceptancePolicy.Decide(null).Should().Be(DuelResponse.Refuse);
      }

      private static DuelAcceptanceContext Ctx(int honor = 0, int valor = 0, int calculating = 0)
         => new() {
            Traits = new TraitFacts {Honor = honor, Valor = valor, Calculating = calculating},
            Stance = new NpcStance {Fear = 0},
            AGrudgeDrivesIt = false,
            ChallengedBeforeWitnesses = false
         };
   }
}
