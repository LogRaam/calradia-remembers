// Code written by Gabriel Mailhot, 17/07/2026.
// Jealousy, witnessed-reaction gate (romance audit M-J1/M-J2). This is the single decision "does a witnessed
// romantic act draw a jealous reaction this time". It guards two real bugs: a MARRIAGE-tier act (a public wedding,
// a consort openly taken) must ALWAYS wake the jealous party, never be swallowed by the 1-in-4 travel roll (M-J2);
// and because the host consumes the pairing's 20-day cooldown only when this returns true, a failed lesser-act
// roll must return false so it never burns the cooldown and swallows a real marriage days later (M-J1). Both are
// silent betrayals of the fiction ("your spouse was taken and you never heard"), so the boundaries are pinned.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class JealousyReactionPolicyTests
   {
      // M-J2: a marriage-tier act ALWAYS reacts, whatever the roll, including a roll that would fail a flirt. A
      // spouse must never have a chance of not learning their partner was wed or taken as consort in public.
      [Test]
      public void GIVEN_a_marriage_tier_act_WHEN_the_roll_would_fail_THEN_it_still_reacts()
      {
         // A roll of 1.0 is the worst possible draw (above any chance below 1); marriage ignores it.
         JealousyReactionPolicy.WitnessedActReacts(RomanticActTier.Marriage, roll: 1.0f, witnessedChance: 0.75f)
            .Should().BeTrue();
      }

      // A lesser act (flirt/intimacy) reacts when the travel roll lands within the chance: word got around.
      [Test]
      public void GIVEN_a_lesser_act_WHEN_the_roll_lands_within_the_chance_THEN_it_reacts()
      {
         JealousyReactionPolicy.WitnessedActReacts(RomanticActTier.Flirt, roll: 0.5f, witnessedChance: 0.75f)
            .Should().BeTrue();
      }

      // M-J1: a lesser act whose roll FAILS must return false, so the host does not consume the cooldown. If this
      // returned true (or the host consumed the cooldown regardless), a failed flirt would put the pair on a
      // 20-day cooldown and swallow a real marriage that happened within it.
      [Test]
      public void GIVEN_a_lesser_act_WHEN_the_roll_exceeds_the_chance_THEN_it_does_not_react()
      {
         JealousyReactionPolicy.WitnessedActReacts(RomanticActTier.Intimacy, roll: 0.9f, witnessedChance: 0.75f)
            .Should().BeFalse();
      }

      // Boundary: a roll exactly at the chance still reacts (inclusive), so the documented chance is not silently
      // one draw stingier than stated.
      [Test]
      public void GIVEN_a_lesser_act_WHEN_the_roll_equals_the_chance_THEN_it_reacts()
      {
         JealousyReactionPolicy.WitnessedActReacts(RomanticActTier.Flirt, roll: 0.75f, witnessedChance: 0.75f)
            .Should().BeTrue();
      }

      // A private act gets the same marriage exemption as a witnessed one: a wedding is telegraphed whether or
      // not the jealous party had eyes on it, so even the worst possible roll cannot hush it up.
      [Test]
      public void GIVEN_a_private_marriage_tier_act_WHEN_the_roll_would_fail_THEN_it_still_reacts()
      {
         JealousyReactionPolicy.PrivateActReacts(RomanticActTier.Marriage, roll: 1.0f, chance: 0.5f)
            .Should().BeTrue();
      }

      // A private flirt leaves no witnesses to carry the tale: it reacts only when the roll lands within the
      // (lower) private chance, word got out despite the discretion.
      [Test]
      public void GIVEN_a_private_flirt_WHEN_the_roll_lands_within_the_chance_THEN_it_reacts()
      {
         JealousyReactionPolicy.PrivateActReacts(RomanticActTier.Flirt, roll: 0.4f, chance: 0.5f)
            .Should().BeTrue();
      }

      // The flip side: a private flirt whose roll fails leaves no rumour at all, so the host must not react, 
      // and must not consume the pairing cooldown either (same M-J1 property as the witnessed gate).
      [Test]
      public void GIVEN_a_private_flirt_WHEN_the_roll_exceeds_the_chance_THEN_it_does_not_react()
      {
         JealousyReactionPolicy.PrivateActReacts(RomanticActTier.Flirt, roll: 0.6f, chance: 0.5f)
            .Should().BeFalse();
      }

      // Boundary: the private gate is EXCLUSIVE (a roll exactly at the chance does not react), unlike the
      // witnessed one, pinned so the two boundaries cannot silently drift into each other.
      [Test]
      public void GIVEN_a_private_flirt_WHEN_the_roll_equals_the_chance_THEN_it_does_not_react()
      {
         JealousyReactionPolicy.PrivateActReacts(RomanticActTier.Intimacy, roll: 0.5f, chance: 0.5f)
            .Should().BeFalse();
      }
   }
}
