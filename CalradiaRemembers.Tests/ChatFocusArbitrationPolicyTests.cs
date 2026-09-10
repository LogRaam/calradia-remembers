// Code written by Gabriel Mailhot, 09/09/2026.
// What this pins: when the chat overlay stands aside for something above it, and, far more importantly, when it
// must NOT.
//
// Why it exists. A player (tashmetu, Nexus, 2026-09-09, v2.5.6) reported that the overlay "yields on NPC-initiated
// conversations and stays hidden for the rest of the session", with the mod otherwise working end to end: profiles
// loaded, vanilla lines seeded, a full LLM response returned, and nothing ever drawn. He read his own log and
// found the fault himself: "top equals host, there is no actual separate screen above the overlay. The layer count
// is 13 here; in sessions where the overlay behaves it is 12. No restore/reclaim line appears anywhere afterwards."
//
// He was right. The arbiter decided "something is above us" by comparing the host screen's layer COUNT against the
// count taken when the chat opened. Checked against the game's binaries, SandBox's MissionGauntletConversationView
// adds a layer named "MissionConversation" to the same MissionScreen as part of starting the conversation. Click an
// NPC and it is already there when the baseline is taken; be approached BY one and it lands a moment after, the
// count grows by one, and the chat slides off the right edge for a window that is not above it at all. It never
// returns, because that layer stays for the whole conversation, and a chat that cannot be seen cannot be closed,
// so every later conversation is refused too.
//
// The rule is now the engine's own: a layer sets IsFocusLayer when it means to take the input. Sweeping every named
// Gauntlet layer in Native and SandBox, not one always-on mission HUD layer sets it, and the only non-modals that
// do are the two vanilla conversation windows this mod draws over. Those two are named and exempt; everything else
// that claims focus gets the screen.
//
// If these tests fail, either a real modal is about to be starved of input behind an unresponsive chat window, or
// the chat is about to slide off screen for something it was built to replace.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatFocusArbitrationPolicyTests
    {
        private static OverlayNeighbour Chat() => new() {Name = "CalradiaRemembers", IsTheChat = true, WantsFocus = true};

        private static OverlayNeighbour Layer(string name, bool wantsFocus = false, bool finalized = false)
            => new() {Name = name, WantsFocus = wantsFocus, IsFinalized = finalized};

        // The mission a conversation actually happens in, as the engine reports it: a scene layer, a pile of HUD
        // layers, none of which claim focus, and the chat. Nothing here is above anything.
        private static List<OverlayNeighbour> AQuietMission() => new() {
            Layer("SceneLayer"), Layer("MissionCrosshair"), Layer("MissionNameMarker"),
            Layer("MissionSPKillFeed"), Layer("MissionObjective"), Layer("MissionOrder"), Chat()
        };

        // The reported bug, and the only case that matters. The vanilla conversation layer arrives AFTER the chat
        // because the NPC started the conversation, and the chat must not read its own understudy as a modal.
        [Test]
        public void GIVEN_vanilla_conversation_layer_arrives_after_the_chat_WHEN_arbitrating_THEN_the_chat_holds_the_screen()
        {
            List<OverlayNeighbour> mission = AQuietMission();
            mission.Add(Layer("MissionConversation", wantsFocus: true)); // the thirteenth layer in his log

            ChatFocusArbitrationPolicy.Decide(hostScreenIsOnTop: true, mission).Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.ReasonFor(true, mission).Should().BeEmpty();
        }

        // The same exemption on the map, where a conversation can also start without a mission behind it.
        [Test]
        public void GIVEN_the_map_conversation_window_WHEN_arbitrating_THEN_the_chat_still_holds_the_screen()
        {
            var map = new List<OverlayNeighbour> {Layer("MapNameplateLayer"), Chat(), Layer("MapConversation", wantsFocus: true)};

            ChatFocusArbitrationPolicy.Decide(true, map).Should().Be(ChatFocusVerdict.Hold);
        }

        // What the arbiter is FOR. Each of these holds an exclusive input grab of its own, and a chat that kept
        // focus over them would leave the player looking at a menu that does not answer the mouse.
        [TestCase("MissionEscapeMenu")]
        [TestCase("MissionBarter")]
        [TestCase("MissionOptions")]
        [TestCase("MissionBoardGame")]
        [TestCase("MissionTournament")]
        [TestCase("MapEscapeMenu")]
        [TestCase("QueryManager")]
        public void GIVEN_a_real_modal_opens_over_the_chat_WHEN_arbitrating_THEN_the_chat_stands_aside(string modal)
        {
            List<OverlayNeighbour> mission = AQuietMission();
            mission.Add(Layer(modal, wantsFocus: true));

            ChatFocusArbitrationPolicy.Decide(true, mission).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.ReasonFor(true, mission).Should().Be(modal);
        }

        // A modal on top of the vanilla conversation window, which is the ordinary shape of a barter: the exempt
        // layer must not shield the one behind it, or the exemption would become a blanket.
        [Test]
        public void GIVEN_a_modal_beside_the_exempt_conversation_layer_WHEN_arbitrating_THEN_the_modal_still_wins()
        {
            List<OverlayNeighbour> mission = AQuietMission();
            mission.Add(Layer("MissionConversation", wantsFocus: true));
            mission.Add(Layer("MissionBarter", wantsFocus: true));

            ChatFocusArbitrationPolicy.Decide(true, mission).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.ReasonFor(true, mission).Should().Be("MissionBarter");
        }

        // A whole screen pushed over the host, which is how the encyclopedia and this mod's own panels arrive.
        // The engine has already decided that one, and it is decided without looking at any layer.
        [Test]
        public void GIVEN_another_screen_was_pushed_over_the_host_WHEN_arbitrating_THEN_the_chat_stands_aside()
        {
            ChatFocusArbitrationPolicy.Decide(hostScreenIsOnTop: false, AQuietMission()).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.Decide(false, null).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.ReasonFor(false, null).Should().NotBeNullOrWhiteSpace();
        }

        // A layer on its way out has no claim on anything. Judging a finalized modal as present is how a yield
        // outlives the thing it yielded to, which is the exact failure this whole file exists to end.
        [Test]
        public void GIVEN_a_modal_that_is_being_torn_down_WHEN_arbitrating_THEN_the_chat_takes_the_screen_back()
        {
            List<OverlayNeighbour> mission = AQuietMission();
            mission.Add(Layer("MissionEscapeMenu", wantsFocus: true, finalized: true));

            ChatFocusArbitrationPolicy.Decide(true, mission).Should().Be(ChatFocusVerdict.Hold);
        }

        // The chat's own layer claims focus, by design, every frame it holds the screen. Reading itself as a
        // reason to yield would strand it the instant it opened.
        [Test]
        public void GIVEN_only_the_chat_itself_claims_focus_WHEN_arbitrating_THEN_it_is_not_its_own_reason_to_yield()
        {
            ChatFocusArbitrationPolicy.Decide(true, AQuietMission()).Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.Decide(true, new List<OverlayNeighbour> {Chat()}).Should().Be(ChatFocusVerdict.Hold);
        }

        // The HUD layers a mission always carries. This is the sweep result written down: if a future game build
        // starts claiming focus from one of these, the chat would vanish behind it and this test says so first.
        [Test]
        public void GIVEN_the_ordinary_mission_hud_WHEN_arbitrating_THEN_none_of_it_counts_as_a_screen_above()
        {
            ChatFocusArbitrationPolicy.Decide(true, AQuietMission()).Should().Be(ChatFocusVerdict.Hold);
            AQuietMission().Where(l => !l.IsTheChat).Should().OnlyContain(l => !l.WantsFocus);
        }

        // Nothing readable must never be read as "something is above us", because that yield is the one nobody
        // is left able to undo. An unreadable screen holds, and the worst case is a modal that waits a frame.
        [Test]
        public void GIVEN_nothing_readable_on_the_host_WHEN_arbitrating_THEN_the_chat_holds_rather_than_disappears()
        {
            ChatFocusArbitrationPolicy.Decide(true, null).Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.Decide(true, new List<OverlayNeighbour>()).Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.Decide(true, new List<OverlayNeighbour> {null, Chat()}).Should().Be(ChatFocusVerdict.Hold);
        }

        // An unnamed layer that claims focus is still a claim, and the log must not go quiet about it. Names come
        // from the engine and a mod's layer may have none.
        [Test]
        public void GIVEN_an_unnamed_layer_claims_focus_WHEN_arbitrating_THEN_it_is_honoured_and_still_reported()
        {
            foreach (string name in new[] {null, ""})
            {
                var layers = new List<OverlayNeighbour> {Chat(), Layer(name, wantsFocus: true)};

                ChatFocusArbitrationPolicy.Decide(true, layers).Should().Be(ChatFocusVerdict.Yield);
                ChatFocusArbitrationPolicy.ReasonFor(true, layers).Should().NotBeNullOrWhiteSpace();
            }
        }

        // The exemption is exactly two names and matches them exactly, so no third layer can slip in behind a
        // near-miss and quietly take the screen from the player.
        [Test]
        public void GIVEN_the_exemption_list_WHEN_matched_THEN_it_is_the_two_vanilla_windows_and_nothing_like_them()
        {
            ChatFocusArbitrationPolicy.VanillaConversationLayerNames
                .Should().BeEquivalentTo("MissionConversation", "MapConversation");

            ChatFocusArbitrationPolicy.IsVanillaConversationLayer("MissionConversation").Should().BeTrue();
            ChatFocusArbitrationPolicy.IsVanillaConversationLayer("MapConversation").Should().BeTrue();

            foreach (string near in new[] {null, "", "missionconversation", "MissionConversationBarter", "Conversation", "MissionEscapeMenu"})
                ChatFocusArbitrationPolicy.IsVanillaConversationLayer(near).Should().BeFalse();
        }

        // Hold and Yield must be the whole of it, and the reason must agree with the verdict every time: a yield
        // with no reason is a yield nobody can diagnose, and a hold with a reason is a lie in the log.
        [Test]
        public void GIVEN_any_verdict_WHEN_reported_THEN_the_reason_and_the_verdict_always_agree()
        {
            var cases = new List<(bool onTop, List<OverlayNeighbour> layers)> {
                (true, AQuietMission()),
                (true, new List<OverlayNeighbour> {Chat(), Layer("MissionConversation", true)}),
                (true, new List<OverlayNeighbour> {Chat(), Layer("MissionEscapeMenu", true)}),
                (false, AQuietMission()),
                (true, null),
                (false, null)
            };

            foreach ((bool onTop, List<OverlayNeighbour> layers) in cases)
            {
                bool yielded = ChatFocusArbitrationPolicy.Decide(onTop, layers) == ChatFocusVerdict.Yield;

                string.IsNullOrEmpty(ChatFocusArbitrationPolicy.ReasonFor(onTop, layers)).Should().Be(!yielded);
            }
        }
    }
}
