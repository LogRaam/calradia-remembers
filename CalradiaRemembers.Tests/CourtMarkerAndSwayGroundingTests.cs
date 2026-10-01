// Code written by Gabriel Mailhot, 01/10/2026.
// Maifailun (Nexus, 01/10/2026, two screenshots): the player asked Lady Bolat whether she had enjoyed the tournament
// and bowed; she "turned against Lady Decantia (-6)", a lady nobody had named, and her Memories panel then read
// "At the player's word, turned against Lady Decantia. [cr_sway:lord_1_1_2]". Two faults. A sway "at the player's
// word" fired with no word said about that person, so a third party lost regard and a hidden grudge was queued
// for nothing. And a cooldown marker, kept in the memory's text since before the structured cooldown store
// existed, was shown to the player and read by the character. If these rules are wrong, either a bow still costs
// a stranger her friends, or a real case made by name is refused, or an old save's cooldowns restart.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourtMarkerAndSwayGroundingTests
    {
        // ── A case about someone names them ───────────────────────────────────────────────────────

        // The reported turn: nothing about Decantia was said, so nothing about her can change.
        [Test]
        public void GIVEN_the_player_only_greeted_and_bowed_WHEN_a_sway_against_someone_unnamed_arrives_THEN_there_was_no_case()
        {
            SwayGroundingPolicy.PlayerSpokeOf("Have you enjoyed the tournament my lady\nYou are most kind my lady *bows before you*", "Decantia")
                               .Should().BeFalse();
        }

        // The verb's own purpose: "she sold your brother to the Vlandians" moves an opinion of HER. A title is not
        // part of who she is, and the player need not write it.
        [Test]
        public void GIVEN_the_player_spoke_of_her_by_name_WHEN_the_sway_arrives_THEN_the_case_was_made()
        {
            SwayGroundingPolicy.PlayerSpokeOf("Decantia sold your brother to the Vlandians.", "Lady Decantia").Should().BeTrue();
            SwayGroundingPolicy.PlayerSpokeOf("decantia lies to you", "Decantia").Should().BeTrue();
        }

        // A name is a whole word: "Arkit" is not found in "arkitecture", and a title alone ("my lady") names nobody.
        [Test]
        public void GIVEN_a_name_only_inside_another_word_or_a_title_alone_WHEN_checked_THEN_nobody_was_named()
        {
            SwayGroundingPolicy.PlayerSpokeOf("a fine arkitecture", "Arkit").Should().BeFalse();
            SwayGroundingPolicy.PlayerSpokeOf("thank you my lady", "Lady Bolat").Should().BeFalse();
        }

        // yozakura12 plays in Japanese (2.6.8): a name in a script without spaces is found inside the sentence.
        [Test]
        public void GIVEN_a_name_in_a_script_without_spaces_WHEN_the_player_wrote_it_THEN_it_is_found()
        {
            SwayGroundingPolicy.PlayerSpokeOf("デカンティアは裏切った", "デカンティア").Should().BeTrue();
        }

        // A council or a scripted path carries no conversation: this rule does not judge what it cannot read.
        [Test]
        public void GIVEN_a_path_with_no_conversation_WHEN_checked_THEN_the_rule_stands_aside()
        {
            SwayGroundingPolicy.PlayerSpokeOf(null, "Decantia").Should().BeTrue();
        }

        // ── The hidden marker leaves the prose ────────────────────────────────────────────────────

        // The screenshot's line, cleaned, and its cooldown kept where the verbs read it first.
        [Test]
        public void GIVEN_an_old_memory_carrying_a_sway_marker_WHEN_scrubbed_THEN_the_text_is_clean_and_the_cooldown_is_kept()
        {
            var profile = new NpcProfile {Id = "lord_bolat", Name = "Bolat", Clan = "Arkit", Faction = "Khuzait", Events = new List<NotableEvent> {
                new NotableEvent(91183, NotableEventType.Collaboration, "At the player's word, turned against Lady Decantia. [cr_sway:lord_1_1_2]")
            }};

            CourtMarkerText.Scrub(profile).Should().BeTrue();

            profile.Events[0].summary.Should().Be("At the player's word, turned against Lady Decantia.");
            profile.CourtActionCooldowns["[cr_sway:lord_1_1_2]"].Should().Be(91183);
        }

        // A cooldown never restarts or shortens on a scrub: the later day already stored stands.
        [Test]
        public void GIVEN_a_later_cooldown_already_stored_WHEN_an_older_marker_is_scrubbed_THEN_the_later_day_stands()
        {
            var profile = new NpcProfile {Id = "lord_bolat", Name = "Bolat", Clan = "Arkit", Faction = "Khuzait", Events = new List<NotableEvent> {
                new NotableEvent(10, NotableEventType.Collaboration, "Lent the player 8 points of influence at court. [cr_influence]")
            }};
            profile.CourtActionCooldowns["[cr_influence]"] = 40;

            CourtMarkerText.Scrub(profile);

            profile.CourtActionCooldowns["[cr_influence]"].Should().Be(40);
            profile.Events[0].summary.Should().Be("Lent the player 8 points of influence at court.");
        }

        // Runs on every launch: a clean profile is left exactly as it is, and no other bracket is touched.
        [Test]
        public void GIVEN_a_clean_profile_WHEN_scrubbed_THEN_nothing_changes()
        {
            var profile = new NpcProfile {Id = "lord_bolat", Name = "Bolat", Clan = "Arkit", Faction = "Khuzait", Events = new List<NotableEvent> {new NotableEvent(5, NotableEventType.FirstMeeting, "We met [at the ford].")}};

            CourtMarkerText.Scrub(profile).Should().BeFalse();
            profile.Events[0].summary.Should().Be("We met [at the ford].");
        }
    }
}
