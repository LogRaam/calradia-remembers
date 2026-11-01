// Code written by Gabriel Mailhot, 31/10/2026.
// The chat's NPC dossier shows what the player has learned of a character. Gabriel (31/10/2026): "Dans le panneau du NPC, on
// pourrait ajouter tout ce que le joueur a appris sur ce NPC et qui a ete ajoute a l'Encyclopedia". The dossier appears in
// three windows, so a section bound in only one is missing for a player of the other skin. The prompt holds truths the
// player has not learned (the model may reveal a fear), so a section reading them straight from the profile would spoil
// what the game means the player to discover. cr.test npc_dossier walks that hiding in a real chat.

#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Chat
{
    [TestFixture]
    public sealed class NpcDossierTests
    {
        private static readonly string[] Sections = {"Discoveries", "Heart", "Kindnesses", "Grievances", "Accords", "Nemesis", "Trauma"};

        // STAKE: a section with nothing in it must read as empty, or its heading stands over a blank line on Ajin's page.
        [Test]
        public void GIVEN_nothing_learned_WHEN_a_section_is_written_THEN_it_stays_empty()
        {
            NpcDossierText.Bullets(new[] {"", "  ", null}).Should().BeEmpty();
        }

        // STAKE: each thing learned reads as its own line, as it does on the encyclopedia page.
        [Test]
        public void GIVEN_two_things_learned_WHEN_a_section_is_written_THEN_each_has_its_own_bullet()
        {
            NpcDossierText.Bullets(new[] {"He fears fire.", "He loves horses."}).Should().Be("• He fears fire.\n• He loves horses.");
        }

        // STAKE: the panel showed a number with no cause; the encyclopedia names the reason, and Gabriel asked for what it holds.
        [Test]
        public void GIVEN_a_known_reason_WHEN_the_regard_is_written_THEN_the_reason_stands_beside_it()
        {
            NpcDossierText.RegardLine("resentment", -40, "you executed his brother").Should().Be("resentment (-40): you executed his brother");
        }

        // STAKE: a stranger's regard must stay as it read on Gabriel's screenshot, with no dangling colon.
        [Test]
        public void GIVEN_no_known_reason_WHEN_the_regard_is_written_THEN_only_the_regard_shows()
        {
            NpcDossierText.RegardLine("neutral", 0, null).Should().Be("neutral (0)");
        }

        // STAKE: the dossier opens in Classic, Dark Leather and the council; a section left out of one is silently missing there.
        [TestCase("CalradiaRemembersChat.xml")]
        [TestCase("CalradiaRemembersChat_Manuscript.xml")]
        [TestCase("CalradiaRemembersCouncil.xml")]
        public void GIVEN_a_window_showing_the_dossier_WHEN_its_sections_are_read_THEN_none_is_missing(string prefab)
        {
            Assume.That(SourceTree.IsReachable, SourceTree.MissingAnchor);
            string xml = File.ReadAllText(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "GUI", "Prefabs", prefab));

            List<string> missing = Sections.Where(s => !xml.Contains("@ShowPanel" + s + "\"")).ToList();

            missing.Should().BeEmpty();
        }

        // STAKE: the model knows fears, superstitions and tastes the player has not learned; read raw, the panel would spoil them.
        [TestCase("Chat", "ChatViewModel.Dossier.cs")]
        [TestCase("Mapping", "DossierReader.cs")]
        public void GIVEN_the_dossier_code_WHEN_it_is_read_THEN_no_secret_comes_straight_from_the_profile(string folder, string file)
        {
            Assume.That(SourceTree.IsReachable, SourceTree.MissingAnchor);
            string code = File.ReadAllText(Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", folder, file));

            Regex.IsMatch(code, @"profile\??\.(Fear|Superstition|MattersMost|LovedGifts|ColdGifts)\b").Should().BeFalse();
        }
    }
}
