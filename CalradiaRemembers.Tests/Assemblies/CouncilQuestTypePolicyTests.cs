// Code written by Gabriel Mailhot, 26/08/2026.
// Documents the council quest_type grounding (player report 2026-08-25: eight siege-council pledges all
// grounded as bandit-clear tasks on the same settlement, so clearing ONE bandit camp stamped the whole
// table's quests "Done" at once). The policy decides WHICH deed a pledge grounds as; the codec round-trips
// that decision through the ledger entry's single TargetHint slot, legacy bare-settlement entries included.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilQuestTypePolicyTests
    {
        // Each taught token must ground as its OWN deed type: a siege pledge that grounded as anything else
        // would again complete on the wrong event, the exact bug this feature fixes.
        [Test]
        public void GIVEN_each_teachable_token_WHEN_resolved_THEN_it_maps_to_its_own_deed_type()
        {
            CouncilQuestTypePolicy.Resolve("siege").Should().Be(QuestType.Siege);
            CouncilQuestTypePolicy.Resolve("scout_army").Should().Be(QuestType.ScoutArmy);
            CouncilQuestTypePolicy.Resolve("raid_village").Should().Be(QuestType.RaidVillage);
            CouncilQuestTypePolicy.Resolve("bandit_hideout").Should().Be(QuestType.BanditHideout);
            CouncilQuestTypePolicy.Resolve("bandit_clear").Should().Be(QuestType.BanditClear);
        }

        // The prompt's vocabulary list and the resolver must agree exactly, or the model would be taught a
        // token that grounds as something else (or falls back), silently breaking the pledge it spoke.
        [Test]
        public void GIVEN_the_teachable_list_WHEN_resolved_THEN_every_token_grounds_as_its_own_deed()
        {
            CouncilQuestTypePolicy.TeachableTokens.Should().HaveCount(5);

            foreach (string token in CouncilQuestTypePolicy.TeachableTokens)
            {
                QuestType resolved = CouncilQuestTypePolicy.Resolve(token);

                if (token == "bandit_clear") resolved.Should().Be(QuestType.BanditClear);
                else resolved.Should().NotBe(QuestType.BanditClear,
                    "the taught token '{0}' must ground as its own deed, not the fallback", token);
            }
        }

        // Legacy behavior is the fallback: a pledge recorded before quest_type existed (or a model that simply
        // omitted it) grounds exactly as it always has, never refused for lacking a type.
        [Test]
        public void GIVEN_no_token_WHEN_resolved_THEN_the_legacy_bandit_clear_grounding_is_kept()
        {
            CouncilQuestTypePolicy.Resolve(null).Should().Be(QuestType.BanditClear);
            CouncilQuestTypePolicy.Resolve("").Should().Be(QuestType.BanditClear);
            CouncilQuestTypePolicy.Resolve("   ").Should().Be(QuestType.BanditClear);
        }

        // An unrecognized token must never fail the pledge NOR be guessed into a different deed: it grounds
        // exactly as it would have before quest_type existed.
        [Test]
        public void GIVEN_an_unrecognized_token_WHEN_resolved_THEN_it_falls_back_to_bandit_clear()
        {
            CouncilQuestTypePolicy.Resolve("show_mercy").Should().Be(QuestType.BanditClear);
        }

        // A RECOGNIZED but unsupported deed (hero-bound: the [RESOLUTION] block teaches no target_hero, so it
        // could never ground) must fall back too, never produce a quest that cannot be completed.
        [Test]
        public void GIVEN_a_recognized_but_hero_bound_token_WHEN_resolved_THEN_it_falls_back_to_bandit_clear()
        {
            CouncilQuestTypePolicy.Resolve("capture_prisoner").Should().Be(QuestType.BanditClear);
            CouncilQuestTypePolicy.Resolve("execute_enemy").Should().Be(QuestType.BanditClear);
        }

        // Synonym tolerance comes from the SDK's single vocabulary: a model that writes "besiege" instead of
        // "siege" still grounds the siege deed.
        [Test]
        public void GIVEN_a_synonym_token_WHEN_resolved_THEN_the_sdk_vocabulary_applies()
        {
            CouncilQuestTypePolicy.Resolve("besiege").Should().Be(QuestType.Siege);
            CouncilQuestTypePolicy.Resolve("scout").Should().Be(QuestType.ScoutArmy);
        }
    }

    [TestFixture]
    public sealed class QuestGroundingCodecTests
    {
        // The round-trip the whole feature rides on: what RecordCouncilResolution composes must come back
        // apart intact at the lift, or the pledge grounds on a mangled settlement name and is refused.
        [Test]
        public void GIVEN_a_type_and_settlement_WHEN_composed_then_decomposed_THEN_both_halves_survive()
        {
            string hint = QuestGroundingCodec.Compose("siege", "Pravend");

            QuestGroundingCodec.TryDecompose(hint, out string questType, out string settlement).Should().BeTrue();
            questType.Should().Be("siege");
            settlement.Should().Be("Pravend");
        }

        // A pledge naming no quest_type must write the LEGACY shape (the bare settlement), so an un-typed
        // pledge stays byte-identical to what every council quest carried before this feature.
        [Test]
        public void GIVEN_no_quest_type_WHEN_composed_THEN_the_bare_legacy_settlement_is_written()
        {
            QuestGroundingCodec.Compose(null, "Pravend").Should().Be("Pravend");
            QuestGroundingCodec.Compose("  ", "Pravend").Should().Be("Pravend");
        }

        // The backward-compatibility contract: a ledger entry recorded BEFORE quest_type existed carries a
        // bare settlement with no delimiter; it must decompose to (null, whole string) so the lift's fallback
        // grounds it as the legacy bandit-clear rather than failing the pledge.
        [Test]
        public void GIVEN_a_legacy_bare_settlement_WHEN_decomposed_THEN_the_type_is_null_and_the_place_whole()
        {
            QuestGroundingCodec.TryDecompose("Pravend", out string questType, out string settlement).Should().BeTrue();
            questType.Should().BeNull();
            settlement.Should().Be("Pravend");
        }

        // A blank hint (a pledge that named no place at all) must still FAIL to decompose, so the lift refuses
        // it exactly as it always has.
        [Test]
        public void GIVEN_a_blank_hint_WHEN_decomposed_THEN_it_fails()
        {
            QuestGroundingCodec.TryDecompose(null, out _, out _).Should().BeFalse();
            QuestGroundingCodec.TryDecompose("  ", out _, out _).Should().BeFalse();
        }
    }
}
