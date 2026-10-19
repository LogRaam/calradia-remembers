// Code written by Gabriel Mailhot, 20/08/2026.
// Protects the AMBITION found-topic selection: which of the four bound ambitions (a command to lead, a post in
// the party, a fief to hold, or the player's blessing to wed) a content companion voices, if any. Get the
// happiness gate wrong and a miserable companion could be made to voice a hopeful ambition, which the design
// brief calls a grievance in disguise; get the cooldown wrong and a refused ambition nags the player every
// settlement visit; get the priority wrong and a companion who qualifies for several ambitions at once voices
// the wrong one, drifting the story away from the rarer, weightier opportunity the design intends to lead with.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionAmbitionPolicyTests
   {
      // A comfortably fresh cooldown well past the constant, so only the fact under test drives the outcome.
      private const int FreshDaysSinceAsked = CompanionAmbitionPolicy.CooldownDays + 10;

      private static CompanionAmbitionFacts BaseFacts() => new CompanionAmbitionFacts {
         HasFieldPartyOfOwn = true,
         HoldsPartyRole = true,
         IsLandless = false,
         IsHighStanding = false,
         IsUnmarriedWithMatch = false,
         Happiness = CompanionHappiness.Default,
         DaysSinceLastAsked = FreshDaysSinceAsked
      };

      // ── Individual ambitions, each under its own condition ──────────────────

      // "A command of their own to lead" (dispatch_mission): qualifies exactly when the companion has no field
      // party of their own to lead already.
      [Test]
      public void GIVEN_a_content_companion_with_no_field_party_WHEN_selected_THEN_command_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Command);
         strength.Should().Be(CompanionAmbitionPolicy.CommandStrength);
      }

      // "A post in the party" (assign_party_role): qualifies exactly when the companion holds no party role yet.
      [Test]
      public void GIVEN_a_content_companion_who_holds_no_party_role_WHEN_selected_THEN_post_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HoldsPartyRole = false;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Post);
         strength.Should().Be(CompanionAmbitionPolicy.PostStrength);
      }

      // "A fief to hold" (appoint_governor): qualifies exactly when the companion is landless AND high-standing;
      // either alone is not enough (a humble landless companion, or a high-standing landed one, has no fief ask).
      [Test]
      public void GIVEN_a_landless_high_standing_content_companion_WHEN_selected_THEN_fief_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.IsLandless = true;
         facts.IsHighStanding = true;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Fief);
         strength.Should().Be(CompanionAmbitionPolicy.FiefStrength);
      }

      [Test]
      public void GIVEN_a_landless_but_NOT_high_standing_companion_WHEN_selected_THEN_fief_is_not_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.IsLandless = true;
         facts.IsHighStanding = false;

         (CompanionAmbition ambition, int _) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().NotBe(CompanionAmbition.Fief);
      }

      [Test]
      public void GIVEN_a_high_standing_but_NOT_landless_companion_WHEN_selected_THEN_fief_is_not_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.IsLandless = false;
         facts.IsHighStanding = true;

         (CompanionAmbition ambition, int _) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().NotBe(CompanionAmbition.Fief);
      }

      // "The player's blessing to wed" (arrange_marriage): qualifies exactly when the companion is unwed with a
      // plausible match already found.
      [Test]
      public void GIVEN_a_content_unmarried_companion_with_a_plausible_match_WHEN_selected_THEN_marriage_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.IsUnmarriedWithMatch = true;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Marriage);
         strength.Should().Be(CompanionAmbitionPolicy.MarriageStrength);
      }

      // ── Nothing qualifies ────────────────────────────────────────────────────

      // Every individual condition withheld: the companion already leads their own party, already holds a role,
      // is not landless/high-standing, and has no marriage match. Silence is the default, mirroring every other
      // found-topic (CompanionAudienceTopicPolicy's own "no topic found means no audience" rule).
      [Test]
      public void GIVEN_no_ambition_condition_holds_WHEN_selected_THEN_the_answer_is_none()
      {
         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(BaseFacts());

         ambition.Should().Be(CompanionAmbition.None);
         strength.Should().Be(0);
      }

      // ── Happiness gate ───────────────────────────────────────────────────────

      // The design brief, verbatim: an ambitious demand from a miserable companion is a grievance, not an
      // ambition (Grievance already covers that path). However many conditions qualify, a companion below the
      // Content band must never be handed an ambition topic.
      [Test]
      public void GIVEN_a_companion_who_is_not_content_WHEN_every_condition_otherwise_qualifies_THEN_none_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.HoldsPartyRole = false;
         facts.IsLandless = true;
         facts.IsHighStanding = true;
         facts.IsUnmarriedWithMatch = true;
         facts.Happiness = CompanionHappiness.LeaveThreshold;

         (CompanionAmbition ambition, int _) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.None);
      }

      // IsContentEnough reuses CompanionHappiness.Band's own Content threshold (the same rule
      // CompanionGratitudePolicy/CompanionSchemeWarningPolicy already follow), so this can never disagree with
      // the encyclopedia/prompt wording the player already sees for this companion's mood.
      [Test]
      public void GIVEN_a_score_exactly_at_the_content_band_WHEN_asked_THEN_is_content_enough_agrees_with_the_band()
      {
         CompanionAmbitionPolicy.IsContentEnough(CompanionHappiness.Default).Should().BeTrue();
         CompanionAmbitionPolicy.IsContentEnough(CompanionHappiness.LeaveThreshold).Should().BeFalse();
      }

      // ── Cooldown ─────────────────────────────────────────────────────────────

      // "A refused ambition does not nag": within the per-companion cooldown window, no ambition may be chosen,
      // even when a condition otherwise qualifies.
      [Test]
      public void GIVEN_a_companion_asked_within_the_cooldown_window_WHEN_selected_THEN_none_is_chosen()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.DaysSinceLastAsked = CompanionAmbitionPolicy.CooldownDays - 1;

         (CompanionAmbition ambition, int _) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.None);
      }

      // The cooldown's own edge counts as cleared (inclusive floor), pinning the constant's exact meaning
      // rather than leaving it to guesswork.
      [Test]
      public void GIVEN_a_companion_asked_exactly_at_the_cooldown_edge_WHEN_selected_THEN_it_is_no_longer_withheld()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.DaysSinceLastAsked = CompanionAmbitionPolicy.CooldownDays;

         (CompanionAmbition ambition, int _) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Command);
      }

      // ── Priority when several qualify ────────────────────────────────────────

      // Fief outranks every other ambition when several qualify at once: the rarest, weightiest opportunity
      // leads the story rather than a lighter one crowding it out.
      [Test]
      public void GIVEN_fief_and_every_other_ambition_qualify_WHEN_selected_THEN_fief_wins()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.HoldsPartyRole = false;
         facts.IsLandless = true;
         facts.IsHighStanding = true;
         facts.IsUnmarriedWithMatch = true;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Fief);
         strength.Should().Be(CompanionAmbitionPolicy.FiefStrength);
      }

      // Marriage outranks Post and Command when Fief is withheld.
      [Test]
      public void GIVEN_marriage_post_and_command_qualify_but_not_fief_WHEN_selected_THEN_marriage_wins()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.HoldsPartyRole = false;
         facts.IsUnmarriedWithMatch = true;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Marriage);
         strength.Should().Be(CompanionAmbitionPolicy.MarriageStrength);
      }

      // Post outranks Command when neither Fief nor Marriage qualify.
      [Test]
      public void GIVEN_post_and_command_qualify_but_not_fief_or_marriage_WHEN_selected_THEN_post_wins()
      {
         CompanionAmbitionFacts facts = BaseFacts();
         facts.HasFieldPartyOfOwn = false;
         facts.HoldsPartyRole = false;

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         ambition.Should().Be(CompanionAmbition.Post);
         strength.Should().Be(CompanionAmbitionPolicy.PostStrength);
      }

      // Every declared strength must stay directly comparable against every other found-topic's own 0..100
      // scale, and below the urgent SchemeWarning constant (design brief: an ambition is real, but never as
      // urgent as a live plot against the player).
      [Test]
      public void GIVEN_every_declared_strength_WHEN_checked_THEN_each_stays_within_0_100_and_below_scheme_warning()
      {
         CompanionAmbitionPolicy.FiefStrength.Should().BeInRange(0, 100).And.BeLessThan(CompanionSchemeWarningPolicy.Strength);
         CompanionAmbitionPolicy.MarriageStrength.Should().BeInRange(0, 100).And.BeLessThan(CompanionSchemeWarningPolicy.Strength);
         CompanionAmbitionPolicy.PostStrength.Should().BeInRange(0, 100).And.BeLessThan(CompanionSchemeWarningPolicy.Strength);
         CompanionAmbitionPolicy.CommandStrength.Should().BeInRange(0, 100).And.BeLessThan(CompanionSchemeWarningPolicy.Strength);
      }

      // ── Null facts ───────────────────────────────────────────────────────────

      // Total function: a null facts object (a caller wiring bug) must degrade to silence, never throw and
      // never crash the audience scan for every other topic alongside it.
      [Test]
      public void GIVEN_null_facts_WHEN_selected_THEN_the_answer_is_none()
      {
         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(null);

         ambition.Should().Be(CompanionAmbition.None);
         strength.Should().Be(0);
      }
   }
}
