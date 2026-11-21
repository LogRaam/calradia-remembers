// Code written by Gabriel Mailhot, 31/10/2026.
// ButtonGlowPolicy decides how strongly a chat button glows under the cursor. Gabriel (31/10/2026): "ce serait bien
// d'ajouter le hover effect sur les boutons". In Dark Leather the tool buttons are transparent hotspots over painted
// tiles, so without the glow nothing says they can be clicked. If a disabled button glowed, it would promise a click that
// does nothing. The prefab guard below keeps the promise for buttons added later: a new button with neither a brush nor a
// glow is silent under the cursor again, and nothing else would notice.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Chat
{
    [TestFixture]
    public sealed class ButtonGlowPolicyTests
    {
        // Each stays silent on purpose: a picture or portrait the glow would cover, or art that has its own hover. A memory
        // picture is a picture like the scene's; a memory row is sized by its text, where a glow would flood the panel (see
        // below), and it answers a click with its selection mark (20/11/2026).
        private static readonly string[] NeverGlows = {
            "AvatarContainer", "ExecuteToggleSceneImageSize", "ExecuteChoose", "KillRollGround", "KillRollStrike", "KillRollStay",
            "ExecuteToggleMemoryPictureSize", "MemoryPictureLarge", "MemoryRowButton"
        };

        // STAKE: the whole request: under the cursor a button must answer, or Dark Leather's painted tiles read as decoration.
        [Test]
        public void GIVEN_the_cursor_on_a_button_WHEN_it_settles_THEN_the_button_glows()
        {
            float strength = 0f;
            for (var i = 0; i < 60; i++) strength = ButtonGlowPolicy.Next(strength, new ButtonGlowFacts {IsHovered = true}, 0.016f);

            strength.Should().Be(ButtonGlowPolicy.HoveredStrength);
        }

        // STAKE: a press needs its own answer, or a click on a hotspot feels as if nothing happened.
        [Test]
        public void GIVEN_a_button_held_down_WHEN_compared_to_hovering_THEN_it_glows_more()
        {
            float pressed = ButtonGlowPolicy.TargetStrength(new ButtonGlowFacts {IsHovered = true, IsPressed = true});

            pressed.Should().BeGreaterThan(ButtonGlowPolicy.TargetStrength(new ButtonGlowFacts {IsHovered = true}));
        }

        // STAKE: Send is disabled while a reply arrives; a glow there would invite a click the chat ignores.
        [Test]
        public void GIVEN_a_disabled_button_under_the_cursor_WHEN_it_settles_THEN_it_stays_dark()
        {
            ButtonGlowPolicy.TargetStrength(new ButtonGlowFacts {IsHovered = true, IsPressed = true, IsDisabled = true}).Should().Be(0f);
        }

        // STAKE: a glow that outlived the cursor would leave a lit tile the player is no longer touching.
        [Test]
        public void GIVEN_the_cursor_gone_WHEN_it_settles_THEN_the_glow_is_out()
        {
            float strength = ButtonGlowPolicy.PressedStrength;
            for (var i = 0; i < 60; i++) strength = ButtonGlowPolicy.Next(strength, new ButtonGlowFacts(), 0.016f);

            strength.Should().Be(0f);
        }

        // STAKE: a button added to either chat skin without a brush or a glow is silent under the cursor, the exact gap
        // Gabriel asked to close; this names it the day it is added.
        [TestCase("CalradiaRemembersChat.xml")]
        [TestCase("CalradiaRemembersChat_Manuscript.xml")]
        public void GIVEN_a_chat_skin_WHEN_its_buttons_are_read_THEN_every_one_answers_the_cursor(string prefab)
        {
            Assume.That(SourceTree.IsReachable, SourceTree.MissingAnchor);
            XDocument doc = XDocument.Load(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "GUI", "Prefabs", prefab));

            List<string> silent = doc.Descendants("ButtonWidget")
                .Where(b => b.Attribute("Brush") == null)
                .Where(b => !NeverGlows.Contains(NameOf(b)))
                .Where(b => !b.Elements("Children").Elements("CrHoverGlowWidget").Any())
                .Where(b => !b.Elements("Children").Elements("TextWidget").Any(t => ((string?) t.Attribute("Brush") ?? "").StartsWith("CR.Link.", StringComparison.Ordinal)))
                .Select(NameOf)
                .ToList();

            silent.Should().BeEmpty("these buttons give no sign under the cursor");
        }

        // STAKE: Gabriel's screenshot, 31/10/2026: a button sized by its text gave the glow nothing to stretch to, and hovering
        // "Model: Roleplay" lit half the screen. Such a button answers by its text instead.
        [TestCase("CalradiaRemembersChat.xml")]
        [TestCase("CalradiaRemembersChat_Manuscript.xml")]
        public void GIVEN_a_button_sized_by_its_text_WHEN_it_is_read_THEN_no_glow_can_flood_the_screen(string prefab)
        {
            Assume.That(SourceTree.IsReachable, SourceTree.MissingAnchor);
            XDocument doc = XDocument.Load(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "GUI", "Prefabs", prefab));

            List<string> flooding = doc.Descendants("ButtonWidget")
                .Where(b => (string?) b.Attribute("WidthSizePolicy") == "CoverChildren" || (string?) b.Attribute("HeightSizePolicy") == "CoverChildren")
                .Where(b => b.Elements("Children").Elements("CrHoverGlowWidget").Any())
                .Select(NameOf)
                .ToList();

            flooding.Should().BeEmpty();
        }

        private static string NameOf(XElement button)
            => (string?) button.Attribute("Id") ?? (string?) button.Attribute("Command.Click") ?? "(unnamed)";
    }
}
