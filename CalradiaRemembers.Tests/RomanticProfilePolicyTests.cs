// Code written by Gabriel Mailhot, 13/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RomanticProfilePolicyTests
   {
      // A hero with few trait-gated leanings can roll a count higher than its own pool size (e.g. a
      // near-neutral trait set with a small preference pool). Must clamp instead of throwing, or a routine
      // profile build would crash rather than simply returning everything the pool has.
      [Test]
      public void GIVEN_a_count_larger_than_the_pool_WHEN_picking_distinct_entries_THEN_it_is_clamped_to_the_pool_size()
      {
         HashSet<Kink> pool = new() {Kink.Roleplay, Kink.Praise};

         RomanticProfilePolicy.PickDistinct(pool, 10, new Random(1)).Should().HaveCount(2);
      }

      // ── PickDistinct ──────────────────────────────────────────────────────

      // This shared shuffle-and-take helper feeds both DeterminePreferences and DetermineKinks. A duplicate
      // pick would show the same preference or kink listed twice to the LLM/player, so the Fisher-Yates
      // shuffle must never emit the same pool entry more than once.
      [Test]
      public void GIVEN_a_pool_larger_than_the_count_WHEN_picking_distinct_entries_THEN_no_duplicates_are_returned()
      {
         HashSet<Kink> pool = new() {Kink.Roleplay, Kink.PublicAffection, Kink.Voyeurism, Kink.Exhibitionism, Kink.Praise};

         List<Kink> picked = RomanticProfilePolicy.PickDistinct(pool, 3, new Random(1234));

         picked.Should().HaveCount(3);
         picked.Distinct().Should().HaveCount(3);
         picked.Should().OnlyContain(k => pool.Contains(k));
      }

      // Pins the lower edge of the BiCurious band: one point below is still Heterosexual, so a boundary bug
      // (< vs <=) would silently shift how many NPCs are opposite-sex-only across a whole campaign's worth
      // of rolls.
      [Test]
      public void GIVEN_a_roll_of_65_WHEN_determining_orientation_THEN_it_is_bi_curious()
      {
         RomanticProfilePolicy.DetermineOrientation(new FixedRoll(65)).Should().Be(SexualOrientation.BiCurious);
      }

      // Pins the upper edge of the BiCurious band, the widest of the three: paired with the roll-of-90 test
      // below, fixes exactly where BiCurious ends and Bisexual begins so a retune can't silently move it.
      [Test]
      public void GIVEN_a_roll_of_89_WHEN_determining_orientation_THEN_it_is_still_bi_curious()
      {
         RomanticProfilePolicy.DetermineOrientation(new FixedRoll(89)).Should().Be(SexualOrientation.BiCurious);
      }

      // The far side of that same boundary: full Bisexual is the rarest band (10 of 100). Getting this edge
      // wrong would either erase the band entirely or bleed it into BiCurious.
      [Test]
      public void GIVEN_a_roll_of_90_WHEN_determining_orientation_THEN_it_is_bisexual()
      {
         RomanticProfilePolicy.DetermineOrientation(new FixedRoll(90)).Should().Be(SexualOrientation.Bisexual);
      }

      // Pins the upper edge of the 2-kink band (85..96): the very next roll starts the rarest, 3-kink band,
      // so this fixes exactly how far "moderately kinked" extends before an NPC becomes an outlier.
      [Test]
      public void GIVEN_a_roll_of_96_WHEN_determining_kinks_THEN_two_kinks_are_rolled()
      {
         RomanticProfilePolicy.DetermineKinks(new FixedRoll(96), new RomanticTraits {Mercy = 1}).Should().HaveCount(2);
      }

      // The rarest band: only rolls 97-99 (3 of 100) stack three kinks on one NPC. Getting this boundary
      // wrong in either direction would make heavily-kinked NPCs either common or impossible to roll.
      [Test]
      public void GIVEN_a_roll_of_97_WHEN_determining_kinks_THEN_three_kinks_are_rolled()
      {
         RomanticProfilePolicy.DetermineKinks(new FixedRoll(97), new RomanticTraits {Mercy = -1}).Should().HaveCount(3);
      }

      // ── DetermineKinks roll bands (count) ────────────────────────────────

      // Per the method's own doc, the count is "weighted toward 0": the majority band (60 of 100 rolls) gets
      // NO kinks at all, so most NPCs stay ordinary and distinctive kink content remains the exception, not
      // the norm, across a full roster.
      [Test]
      public void GIVEN_a_roll_under_60_WHEN_determining_kinks_THEN_no_kink_is_rolled()
      {
         RomanticProfilePolicy.DetermineKinks(new FixedRoll(0), Neutral()).Should().BeEmpty();
      }

      // ── DetermineOrientation roll bands ──────────────────────────────────

      // Per the method's own XML doc: Homosexual, Pansexual and Asexual stay in the enum for mod use but are
      // never assigned here, because vanilla lords marry the opposite gender by game mechanic, and rolling
      // exclusive homosexuality would produce a lord whose marriage contradicts their own orientation. The
      // roll only ever lands on Heterosexual/BiCurious/Bisexual, all opposite-sex-compatible. Majority band.
      [Test]
      public void GIVEN_a_roll_under_65_WHEN_determining_orientation_THEN_it_is_heterosexual()
      {
         RomanticProfilePolicy.DetermineOrientation(new FixedRoll(64)).Should().Be(SexualOrientation.Heterosexual);
      }

      // High Calculating ("nothing about their approach is accidental... the architecture is deliberate")
      // is the largest leaning set (control, ownership, sensory deprivation, training). A deliberate,
      // strategic personality should plausibly extend that same control into intimacy, not roll it at random.
      [Test]
      public void GIVEN_high_calculating_WHEN_building_the_kink_pool_THEN_it_gains_control_and_ownership_kinks()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Calculating = 1});

         pool.Should().Contain(new[] {
            Kink.Dominance, Kink.Possessiveness, Kink.BondageGiving, Kink.OrgasmControl, Kink.Chastity,
            Kink.Objectification, Kink.SensoryDeprivation, Kink.Training, Kink.MasterSlave, Kink.PetPlay
         });
      }

      // High Honor ("their word means something in love as in all things") reads as sanctioned, traditional
      // intimacy (Breeding, Praise), not the transgressive set. Pins that the honor axis colours kink FLAVOUR,
      // not just the relational sketch prose.
      [Test]
      public void GIVEN_high_honor_WHEN_building_the_kink_pool_THEN_it_gains_breeding_and_praise()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Honor = 1});

         pool.Should().Contain(new[] {Kink.Breeding, Kink.Praise});
      }

      // Mirrors the low-Mercy case, and the NotContain(Dominance) assertion is the important half: a kind,
      // high-Mercy NPC ("genuine warmth... forgives, considers, shows up") must never be able to roll the
      // opposite leaning, or the two trait bands stop being mutually exclusive and characterization breaks.
      [Test]
      public void GIVEN_high_mercy_WHEN_building_the_kink_pool_THEN_it_gains_submission_leaning_kinks()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Mercy = 1});

         pool.Should().Contain(new[] {Kink.Submission, Kink.BondageReceiving, Kink.Masochism, Kink.Praise});
         pool.Should().NotContain(Kink.Dominance);
      }

      // Low Honor ("oaths sit lightly... warmth may be real without the reliability") reads as the
      // transgressive set (power imbalance, corruption, degradation): the same trait that makes a character
      // unreliable in love also makes their intimate leanings less bound by convention.
      [Test]
      public void GIVEN_low_honor_WHEN_building_the_kink_pool_THEN_it_gains_power_imbalance_and_corruption()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Honor = -1});

         pool.Should().Contain(new[] {Kink.PowerImbalance, Kink.SwitchTendencies, Kink.CorruptionKink, Kink.Degradation});
      }

      // A cruel NPC (low Mercy) must roll dominance/sadism-leaning kinks, matching the "no softness... direct,
      // demanding" character BuildTraitBasedSketches already writes for the same trait. Keeps the intimate
      // content coherent with the personality the player is told about, instead of being independently random.
      [Test]
      public void GIVEN_low_mercy_WHEN_building_the_kink_pool_THEN_it_gains_dominance_and_sadism_leaning_kinks()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Mercy = -1});

         pool.Should().Contain(new[] {
            Kink.Dominance, Kink.Sadism, Kink.BondageGiving, Kink.PowerImbalance,
            Kink.Degradation, Kink.FreeUse, Kink.ImpactPlay, Kink.FearPlay, Kink.Objectification
         });
      }
      // ── BuildKinkPool trait correlations ─────────────────────────────────

      // The floor: BuildKinkPool always seeds the small base pool (per its own XML doc), but an "ordinary"
      // NPC with no trait pushing either way must gain none of the trait-gated leanings by accident. Without
      // this floor a bug that always adds a leaning set would go unnoticed on the common, neutral-trait case.
      [Test]
      public void GIVEN_neutral_traits_WHEN_building_the_kink_pool_THEN_only_the_base_pool_is_present()
      {
         HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(Neutral());

         pool.Should().BeEquivalentTo(new[] {Kink.Roleplay, Kink.PublicAffection, Kink.Voyeurism, Kink.Exhibitionism});
      }

      // ── Determinism ───────────────────────────────────────────────────────

      // The single most important test in this file. RomanticProfileBuilder seeds its Random by Hero.StringId
      // specifically so the same NPC "always rolls the same orientation, preferences, and kinks across
      // sessions" (its own XML doc), and this extraction was done BEHAVIOR-PRESERVING on the promise that the
      // call order never changes. If a future edit reorders these three calls (or adds/removes a draw between
      // them), every existing NPC's frozen romantic profile would silently reroll on the next save load,
      // contradicting DeriveKinks' own contract that re-deriving is "stable, not a reroll".
      [Test]
      public void GIVEN_the_same_seed_WHEN_building_a_full_profile_twice_THEN_every_roll_matches()
      {
         RomanticTraits traits = new() {Honor = 1, Mercy = -1, Valor = 1, Generosity = 0, Calculating = 1};


         SexualOrientation Orientation(Random r)
         {
            return RomanticProfilePolicy.DetermineOrientation(r);
         }


         List<RomanticPreference> Preferences(Random r)
         {
            return RomanticProfilePolicy.DeterminePreferences(r, traits);
         }


         List<Kink> Kinks(Random r)
         {
            return RomanticProfilePolicy.DetermineKinks(r, traits);
         }


         var rngA = new Random(42);
         var rngB = new Random(42);

         Orientation(rngA).Should().Be(Orientation(rngB));
         Preferences(rngA).Should().Equal(Preferences(rngB));
         Kinks(rngA).Should().Equal(Kinks(rngB));
      }

      /// <summary>
      ///   A stub System.Random (Next(int) is virtual) that fixes only the 0..99 percentile roll
      ///   (maxValue == 100) used to pick a band; any other call (e.g. the Fisher-Yates shuffle
      ///   inside PickDistinct, whose maxValue is the pool size) falls through to a real draw so
      ///   it never asks for an out-of-range index.
      /// </summary>
      private sealed class FixedRoll : Random
      {
         private readonly int _roll;

         public FixedRoll(int roll)
         {
            _roll = roll;
         }

         public override int Next(int maxValue) => maxValue == 100
            ? _roll
            : base.Next(maxValue);
      }

      #region private

      private static RomanticTraits Neutral() => new();

      #endregion
   }
}