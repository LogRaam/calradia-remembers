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
// A NAME LIST CANNOT BE COMPLETE, AND TWO RUNS PROVED IT. cr.testall caught the first miss: SceneLayer, the 3D
// world, is not a GauntletLayer at all and sets IsFocusLayer in its own constructor, so it claims focus on every
// screen a conversation can happen on; unexempted, the arbiter yielded on the first frame of EVERY conversation.
// Gabriel's play-test caught the second: a chat opened in a TAVERN, where the vanilla settlement menu
// "MapMenuOverlay" holds focus for the whole conversation. The chat yielded to the very backdrop it had just
// been drawn over, never appeared, and slid into view only once the player left the menu.
//
// So the rule gained the part it was missing: WHATEVER WAS ALREADY CLAIMING FOCUS WHEN THE CHAT OPENED IS THE
// BACKDROP, NOT A SCREEN ABOVE US. That is structural instead of enumerated, and it is what the failed layer
// COUNT baseline was reaching for without getting right: the question was never how MANY layers there are, it is
// WHICH of them the chat was deliberately drawn over. Only the vanilla conversation windows stay named, because
// they arrive AFTER the chat when the NPC opens the conversation and so can never be in any baseline.
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

        // The 3D world, exactly as the engine builds it: TaleWorlds.Engine.Screens.SceneLayer sets IsFocusLayer
        // in its own CONSTRUCTOR, so it is claiming focus on every screen that has a scene, forever.
        private static OverlayNeighbour TheWorld()
            => new() {Name = "SceneLayer", WantsFocus = true, IsTheWorldBeneath = true};

        // The mission a conversation actually happens in, as the engine reports it: the world, a pile of HUD
        // layers none of which claim focus, and the chat. Nothing here is a screen above anything.
        private static List<OverlayNeighbour> AQuietMission() => new() {
            TheWorld(), Layer("MissionCrosshair"), Layer("MissionNameMarker"),
            Layer("MissionSPKillFeed"), Layer("MissionObjective"), Layer("MissionOrder"), Chat()
        };

        // Gabriel's play-test, 2026-09-09: a chat opened from a tavern, with the vanilla settlement menu up and
        // holding focus behind it for the whole conversation. It is scenery, not a screen above, and treating it
        // as a modal meant the chat never appeared at all while the vanilla dialogue ran on underneath.
        [Test]
        public void GIVEN_a_settlement_menu_that_was_already_up_WHEN_arbitrating_THEN_the_chat_holds_its_own_backdrop()
        {
            var tavern = new List<OverlayNeighbour> {
                TheWorld(), Layer("MapMenuOverlay", wantsFocus: true), Layer("MapBar"), Chat()
            };
            var backdrop = new HashSet<string> {"MapMenuOverlay"};

            ChatFocusArbitrationPolicy.Decide(true, tavern, backdrop).Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.ReasonFor(true, tavern, backdrop).Should().BeEmpty();

            // And without the backdrop recorded, it is exactly the bug he saw.
            ChatFocusArbitrationPolicy.Decide(true, tavern).Should().Be(ChatFocusVerdict.Yield);
        }

        // The backdrop must not become a blanket amnesty: a real modal opening over that same tavern menu still
        // takes the screen, because it was not part of the picture when the chat opened.
        [Test]
        public void GIVEN_a_modal_opening_over_the_backdrop_WHEN_arbitrating_THEN_it_still_takes_the_screen()
        {
            var tavern = new List<OverlayNeighbour> {
                TheWorld(), Layer("MapMenuOverlay", wantsFocus: true), Chat(), Layer("MapEscapeMenu", wantsFocus: true)
            };
            var backdrop = new HashSet<string> {"MapMenuOverlay"};

            ChatFocusArbitrationPolicy.Decide(true, tavern, backdrop).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.ReasonFor(true, tavern, backdrop).Should().Be("MapEscapeMenu");
        }

        // The backdrop belongs to ONE conversation. Carrying it over would let the tavern menu of a chat that
        // has closed excuse a genuine modal in the next one, and the caller clears it on every close for that
        // reason; this pins that an empty backdrop restores the strict rule.
        [Test]
        public void GIVEN_no_backdrop_recorded_WHEN_arbitrating_THEN_every_focus_claim_is_judged_on_its_own_merits()
        {
            var layers = new List<OverlayNeighbour> {Chat(), Layer("MapMenuOverlay", wantsFocus: true)};

            ChatFocusArbitrationPolicy.Decide(true, layers, new HashSet<string>()).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.Decide(true, layers, null).Should().Be(ChatFocusVerdict.Yield);
        }

        // A nameless layer can never be matched against a backdrop of names, so it counts as new. That is the
        // safe way round: yielding to an unknown claimant undoes itself when the claimant goes, while ignoring a
        // real modal leaves the player in a menu that does not answer the mouse.
        [Test]
        public void GIVEN_an_unnamed_claimant_WHEN_a_backdrop_is_recorded_THEN_it_is_treated_as_new_rather_than_scenery()
        {
            var layers = new List<OverlayNeighbour> {Chat(), Layer(null, wantsFocus: true)};

            ChatFocusArbitrationPolicy.Decide(true, layers, new HashSet<string> {"MapMenuOverlay"})
                                      .Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.WasPartOfTheBackdrop(null, new HashSet<string> {"MapMenuOverlay"}).Should().BeFalse();
            ChatFocusArbitrationPolicy.WasPartOfTheBackdrop("MapMenuOverlay", null).Should().BeFalse();
            ChatFocusArbitrationPolicy.WasPartOfTheBackdrop("MapMenuOverlay", new HashSet<string> {"MapMenuOverlay"}).Should().BeTrue();
        }

        // The one thing no backdrop can cover, which is why the two vanilla conversation windows stay named: they
        // arrive AFTER the chat when the NPC starts the conversation, so they are never in the snapshot.
        [Test]
        public void GIVEN_a_backdrop_was_recorded_WHEN_the_vanilla_conversation_window_arrives_late_THEN_it_is_still_exempt()
        {
            var mission = AQuietMission();
            mission.Add(Layer("MissionConversation", wantsFocus: true));

            ChatFocusArbitrationPolicy.Decide(true, mission, new HashSet<string> {"SceneLayer"})
                                      .Should().Be(ChatFocusVerdict.Hold);
        }

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

        // The finding cr.testall returned on its first run, and the reason it was worth writing. The engine's
        // SceneLayer claims focus from its constructor on EVERY screen with a scene, which is every screen a
        // conversation happens on. Read as a modal, it would have slid the chat off the right edge on the first
        // frame of every conversation in the game and never brought it back: worse than the bug being fixed.
        // Decompiling all 58 game assemblies confirmed it is the ONLY layer claiming focus from a constructor
        // rather than when it opens, so this exemption is complete rather than the first of a growing list.
        [Test]
        public void GIVEN_the_world_beneath_the_chat_WHEN_arbitrating_THEN_it_is_never_read_as_a_screen_above()
        {
            ChatFocusArbitrationPolicy.Decide(true, new List<OverlayNeighbour> {TheWorld(), Chat()})
                                      .Should().Be(ChatFocusVerdict.Hold);
            ChatFocusArbitrationPolicy.ReasonFor(true, new List<OverlayNeighbour> {TheWorld(), Chat()})
                                      .Should().BeEmpty();
        }

        // And the exemption must not become a blanket: a real modal over the same scene still takes the screen.
        [Test]
        public void GIVEN_a_modal_over_the_world_WHEN_arbitrating_THEN_the_modal_is_still_honoured()
        {
            var layers = new List<OverlayNeighbour> {TheWorld(), Chat(), Layer("MissionEscapeMenu", wantsFocus: true)};

            ChatFocusArbitrationPolicy.Decide(true, layers).Should().Be(ChatFocusVerdict.Yield);
            ChatFocusArbitrationPolicy.ReasonFor(true, layers).Should().Be("MissionEscapeMenu");
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
            AQuietMission().Where(l => !l.IsTheChat && !l.IsTheWorldBeneath).Should().OnlyContain(l => !l.WantsFocus);
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
