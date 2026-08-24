// Code written by Gabriel Mailhot, 23/08/2026.
// This policy decides how many AMBIENT hero witnesses a gathered conversation actually keeps, once the resolver
// has already collected them (WitnessResolver.ResolveAutoWitnesses / ApplyWitnessDensity). Its answer feeds
// straight into the system prompt: every witness it keeps gets a voice, a portrait, and a slot in [WITNESS_REACTION]
// exchanges, so an over-generous keep means the same crowded room players complained about, and an over-eager
// thinning means a lord's own audience quietly loses its retinue for no reason the player can see. The mod is
// LIVE, so the single rule every test here ultimately serves is: Bustling, the shipped default, must change
// NOTHING for a player who never opens the menu. The rng is injected so the crowd stays unpredictable in play
// (backed by MBRandom in the engine) while remaining fully deterministic here.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WitnessPresencePolicyTests
   {
      private static readonly List<string> FourWitnesses = new() {"Aldric", "Boyd", "Corwin", "Derthert"};

      // The load-bearing guarantee of the whole feature: the mod is LIVE, and this setting must be pure opt-in.
      // If Bustling ever removed anyone, every existing player's conversations would change the moment they
      // updated, for a setting they never touched.
      [Test]
      public void GIVEN_Bustling_WHEN_filtering_a_large_room_THEN_every_witness_is_kept_unchanged()
      {
         var large = new List<string> {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J"};

         List<string> result = WitnessPresencePolicy.Filter(large, WitnessDensity.Bustling, () => 0.0);

         result.Should().Equal(large);
      }

      // Bustling must be a passthrough even at the worst-case roll (just under 1.0): if the probability check
      // were ever mis-written as "<=" instead of "<", a value this close to 1.0 could slip through the cap and
      // silently start thinning the default experience.
      [Test]
      public void GIVEN_Bustling_WHEN_the_rng_returns_just_under_one_THEN_nothing_is_dropped()
      {
         List<string> result = WitnessPresencePolicy.Filter(FourWitnesses, WitnessDensity.Bustling, () => 0.999999);

         result.Should().Equal(FourWitnesses);
      }

      // Sparse exists to make private moments common again (the player complaint this feature answers). A cap
      // of 1 that quietly let a second witness through would defeat the entire point of choosing Sparse.
      [Test]
      public void GIVEN_Sparse_WHEN_every_witness_would_pass_the_probability_roll_THEN_the_room_is_still_capped_to_one()
      {
         List<string> result = WitnessPresencePolicy.Filter(FourWitnesses, WitnessDensity.Sparse, () => 0.0);

         result.Should().HaveCount(1);
         result.Should().Equal("Aldric");
      }

      // Normal is the advertised "genuine middle ground": if its cap were mis-set to 1 it would be Sparse in
      // disguise, and if it were left uncapped it would be Bustling in disguise, either way the player would
      // pick a setting and feel nothing change.
      [Test]
      public void GIVEN_Normal_WHEN_every_witness_would_pass_the_probability_roll_THEN_the_room_is_capped_to_two()
      {
         List<string> result = WitnessPresencePolicy.Filter(FourWitnesses, WitnessDensity.Normal, () => 0.0);

         result.Should().HaveCount(2);
         result.Should().Equal("Aldric", "Boyd");
      }

      // The exact boundary the spec pins: an rng returning a value strictly BELOW the level's probability keeps
      // the witness. Get this comparison backwards and every density level either keeps everyone (roll <= p
      // mis-read as always true) or drops everyone (roll < p mis-read with p and roll swapped).
      [Test]
      public void GIVEN_Normal_WHEN_the_rng_returns_just_below_the_probability_THEN_the_witness_is_kept()
      {
         // Normal's probability is 0.5; 0.499999 must fall on the KEEP side of the cutoff.
         List<string> result = WitnessPresencePolicy.Filter(new List<string> {"Solo"}, WitnessDensity.Normal, () => 0.499999);

         result.Should().Equal("Solo");
      }

      // The other half of the same boundary: a roll landing exactly ON the probability, or above it, must drop
      // the witness. Without this half, a "<=" vs "<" typo would go undetected by the just-below test alone.
      [Test]
      public void GIVEN_Normal_WHEN_the_rng_returns_exactly_the_probability_THEN_the_witness_is_dropped()
      {
         List<string> result = WitnessPresencePolicy.Filter(new List<string> {"Solo"}, WitnessDensity.Normal, () => 0.5);

         result.Should().BeEmpty();
      }

      // Same boundary again for Sparse, at its own (lower) cutoff, so a mapping bug that swapped Sparse's and
      // Normal's probabilities would still be caught even though both are "< p" comparisons.
      [Test]
      public void GIVEN_Sparse_WHEN_the_rng_straddles_its_own_cutoff_THEN_below_keeps_and_at_or_above_drops()
      {
         WitnessPresencePolicy.Filter(new List<string> {"Solo"}, WitnessDensity.Sparse, () => 0.249999)
            .Should().Equal("Solo");
         WitnessPresencePolicy.Filter(new List<string> {"Solo"}, WitnessDensity.Sparse, () => 0.25)
            .Should().BeEmpty();
      }

      // A resolver that gathered nobody (a lone notable with no hero witnesses at all) must not throw or
      // fabricate an entry; the chat should proceed exactly as it does today with zero witnesses.
      [Test]
      public void GIVEN_an_empty_eligible_list_WHEN_filtering_at_any_density_THEN_the_result_is_empty()
      {
         WitnessPresencePolicy.Filter(new List<string>(), WitnessDensity.Bustling, () => 0.0).Should().BeEmpty();
         WitnessPresencePolicy.Filter(new List<string>(), WitnessDensity.Normal, () => 0.0).Should().BeEmpty();
         WitnessPresencePolicy.Filter(new List<string>(), WitnessDensity.Sparse, () => 0.0).Should().BeEmpty();
      }

      // A null eligible list must degrade the same way an empty one does (fail-soft, matching WitnessResolver's
      // own "never throw, return what you can" contract) rather than crash the conversation mid-turn.
      [Test]
      public void GIVEN_a_null_eligible_list_WHEN_filtering_THEN_the_result_is_an_empty_list_not_a_crash()
      {
         WitnessPresencePolicy.Filter<string>(null, WitnessDensity.Normal, () => 0.0).Should().NotBeNull().And.BeEmpty();
      }

      // Sparse and Normal's caps (1 and 2) are both smaller than a two-witness room, so this proves the cap
      // never THROWS or pads the list when there is less to cap than the ceiling allows, only the mapping
      // itself changes what gets kept.
      [Test]
      public void GIVEN_a_room_smaller_than_the_cap_WHEN_filtering_THEN_the_whole_room_survives()
      {
         var two = new List<string> {"Aldric", "Boyd"};

         WitnessPresencePolicy.Filter(two, WitnessDensity.Normal, () => 0.0).Should().Equal(two);
      }

      // The list order is the resolver's own gathering order (settlement pass 1 then pass 2, or campaign-map
      // roster order). Silently reordering survivors would make "who speaks first" nondeterministic from the
      // player's point of view even though the underlying gather order never changed.
      [Test]
      public void GIVEN_several_witnesses_survive_the_roll_WHEN_capped_THEN_their_original_order_is_preserved()
      {
         List<string> result = WitnessPresencePolicy.Filter(FourWitnesses, WitnessDensity.Normal, () => 0.0);

         result.Should().Equal("Aldric", "Boyd"); // first two in ORIGINAL order, not reshuffled
      }

      // A caller that forgot to wire the engine's random source (a null Func) must not crash a live
      // conversation; it degrades to "nobody survives the roll" rather than throwing mid-turn, which a
      // NullReferenceException from inside a witness-gathering call would do.
      [Test]
      public void GIVEN_a_null_rng_WHEN_filtering_THEN_it_degrades_to_an_empty_room_instead_of_throwing()
      {
         WitnessPresencePolicy.Filter(FourWitnesses, WitnessDensity.Normal, null).Should().BeEmpty();
      }

      // The mapping itself is the contract every other test above assumes; pinning the raw numbers here means
      // a future edit to the mapping table shows up as a failure on THIS test specifically, not as a mysterious
      // failure three tests away that used the numbers implicitly.
      [Test]
      public void GIVEN_the_three_density_levels_WHEN_their_probability_and_cap_are_read_THEN_they_match_the_shipped_mapping()
      {
         WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Sparse).Should().Be(0.25);
         WitnessPresencePolicy.CapFor(WitnessDensity.Sparse).Should().Be(1);

         WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Normal).Should().Be(0.5);
         WitnessPresencePolicy.CapFor(WitnessDensity.Normal).Should().Be(2);

         // Bustling's mapping IS the live-behaviour guarantee: p=1.0 means nobody is ever rolled away, and an
         // uncapped ceiling means the resolver's own existing cap (MaxLordWitnesses) is the only limit left.
         WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Bustling).Should().Be(1.0);
         WitnessPresencePolicy.CapFor(WitnessDensity.Bustling).Should().Be(int.MaxValue);
      }

      // Each step up the dial must genuinely mean a fuller room, or the setting is a dial the player cannot
      // feel: Sparse < Normal < Bustling on both the chance to keep any one witness and the ceiling on how many.
      [Test]
      public void GIVEN_the_three_levels_WHEN_ordered_THEN_each_one_up_genuinely_means_a_fuller_room()
      {
         WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Sparse)
            .Should().BeLessThan(WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Normal));
         WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Normal)
            .Should().BeLessThan(WitnessPresencePolicy.ProbabilityFor(WitnessDensity.Bustling));

         WitnessPresencePolicy.CapFor(WitnessDensity.Sparse)
            .Should().BeLessThan(WitnessPresencePolicy.CapFor(WitnessDensity.Normal));
         WitnessPresencePolicy.CapFor(WitnessDensity.Normal)
            .Should().BeLessThan(WitnessPresencePolicy.CapFor(WitnessDensity.Bustling));
      }
   }
}
