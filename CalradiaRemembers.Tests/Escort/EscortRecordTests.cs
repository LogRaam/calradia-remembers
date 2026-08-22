// Code written by Gabriel Mailhot, 04/08/2026.
// The mod's FollowMeBehavior persists a List<EscortRecord> as byte[] JSON via Newtonsoft.Json, mirroring
// CouncilStipendBehavior's own SyncData shape exactly (idempotent load, graceful on missing/corrupt). The mod
// project (CampaignBehaviorBase, IDataStore, Hero) is not referenced by this test project, so the behavior's
// actual SyncData call is integration-only (see project_testing_architecture: mod = engine, untestable here).
// What IS unit-testable, and IS the real save contract the behavior depends on, is the DTO's own round trip
// through the same serializer: a save made mid-escort corrupting either field would either strand a lord
// riding forever at the player's side (LordId lost) or forget how many days of the promised term remain
// (DaysRemaining lost), so both must survive a save/load cycle intact.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortRecordTests
    {
        // Baseline: a single active escort must survive a full serialize/deserialize cycle with every field
        // intact, or a save made mid-term would silently lose track of who is escorting and for how long.
        [Test]
        public void GIVEN_an_active_escort_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<EscortRecord> {
                new EscortRecord {LordId = "hero_derthert", DaysRemaining = 9}
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<EscortRecord> restored = JsonConvert.DeserializeObject<List<EscortRecord>>(json)!;

            restored.Should().HaveCount(1);
            restored[0].LordId.Should().Be("hero_derthert");
            restored[0].DaysRemaining.Should().Be(9);
        }

        // Mirrors CouncilStipendBehavior's own empty-save contract: no active escorts must round-trip to an
        // empty list, never null, since the behavior's own load path only degrades gracefully when it gets one.
        [Test]
        public void GIVEN_no_active_escorts_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<EscortRecord>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<EscortRecord> restored = JsonConvert.DeserializeObject<List<EscortRecord>>(json)!;

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CompanionMissionBehavior's/CouncilStipendBehavior's
        // own catch path): malformed JSON must throw so the behavior's own try/catch can fall back to an empty
        // list and let the campaign open, never return a half-populated escort roster silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<EscortRecord>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
