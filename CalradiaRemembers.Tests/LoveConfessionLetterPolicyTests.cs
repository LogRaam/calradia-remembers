// Code written by Gabriel Mailhot, 23/11/2026.
// The love letter: a character who has come to love the player, and never said so, writes to confess it. Gabriel asked
// whether a romance could ever begin on the NPC's side (23/11/2026); outside companions no one took the first step. His
// rulings: "Une lettre d'aveux ou d'amour serait tres bien"; "NPC marie devrait avoir moins de chances que ca arrive. Pas
// de relance avec un silence. Il faudrait qu'il y ait une forme d'affinite; culture, traits, etc."
// This decides who may write, how likely they are this week, and whether the feeling still stands at delivery. If it is
// wrong, a letter confesses love the next conversation forbids (yozakura12's disconnect, 09/09/2026), a companion both
// asks for an audience and writes, a confession arrives twice, a married lord writes as readily as a free one, or
// affinity changes nothing.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LoveConfessionLetterPolicyTests
    {
        private static LoveConfessionFacts Ready(RomanticRegister register = RomanticRegister.Attachment, RomanticStatus status = RomanticStatus.None,
                                                 bool companion = false, bool met = true, bool confessed = false) => new() {
            Register = register, Status = status, IsPlayerCompanion = companion, HasMet = met, AlreadyConfessed = confessed
        };

        private static LoveAffinityFacts Plain(bool married = false) => new() {GameAttractionPercent = 55, IsMarried = married};

        // STAKE: the letter says only what the conversation already allows ("you may say it first"); merely noticing the
        // player (Interest) or nothing at all (Platonic) never writes a confession the next talk would contradict.
        [Test]
        public void GIVEN_a_character_whose_conversation_would_not_let_them_declare_WHEN_judged_THEN_they_do_not_write()
        {
            LoveConfessionLetterPolicy.MayConfess(Ready()).Should().BeTrue();
            LoveConfessionLetterPolicy.MayConfess(Ready(RomanticRegister.Interest)).Should().BeFalse();
            LoveConfessionLetterPolicy.MayConfess(Ready(RomanticRegister.Platonic)).Should().BeFalse();
            LoveConfessionLetterPolicy.MayConfess(Ready(RomanticRegister.Bound)).Should().BeFalse();
        }

        // STAKE: a courtship already has its own letters, and a confession "I have never told you" is false once one began.
        [Test]
        public void GIVEN_a_courtship_already_under_way_WHEN_judged_THEN_no_confession_is_written()
        {
            LoveConfessionLetterPolicy.MayConfess(Ready(status: RomanticStatus.Curious)).Should().BeTrue();
            LoveConfessionLetterPolicy.MayConfess(Ready(status: RomanticStatus.Courting)).Should().BeFalse();
        }

        // STAKE: a companion confides on the road (the audience); writing too would be two roads for one act.
        [Test]
        public void GIVEN_a_companion_WHEN_judged_THEN_the_letter_is_not_theirs_to_write()
        {
            LoveConfessionLetterPolicy.MayConfess(Ready(companion: true)).Should().BeFalse();
        }

        // STAKE: no one confesses love to a stranger, and a confession is said once, never again by letter.
        [Test]
        public void GIVEN_a_stranger_or_one_who_already_confessed_WHEN_judged_THEN_they_do_not_write()
        {
            LoveConfessionLetterPolicy.MayConfess(Ready(met: false)).Should().BeFalse();
            LoveConfessionLetterPolicy.MayConfess(Ready(confessed: true)).Should().BeFalse();
        }

        // STAKE: Gabriel, "NPC marie devrait avoir moins de chances": a married character writes far more rarely.
        [Test]
        public void GIVEN_a_married_character_WHEN_their_chance_is_weighed_THEN_it_is_far_lower_than_a_free_ones()
        {
            int free = LoveConfessionLetterPolicy.WeeklyChancePermille(new Regard(60), RelationWarmth.Normal, Plain());
            int wed = LoveConfessionLetterPolicy.WeeklyChancePermille(new Regard(60), RelationWarmth.Normal, Plain(married: true));

            wed.Should().BeLessThan(free / 2);
        }

        // STAKE: Gabriel, "une forme d'affinite; culture, traits": a shared culture and a like character draw them on.
        [Test]
        public void GIVEN_a_shared_culture_and_traits_held_alike_WHEN_affinity_is_weighed_THEN_it_rises_and_opposed_traits_lower_it()
        {
            int plain = LoveConfessionLetterPolicy.AffinityPercent(Plain());

            LoveConfessionLetterPolicy.AffinityPercent(new LoveAffinityFacts {GameAttractionPercent = 55, SharesCulture = true}).Should().BeGreaterThan(plain);
            LoveConfessionLetterPolicy.AffinityPercent(new LoveAffinityFacts {GameAttractionPercent = 55, TraitAgreement = 3}).Should().BeGreaterThan(plain);
            LoveConfessionLetterPolicy.AffinityPercent(new LoveAffinityFacts {GameAttractionPercent = 55, TraitAgreement = -3}).Should().BeLessThan(plain);
        }

        // STAKE: the game's own courtship attraction counts, at its own bands: "drawn to you" above 70, "not my type" at
        // 40 or less (RomanceCampaignBehavior), so the letter agrees with what the game's courtship would say.
        [Test]
        public void GIVEN_the_games_courtship_attraction_WHEN_affinity_is_weighed_THEN_its_own_bands_decide()
        {
            int Of(int attraction) => LoveConfessionLetterPolicy.AffinityPercent(new LoveAffinityFacts {GameAttractionPercent = attraction});

            Of(71).Should().BeGreaterThan(Of(70));
            Of(41).Should().BeGreaterThan(Of(40));
        }

        // STAKE: deep feeling can still overcome much; a character of opposed nature is rarer, never impossible.
        [Test]
        public void GIVEN_every_trait_opposed_and_no_attraction_WHEN_weighed_THEN_a_chance_remains()
        {
            var worst = new LoveAffinityFacts {GameAttractionPercent = 0, TraitAgreement = -5};

            LoveConfessionLetterPolicy.WeeklyChancePermille(new Regard(80), RelationWarmth.Normal, worst).Should().BePositive();
        }

        // STAKE: however strong the affinity, the letter never comes more often than the companion confession's cap.
        [Test]
        public void GIVEN_the_strongest_affinity_WHEN_weighed_THEN_the_chance_stays_under_the_confessions_cap()
        {
            var best = new LoveAffinityFacts {GameAttractionPercent = 99, SharesCulture = true, TraitAgreement = 5, AttractionToPlayer = 80};

            LoveConfessionLetterPolicy.WeeklyChancePermille(new Regard(100), RelationWarmth.VeryEasy, best)
                .Should().BeLessThanOrEqualTo(CompanionSecretAffectionPolicy.MaxWeeklyChancePercent * 10);
        }

        // STAKE: the mailbox ticks daily; weighing every day would send the letter seven times as often as its weekly chance.
        [Test]
        public void GIVEN_one_character_WHEN_a_week_passes_THEN_they_weigh_writing_on_exactly_one_day()
        {
            int days = 0;
            for (int day = 100; day < 107; day++)
                if (LoveConfessionLetterPolicy.IsTheirDayToWeigh(day, 123456789u)) days++;

            days.Should().Be(1);
        }

        // STAKE: a letter on the road whose feeling fell, or whose romance began or ended meanwhile, is never delivered.
        [Test]
        public void GIVEN_a_letter_on_the_road_WHEN_the_feeling_fell_or_a_romance_began_THEN_it_no_longer_stands()
        {
            LoveConfessionLetterPolicy.IsStillHeartfelt(RomanticRegister.Attachment, RomanticStatus.None).Should().BeTrue();
            LoveConfessionLetterPolicy.IsStillHeartfelt(RomanticRegister.Interest, RomanticStatus.None).Should().BeFalse();
            LoveConfessionLetterPolicy.IsStillHeartfelt(RomanticRegister.Attachment, RomanticStatus.Courting).Should().BeFalse();
        }
    }
}
