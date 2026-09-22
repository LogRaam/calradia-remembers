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

      // Pins the base betrayal scale (10/25/40) an exclusive partner's regard eats via
      // ProfileMutator.ApplyReputationDelta in JealousyBehavior.FireReaction. These numbers shape
      // how the mechanic FEELS and a wrong scale never throws (see file header): only this test
      // would catch it.
      [Test]
      public void GIVEN_a_betrayed_exclusive_partner_WHEN_scored_THEN_the_hit_grows_with_the_act()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act/Assert — exclusive partner, NOT competition.
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Flirt, JealousyTrack.Betrayal, monogamous).Should().Be(10);
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, JealousyTrack.Betrayal, monogamous).Should().Be(25);
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Betrayal, monogamous).Should().Be(40);
      }

      // Guards the core betrayal-vs-competition split: a sharing-culture partner (see
      // JealousyCulture.ToleratesSharedPartners) must sting far less than a betrayed exclusive one.
      // If competition ever scored close to betrayal, a polygamous household would feel punished
      // as hard as a monogamous betrayal, defeating the point of a permissive culture.
      [Test]
      public void GIVEN_a_sharing_culture_partner_WHEN_scored_THEN_competition_stings_far_less_than_betrayal()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act
         int betrayal    = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, JealousyTrack.Betrayal, monogamous);
         int competition = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Intimacy, JealousyTrack.Competition,  monogamous);

         //Assert
         competition.Should().Be(6);
         competition.Should().BeLessThan(betrayal, "a permissive partner competes for rank, they are not betrayed");
      }

      // Competition is a flat rank/attention sting, deliberately routed around Scale() (unlike the
      // other stakes). If culture outrage crept in here, a Low-outrage culture's competing partner
      // would feel a weaker sting than a High-outrage one for the identical neglect, which makes no
      // sense for a mechanic about rank, not exclusivity.
      [Test]
      public void GIVEN_a_competition_hit_WHEN_scored_THEN_culture_outrage_does_not_scale_it()
      {
         //Arrange — the competition track is a flat rank/attention sting, independent of exclusivity stance.
         JealousyCulture low  = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "Low", PolygamyEliteTier = 0, HierarchySensitivity = "Low", EquityExpectation = false, PromptHint = ""});
         JealousyCulture high = JealousyCulture.Monogamous;

         //Act/Assert
         JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Competition, low)
            .Should().Be(JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Competition, high));
      }

      // Pins Scale()'s never-to-zero floor (Math.Max(1, ...)) for SpouseOfCourted, the strongest and
      // most telegraphed stake (JealousPartyResolver checks it first). A scale bug that reached zero
      // for a permissive culture would silently disable this stake's reaction entirely.
      [Test]
      public void GIVEN_a_courted_NPCs_spouse_WHEN_outrage_is_lower_THEN_the_hit_scales_down_but_never_to_zero()
      {
         //Arrange — SpouseOfCourted Intimacy has a base of 20, scaled by the writer's outrage.
         JealousyCulture high   = JealousyCulture.Monogamous;                               // ×1.0
         JealousyCulture medium = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "Medium", PolygamyEliteTier = 0, HierarchySensitivity = "Low", EquityExpectation = false, PromptHint = ""});   // ×0.85
         JealousyCulture low    = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "Low", PolygamyEliteTier = 0, HierarchySensitivity = "Low", EquityExpectation = false, PromptHint = ""});      // ×0.7

         //Act/Assert
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, JealousyTrack.Betrayal, high).Should().Be(20);
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, JealousyTrack.Betrayal, medium).Should().Be(17);
         JealousyReaction.Severity(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, JealousyTrack.Betrayal, low).Should().Be(14);
      }

      // Pins the base values (RivalSuitor 15, SpurnedAdmirer 5) at High outrage, the last two
      // fallback stakes JealousPartyResolver tries (after spouse, player partner, secret lover).
      [Test]
      public void GIVEN_a_rival_or_spurned_admirer_WHEN_scored_at_high_outrage_THEN_it_uses_the_base_scale()
      {
         //Arrange
         JealousyCulture high = JealousyCulture.Monogamous;

         //Act/Assert
         JealousyReaction.Severity(JealousStake.RivalSuitor, RomanticActTier.Intimacy, JealousyTrack.Betrayal, high).Should().Be(15);
         JealousyReaction.Severity(JealousStake.SpurnedAdmirer, RomanticActTier.Flirt, JealousyTrack.Betrayal, high).Should().Be(5);
      }

      // ── Letter likelihood ──────────────────────────────────────────────────

      // LetterChance feeds the coin flip (JealousyBehavior.SchedulePendingReaction / EvaluateAwareness)
      // deciding whether a threatening courier letter follows the relation hit. Pins the ordering
      // (rival 80% > spouse 70% > partner 65% > spurned 50%): a rival suitor is meant to be the most
      // likely to actually confront, matching BuildLetterContext's "you will not yield quietly" framing.
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

      // BuildGrievance's text becomes the permanent NotableEventType.Jealousy memory and, for every
      // stake but SecretLover, feeds GrudgeBehavior.TryPlant (GrudgeSource.Jealousy). If betrayal and
      // competition ever produced the same text, a permissive-culture partner's memory would
      // misread as "betrayed" even though their culture explicitly tolerates sharing.
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

      // BuildLetterContext seeds the LLM prompt for the jealous threatening letter
      // (LetterGenerationService.QueueInitialLetter). The culture's PromptHint is how a faction's
      // own customs voice ("By Vlandian custom...") reach that letter; losing it would make every
      // jealous letter read culturally generic regardless of the writer's people.
      [Test]
      public void GIVEN_a_culture_hint_WHEN_a_letter_context_is_built_THEN_it_carries_the_names_and_the_hint()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "High", PolygamyEliteTier = 0, HierarchySensitivity = "Low", EquityExpectation = false, PromptHint = "By Vlandian custom a wife's honour is the house's honour."});

         //Act
         string context = JealousyReaction.BuildLetterContext(
            JealousStake.SpouseOfCourted, false, "Sir Wythe", "Liena", culture);

         //Assert
         context.Should().Contain("Sir Wythe").And.Contain("Liena").And.Contain("Vlandian custom");
      }

      // partyName can legitimately be null (e.g. an unnamed rival); the "the writer" fallback stops
      // that from surfacing as a literal blank in the LLM prompt, which would read as a broken
      // sentence to the model and, likely, in the letter it hands back to the player.
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

      // v1.30.3 fix ("your secret lover now feels it"): before this, a secret lover was inert and
      // never reacted to the player courting elsewhere. Pins that every tier is strictly below the
      // equivalent open PlayerPartner betrayal value, protecting the design promise that discretion
      // softens the blow rather than making the secret bond cost as much as an open marriage.
      [Test]
      public void GIVEN_a_secret_lover_WHEN_scored_THEN_the_hurt_is_real_but_milder_than_open_betrayal()
      {
         //Arrange
         JealousyCulture monogamous = JealousyCulture.Monogamous;

         //Act/Assert: quieter than a betrayed exclusive partner at every tier.
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Flirt, JealousyTrack.Betrayal, monogamous).Should().Be(5);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Intimacy, JealousyTrack.Betrayal, monogamous).Should().Be(12);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Marriage, JealousyTrack.Betrayal, monogamous).Should().Be(20);
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Marriage, JealousyTrack.Betrayal, monogamous)
                         .Should().BeLessThan(JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Betrayal, monogamous));
      }

      // Unlike every other stake, SecretLover severity deliberately skips Scale() ("the bond is
      // personal, outside any public custom", per JealousyReaction.Severity). If culture crept in
      // here, a permissive culture's secret lover would feel diluted hurt for a bond that, by
      // design, has nothing to do with public custom.
      [Test]
      public void GIVEN_a_secret_lover_WHEN_scored_THEN_culture_outrage_never_scales_the_private_wound()
      {
         //Arrange: a Low-outrage culture scales public stakes down, but the secret bond is personal.
         JealousyCulture permissive = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "Low", PolygamyEliteTier = 0, HierarchySensitivity = "Low", EquityExpectation = false, PromptHint = ""});

         //Act/Assert
         JealousyReaction.Severity(JealousStake.SecretLover, RomanticActTier.Intimacy, JealousyTrack.Betrayal, permissive).Should().Be(12);
      }

      // The entire point of the secret-lover stake (CHANGELOG v1.30.3: "kept hidden... Never a
      // public scene, never a threat to expose what you share") is discretion. Pins that the LLM
      // prompt itself instructs no public scene and no exposure threat; a regression here could
      // have the model write a letter that outs the affair, breaking that promise to the player.
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

      // The permanent memory text must read as quietly hurt, not vengeful (unlike RivalSuitor's "I
      // mean to make them regret it"): JealousyBehavior.FireReaction explicitly skips
      // GrudgeBehavior.TryPlant for SecretLover ("the bond absorbs it... never a grudge"), so wrong
      // tone here would misrepresent a relationship the mod deliberately keeps free of grudges.
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
