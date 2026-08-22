// Code written by Gabriel Mailhot, 05/08/2026.
// The mod's CouncilWardBehavior persists a List<CouncilWard> as byte[] JSON via Newtonsoft.Json, mirroring
// CouncilStipendBehavior's/CouncilTributeBehavior's/CouncilHostageBehavior's own SyncData shape exactly
// (idempotent load, graceful on missing/corrupt). The mod project (CampaignBehaviorBase, IDataStore, Hero) is
// not referenced by this test project, so the behavior's actual SyncData call is integration-only (see
// project_testing_architecture: mod = engine, untestable here). What IS unit-testable, and IS the real save
// contract the behavior depends on, is the DTO's own round trip through the same serializer: these tests pin
// that a bond's mentor/protege ids survive a save/load cycle intact, since a save made mid-bond corrupting
// either id would silently orphan the mentorship (or worse, misattribute it to the wrong pair of heroes).
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
    public sealed class CouncilWardTests
    {
        // Baseline: a single active bond must survive a full serialize/deserialize cycle with both ids intact,
        // or a save made mid-bond would silently corrupt or orphan a live mentorship.
        [Test]
        public void GIVEN_an_active_bond_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilWard> {
                new CouncilWard {MentorId = "hero_ira", ProtegeId = "hero_aldric"}
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilWard> restored = JsonConvert.DeserializeObject<List<CouncilWard>>(json)!;

            restored.Should().HaveCount(1);
            restored[0].MentorId.Should().Be("hero_ira");
            restored[0].ProtegeId.Should().Be("hero_aldric");
        }

        // Mirrors CouncilStipendBehavior's/CouncilHostageBehavior's own empty-save contract: no active bonds must
        // round-trip to an empty list, never null, since the behavior's own load path only degrades gracefully
        // when it gets one.
        [Test]
        public void GIVEN_no_active_bonds_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilWard>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilWard> restored = JsonConvert.DeserializeObject<List<CouncilWard>>(json)!;

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CompanionMissionBehavior's/CouncilHostageBehavior's
        // own catch path): malformed JSON must throw so the behavior's own try/catch can fall back to an empty
        // list and let the campaign open, never return a half-populated bond list silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilWard>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
