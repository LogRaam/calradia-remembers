// Code written by Gabriel Mailhot, 01/08/2026.
// Protects the fix for a player report: a pregnant NPC woman has no idea she is pregnant in chat and
// denies it when asked, because her pregnancy state was never injected into the LLM prompt. Describe's
// whole purpose is the ACKNOWLEDGEMENT directive ("you are aware of this and never deny being with
// child"), every test here either pins that the directive always fires while pregnant, or that the
// stage/father wording built around it never contradicts the game's own facts.
//
// WHY IT MATTERS: EncounterContextBuilder feeds this into EncounterContext.PregnancyNote, which
// PromptBuilder renders in the NPC's own identity section (mirroring LifeStageDescriber). The father
// clause is gated on fatherNamable, set true ONLY when the father is the mother's LEGAL spouse, so an
// affair (the player, or another lord) or a faceless bandit conception never leaks the true father here,
// preserving the hidden-bastard secrecy pillar. A wrong gate would either spoil a secret paternity or
// wrongly out a legitimate one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PregnancyNotePolicyTests
    {
        private const int TotalDuration = 90;

        // ── Not pregnant / unknown duration → nothing rendered ─────────────────

        // The baseline non-bug case: a woman who is not pregnant must get no pregnancy line at all, so
        // this policy never invents a pregnancy for an ordinary NPC.
        [Test]
        public void GIVEN_a_woman_who_is_not_pregnant_WHEN_describing_THEN_returns_empty_string()
        {
            PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = false, FatherIsPlayer = false, FatherName = "Derthert", FatherNamable = true, DaysRemaining = 30, TotalDurationDays = TotalDuration})
                .Should().BeEmpty();
        }

        // totalDurationDays <= 0 is the sentinel for "the game could not resolve the pregnancy model's
        // duration" (same defensive contract as LifeStageDescriber's age <= 0). Rendering a stage from a
        // zero/negative duration would divide by zero or produce a meaningless fraction, so this must
        // short-circuit to empty rather than guess.
        [Test]
        public void GIVEN_an_unknown_total_duration_WHEN_describing_THEN_returns_empty_string()
        {
            PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = "Derthert", FatherNamable = true, DaysRemaining = 10, TotalDurationDays = 0})
                .Should().BeEmpty();
        }

        // ── Stage bands, from how far along she is ─────────────────────────────

        // Early stage: well under a third elapsed. This is the exact case the player report was about:
        // the NPC must at minimum recognise she is expecting, even this early.
        [Test]
        public void GIVEN_a_pregnancy_barely_begun_WHEN_describing_THEN_early_weeks_stage()
        {
            // elapsed = 90 - 80 = 10 days -> fraction ~0.11, well below the early threshold (1/3).
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 80, TotalDurationDays = TotalDuration});

            note.Should().Contain("in the early weeks of a pregnancy");
        }

        // Boundary: just below the early threshold (elapsed fraction fractionally under 1/3) must still
        // read as early, pinning the "<" comparison rather than "<=" at the low edge.
        [Test]
        public void GIVEN_elapsed_fraction_just_under_one_third_WHEN_describing_THEN_still_early_weeks_stage()
        {
            // elapsed = 90 - 61 = 29 days -> fraction = 29/90 ≈ 0.322, just under 1/3 (0.333...).
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 61, TotalDurationDays = TotalDuration});

            note.Should().Contain("in the early weeks of a pregnancy");
        }

        // Boundary: exactly one third elapsed tips into the mid (visibly pregnant) band. Without this
        // pin, an off-by-one in the threshold comparison could silently widen or narrow the early band.
        [Test]
        public void GIVEN_elapsed_fraction_at_one_third_WHEN_describing_THEN_visibly_pregnant_stage()
        {
            // elapsed = 30 days of 90 -> fraction exactly 1/3.
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 60, TotalDurationDays = TotalDuration});

            note.Should().Contain("visibly pregnant");
        }

        // Interior pin for the mid band, clear of either boundary.
        [Test]
        public void GIVEN_a_pregnancy_at_the_halfway_point_WHEN_describing_THEN_visibly_pregnant_stage()
        {
            // elapsed = 45 of 90 -> fraction 0.5.
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 45, TotalDurationDays = TotalDuration});

            note.Should().Contain("visibly pregnant");
        }

        // Boundary: just under two-thirds elapsed must still read as the mid band, not yet late.
        [Test]
        public void GIVEN_elapsed_fraction_just_under_two_thirds_WHEN_describing_THEN_still_visibly_pregnant_stage()
        {
            // elapsed = 59 of 90 -> fraction ≈ 0.655, just under 2/3 (0.666...).
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 31, TotalDurationDays = TotalDuration});

            note.Should().Contain("visibly pregnant");
        }

        // Boundary: exactly two-thirds elapsed tips into the late (near term) band.
        [Test]
        public void GIVEN_elapsed_fraction_at_two_thirds_WHEN_describing_THEN_heavily_pregnant_stage()
        {
            // elapsed = 60 of 90 -> fraction exactly 2/3.
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 30, TotalDurationDays = TotalDuration});

            note.Should().Contain("heavily pregnant and near your term");
        }

        // Interior pin for the late band: nearly due, days remaining almost zero.
        [Test]
        public void GIVEN_a_pregnancy_almost_due_WHEN_describing_THEN_heavily_pregnant_stage()
        {
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 2, TotalDurationDays = TotalDuration});

            note.Should().Contain("heavily pregnant and near your term");
        }

        // ── Father clause: only when namable ────────────────────────────────────

        // A father who IS namable (her legal spouse) and is a third-party lord must be named: the ordinary,
        // public, legitimate case.
        [Test]
        public void GIVEN_a_namable_lord_father_WHEN_describing_THEN_names_him_as_the_father()
        {
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = "Derthert", FatherNamable = true, DaysRemaining = 45, TotalDurationDays = TotalDuration});

            note.Should().Contain("carrying Derthert's child");
        }

        // The father is a real, non-empty name but fatherNamable is false (an affair, or a faceless
        // bandit): the clause must be suppressed entirely, never leaking the true father while she still
        // acknowledges the pregnancy itself. This is the crux of the hidden-bastard secrecy pillar.
        [Test]
        public void GIVEN_a_named_father_who_is_not_namable_WHEN_describing_THEN_no_father_clause_but_still_acknowledges()
        {
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = "Derthert", FatherNamable = false, DaysRemaining = 45, TotalDurationDays = TotalDuration});

            note.Should().NotContain("Derthert");
            note.Should().Contain("You are");
            note.Should().Contain("never deny being with child");
        }

        // Player-legitimate case: the player is the father AND namable (the mother's own spouse), so the
        // prompt tells the NPC plainly it is the player's child.
        [Test]
        public void GIVEN_the_player_is_the_namable_father_WHEN_describing_THEN_names_the_player_as_the_father()
        {
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = true, FatherName = "PlayerName", FatherNamable = true, DaysRemaining = 45, TotalDurationDays = TotalDuration});

            note.Should().Contain("carrying the player's child");
        }

        // Player-adulterous case: the player fathered the child but is NOT the mother's spouse (an
        // affair), so fatherNamable is false. She must still say nothing of the father, not even that it
        // is the player, while still acknowledging the pregnancy, exactly like the third-party affair case.
        [Test]
        public void GIVEN_the_player_is_the_father_but_not_namable_WHEN_describing_THEN_no_father_clause_but_still_acknowledges()
        {
            string note = PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = true, FatherName = "PlayerName", FatherNamable = false, DaysRemaining = 45, TotalDurationDays = TotalDuration});

            note.Should().NotContain("carrying");
            note.Should().Contain("never deny being with child");
        }

        // ── The never-deny directive: the whole point of this policy ────────────

        // Whatever the stage or father situation, the acknowledgement directive that fixes the reported
        // bug (the NPC denying she is pregnant) must always be present while she is pregnant.
        [Test]
        public void GIVEN_any_pregnant_state_WHEN_describing_THEN_the_never_deny_directive_is_always_present()
        {
            PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = false, FatherName = null, FatherNamable = false, DaysRemaining = 80, TotalDurationDays = TotalDuration})
                .Should().Contain("You are aware of this and never deny being with child");

            PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = true, FatherName = "PlayerName", FatherNamable = true, DaysRemaining = 10, TotalDurationDays = TotalDuration})
                .Should().Contain("You are aware of this and never deny being with child");
        }
    }
}
