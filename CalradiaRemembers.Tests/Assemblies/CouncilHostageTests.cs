// Code written by Gabriel Mailhot, 04/08/2026.
// The mod's CouncilHostageBehavior persists a List<CouncilHostage> as byte[] JSON via Newtonsoft.Json, mirroring
// CouncilTributeBehavior's own SyncData shape exactly (idempotent load, graceful on missing/corrupt). The mod
// project (CampaignBehaviorBase, IDataStore, Hero) is not referenced by this test project, so the behavior's
// actual SyncData call is integration-only (see project_testing_architecture: mod = engine, untestable here).
// What IS unit-testable, and IS the real save contract the behavior depends on, is the DTO's own round trip
// through the same serializer: these tests pin that every field of an active hostage term (including HomeClanId,
// the field CouncilTribute never needed, since a hostage's own term must survive the ORIGINAL pledger dying or
// losing their party long before the 45 days are up) survives a save/load cycle intact.
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
    public sealed class CouncilHostageTests
    {
        // Baseline: a single active hostage term must survive a full serialize/deserialize cycle with every
        // field intact, or a save made mid-term would silently corrupt the home clan id (the ONLY thread back to
        // where this hostage must eventually return once the original pledger is long gone) or the days owed.
        [Test]
        public void GIVEN_an_active_hostage_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilHostage> {
                new CouncilHostage {
                    HostageId = "hero_boyar_sevin", PledgerId = "hero_ira", HomeClanId = "clan_dey_meroc",
                    DaysRemaining = 30, Residence = "MainParty"
                }
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilHostage> restored = JsonConvert.DeserializeObject<List<CouncilHostage>>(json)!;

            restored.Should().HaveCount(1);
            restored[0].HostageId.Should().Be("hero_boyar_sevin");
            restored[0].PledgerId.Should().Be("hero_ira");
            restored[0].HomeClanId.Should().Be("clan_dey_meroc");
            restored[0].DaysRemaining.Should().Be(30);
            restored[0].Residence.Should().Be("MainParty");
        }

        // Mirrors CouncilTributeTests' own empty-save contract: no active hostages must round-trip to an empty
        // list, never null, since the behavior's own load path only degrades gracefully when it gets one.
        [Test]
        public void GIVEN_no_active_hostages_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilHostage>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilHostage> restored = JsonConvert.DeserializeObject<List<CouncilHostage>>(json)!;

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CouncilTributeTests' own): malformed JSON must
        // throw so the behavior's own try/catch can fall back to an empty list and let the campaign open, never
        // return a half-populated hostage record silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilHostage>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
