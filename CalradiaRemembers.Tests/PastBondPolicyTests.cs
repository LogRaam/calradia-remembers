// Code written by Gabriel Mailhot, 18/11/2026.

// PastBondPolicy decides which past bonds written in character_overrides.json a character brings to mind in a turn, and
// how each reads. Lufaine (Discord, 18/11/2026) wanted past romances and rivalries "instantly" loaded "without having to
// bloat the backstory"; Gabriel (18/11/2026) ruled them recalled when the other person is named or present, a bond with
// the player only from the campaign's own overlay. If the policy is wrong, a character forgets an old love standing in
// front of them, carries every bond in every conversation (the bloat Lufaine wanted gone), or a shared pack hands every
// player the same invented past with the character.

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
   public sealed class PastBondPolicyTests
   {
      private const string Speaker = "lord_a";
      private const string Rival = "lord_b";
      private const string Player = "main_hero";

      private static PastBondEntry Bond(string owner, string other, string kind = "rivalry", bool fromCampaign = false)
         => new() {OwnerId = owner, OtherId = other, Kind = kind, Summary = "They fought over the same fief for ten years.", FromCampaign = fromCampaign};

      private static IReadOnlyList<RecalledBond> Recall(IEnumerable<PastBondEntry> bonds, string speaker = Speaker, ICollection<string> named = null,
                                                        ICollection<string> present = null)
         => PastBondPolicy.Recall(new PastBondRecallFacts {
            SpeakerId = speaker, PlayerId = Player, Bonds = bonds.ToList(), NamedIds = named ?? new List<string>(), PresentIds = present ?? new List<string>()
         });

      // STAKE: the bond must not ride in every conversation (Lufaine's "bloat"); it comes up when the player names the other.
      [Test]
      public void GIVEN_a_bond_with_another_lord_WHEN_the_player_names_him_or_not_THEN_it_comes_to_mind_only_when_named()
      {
         Recall(new[] {Bond(Speaker, Rival)}).Should().BeEmpty();
         Recall(new[] {Bond(Speaker, Rival)}, named: new[] {Rival}).Should().ContainSingle(b => b.OtherId == Rival);
      }

      // STAKE: an old rival standing in the same scene is remembered without the player having to say his name.
      [Test]
      public void GIVEN_a_bond_with_someone_in_the_scene_WHEN_no_one_names_him_THEN_it_still_comes_to_mind()
      {
         Recall(new[] {Bond(Speaker, Rival)}, present: new[] {Rival}).Should().ContainSingle();
      }

      // STAKE: a romance written on one side only must not leave the other person a stranger to it: both remember.
      [Test]
      public void GIVEN_a_bond_written_in_the_other_characters_override_WHEN_this_one_speaks_of_him_THEN_this_one_remembers_it_too()
      {
         IReadOnlyList<RecalledBond> recalled = Recall(new[] {Bond(Rival, Speaker, "romance")}, named: new[] {Rival});

         recalled.Should().ContainSingle(b => b.OtherId == Rival && b.Kind == "romance");
      }

      // STAKE: a shared pack cannot know who the player will be; a bond with "the player" there would hand every player the same past.
      [Test]
      public void GIVEN_a_bond_with_the_player_WHEN_it_comes_from_the_shared_file_or_the_campaigns_own_THEN_only_the_campaigns_own_counts()
      {
         Recall(new[] {Bond(Speaker, Player, "romance", fromCampaign: false)}).Should().BeEmpty();
         Recall(new[] {Bond(Speaker, Player, "romance", fromCampaign: true)}).Should().ContainSingle(b => b.WithPlayer);
      }

      // STAKE: the player is always in the scene, so a past with them is always remembered, and first.
      [Test]
      public void GIVEN_bonds_with_the_player_and_a_named_lord_WHEN_recalled_THEN_the_players_comes_first()
      {
         IReadOnlyList<RecalledBond> recalled = Recall(new[] {Bond(Speaker, Rival), Bond(Speaker, Player, "friendship", fromCampaign: true)}, named: new[] {Rival});

         recalled.Select(b => b.OtherId).Should().Equal(Player, Rival);
      }

      // STAKE: a character with many bonds named at once must not flood the prompt.
      [Test]
      public void GIVEN_more_bonds_than_the_cap_all_named_WHEN_recalled_THEN_at_most_the_cap_come_to_mind()
      {
         var others = Enumerable.Range(0, 8).Select(i => "lord_" + i).ToList();

         Recall(others.Select(o => Bond(Speaker, o)), named: others).Should().HaveCount(PastBondPolicy.MaxLines);
      }

      // STAKE: a bond between two other people is not this character's memory.
      [Test]
      public void GIVEN_a_bond_between_two_others_WHEN_both_are_named_THEN_this_character_does_not_remember_it()
      {
         Recall(new[] {Bond(Rival, "lord_c")}, named: new[] {Rival, "lord_c"}).Should().BeEmpty();
      }

      // STAKE: the line must read as a past the character lived, name the other, and never name the player (whose name the
      // character may not know).
      [Test]
      public void GIVEN_recalled_bonds_WHEN_written_as_lines_THEN_they_read_as_lived_and_name_the_other()
      {
         string line = PastBondPolicy.Line(new RecalledBond {OtherId = Rival, Kind = "romance", Summary = "They loved each other in Pravend."}, "Lord B");
         line.Should().Contain(PastOnMentionPolicy.LivedItTag).And.Contain("An old romance with Lord B").And.Contain("They loved each other in Pravend");

         PastBondPolicy.Line(new RecalledBond {OtherId = Player, Kind = "feud", Summary = "A blood debt.", WithPlayer = true}, "Main Hero")
                       .Should().Contain("the one before you").And.NotContain("Main Hero");
      }

      // STAKE: an author's typo in the kind must not drop the bond in silence; it is kept as "other" and cr.lore_status says so.
      [Test]
      public void GIVEN_a_written_bond_with_mistakes_WHEN_its_problems_are_listed_THEN_each_is_named_for_the_author()
      {
         PastBondPolicy.KindOf("Romance").Should().Be("romance");
         PastBondPolicy.KindOf("lover").Should().Be("other");

         PastBondPolicy.Problems(new PastBond {With = "lord_b", Kind = "lover", Summary = "x"}).Should().ContainSingle(p => p.Contains("read as other"));
         PastBondPolicy.Problems(new PastBond {With = "player", Summary = "x"}).Should().ContainSingle(p => p.Contains("campaign"));
         PastBondPolicy.Problems(new PastBond {With = "player", Summary = "x", FromCampaign = true}).Should().BeEmpty();
         PastBondPolicy.Problems(new PastBond {With = "lord_b"}).Should().ContainSingle(p => p.Contains("no summary"));
      }

      // STAKE: Lufaine's whole point: a character with only past bonds is authored content and cr.reload_lore must not skip it.
      [Test]
      public void GIVEN_an_override_with_only_past_bonds_WHEN_asked_if_it_holds_authored_content_THEN_it_does()
      {
         new CharacterOverride {Hero = "Lufaine", Past = new List<PastBond> {new() {With = "lord_b", Summary = "x"}}}.HasAnyAuthoredContent.Should().BeTrue();
      }
   }
}
