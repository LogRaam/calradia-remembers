// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Heart section (Gabriel, 20/10/2026): on the page of a character engaged with the player ("seulement les
// npc engages avec le joueur"), the story of the bond told "plus personnel", in the player's second person; on the player's
// own page, one recap line per such person. If this is wrong, a stranger gets a love story, an ex-wife is still called the
// player's wife, a date is invented for a bond an old save never dated, or a hidden lover is revealed on her page.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HeartPagePolicyTests
   {
      private static string When(int day) => day switch {100 => "spring 1091", 200 => "summer 1093", 300 => "autumn 1094", _ => $"day {day}"};

      // STAKE: "seulement les npc engages avec le joueur": a flirt that went nowhere is not a love story.
      [Test]
      public void GIVEN_a_character_only_curious_about_the_player_WHEN_her_page_opens_THEN_there_is_no_heart_section()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Curious};

         HeartPagePolicy.Lines(f, When).Should().BeEmpty();
         HeartPagePolicy.RecapLine(f, When).Should().BeNull();
      }

      // STAKE: Gabriel's own example of the tone: the bond told to the player, dated.
      [Test]
      public void GIVEN_a_secret_lover_since_a_day_WHEN_her_page_opens_THEN_it_says_so_to_the_player_with_the_date()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.SecretLover, IsSecretLover = true, SecretLoverSinceDay = 100, LoversSinceDay = 100};

         HeartPagePolicy.Lines(f, When).Should().Equal("She has been your secret lover since spring 1091.");
      }

      // STAKE: a divorce does not reset the romantic state, so it may still read Committed; the page must not call an ex-wife
      // the player's wife.
      [Test]
      public void GIVEN_an_ex_wife_whose_bond_still_reads_committed_WHEN_her_page_opens_THEN_the_marriage_reads_dissolved()
      {
         var f = new HeartFacts {Name = "Ylva", IsFemale = true, Status = RomanticStatus.Committed, IsExSpouse = true, WeddedOnDay = 200};

         HeartPagePolicy.Lines(f, When)[0].Should().Be("She was your wife, wed in summer 1093. The marriage was dissolved.");
         HeartPagePolicy.RecapLine(f, When).Should().Be("Ylva, once your wife (wed in summer 1093)");
      }

      // STAKE: never fake what the player can verify: an old save recorded no day, so the bond is told without one.
      [Test]
      public void GIVEN_a_bond_from_before_dates_were_kept_WHEN_her_page_opens_THEN_it_is_told_without_a_date()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Intimate};

         HeartPagePolicy.Lines(f, When).Should().Equal("She has been your lover.");
      }

      // STAKE: a wife who was a lover first keeps both moments: the wedding, and when it began.
      [Test]
      public void GIVEN_a_wife_who_was_a_lover_first_WHEN_her_page_opens_THEN_it_tells_the_wedding_and_when_they_became_lovers()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Committed, IsSpouse = true, WeddedOnDay = 200, LoversSinceDay = 100};

         HeartPagePolicy.Lines(f, When).Should().Equal("She is your wife, wed in summer 1093.", "You first became lovers in spring 1091.");
      }

      // STAKE: a lover's line already gives the day they became lovers; it is not said twice.
      [Test]
      public void GIVEN_lovers_dated_once_WHEN_her_page_opens_THEN_the_day_is_given_once()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Intimate, LoversSinceDay = 100, StatusSinceDay = 100};

         HeartPagePolicy.Lines(f, When).Should().Equal("She has been your lover since spring 1091.");
      }

      // STAKE: the player can be a woman and the partner a man: the page says he, his and husband.
      [Test]
      public void GIVEN_a_husband_with_a_lover_the_player_knows_of_WHEN_his_page_opens_THEN_it_speaks_of_him_as_a_man()
      {
         var f = new HeartFacts {IsFemale = false, IsSpouse = true, Status = RomanticStatus.Committed, IsOpenArrangement = true, KnownLoverName = "Ylva"};

         HeartPagePolicy.Lines(f, When).Should().Equal("He is your husband.", "You hold each other free to love others.",
            "He has a lover of his own, Ylva, and you know it.");
      }

      // STAKE: her own lover is shown only once the player knows of him; the page must not reveal a hidden affair.
      [Test]
      public void GIVEN_a_lover_the_player_does_not_know_of_WHEN_her_page_opens_THEN_he_is_not_named()
      {
         var hidden = new HeartFacts {IsFemale = true, IsConsort = true, Status = RomanticStatus.Committed};
         var discovered = new HeartFacts {IsFemale = true, IsConsort = true, Status = RomanticStatus.Committed, KnownLoverName = "Erek", AffairDiscovered = true};

         HeartPagePolicy.Lines(hidden, When).Should().Equal("She is your consort.");
         HeartPagePolicy.Lines(discovered, When).Should().Contain("She took Erek as a lover behind your back.");
      }

      // STAKE: a story that ended badly is still a story, told with when it ended.
      [Test]
      public void GIVEN_a_broken_bond_WHEN_her_page_opens_THEN_it_says_it_is_over_and_when_they_were_lovers()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Broken, StatusSinceDay = 300, LoversSinceDay = 100};

         HeartPagePolicy.Lines(f, When).Should().Equal("What was between you is over. It ended in autumn 1094.", "You first became lovers in spring 1091.");
      }

      // STAKE: "un recap sur la page du joueur": one line per person, with what she is to the player, since when, and the
      // children they have together.
      [Test]
      public void GIVEN_a_wife_with_children_WHEN_the_players_page_opens_THEN_her_recap_names_the_bond_the_date_and_the_children()
      {
         var f = new HeartFacts
         {
            Name = "Ylva", IsFemale = true, IsSpouse = true, Status = RomanticStatus.Committed, WeddedOnDay = 200,
            Children = new List<HeartChild> {new() {Name = "Arven", BornYear = 1094}, new() {Name = "Liena", BornYear = 1096}}
         };

         HeartPagePolicy.RecapLine(f, When).Should().Be("Ylva, your wife since summer 1093; your children: Arven, Liena");
         HeartPagePolicy.Lines(f, When).Should().Contain("Your children together: Arven (1094), Liena (1096).");
      }

      // STAKE: avalanche24 (Nexus, 04/11/2026): Rhagaea was wed to another while the player courted her, and her page said
      // only "grown distant", naming no one. The wedding the whole realm saw is what ended it, so it is told, dated.
      [Test]
      public void GIVEN_a_courtship_ended_by_her_wedding_another_WHEN_her_page_opens_THEN_it_names_him_and_the_wedding()
      {
         var f = new HeartFacts {Name = "Rhagaea", IsFemale = true, Status = RomanticStatus.Estranged, StatusSinceDay = 300,
                                 WedToAnotherName = "Garios", WedToAnotherOnDay = 200};

         HeartPagePolicy.Lines(f, When).Should().Equal("She wed Garios in summer 1093, and has grown distant from you.");
         HeartPagePolicy.RecapLine(f, When).Should().Be("Rhagaea, grown distant from you, wed to Garios in summer 1093");
      }

      // STAKE: a bond the player broke off is over, and her later marriage is told as such, never as a bond still alive.
      [Test]
      public void GIVEN_a_broken_bond_and_her_wedding_another_WHEN_her_page_opens_THEN_it_says_it_is_over()
      {
         var f = new HeartFacts {IsFemale = false, Status = RomanticStatus.Broken, WedToAnotherName = "Ira"};

         HeartPagePolicy.Lines(f, When).Should().Equal("He wed Ira. What was between you is over.");
      }

      // STAKE: a lover still warm toward the player while wed to another: the page must not hide the marriage the player
      // can see in the game, nor tell it as the end of the bond.
      [Test]
      public void GIVEN_a_lover_wed_to_another_WHEN_her_page_opens_THEN_the_bond_comes_first_and_her_marriage_follows()
      {
         var f = new HeartFacts {IsFemale = true, Status = RomanticStatus.Intimate, LoversSinceDay = 100, WedToAnotherName = "Garios", WedToAnotherOnDay = 200};

         HeartPagePolicy.Lines(f, When).Should().Equal("She has been your lover since spring 1091.", "She has been wed to Garios since summer 1093.");
      }
   }
}
