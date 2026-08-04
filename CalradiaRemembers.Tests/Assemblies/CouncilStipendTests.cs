// Code written by Gabriel Mailhot, 03/08/2026.
// The mod's CouncilStipendBehavior persists a List<CouncilStipend> as byte[] JSON via Newtonsoft.Json, mirroring
// CompanionMissionBehavior's own SyncData shape exactly (idempotent load, graceful on missing/corrupt). The mod
// project (CampaignBehaviorBase, IDataStore, Hero) is not referenced by this test project, so the behavior's
// actual SyncData call is integration-only (see project_testing_architecture: mod = engine, untestable here).
// What IS unit-testable, and IS the real save contract the behavior depends on, is the DTO's own round trip
// through the same serializer: these tests pin that every field of an escrowed stipend survives a save/load
// cycle intact, since a save made mid-tranche corrupting the escrow would either strand the player's already-spent
// gold or double-pay a companion.

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
    public sealed class CouncilStipendTests
    {
        // Baseline: a single active stipend must survive a full serialize/deserialize cycle with every field
        // intact, or a save made mid-tranche would silently corrupt an escrow the player already paid for.
        [Test]
        public void GIVEN_an_active_stipend_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilStipend> {
                new CouncilStipend {PayeeId = "hero_ira", AmountPerDay = 120, DaysRemaining = 17, EscrowBalance = 2040}
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilStipend> restored = JsonConvert.DeserializeObject<List<CouncilStipend>>(json);

            restored.Should().HaveCount(1);
            restored[0].PayeeId.Should().Be("hero_ira");
            restored[0].AmountPerDay.Should().Be(120);
            restored[0].DaysRemaining.Should().Be(17);
            restored[0].EscrowBalance.Should().Be(2040);
        }

        // Mirrors CompanionMissionBehavior's own empty-save contract: no active stipends must round-trip to an
        // empty list, never null, since the behavior's own load path only degrades gracefully when it gets one.
        [Test]
        public void GIVEN_no_active_stipends_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilStipend>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilStipend> restored = JsonConvert.DeserializeObject<List<CouncilStipend>>(json);

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CompanionMissionBehavior's own catch path,
        // CompanionMissionBehavior.SyncData): malformed JSON must throw so the behavior's own try/catch can fall
        // back to an empty list and let the campaign open, never return a half-populated escrow silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilStipend>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
