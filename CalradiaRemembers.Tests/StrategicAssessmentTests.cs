// Code written by Gabriel Mailhot, 04/09/2026.
//
// WHY IT MATTERS: the Strategic Assessment is a COMPANION strategist counselling the player from the World Almanac,
// and its whole value rests on two rules that are invisible once it is LLM prose: the counsel's QUALITY must scale
// with the advisor's Tactics skill (the player's incentive to keep a good strategist), and the advisor must be
// grounded in the REAL standings and the player's own position while never being licensed to invent a fact. Both
// live in the pure prompt this builds; if the persona, the quality dial, the facts, the player's frame, or the
// no-invention rule ever fell out of the prompt, the feature would silently drift into a generic, ungrounded blurb.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StrategicAssessmentTests
    {
        // ── Tactics -> counsel quality tier ─────────────────────────────────

        // The bands are the dial for "how good must a strategist be to give expert counsel". A drift here quietly
        // rescales every advisor's quality, so each boundary is pinned.
        [Test]
        public void GIVEN_tactics_levels_WHEN_tiering_THEN_each_boundary_lands_in_the_right_tier()
        {
            TacticsAdvicePolicy.TierFor(-5).Should().Be(AdvisorTacticsTier.Green);
            TacticsAdvicePolicy.TierFor(0).Should().Be(AdvisorTacticsTier.Green);
            TacticsAdvicePolicy.TierFor(49).Should().Be(AdvisorTacticsTier.Green);
            TacticsAdvicePolicy.TierFor(50).Should().Be(AdvisorTacticsTier.Seasoned);
            TacticsAdvicePolicy.TierFor(99).Should().Be(AdvisorTacticsTier.Seasoned);
            TacticsAdvicePolicy.TierFor(100).Should().Be(AdvisorTacticsTier.Veteran);
            TacticsAdvicePolicy.TierFor(174).Should().Be(AdvisorTacticsTier.Veteran);
            TacticsAdvicePolicy.TierFor(175).Should().Be(AdvisorTacticsTier.Expert);
            TacticsAdvicePolicy.TierFor(249).Should().Be(AdvisorTacticsTier.Expert);
            TacticsAdvicePolicy.TierFor(250).Should().Be(AdvisorTacticsTier.Master);
            TacticsAdvicePolicy.TierFor(300).Should().Be(AdvisorTacticsTier.Master);
        }

        // Every tier must hand the model a non-empty instruction, and a master must not read like a novice: if the
        // guidance were the same across tiers, Tactics would buy the player nothing.
        [Test]
        public void GIVEN_the_tiers_WHEN_reading_guidance_THEN_each_is_present_and_a_master_differs_from_a_green()
        {
            string master = TacticsAdvicePolicy.GuidanceFor(AdvisorTacticsTier.Master);
            string green = TacticsAdvicePolicy.GuidanceFor(AdvisorTacticsTier.Green);

            master.Should().NotBeNullOrWhiteSpace();
            green.Should().NotBeNullOrWhiteSpace();
            master.Should().NotBe(green);
        }

        // ── Prompt: persona, quality dial, grounded facts, player frame, no invention ──

        // The counsel must be SPOKEN by the named companion, so the advisor's name and culture reach the prompt.
        [Test]
        public void GIVEN_an_advisor_WHEN_building_the_prompt_THEN_it_names_them_and_their_culture()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("Ira");
            prompt.Should().Contain("Battanian");
        }

        // THE quality rule: a master's prompt carries the master guidance, a green's the green guidance, so the LLM
        // is steered to sharper or vaguer counsel by the advisor's Tactics. This is the player's whole incentive.
        [Test]
        public void GIVEN_a_master_versus_a_green_advisor_WHEN_building_THEN_the_quality_guidance_matches_the_skill()
        {
            string master = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 260));
            string green = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Boots", "Vlandian", 10));

            master.Should().Contain(TacticsAdvicePolicy.GuidanceFor(AdvisorTacticsTier.Master));
            green.Should().Contain(TacticsAdvicePolicy.GuidanceFor(AdvisorTacticsTier.Green));
            master.Should().NotContain(TacticsAdvicePolicy.GuidanceFor(AdvisorTacticsTier.Green));
        }

        // The advisor may cite only the real standings: the top realm and a lesser one both reach the prompt, and
        // the model is told never to invent. Grounding is CR's cardinal rule.
        [Test]
        public void GIVEN_the_world_standings_WHEN_building_THEN_real_kingdoms_are_laid_out_and_invention_is_forbidden()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("Southern Empire"); // rank 1
            prompt.Should().Contain("Sturgia");         // a lesser realm
            prompt.Should().Contain("never invent");
        }

        // A companion counsels FOR the player's realm, so the player's own kingdom, standing and wars frame the
        // advice rather than a neutral world summary.
        [Test]
        public void GIVEN_a_player_with_a_realm_at_war_WHEN_building_THEN_their_kingdom_rank_and_wars_frame_the_counsel()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("Vlandia");   // the player's realm
            prompt.Should().Contain("Sturgia");   // whom they war (also appears in standings, but the war line names it)
            prompt.Should().Contain("At war with");
        }

        // A landless player has no realm to defend: the advisor must reframe rather than pretend a kingdom exists.
        [Test]
        public void GIVEN_a_landless_player_WHEN_building_THEN_the_counsel_reframes_for_a_captain_with_no_realm()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(),
                new PlayerStrategicContext("", 0, null),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("no realm");
        }

        // The task framing must survive: second person, a few sentences, and the describe/predict/advise arc.
        [Test]
        public void GIVEN_any_inputs_WHEN_building_THEN_it_asks_for_in_character_counsel_of_a_few_sentences()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("second person");
            prompt.Should().Contain("three to five sentences");
            prompt.ToLowerInvariant().Should().Contain("advise");
        }

        // Defensive: an empty world (no kingdoms scored) must still yield a prompt, not throw, so the panel never
        // breaks on a world between reigns.
        [Test]
        public void GIVEN_an_empty_world_WHEN_building_THEN_it_produces_a_prompt_without_throwing()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(new PowerIndexResult(),
                new PlayerStrategicContext("", 0, null), new StrategicAdvisor("", "", 0));

            prompt.Should().NotBeNullOrWhiteSpace();
            prompt.Should().Contain("without a reigning power");
        }

        // A blank advisor name falls back to a spoken "war council", so the prompt never addresses the player as a
        // nameless void.
        [Test]
        public void GIVEN_no_advisor_name_WHEN_building_THEN_it_falls_back_to_the_war_council()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("", "", 120));

            prompt.Should().Contain("war council");
        }

        #region fixtures

        private static PowerIndexResult SampleWorld()
        {
            return new PowerIndexResult
            {
                Ranked = new List<KingdomPowerScore>
                {
                    new KingdomPowerScore {KingdomId = "empire_s", KingdomName = "Southern Empire", Rank = 1, PowerIndex = 87, Military = 92, Territory = 81, Economy = 86, Political = 94, WarPerformance = 83, Diplomatic = 60},
                    new KingdomPowerScore {KingdomId = "vlandia", KingdomName = "Vlandia", Rank = 2, PowerIndex = 74, Military = 70, Territory = 84, Economy = 77, Political = 72, WarPerformance = 50, Diplomatic = 66},
                    new KingdomPowerScore {KingdomId = "sturgia", KingdomName = "Sturgia", Rank = 3, PowerIndex = 60, Military = 65, Territory = 58, Economy = 55, Political = 60, WarPerformance = 62, Diplomatic = 61}
                },
                Indicators = new KeyIndicators
                {
                    MostPowerfulArmyKingdomId = "empire_s", MostPowerfulArmyTroops = 18430,
                    LargestTerritoryKingdomId = "vlandia", LargestTerritorySettlements = 23,
                    StrongestEconomyKingdomId = "empire_s", StrongestEconomyWealth = 5800000,
                    MostInfluentialLeaderKingdomId = "empire_s", MostInfluentialLeaderName = "Rhagaea", MostInfluentialLeaderInfluence = 1842,
                    BestWarRecordKingdomId = "empire_s", BestWarRecordVictories = 23, BestWarRecordDefeats = 9
                },
                Totals = new PowerFieldTotals {TotalTroops = 54163, TotalSettlements = 440, TotalClans = 94, TotalWealth = 45991668}
            };
        }

        // The player leads Vlandia (rank 2), at war with Sturgia.
        private static PlayerStrategicContext SampleRealmPlayer()
            => new PlayerStrategicContext("Vlandia", 2, new List<string> {"Sturgia"});

        #endregion
    }
}
