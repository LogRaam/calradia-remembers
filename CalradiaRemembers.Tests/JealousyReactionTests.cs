// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the jealousy reaction barèmes: the relation hit, the letter likelihood, and the
// betrayal-vs-competition framing. These numbers and phrasings shape how the mechanic FEELS and
// regress in silence — a wrong scale or a swapped branch never throws.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class JealousyReactionTests
   {
      // ── Severity ───────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_betrayed_exclusive_partner_WHEN_scored_THEN_the_hit_grows_with_the_act()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act/Assert — exclusive partner, NOT competition.
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Flirt, false, monogamous).Should().Be(10);
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, false, monogamous).Should().Be(25);
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, false, monogamous).Should().Be(40);
      }

      [Test]
      public void GIVEN_a_sharing_culture_partner_WHEN_scored_THEN_competition_stings_far_less_than_betrayal()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act
         int betrayal    = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, false, monogamous);
         int competition = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, true,  monogamous);

         //Assert
         competition.Should().Be(6);
         competition.Should().BeLessThan(betrayal, "a permissive partner competes for rank, they are not betrayed");
      }

      [Test]
      public void GIVEN_a_competition_hit_WHEN_scored_THEN_culture_outrage_does_not_scale_it()
      {
         //Arrange — the competition track is a flat rank/attention sting, independent of exclusivity stance.
         JealousyCulture low  = JealousyCulture.FromRaw("Low", 0, "Low", false, "");
         JealousyCulture high = JealousyCulture.Monogamous;

         //Act/Assert
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, true, low)
            .Should().Be(JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, true, high));
      }

      [Test]
      public void GIVEN_a_courted_NPCs_spouse_WHEN_outrage_is_lower_THEN_the_hit_scales_down_but_never_to_zero()
      {
         //Arrange — SpouseOfCourted Intimacy has a base of 20, scaled by the writer's outrage.
         JealousyCulture high   = JealousyCulture.Monogamous;                               // ×1.0
         JealousyCulture medium = JealousyCulture.FromRaw("Medium", 0, "Low", false, "");   // ×0.85
         JealousyCulture low    = JealousyCulture.FromRaw("Low", 0, "Low", false, "");      // ×0.7

         //Act/Assert
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, false, high).Should().Be(20);
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, false, medium).Should().Be(17);
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, false, low).Should().Be(14);
      }

      [Test]
      public void GIVEN_a_rival_or_spurned_admirer_WHEN_scored_at_high_outrage_THEN_it_uses_the_base_scale()
      {
         //Arrange
         JealousyCulture high = JealousyCulture.Monogamous;

         //Act/Assert
         JealousyReaction.Severity(JealousStake.RivalSuitor, RomanticActTier.Intimacy, false, high).Should().Be(15);
         JealousyReaction.Severity(JealousStake.SpurnedAdmirer, RomanticActTier.Flirt, false, high).Should().Be(5);
      }

      // ── Letter likelihood ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_each_stake_WHEN_asked_for_the_letter_chance_THEN_the_rival_is_likeliest_to_write()
      {
         //Arrange/Act/Assert
         JealousyReaction.LetterChance(JealousStake.RivalSuitor).Should().Be(0.80f);
         JealousyReaction.LetterChance(JealousStake.SpouseOfCourted).Should().Be(0.70f);
         JealousyReaction.LetterChance(JealousStake.PlayerPartner).Should().Be(0.65f);
         JealousyReaction.LetterChance(JealousStake.SpurnedAdmirer).Should().Be(0.50f);
      }

      // ── Framing (betrayal vs competition) ──────────────────────────────────

      [Test]
      public void GIVEN_a_player_partner_WHEN_the_grievance_is_built_THEN_betrayal_and_competition_read_differently()
      {
         //Arrange/Act
         string betrayal    = JealousyReaction.BuildGrievance(JealousStake.PlayerPartner, false, "Liena");
         string competition = JealousyReaction.BuildGrievance(JealousStake.PlayerPartner, true,  "Liena");

         //Assert
         betrayal.Should().Contain("Liena").And.Contain("betrayal");
         competition.Should().Contain("Liena").And.Contain("lesser");
         competition.Should().NotBe(betrayal);
      }

      [Test]
      public void GIVEN_a_culture_hint_WHEN_a_letter_context_is_built_THEN_it_carries_the_names_and_the_hint()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.FromRaw("High", 0, "Low", false, "By Vlandian custom a wife's honour is the house's honour.");

         //Act
         string context = JealousyReaction.BuildLetterContext(
            JealousStake.SpouseOfCourted, false, "Sir Wythe", "Liena", culture);

         //Assert
         context.Should().Contain("Sir Wythe").And.Contain("Liena").And.Contain("Vlandian custom");
      }

      [Test]
      public void GIVEN_no_writer_name_WHEN_a_letter_context_is_built_THEN_it_falls_back_to_a_safe_label()
      {
         //Arrange/Act
         string context = JealousyReaction.BuildLetterContext(
            JealousStake.RivalSuitor, false, null, "Liena", JealousyCulture.Monogamous);

         //Assert
         context.Should().Contain("the writer", "a null hero name must never surface as an empty gap in the prompt");
      }

      // ── Secret lover (the discreet stake) ─────────────────────────────────

      [Test]
      public void GIVEN_a_secret_lover_WHEN_scored_THEN_the_hurt_is_real_but_milder_than_open_betrayal()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act/Assert: quieter than a betrayed exclusive partner at every tier.
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Flirt, false, monogamous).Should().Be(5);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Intimacy, false, monogamous).Should().Be(12);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Marriage, false, monogamous).Should().Be(20);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Marriage, false, monogamous)
                         .Should().BeLessThan(JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, false, monogamous));
      }

      [Test]
      public void GIVEN_a_secret_lover_WHEN_scored_THEN_culture_outrage_never_scales_the_private_wound()
      {
         //Arrange: a Low-outrage culture scales public stakes down, but the secret bond is personal.
         JealousyCulture permissive = JealousyCulture.FromRaw("Low", 0, "Low", false, "");

         //Act/Assert
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Intimacy, false, permissive).Should().Be(12);
      }

      [Test]
      public void GIVEN_a_secret_lover_WHEN_the_letter_context_is_built_THEN_it_stays_private_and_never_threatens_exposure()
      {
         //Act
         string context = JealousyReaction.BuildLetterContext(
            JealousStake.SecretLover, false, "Alara", "Liena", JealousyCulture.Monogamous);

         //Assert: the discretion holds even in the hurt (private, tender, no scene, no exposure).
         context.Should().Contain("Alara").And.Contain("Liena");
         context.Should().Contain("SECRET").And.Contain("NO public scene");
         context.Should().Contain("NEVER hint at exposing");
      }

      [Test]
      public void GIVEN_a_secret_lover_WHEN_the_grievance_is_built_THEN_it_reads_as_quiet_hurt_not_menace()
      {
         //Act
         string grievance = JealousyReaction.BuildGrievance(JealousStake.SecretLover, false, "Liena");

         //Assert
         grievance.Should().Contain("Liena").And.Contain("quietly");
      }
   }
}
