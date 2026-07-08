// Code written by Gabriel Mailhot, 05/07/2026.
// Player appearance pillar, composer increment: derived body-fact banding + authored prose, pure
// natural-language rendering. See AppearanceComposer's own header for the design.

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

        [Test]
        public void GIVEN_height_below_the_short_threshold_WHEN_phrasing_THEN_it_reads_short()
        {
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightShortBelow - 0.01).Should().Be("short");
        }

        [Test]
        public void GIVEN_height_above_the_tall_threshold_WHEN_phrasing_THEN_it_reads_tall()
        {
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightTallAbove + 0.01).Should().Be("tall");
        }

        [Test]
        public void GIVEN_average_height_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.HeightPhrase(0.5).Should().BeNull();
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightShortBelow).Should().BeNull();
            AppearanceComposer.HeightPhrase(AppearanceComposer.HeightTallAbove).Should().BeNull();
        }

        // ── BuildPhrase (build + weight combined) ───────────────────────────

        [Test]
        public void GIVEN_a_slight_build_and_weight_WHEN_phrasing_THEN_it_reads_slightly_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildSlightBelow - 0.05, AppearanceComposer.BuildSlightBelow - 0.05)
                .Should().Be("slightly built");
        }

        [Test]
        public void GIVEN_a_solid_build_and_weight_WHEN_phrasing_THEN_it_reads_solidly_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildSolidAbove + 0.05, AppearanceComposer.BuildSolidAbove + 0.05)
                .Should().Be("solidly built");
        }

        [Test]
        public void GIVEN_a_powerful_build_and_weight_WHEN_phrasing_THEN_it_reads_powerfully_built()
        {
            AppearanceComposer.BuildPhrase(AppearanceComposer.BuildPowerfulAbove + 0.05, AppearanceComposer.BuildPowerfulAbove + 0.05)
                .Should().Be("powerfully built");
        }

        [Test]
        public void GIVEN_an_average_build_and_weight_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.BuildPhrase(0.5, 0.5).Should().BeNull();
        }

        // ── AgePhrase, boundary tests ────────────────────────────────────────

        [Test]
        public void GIVEN_age_just_below_the_youth_cutoff_WHEN_phrasing_THEN_it_reads_youth()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeYouthBelow - 1).Should().Be("youth");
        }

        [Test]
        public void GIVEN_age_at_the_youth_cutoff_WHEN_phrasing_THEN_it_reads_prime()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeYouthBelow).Should().Be("prime");
        }

        [Test]
        public void GIVEN_age_just_below_the_prime_cutoff_WHEN_phrasing_THEN_it_reads_prime()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgePrimeBelow - 1).Should().Be("prime");
        }

        [Test]
        public void GIVEN_age_at_the_prime_cutoff_WHEN_phrasing_THEN_it_reads_middle_years()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgePrimeBelow).Should().Be("middle years");
        }

        [Test]
        public void GIVEN_age_just_below_the_middle_cutoff_WHEN_phrasing_THEN_it_reads_middle_years()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeMiddleBelow - 1).Should().Be("middle years");
        }

        [Test]
        public void GIVEN_age_at_the_middle_cutoff_WHEN_phrasing_THEN_it_reads_old_age()
        {
            AppearanceComposer.AgePhrase(AppearanceComposer.AgeMiddleBelow).Should().Be("old age");
        }

        [Test]
        public void GIVEN_age_zero_or_below_WHEN_phrasing_THEN_it_is_null()
        {
            AppearanceComposer.AgePhrase(0).Should().BeNull();
            AppearanceComposer.AgePhrase(-1).Should().BeNull();
        }

        // ── Compose, full sentences ──────────────────────────────────────────

        [Test]
        public void GIVEN_a_tall_powerful_male_in_his_middle_years_with_dark_hair_WHEN_composing_THEN_it_reads_the_flagship_sentence()
        {
            var facts = new AppearanceFacts(
                isFemale: false,
                ageYears: 45,
                heightNormalized: AppearanceComposer.HeightTallAbove + 0.1,
                buildNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                weightNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                hairColorDescriptor: "dark",
                hasBeard: false);

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A tall, powerfully built man in his middle years, with dark hair.");
        }

        [Test]
        public void GIVEN_a_short_slight_female_with_no_hair_descriptor_WHEN_composing_THEN_the_hair_clause_is_omitted()
        {
            var facts = new AppearanceFacts(
                isFemale: true,
                ageYears: 0,
                heightNormalized: AppearanceComposer.HeightShortBelow - 0.1,
                buildNormalized: AppearanceComposer.BuildSlightBelow - 0.1,
                weightNormalized: AppearanceComposer.BuildSlightBelow - 0.1,
                hairColorDescriptor: null,
                hasBeard: false);

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A short, slightly built woman.");
        }

        [Test]
        public void GIVEN_unknown_hair_as_empty_or_whitespace_WHEN_composing_THEN_the_hair_clause_is_omitted()
        {
            var facts = new AppearanceFacts(false, 0, 0.5, 0.5, 0.5, "   ", false);

            AppearanceComposer.Compose(facts, "").Should().Be("");
        }

        [Test]
        public void GIVEN_a_male_with_a_beard_WHEN_composing_THEN_the_beard_clause_is_included()
        {
            var facts = new AppearanceFacts(false, 0, 0.5, 0.5, 0.5, null, hasBeard: true);

            AppearanceComposer.Compose(facts, "").Should().Be("A man, with a beard.");
        }

        // ── Skin tone (an adjective beside height/build) ────────────────────

        [Test]
        public void GIVEN_a_skin_tone_WHEN_composing_THEN_it_reads_as_an_adjective_beside_build()
        {
            var facts = new AppearanceFacts(
                isFemale: false,
                ageYears: 45,
                heightNormalized: AppearanceComposer.HeightTallAbove + 0.1,
                buildNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                weightNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                hairColorDescriptor: "dark",
                hasBeard: false,
                skinToneDescriptor: "dark-skinned");

            AppearanceComposer.Compose(facts, "")
                .Should().Be("A tall, powerfully built, dark-skinned man in his middle years, with dark hair.");
        }

        [Test]
        public void GIVEN_no_skin_tone_WHEN_composing_THEN_no_skin_adjective_appears()
        {
            var facts = new AppearanceFacts(false, 30, 0.5, 0.5, 0.5, null, false, skinToneDescriptor: null);

            AppearanceComposer.Compose(facts, "").Should().NotContain("skinned");
        }

        [Test]
        public void GIVEN_only_a_skin_tone_and_age_WHEN_composing_THEN_it_still_reads()
        {
            var facts = new AppearanceFacts(true, 30, 0.5, 0.5, 0.5, null, false, skinToneDescriptor: "fair-skinned");

            AppearanceComposer.Compose(facts, "").Should().Be("A fair-skinned woman in her prime.");
        }

        // ── Eye colour (a "with ... eyes" clause beside hair) ───────────────

        [Test]
        public void GIVEN_an_eye_colour_WHEN_composing_THEN_it_reads_in_the_with_clause_after_hair()
        {
            var facts = new AppearanceFacts(false, 30, 0.5, 0.5, 0.5, "dark", false,
                skinToneDescriptor: null, eyeColorDescriptor: "blue");

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime, with dark hair and blue eyes.");
        }

        [Test]
        public void GIVEN_hair_eyes_and_a_beard_WHEN_composing_THEN_the_with_clause_joins_naturally()
        {
            var facts = new AppearanceFacts(false, 30, 0.5, 0.5, 0.5, "dark", hasBeard: true,
                skinToneDescriptor: null, eyeColorDescriptor: "green");

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime, with dark hair, green eyes and a beard.");
        }

        [Test]
        public void GIVEN_hair_and_a_beard_together_WHEN_composing_THEN_both_join_in_one_with_clause()
        {
            var facts = new AppearanceFacts(false, 0, 0.5, 0.5, 0.5, "dark", hasBeard: true);

            AppearanceComposer.Compose(facts, "").Should().Be("A man, with dark hair and a beard.");
        }

        [Test]
        public void GIVEN_a_female_flagged_with_a_beard_WHEN_composing_THEN_the_beard_clause_never_appears()
        {
            var facts = new AppearanceFacts(true, 0, 0.5, 0.5, 0.5, null, hasBeard: true);

            AppearanceComposer.Compose(facts, "").Should().Be("");
        }

        [Test]
        public void GIVEN_wholly_neutral_facts_and_empty_prose_WHEN_composing_THEN_it_is_an_empty_string_not_null()
        {
            var facts = new AppearanceFacts(false, 0, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "").Should().Be("");
            AppearanceComposer.Compose(facts, null).Should().Be("");
        }

        [Test]
        public void GIVEN_wholly_neutral_facts_and_authored_prose_WHEN_composing_THEN_only_the_authored_prose_is_returned()
        {
            var facts = new AppearanceFacts(false, 0, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "  They carry a mysterious air and limp on the left leg.  ")
                .Should().Be("They carry a mysterious air and limp on the left leg.");
        }

        [Test]
        public void GIVEN_both_derived_facts_and_authored_prose_WHEN_composing_THEN_the_authored_prose_is_appended_as_its_own_sentence()
        {
            var facts = new AppearanceFacts(
                isFemale: false,
                ageYears: 45,
                heightNormalized: AppearanceComposer.HeightTallAbove + 0.1,
                buildNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                weightNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                hairColorDescriptor: "dark",
                hasBeard: false);

            AppearanceComposer.Compose(facts, "They carry a mysterious air and limp on the left leg.")
                .Should().Be("A tall, powerfully built man in his middle years, with dark hair. " +
                              "They carry a mysterious air and limp on the left leg.");
        }

        [Test]
        public void GIVEN_a_female_in_her_prime_WHEN_composing_THEN_the_pronoun_reads_her()
        {
            var facts = new AppearanceFacts(true, 30, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "").Should().Be("A woman in her prime.");
        }

        [Test]
        public void GIVEN_a_male_in_his_prime_WHEN_composing_THEN_the_pronoun_reads_his()
        {
            var facts = new AppearanceFacts(false, 30, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime.");
        }

        [Test]
        public void GIVEN_no_facts_at_all_WHEN_composing_THEN_no_em_dash_ever_appears_in_output()
        {
            var facts = new AppearanceFacts(
                isFemale: false,
                ageYears: 45,
                heightNormalized: AppearanceComposer.HeightTallAbove + 0.1,
                buildNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                weightNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                hairColorDescriptor: "dark",
                hasBeard: true);

            string result = AppearanceComposer.Compose(facts, "A mysterious air clings to them.");

            result.Should().NotContain("—");
        }

        // ── Compose, Self voice (the speaking NPC's own body, second person) ──

        [Test]
        public void GIVEN_the_self_voice_WHEN_composing_THEN_it_reads_second_person_with_a_your_possessive()
        {
            var facts = new AppearanceFacts(
                isFemale: false,
                ageYears: 45,
                heightNormalized: AppearanceComposer.HeightTallAbove + 0.1,
                buildNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                weightNormalized: AppearanceComposer.BuildPowerfulAbove + 0.1,
                hairColorDescriptor: "dark",
                hasBeard: false);

            AppearanceComposer.Compose(facts, "", AppearanceVoice.Self)
                .Should().Be("You are a tall, powerfully built man in your middle years, with dark hair.");
        }

        [Test]
        public void GIVEN_a_female_in_the_self_voice_WHEN_composing_THEN_the_age_possessive_reads_your_not_her()
        {
            var facts = new AppearanceFacts(true, 30, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "", AppearanceVoice.Self).Should().Be("You are a woman in your prime.");
        }

        [Test]
        public void GIVEN_no_explicit_voice_WHEN_composing_THEN_it_defaults_to_the_third_person_subject_voice()
        {
            var facts = new AppearanceFacts(false, 30, 0.5, 0.5, 0.5, null, false);

            AppearanceComposer.Compose(facts, "").Should().Be(AppearanceComposer.Compose(facts, "", AppearanceVoice.Subject));
            AppearanceComposer.Compose(facts, "").Should().Be("A man in his prime.");
        }
    }
}
