// Code written by Gabriel Mailhot, 30/09/2026.
// The perceived image (ROADMAP, SELF-ESTEEM, P1; Gabriel 30/09/2026: "la combinaison des deux devrait permettre de
// generer une image percue du NPC qui serait affichee dans le panneau d'info sur le NPC dans le chat"). One numberless
// sentence: how he carries himself (self-esteem) and how his day shows (the mood of the day). Ruling 2: it shows what
// shows, so a man who keeps face hides a shaken self-regard from a stranger, and those who know him see through it.
// If this is wrong, the player reads a number in words (the rejected bar), or sees straight through every mask, or
// never learns to read a character at all.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Mood;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PerceivedImagePolicyTests
    {
        private static string Seen(SelfEsteemBand? esteem, DayMood? mood, bool female = false, bool knowsWell = false, bool masks = false)
            => PerceivedImagePolicy.Compose(new PerceivedImageFacts {SelfEsteem = esteem, Mood = mood, IsFemale = female, KnowsHimWell = knowsWell, KeepsFace = masks});

        // Both halves, in one sentence: the bearing and the day.
        [Test]
        public void GIVEN_a_proud_man_on_a_grim_day_WHEN_seen_THEN_both_his_bearing_and_his_day_show()
        {
            string seen = Seen(SelfEsteemBand.Proud, DayMood.Grim);

            seen.Should().Contain("deferred to");
            seen.Should().Contain("grim");
        }

        // A woman is described as she is.
        [Test]
        public void GIVEN_a_woman_WHEN_seen_THEN_she_is_described_as_she()
        {
            Seen(SelfEsteemBand.Assured, DayMood.Expansive, female: true).Should().StartWith("She ");
        }

        // Ruling 2: a man who keeps face hides a shaken self-regard from a stranger.
        [Test]
        public void GIVEN_a_shaken_man_who_keeps_face_WHEN_a_stranger_looks_THEN_he_seems_at_ease()
        {
            string seen = Seen(SelfEsteemBand.Shaken, null, masks: true);

            seen.Should().Contain("easily");
            seen.Should().NotContain("blow");
        }

        // ... and one who knows him well sees the strain beneath: the image rewards the relationship.
        [Test]
        public void GIVEN_a_shaken_man_who_keeps_face_WHEN_someone_who_knows_him_well_looks_THEN_they_see_the_strain()
        {
            Seen(SelfEsteemBand.Shaken, null, knowsWell: true, masks: true).Should().Contain("strain");
        }

        // A man who does not keep face shows what he is, to anyone.
        [Test]
        public void GIVEN_a_shaken_man_who_does_not_keep_face_WHEN_a_stranger_looks_THEN_it_shows()
        {
            Seen(SelfEsteemBand.Shaken, null).Should().Contain("blow");
        }

        // An ordinary day adds nothing to the sentence but a full stop.
        [Test]
        public void GIVEN_a_steady_man_on_an_ordinary_day_WHEN_seen_THEN_a_plain_sentence_is_given()
        {
            Seen(SelfEsteemBand.Steady, DayMood.Even).Should().Be("He carries himself easily.");
        }

        // Nothing known, nothing said: the section hides.
        [Test]
        public void GIVEN_nothing_known_WHEN_seen_THEN_nothing_is_said()
        {
            Seen(null, null).Should().BeNull();
        }

        // Never a number, never a label from the code.
        [Test]
        public void GIVEN_any_reading_WHEN_seen_THEN_no_number_or_band_name_appears()
        {
            string seen = Seen(SelfEsteemBand.Broken, DayMood.ShortTempered);

            seen.Should().NotMatchRegex("[0-9]");
            seen.Should().NotContain("Broken");
            seen.Should().NotContain("ShortTempered");
        }
    }
}
