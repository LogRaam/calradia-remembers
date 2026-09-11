// Code written by Gabriel Mailhot, 05/07/2026.
// Player appearance pillar, composer increment: derived body-fact banding + authored prose, pure
// natural-language rendering. See AppearanceComposer's own header for the design.
//
// WHY IT MATTERS: this is the ONLY place that turns a hero's real BodyProperties (height, build, age,
// hair, skin, eyes) into the prose the prompt injects as the player's/other heroes' appearance (Subject
// voice, read BY the speaking NPC) and the speaking NPC's own body (Self voice, "You are..."). Deriving
// from the engine instead of letting the LLM invent looks is exactly what the appearance pillar exists to
// do (ROADMAP: "Kills the backstory/appearance hallucination"), so a wrong band, a wrong voice/pronoun, or
// a stray "unknown" fragment here is a wrong look reaching the player or the NPC's own self-image, every
// turn. Compose() also renders the Encyclopedia "How others see you" line Gabriel edits directly, so a
// formatting bug is one he would see in his own UI, not just in a log.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AppearanceComposerTests
    {
        // ── HeightPhrase ─────────────────────────────────────────────────────

        // HeightShortBelow is a TUNE-flagged constant (ROADMAP: height bands 0.35/0.65); pinning just
        // inside the edge locks where "short" starts, so a future retune can't silently move it unnoticed.
        [Test]
        public void GIVEN_height_below_the_short_threshold_WHEN_phrasing_THEN_it_reads_short()
        {
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightShortBelow - 0.01).Should().Be("short");
        }

        // Mirrors the short-edge test on the "tall" side, so retuning one TUNE-flagged edge can never
        // silently widen or shrink the other.
        [Test]
        public void GIVEN_height_above_the_tall_threshold_WHEN_phrasing_THEN_it_reads_tall()
        {
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightTallAbove + 0.01).Should().Be("tall");
        }

        // The header's own discipline: an axis sitting in its "average" middle band stays UNVOICED (the
        // same rule StanceBearingText follows), and the boundary values themselves (strict < / >) must
        // land on the silent side, never get accidentally read as short or tall.
        [Test]
        public void GIVEN_average_height_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.HeightPhrase(0.5).Should().BeNull();
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightShortBelow).Should().BeNull();
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightTallAbove).Should().BeNull();
        }

        // ── BuildPhrase (build + weight combined) ───────────────────────────

        // BuildPhrase folds build+weight into ONE phrase (per the XML doc, an appearance description never
        // separately narrates "muscular" and "heavy"); pins the slight edge, also TUNE-flagged.
        [Test]
        public void GIVEN_a_slight_build_and_weight_WHEN_phrasing_THEN_it_reads_slightly_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildSlightBelow - 0.05, AppearanceComposer.BuildSlightBelow - 0.05)
                .Should().Be("slightly built");
        }

        // The middle build band is only reachable once a build clears BuildSolidAbove WITHOUT also
        // clearing BuildPowerfulAbove; guards that the powerful check (evaluated first in code) doesn't
        // shadow the solid band.
        [Test]
        public void GIVEN_a_solid_build_and_weight_WHEN_phrasing_THEN_it_reads_solidly_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildSolidAbove + 0.05, AppearanceComposer.BuildSolidAbove + 0.05)
                .Should().Be("solidly built");
        }

        // Pins the top build edge, BuildPowerfulAbove, past which a build reads powerfully rather than
        // merely solidly built.
        [Test]
        public void GIVEN_a_powerful_build_and_weight_WHEN_phrasing_THEN_it_reads_powerfully_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildPowerfulAbove + 0.05, AppearanceComposer.BuildPowerfulAbove + 0.05)
                .Should().Be("powerfully built");
        }

        // 0.5/0.5 sits in the middle of every build band: same unvoiced-average discipline as height.
        [Test]
        public void GIVEN_an_average_build_and_weight_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.BuildPhrase(0.5, 0.5).Should().BeNull();
        }

        // ── AgePhrase, boundary tests ────────────────────────────────────────

        // AgeYouthBelow is TUNE-flagged (ROADMAP: age bands 25/40/60); one year below it must still read
        // "youth", pinning the edge before the next test crosses it into "prime".
        [Test]
        public void GIVEN_age_just_below_the_youth_cutoff_WHEN_phrasing_THEN_it_reads_youth()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeYouthBelow - 1).Should().Be("youth");
        }

        // The youth/prime edge is INCLUSIVE on the prime side: age == AgeYouthBelow already reads "prime".
        // Paired with the test above, this fixes exactly where the line falls.
        [Test]
        public void GIVEN_age_at_the_youth_cutoff_WHEN_phrasing_THEN_it_reads_prime()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeYouthBelow).Should().Be("prime");
        }

        // One year below AgePrimeBelow still reads "prime"; pins the far edge of the prime band before the
        // next test crosses into "middle years".
        [Test]
        public void GIVEN_age_just_below_the_prime_cutoff_WHEN_phrasing_THEN_it_reads_prime()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgePrimeBelow - 1).Should().Be("prime");
        }

        // Mirrors the youth/prime edge: age == AgePrimeBelow already reads "middle years", not "prime".
        [Test]
        public void GIVEN_age_at_the_prime_cutoff_WHEN_phrasing_THEN_it_reads_middle_years()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgePrimeBelow).Should().Be("middle years");
        }

        // One year below AgeMiddleBelow still reads "middle years"; the last point before the final band.
        [Test]
        public void GIVEN_age_just_below_the_middle_cutoff_WHEN_phrasing_THEN_it_reads_middle_years()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeMiddleBelow - 1).Should().Be("middle years");
        }

        // age == AgeMiddleBelow is the point past which every remaining age reads "old age" (no upper
        // bound), so this pins the last of the three TUNE-flagged age edges.
        [Test]
        public void GIVEN_age_at_the_middle_cutoff_WHEN_phrasing_THEN_it_reads_old_age()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeMiddleBelow).Should().Be("old age");
        }

        // ageYears <= 0 means age is not known/derivable (per the XML doc); the composer's rule is to omit
        // it silently rather than ever render "unknown" or a wrong guess.
        [Test]
        public void GIVEN_age_zero_or_below_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.AgePhrase(0).Should().BeNull();
            AppearanceComposer.AgePhrase(-1).Should().BeNull();
        }

        // ── Compose, full sentences ──────────────────────────────────────────

        // The flagship end-to-end sentence: every band (height, build, age, hair) composing together in
        // the order the prompt and the Encyclopedia preview actually render them. A wrong word order or a
        // missing comma here ships straight to the player as a broken sentence.
        [Test]
        public void GIVEN_a_tall_powerful_male_in_his_middle_years_with_dark_hair_WHEN_composing_THEN_it_reads_the_flagship_sentence()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 45, HeightNormalized = AppearanceComposer.HeightTallAbove + 0.1, BuildNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, WeightNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, HairColorDescriptor = "dark", HasBeard = false};

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A tall, powerfully built man in his middle years, with dark hair.");
        }

        // hairColorDescriptor is null here: the composer must omit the hair clause outright rather than
        // invent a colour or print "unknown hair", per the header's "never rendered as unknown" rule.
        [Test]
        public void GIVEN_a_short_slight_female_with_no_hair_descriptor_WHEN_composing_THEN_the_hair_clause_is_omitted()
        {
            var facts = new AppearanceFacts {IsFemale = true, AgeYears = 0, HeightNormalized = AppearanceComposer.HeightShortBelow - 0.1, BuildNormalized = AppearanceComposer.BuildSlightBelow - 0.1, WeightNormalized = AppearanceComposer.BuildSlightBelow - 0.1, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A short, slightly built woman.");
        }

        // Guards against a whitespace-only hair string (what a failed colour read could plausibly leave
        // behind) being treated as a real colour: it must be trimmed away like a null, not printed verbatim.
        [Test]
        public void GIVEN_unknown_hair_as_empty_or_whitespace_WHEN_composing_THEN_the_hair_clause_is_omitted()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = "   ", HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().Be("");
        }

        // HasBeard only renders for a male subject (see the female-flagged test below); pins the ordinary
        // case where the beard clause IS expected to appear.
        [Test]
        public void GIVEN_a_male_with_a_beard_WHEN_composing_THEN_the_beard_clause_is_included()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = true};

            AppearanceComposer.Compose(facts, "").Should().Be("A man, with a beard.");
        }

        // ── Skin tone (an adjective beside height/build) ────────────────────

        // Skin tone is rendered as an adjective alongside height/build, not as its own sentence or a
        // "with" clause; pins exactly where in the sentence it lands.
        [Test]
        public void GIVEN_a_skin_tone_WHEN_composing_THEN_it_reads_as_an_adjective_beside_build()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 45, HeightNormalized = AppearanceComposer.HeightTallAbove + 0.1, BuildNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, WeightNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, HairColorDescriptor = "dark", HasBeard = false, SkinToneDescriptor = "dark-skinned"};

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A tall, powerfully built, dark-skinned man in his middle years, with dark hair.");
        }

        // Guards that a null/absent skin tone never leaks a stray "skinned" fragment into the sentence.
        [Test]
        public void GIVEN_no_skin_tone_WHEN_composing_THEN_no_skin_adjective_appears()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().NotContain("skinned");
        }

        // The minimal-fact case: with nothing else derivable, skin tone plus age alone must still produce
        // a complete, grammatical sentence, not a truncated or malformed one.
        [Test]
        public void GIVEN_only_a_skin_tone_and_age_WHEN_composing_THEN_it_still_reads()
        {
            var facts = new AppearanceFacts {IsFemale = true, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false, SkinToneDescriptor = "fair-skinned"};

            AppearanceComposer.Compose(facts, "").Should().Be("A fair-skinned woman in her prime.");
        }

        // ── Eye colour (a "with ... eyes" clause beside hair) ───────────────

        // Eye colour is banded HEURISTICALLY (the engine exposes no eye palette; ROADMAP flags the bands
        // as TUNE) and reads in the same "with ... hair and ... eyes" clause as hair, never its own
        // sentence; pins that ordering.
        [Test]
        public void GIVEN_an_eye_colour_WHEN_composing_THEN_it_reads_in_the_with_clause_after_hair()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = "dark", HasBeard = false, EyeColorDescriptor = "blue"};

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime, with dark hair and blue eyes.");
        }

        // JoinNatural must handle three with-clause items (hair, eyes, beard) with no Oxford comma; three
        // items is the case a join implementation built only for two would get wrong.
        [Test]
        public void GIVEN_hair_eyes_and_a_beard_WHEN_composing_THEN_the_with_clause_joins_naturally()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = "dark", HasBeard = true, EyeColorDescriptor = "green"};

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime, with dark hair, green eyes and a beard.");
        }

        // Two with-clause items (no eyes) still join as "X and Y"; guards the two-item branch of
        // JoinNatural independently of the three-item case above.
        [Test]
        public void GIVEN_hair_and_a_beard_together_WHEN_composing_THEN_both_join_in_one_with_clause()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = "dark", HasBeard = true};

            AppearanceComposer.Compose(facts, "").Should().Be("A man, with dark hair and a beard.");
        }

        // HasBeard is gated by !facts.IsFemale in the composer; this guards that a caller passing
        // hasBeard=true on a female fact (a bad extractor read, or a stale profile) can never render a
        // beard clause regardless.
        [Test]
        public void GIVEN_a_female_flagged_with_a_beard_WHEN_composing_THEN_the_beard_clause_never_appears()
        {
            var facts = new AppearanceFacts {IsFemale = true, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = true};

            AppearanceComposer.Compose(facts, "").Should().Be("");
        }

        // The header's contract: Compose returns "" (never null) when nothing derived stands out and no
        // prose was authored, so the caller can render nothing without a null-check landmine.
        [Test]
        public void GIVEN_wholly_neutral_facts_and_empty_prose_WHEN_composing_THEN_it_is_an_empty_string_not_null()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().Be("");
            AppearanceComposer.Compose(facts, null).Should().Be("");
        }

        // When nothing is derived, the authored Encyclopedia note must stand alone, not be prefixed by an
        // empty or malformed derived clause (e.g. a stray "A man." ahead of Gabriel's own words).
        [Test]
        public void GIVEN_wholly_neutral_facts_and_authored_prose_WHEN_composing_THEN_only_the_authored_prose_is_returned()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 0, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "  They carry a mysterious air and limp on the left leg.  ")
                .Should().Be("They carry a mysterious air and limp on the left leg.");
        }

        // The derived sentence and the authored prose are two DISTINCT sentences (space-joined, not merged
        // into one run-on); this is the exact format both the prompt block and the Encyclopedia preview
        // rely on.
        [Test]
        public void GIVEN_both_derived_facts_and_authored_prose_WHEN_composing_THEN_the_authored_prose_is_appended_as_its_own_sentence()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 45, HeightNormalized = AppearanceComposer.HeightTallAbove + 0.1, BuildNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, WeightNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, HairColorDescriptor = "dark", HasBeard = false};

            AppearanceComposer.Compose(facts, "They carry a mysterious air and limp on the left leg.")
                .Should().Be("A tall, powerfully built man in his middle years, with dark hair. " +
                              "They carry a mysterious air and limp on the left leg.");
        }

        // Pins the female age-possessive pronoun ("her") in the default Subject voice, paired with the
        // male test next.
        [Test]
        public void GIVEN_a_female_in_her_prime_WHEN_composing_THEN_the_pronoun_reads_her()
        {
            var facts = new AppearanceFacts {IsFemale = true, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().Be("A woman in her prime.");
        }

        // Pins the male age-possessive pronoun ("his"), so a future refactor of AgePossessive can't
        // silently swap the two genders' pronouns.
        [Test]
        public void GIVEN_a_male_in_his_prime_WHEN_composing_THEN_the_pronoun_reads_his()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime.");
        }

        // Gabriel's own no-em-dash writing convention (greppable, project-wide) extends to generated text
        // too; guards that no path through Compose (JoinNatural, the derived/authored join) ever emits one.
        [Test]
        public void GIVEN_no_facts_at_all_WHEN_composing_THEN_no_em_dash_ever_appears_in_output()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 45, HeightNormalized = AppearanceComposer.HeightTallAbove + 0.1, BuildNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, WeightNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, HairColorDescriptor = "dark", HasBeard = true};

            string result = AppearanceComposer.Compose(facts, "A mysterious air clings to them.");

            result.Should().NotContain("—");
        }

        // ── Compose, Self voice (the speaking NPC's own body, second person) ──

        // The Self voice is how the SPEAKING NPC reads its own body ("You are...", "your middle years"),
        // grounding its self-image per the header's framing note; it must never leak third-person pronouns.
        [Test]
        public void GIVEN_the_self_voice_WHEN_composing_THEN_it_reads_second_person_with_a_your_possessive()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 45, HeightNormalized = AppearanceComposer.HeightTallAbove + 0.1, BuildNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, WeightNormalized = AppearanceComposer.BuildPowerfulAbove + 0.1, HairColorDescriptor = "dark", HasBeard = false};

            AppearanceComposer.Compose(facts, "", AppearanceVoice.Self)
                .Should().Be("You are a tall, powerfully built man in your middle years, with dark hair.");
        }

        // Guards that the Self voice's possessive is ALWAYS "your", overriding the gendered her/his the
        // Subject voice would otherwise use for the same facts.
        [Test]
        public void GIVEN_a_female_in_the_self_voice_WHEN_composing_THEN_the_age_possessive_reads_your_not_her()
        {
            var facts = new AppearanceFacts {IsFemale = true, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "", AppearanceVoice.Self).Should().Be("You are a woman in your prime.");
        }

        // AppearanceVoice.Subject is the default (third person); pins that callers who omit the voice
        // parameter (most existing call sites, e.g. EncounterContextBuilder for the player) get the
        // third-person framing, not Self.
        [Test]
        public void GIVEN_no_explicit_voice_WHEN_composing_THEN_it_defaults_to_the_third_person_subject_voice()
        {
            var facts = new AppearanceFacts {IsFemale = false, AgeYears = 30, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5, HairColorDescriptor = null, HasBeard = false};

            AppearanceComposer.Compose(facts, "").Should().Be(AppearanceComposer.Compose(facts, "", AppearanceVoice.Subject));
            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime.");
        }
    }
}
