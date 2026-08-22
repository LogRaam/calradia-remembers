// Code written by Gabriel Mailhot, 05/08/2026.
// The mod's CouncilPactBehavior persists a List<CouncilPact> as byte[] JSON via Newtonsoft.Json, mirroring
// CouncilTributeBehavior's/CouncilHostageBehavior's own SyncData shape exactly (idempotent load, graceful on
// missing/corrupt). The mod project (CampaignBehaviorBase, IDataStore, Hero, IFaction) is not referenced by this
// test project, so the behavior's actual SyncData call is integration-only (see project_testing_architecture:
// mod = engine, untestable here). What IS unit-testable, and IS the real save contract the behavior depends on,
// is the DTO's own round trip through the same serializer: these tests pin that every field of an active pact
// (both faction ids, and the days remaining) survives a save/load cycle intact, since a save made mid-term losing
// either faction id would make the pact's own IsUnderPact query silently stop matching after a reload.
#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilPactTests
    {
        // Baseline: a single active pact must survive a full serialize/deserialize cycle with every field intact,
        // or a save made mid-term would silently corrupt one side of the faction pair or the days remaining.
        [Test]
        public void GIVEN_an_active_pact_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilPact> {
                new CouncilPact {FactionAId = "kingdom_player_realm", FactionBId = "kingdom_battania", DaysRemaining = 30}
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilPact> restored = JsonConvert.DeserializeObject<List<CouncilPact>>(json)!;

            restored.Should().HaveCount(1);
            restored[0].FactionAId.Should().Be("kingdom_player_realm");
            restored[0].FactionBId.Should().Be("kingdom_battania");
            restored[0].DaysRemaining.Should().Be(30);
        }

        // Mirrors CouncilTributeTests'/CouncilHostageTests' own empty-save contract: no active pacts must round-
        // trip to an empty list, never null, since the behavior's own load path only degrades gracefully when it
        // gets one.
        [Test]
        public void GIVEN_no_active_pacts_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilPact>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilPact> restored = JsonConvert.DeserializeObject<List<CouncilPact>>(json)!;

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CouncilTributeTests'/CouncilHostageTests' own):
        // malformed JSON must throw so the behavior's own try/catch can fall back to an empty list and let the
        // campaign open, never return a half-populated pact silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilPact>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
