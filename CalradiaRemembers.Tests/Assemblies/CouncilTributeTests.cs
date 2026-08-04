// Code written by Gabriel Mailhot, 04/08/2026.
// The mod's CouncilTributeBehavior persists a List<CouncilTribute> as byte[] JSON via Newtonsoft.Json, mirroring
// CouncilStipendBehavior's own SyncData shape exactly (idempotent load, graceful on missing/corrupt). The mod
// project (CampaignBehaviorBase, IDataStore, Hero) is not referenced by this test project, so the behavior's
// actual SyncData call is integration-only (see project_testing_architecture: mod = engine, untestable here).
// What IS unit-testable, and IS the real save contract the behavior depends on, is the DTO's own round trip
// through the same serializer: these tests pin that every field of an active tribute (including MissedDays, the
// pay-as-you-go grace counter grant_stipend's own escrow never needed) survives a save/load cycle intact, since a
// save made mid-term losing that counter would let a payer who had already missed two days start over at zero.
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
    public sealed class CouncilTributeTests
    {
        // Baseline: a single active tribute must survive a full serialize/deserialize cycle with every field
        // intact, or a save made mid-term would silently corrupt the grace counter or the remaining days owed.
        [Test]
        public void GIVEN_an_active_tribute_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilTribute> {
                new CouncilTribute {PayerHeroId = "hero_boyar_sevin", AmountPerDay = 300, DaysRemaining = 12, MissedDays = 2}
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilTribute> restored = JsonConvert.DeserializeObject<List<CouncilTribute>>(json);

            restored.Should().HaveCount(1);
            restored[0].PayerHeroId.Should().Be("hero_boyar_sevin");
            restored[0].AmountPerDay.Should().Be(300);
            restored[0].DaysRemaining.Should().Be(12);
            restored[0].MissedDays.Should().Be(2);
        }

        // Mirrors CouncilStipendTests' own empty-save contract: no active tributes must round-trip to an empty
        // list, never null, since the behavior's own load path only degrades gracefully when it gets one.
        [Test]
        public void GIVEN_no_active_tributes_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilTribute>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilTribute> restored = JsonConvert.DeserializeObject<List<CouncilTribute>>(json);

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CouncilStipendTests' own): malformed JSON must
        // throw so the behavior's own try/catch can fall back to an empty list and let the campaign open, never
        // return a half-populated tribute silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilTribute>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
