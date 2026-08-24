// Code written by Gabriel Mailhot, 02/08/2026.
// This policy decides which situational SCENE image covers the anchor NPC's portrait on the council's "thinking"
// placeholder. It feeds straight into the message row's runtime image path (ChatViewModel), so a wrong basename
// here is a wrong (or missing, file-not-found) image in play. What breaks if it is wrong: the wrong mood art shows
// during a council turn (e.g. a war council image in a family sitting), or the wrong gendered file is requested
// and silently fails to load, leaving the ordinary anchor portrait as the only visible fallback.
// 2026-08-23: reworked to Gabriel's confirmed mapping. The council's TYPE decides first, not the location: a Family
// or War council always shows its own sex-keyed art even in the wilderness (type primes over location, Q1). Only a
// Generic scope (companions, clan lords) lets the location choose (tavern indoors, outside art by terrain).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilSceneImagePolicyTests
    {
        // STAKE: a Generic council (companions, clan lords) held on the open map has terrain as its ONLY axis (the
        // three outside images are gender-neutral). Getting the bucket wrong shows a desert scene over a steppe camp.
        [Test]
        public void GIVEN_a_generic_council_outside_in_desert_THEN_the_desert_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.Generic, CouncilSceneTerrain.Desert, false)
                .Should().Be("OutsideCounselDesert");
        }

        [Test]
        public void GIVEN_a_generic_council_outside_in_steppe_THEN_the_steppe_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.Generic, CouncilSceneTerrain.Steppe, true)
                .Should().Be("OutsideCounselSteppes");
        }

        // STAKE: every OTHER engine terrain (Forest, Mountain, Plain, Snow, Water...) collapses to this one
        // "forest/mountains" image (there is no dedicated art for each), so CouncilSceneTerrain.Other must be the
        // safe catch-all, not an unmapped case that throws or falls back to nothing.
        [Test]
        public void GIVEN_a_generic_council_outside_on_any_other_terrain_THEN_the_forest_mountains_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.Generic, CouncilSceneTerrain.Other, false)
                .Should().Be("OutsideCounselForestMountains");
        }

        // STAKE: a Generic council inside a settlement (of ANY owner) falls back to the single neutral Tavern image
        // (there is no gender variant), so a wrong branch here would ask for a file ("TavernCounselMale") that does
        // not exist on disk.
        [Test]
        public void GIVEN_a_generic_council_inside_a_settlement_THEN_the_tavern_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.Generic, CouncilSceneTerrain.Other, true)
                .Should().Be("TavernCounsel");
        }

        // STAKE: the basename must match the shipped JPG on disk exactly (FamilyCounselFemale.jpg, spelling fixed
        // 2026-08-23 from the earlier "Fermale" typo). A mismatch silently fails to load (file not found) and the
        // player sees the plain anchor portrait instead of the scene art.
        [Test]
        public void GIVEN_a_family_council_WHEN_the_player_is_female_THEN_the_female_family_asset_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.Family, CouncilSceneTerrain.Other, true)
                .Should().Be("FamilyCounselFemale");
        }

        [Test]
        public void GIVEN_a_family_council_WHEN_the_player_is_male_THEN_the_male_family_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.Family, CouncilSceneTerrain.Other, false)
                .Should().Be("FamilyCounselMale");
        }

        // STAKE: War art is keyed by the council TYPE, not by who owns the settlement (the old rule guessed war from
        // ownership and showed a war-room in a companions' sitting held in the player's own town). A real war
        // council must show the war scene wherever it sits.
        [Test]
        public void GIVEN_a_war_council_WHEN_male_THEN_the_male_war_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.War, CouncilSceneTerrain.Other, false)
                .Should().Be("WarCounselMale");
        }

        [Test]
        public void GIVEN_a_war_council_WHEN_female_THEN_the_female_war_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.War, CouncilSceneTerrain.Other, true)
                .Should().Be("WarCounselFemale");
        }

        // STAKE: the confirmed rule (Q1) reversed the old "outside wins" behavior: a family council called out in the
        // wilderness must STILL show the indoor Family art, never the outside scene, because the family image is
        // keyed on the council's type and that primes over the player's location.
        [Test]
        public void GIVEN_a_family_council_but_the_player_is_outside_THEN_the_family_image_still_wins_over_the_outside_art()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.Family, CouncilSceneTerrain.Steppe, true)
                .Should().Be("FamilyCounselFemale");
        }

        // STAKE: same rule for a war council in the field (a camp on the march): the war art is type-keyed, so it
        // must win over the outside terrain image just as the family image does.
        [Test]
        public void GIVEN_a_war_council_but_the_player_is_outside_THEN_the_war_image_still_wins_over_the_outside_art()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.War, CouncilSceneTerrain.Desert, false)
                .Should().Be("WarCounselMale");
        }

        // STAKE: an Assembly has no art of its own; Gabriel confirmed it borrows the neutral Tavern scene. A wrong
        // branch here would ask for a non-existent "AssemblyCounsel" file and fall back to the bare portrait.
        [Test]
        public void GIVEN_an_assembly_THEN_the_tavern_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, CouncilSceneKind.Assembly, CouncilSceneTerrain.Other, true)
                .Should().Be("TavernCounsel");
        }

        // STAKE: the Assembly's tavern borrow is type-keyed too, so it holds even when the assembly is convened in
        // the field: it must not slip into the outside terrain art just because the player is on the map.
        [Test]
        public void GIVEN_an_assembly_but_the_player_is_outside_THEN_the_tavern_image_still_wins()
        {
            CouncilSceneImagePolicy.Resolve(true, CouncilSceneKind.Assembly, CouncilSceneTerrain.Desert, false)
                .Should().Be("TavernCounsel");
        }
    }
}
