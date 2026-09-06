// Code written by Gabriel Mailhot, 06/09/2026.
// BattleFeatDistiller decides which of a battle's many entries an NPC would actually bring up. It is the guard
// against the obvious failure of this whole pillar: a battle can produce dozens of entries, and a memory that kept
// them all would be a casualty list rather than a recollection, bloating every profile in the save and making lords
// recite statistics at the player. It is equally a guard the other way: the ONE deed worth naming, and the foe who
// put the player down (the seed of a rivalry or a grudge), must survive the cut, or the feature records nothing
// worth talking about.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Battles
{
    [TestFixture]
    public sealed class BattleFeatDistillerTests
    {
        private static CombatFeat Feat(string name, CombatFeatTier tier)
            => new CombatFeat(name.ToLowerInvariant(), name, CombatFeatOutcome.Bested, tier);

        // Beating someone far beneath you is not a memory: an Unremarkable feat must be dropped outright, or every
        // ridden-down bandit would end up in a lord's recollection.
        [Test]
        public void GIVEN_only_unremarkable_feats_WHEN_distilled_THEN_nothing_is_kept()
        {
            BattleFeatDigest digest = BattleFeatDistiller.Distil(
                new[] {Feat("Bandit", CombatFeatTier.Unremarkable)}, null, 0);

            digest.BestFeat.Should().BeNull();
            digest.IsEmpty.Should().BeTrue();
        }

        // The standout deed must win over a lesser one: an exploit is exactly what the NPC would raise, and it must
        // not be crowded out by an ordinary win recorded earlier in the same battle.
        [Test]
        public void GIVEN_a_notable_and_an_exploit_WHEN_distilled_THEN_the_exploit_is_chosen()
        {
            BattleFeatDigest digest = BattleFeatDistiller.Distil(
                new[] {Feat("Ordinary", CombatFeatTier.Notable), Feat("Champion", CombatFeatTier.Exploit)}, null, 0);

            digest.BestFeat.FoeName.Should().Be("Champion");
        }

        // Only ONE deed survives, however many were recorded: the cap is what keeps a memory from becoming a list.
        [Test]
        public void GIVEN_many_worthy_feats_WHEN_distilled_THEN_only_one_is_kept()
        {
            var feats = new List<CombatFeat>();
            for (var i = 0; i < 8; i++) feats.Add(Feat($"Lord{i}", CombatFeatTier.Notable));

            BattleFeatDistiller.Distil(feats, null, 0).BestFeat.Should().NotBeNull();
            // (a digest holds exactly one BestFeat by construction; this pins that the cap is not bypassed)
            BattleFeatDistiller.Distil(feats, null, 0).BestFeat.FoeName.Should().Be("Lord0");
        }

        // On an equal tier the FIRST recorded wins, so the same battle always distils to the same memory instead of
        // shifting with enumeration order.
        [Test]
        public void GIVEN_equal_tiers_WHEN_distilled_THEN_the_first_recorded_wins()
        {
            BattleFeatDigest digest = BattleFeatDistiller.Distil(
                new[] {Feat("First", CombatFeatTier.Notable), Feat("Second", CombatFeatTier.Notable)}, null, 0);

            digest.BestFeat.FoeName.Should().Be("First");
        }

        // The mirror deed is kept whatever else happened: who put the player down is the seed of a rivalry or a
        // grudge, so it must never be crowded out by the player's own victories.
        [Test]
        public void GIVEN_the_player_was_downed_WHEN_distilled_THEN_that_foe_is_kept()
        {
            BattleFeatDigest digest = BattleFeatDistiller.Distil(
                new[] {Feat("Champion", CombatFeatTier.Exploit)}, "Ganak", 0);

            digest.DownedThePlayer.Should().Be("Ganak");
            digest.BestFeat.Should().NotBeNull();
        }

        // A small toll is an ordinary day's fighting and must stay silent: an NPC quoting "you felled three men"
        // reads as a statistic, which is exactly what Gabriel ruled against.
        [Test]
        public void GIVEN_an_ordinary_toll_WHEN_distilled_THEN_it_is_not_mentioned()
        {
            BattleFeatDistiller.Distil(null, null, BattleFeatDistiller.RemarkableTollThreshold - 1)
                .TollToMention.Should().Be(0);
        }

        // At the threshold the showing is genuinely remarkable and worth a word, so the boundary is inclusive and a
        // real feat of arms is not lost to an off-by-one.
        [Test]
        public void GIVEN_a_remarkable_toll_WHEN_distilled_THEN_it_is_mentioned()
        {
            int toll = BattleFeatDistiller.RemarkableTollThreshold;

            BattleFeatDistiller.Distil(null, null, toll).TollToMention.Should().Be(toll);
        }

        // An empty battle leaves nothing to add beyond the ordinary "we fought together" line the mod already
        // writes, so the caller can skip appending anything at all.
        [Test]
        public void GIVEN_nothing_of_note_WHEN_distilled_THEN_the_digest_is_empty()
        {
            BattleFeatDistiller.Distil(null, null, 0).IsEmpty.Should().BeTrue();
            BattleFeatDistiller.Distil(new List<CombatFeat>(), "   ", 0).IsEmpty.Should().BeTrue();
        }
    }
}
