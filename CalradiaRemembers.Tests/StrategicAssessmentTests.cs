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

        // The task framing must survive: second person, a few sentences, and the ADVICE-not-recital arc. The numbers
        // are already on the panel, so the counsel's whole worth is the judgment on top; if the prompt slid back to
        // "describe the standings", the feature would just narrate the table the player is looking at (Gabriel,
        // 2026-09-04: "j'ai plus besoin de conseils strategiques que de constats").
        [Test]
        public void GIVEN_any_inputs_WHEN_building_THEN_it_asks_for_advice_not_a_recital_in_a_few_sentences()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("second person");
            prompt.Should().Contain("three to five sentences");
            prompt.ToLowerInvariant().Should().Contain("counsel");
            prompt.Should().Contain("do NOT merely recite"); // advice, not a read-out of the visible numbers
        }

        // The narrative style adds *stage directions*, and without a person rule the model wrote "*You tap the map*"
        // about ITSELF and addressed the advisor instead of the lord (player report 2026-09-04). The prompt must pin
        // "you" to the lord and force the advisor's own actions into the third person.
        [Test]
        public void GIVEN_any_inputs_WHEN_building_THEN_it_pins_second_person_to_the_lord_and_self_actions_to_third()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("'you' always means your lord");
            prompt.Should().Contain("THIRD PERSON");
        }

        // ── A: the real wars frame the reading ──────────────────────────────

        // The single richest input for a strategic reading is who is actually at war with whom, and it is the fact the
        // model most wants to invent when absent (an early counsel imagined a "clash between X and Y" from nothing).
        // Given the real wars, they must reach the prompt verbatim so the advisor reasons about real fronts.
        [Test]
        public void GIVEN_active_wars_WHEN_building_THEN_the_real_belligerents_are_named()
        {
            var wars = new CalradiaWarState(new List<WarPair>
            {
                new WarPair("Sturgia", "Vlandia"),
                new WarPair("Aserai", "Khuzait Khanate")
            });

            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210), wars);

            prompt.Should().Contain("Sturgia with Vlandia");
            prompt.Should().Contain("Aserai with Khuzait Khanate");
        }

        // A world at peace must SAY so, so the advisor cannot invent a war to explain the map.
        [Test]
        public void GIVEN_no_wars_WHEN_building_THEN_it_states_the_realms_are_at_peace()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210), new CalradiaWarState(null));

            prompt.Should().Contain("No realms are presently at war");
        }

        // fkasad (01/10/2026): "List of wars could be changed the same way you did in the discussion prompt (faction is at
        // war with x, y, z; allowing a shorter prompt)". Each war is named once, grouped under one realm, on one line.
        [Test]
        public void GIVEN_a_realm_at_war_on_several_fronts_WHEN_building_THEN_each_war_is_named_once_grouped_by_realm()
        {
            var wars = new CalradiaWarState(new List<WarPair>
            {
                new WarPair("Sturgia", "Vlandia"),
                new WarPair("Sturgia", "Battania"),
                new WarPair("Vlandia", "Sturgia"),
                new WarPair("Aserai", "Khuzait Khanate")
            });

            StrategicAssessmentPromptBuilder.CompactWars(wars).Should().Be("Sturgia with Vlandia and Battania; Aserai with Khuzait Khanate");
        }

        // fkasad (01/10/2026): "A low number of victories does not necessarily mean a high number of defeats. Some factions
        // are at war without actually engaging in battle... the advisor's analysis: no victories = you are losing." A realm
        // with no battles is said to have none and is never named weakest in war performance; the advisor is told few
        // victories is not losing.
        [Test]
        public void GIVEN_a_realm_that_has_not_fought_WHEN_building_THEN_it_reads_as_unfought_never_as_losing()
        {
            var result = new PowerIndexResult {
                Ranked = new List<KingdomPowerScore> {
                    new KingdomPowerScore {Rank = 1, KingdomName = "Vlandia", Military = 90, Territory = 80, Economy = 70, Political = 60, WarPerformance = 100, Diplomatic = 50, RecentVictories = 6, RecentDefeats = 1},
                    new KingdomPowerScore {Rank = 2, KingdomName = "Aserai", Military = 70, Territory = 60, Economy = 90, Political = 50, WarPerformance = 0, Diplomatic = 40}
                }
            };

            string prompt = StrategicAssessmentPromptBuilder.Build(result, SampleRealmPlayer(), new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("Aserai: power").And.Contain("No battles recorded lately.");
            prompt.Should().Contain("Recent battles: 6 won, 1 lost.");
            prompt.Should().NotContain("war performance");
            prompt.Should().Contain("Few or no victories does NOT mean a realm is losing");
        }

        // fkasad (01/10/2026): "Using the Setting.json (the right world name), World.txt file if needed to provide
        // perspective... The post_history_instructions.txt (I'm using it to give specific style to npc)". The counsel
        // speaks of the player's own world, with its description and his instructions, not of Calradia regardless.
        [Test]
        public void GIVEN_the_players_own_world_files_WHEN_building_THEN_the_counsel_is_given_in_that_world()
        {
            var world = new StrategicWorld {Name = "the Old World", Description = "Chaos stirs in the north.", PostHistoryInstructions = "Speak tersely."};

            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(), new StrategicAdvisor("", "", 10, AdvisorKind.CommonSoldier), null, null, world);

            prompt.Should().Contain("THE STATE OF THE OLD WORLD").And.Contain("across the Old World");
            prompt.Should().Contain("Chaos stirs in the north.").And.Contain("Speak tersely.");
            prompt.Should().NotContain("CALRADIA");
        }

        // ── B: no invented war history (there is no battle ledger yet) ──────

        // War performance is neutral in V1 (no persisted battle ledger), so the advisor has NO basis to call a realm
        // "war-weary" or "bled dry" and did exactly that from nothing. The prompt must fence that off explicitly.
        [Test]
        public void GIVEN_no_battle_ledger_WHEN_building_THEN_the_advisor_is_forbidden_to_invent_a_war_history()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210));

            prompt.Should().Contain("record of past battles"); // it is told what it does NOT know
            prompt.Should().Contain("war-weary");                 // named so the model cannot claim it
            prompt.Should().Contain("bled dry");
        }

        // ── Persona + narrative style: the companion's own voice ────────────

        // A companion counsels as THEMSELVES, so their authored character (trait archetype, backstory, conviction) must
        // reach the prompt and the flat "the strategist counselling your lord" line must give way to it. Without this
        // the counsel reads like a faceless oracle instead of the person the player knows from the chatroom.
        [Test]
        public void GIVEN_a_companion_with_a_persona_WHEN_building_THEN_it_speaks_in_their_character()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210, AdvisorKind.CompanionStrategist,
                    "The Spider. No virtue but cleverness."));

            prompt.Should().Contain("The Spider. No virtue but cleverness.");
            prompt.Should().Contain("in your own voice");
            prompt.Should().NotContain("the strategist counselling your lord");
        }

        // The active narrative style is part of the voice (the same style the chatroom uses), so it must reach the
        // prompt when set, and be trimmed so a long style file cannot crowd out the facts.
        [Test]
        public void GIVEN_a_narrative_style_WHEN_building_THEN_it_is_injected_and_capped()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210), new CalradiaWarState(null), "Speak in a grim, laconic register.");

            prompt.Should().Contain("narrative style");
            prompt.Should().Contain("grim, laconic register");

            string huge = new string('x', StrategicAssessmentPromptBuilder.MaxNarrativeStyleChars + 500);
            string capped = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("Ira", "Battanian", 210), new CalradiaWarState(null), huge);

            capped.Should().NotContain(huge); // the full over-cap style never lands whole
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

        // No companion, but soldiers in the party: a plain soldier stands in, and the prompt must voice them AS a
        // soldier (humble, no strategist's polish), not as an officer, or the fallback reads wrong.
        [Test]
        public void GIVEN_a_common_soldier_advisor_WHEN_building_THEN_it_voices_a_plain_soldier_not_a_strategist()
        {
            string prompt = StrategicAssessmentPromptBuilder.Build(SampleWorld(), SampleRealmPlayer(),
                new StrategicAdvisor("a grizzled sergeant", "Vlandian", 30, AdvisorKind.CommonSoldier));

            prompt.Should().Contain("common soldier");
            prompt.Should().Contain("no trained strategist");
            prompt.Should().NotContain("the strategist counselling your lord");
        }

        // ── The alone case: a factual summary with NO LLM call ──────────────

        // A player travelling alone gets a plain factual summary, composed offline: it must state the leading realm
        // and standings but carry NO advisor voice and NO "give counsel" task (there is no advisor), so it can never
        // be mistaken for the LLM prompt or trigger a call.
        [Test]
        public void GIVEN_the_standings_WHEN_summarising_offline_THEN_it_states_facts_with_no_advisor_or_task()
        {
            string summary = StrategicAssessmentSummary.Compose(SampleWorld(), SampleRealmPlayer());

            summary.Should().Contain("Southern Empire"); // the leader
            summary.Should().Contain("Vlandia");         // the player's own realm line
            summary.Should().NotContain("second person"); // not the LLM task
            summary.ToLowerInvariant().Should().NotContain("you are"); // no persona
        }

        // The offline summary must also survive a world between reigns without throwing.
        [Test]
        public void GIVEN_an_empty_world_WHEN_summarising_offline_THEN_it_reports_no_reigning_power()
        {
            string summary = StrategicAssessmentSummary.Compose(new PowerIndexResult(),
                new PlayerStrategicContext("", 0, null));

            summary.Should().Contain("without a reigning power");
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
