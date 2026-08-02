// Code written by Gabriel Mailhot, 02/08/2026.
// This policy decides which situational SCENE image covers the anchor NPC's portrait on the council's "thinking"
// placeholder. It feeds straight into the message row's runtime image path (ChatViewModel), so a wrong basename
// here is a wrong (or missing, file-not-found) image in play. What breaks if it is wrong: the wrong mood art shows
// during a council turn (e.g. a war council image in a family sitting), or the wrong gendered file is requested
// and silently fails to load, leaving the ordinary anchor portrait as the only visible fallback.

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
        // STAKE: outside the settlement walls, terrain is the ONLY axis that matters (Gabriel confirmed the three
        // outside images are gender/scope-neutral). Getting the terrain bucket wrong shows a desert scene over a
        // steppe camp or vice versa.
        [Test]
        public void GIVEN_outside_in_desert_THEN_the_desert_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, false, false, CouncilSceneTerrain.Desert, false)
                .Should().Be("OutsideCounselDesert");
        }

        [Test]
        public void GIVEN_outside_in_steppe_THEN_the_steppe_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, false, false, CouncilSceneTerrain.Steppe, true)
                .Should().Be("OutsideCounselSteppes");
        }

        // STAKE: every OTHER engine terrain (Forest, Mountain, Plain, Snow, Water...) collapses to this one
        // "forest/mountains" image (there is no dedicated art for each), so CouncilSceneTerrain.Other must be the
        // safe catch-all, not an unmapped case that throws or falls back to nothing.
        [Test]
        public void GIVEN_outside_on_any_other_terrain_THEN_the_forest_mountains_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(true, false, false, CouncilSceneTerrain.Other, false)
                .Should().Be("OutsideCounselForestMountains");
        }

        // STAKE: this is the confirmed asset TYPO ("Fermale", not "Female"). If a future rename "fixes" the
        // spelling here without also renaming the shipped JPG, the image silently fails to load (file not found)
        // and the player sees the plain anchor portrait instead of the scene art.
        [Test]
        public void GIVEN_a_family_council_indoors_WHEN_the_player_is_female_THEN_the_misspelled_Fermale_asset_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, true, false, CouncilSceneTerrain.Other, true)
                .Should().Be("FamilyCounselFermale");
        }

        [Test]
        public void GIVEN_a_family_council_indoors_WHEN_the_player_is_male_THEN_the_male_family_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, true, false, CouncilSceneTerrain.Other, false)
                .Should().Be("FamilyCounselMale");
        }

        // STAKE: "War" art is reserved for a settlement the player's OWN clan owns (the one place a war council
        // plausibly convenes). Mixing this up with the Tavern case would show a war-room image in someone else's
        // town, which reads wrong to the player (they don't hold this settlement).
        [Test]
        public void GIVEN_a_non_family_council_in_a_player_owned_settlement_WHEN_male_THEN_the_male_war_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, false, true, CouncilSceneTerrain.Other, false)
                .Should().Be("WarCounselMale");
        }

        [Test]
        public void GIVEN_a_non_family_council_in_a_player_owned_settlement_WHEN_female_THEN_the_female_war_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, false, true, CouncilSceneTerrain.Other, true)
                .Should().Be("WarCounselFemale");
        }

        // STAKE: any settlement NOT owned by the player's clan (a foreign town, a free city) falls back to the
        // single neutral Tavern image (there is no gender variant for it), so a wrong branch here would ask for a
        // file ("TavernCounselMale") that does not exist on disk.
        [Test]
        public void GIVEN_a_non_family_council_in_a_settlement_not_owned_by_the_player_THEN_the_tavern_image_is_chosen()
        {
            CouncilSceneImagePolicy.Resolve(false, false, false, CouncilSceneTerrain.Other, true)
                .Should().Be("TavernCounsel");
        }

        // STAKE: a family sitting called out in the wilderness (a camped council, no settlement) must still show
        // the OUTSIDE art, never the indoor Family image: the confirmed rule is "outside wins over scope", and
        // this is the one case likely to be missed since Family is normally checked first once indoors.
        [Test]
        public void GIVEN_a_family_council_but_the_player_is_outside_THEN_outside_wins_over_the_family_image()
        {
            CouncilSceneImagePolicy.Resolve(true, true, false, CouncilSceneTerrain.Steppe, true)
                .Should().Be("OutsideCounselSteppes");
        }

        // STAKE: ownership only matters once Family scope has already been ruled out: a family council never
        // reads WarCounselMale/Female just because the settlement happens to be the player's own.
        [Test]
        public void GIVEN_a_family_council_in_a_player_owned_settlement_THEN_the_family_image_still_wins_over_war()
        {
            CouncilSceneImagePolicy.Resolve(false, true, true, CouncilSceneTerrain.Other, false)
                .Should().Be("FamilyCounselMale");
        }
    }
}
