// Code written by Gabriel Mailhot, 23/10/2026.
// The Misdeeds section on the player's own page (Gabriel, 23/10/2026): "Heart doit etre une section sur l'amour et le desir.
// L'evenement 'take by force' devrait etre une autre section avec des actions reprehensibles", then "oui [beatings and
// robbery below Hardcore too], et on pourrait aussi ajouter des trahisons, extortions, et tout autres gestes qui pourraient
// etres classes comme misdeeds", "divorce est un choix de vie. adultere est un misdeed." What the player did of their own
// will; "les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur." If this is wrong, a player
// below Hardcore reads a captive scene they never opted into, what a captor did or forced the player to do is written as
// the player's own deed, a forgiven task reads as a broken word, or a deed two sources hold is told twice.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Logic.Trauma;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MisdeedsPagePolicyTests
   {
      private static string When(int day) => day switch {100 => "spring 1091", 200 => "summer 1092", 300 => "autumn 1093", _ => $"day {day}"};

      private static MisdeedRecord Deed(string victim, MisdeedKind kind, int day) => new() {VictimId = victim, Kind = kind, Day = day};

      // STAKE: the section is the player's own; a character's page tells what they lived through in its Trauma section.
      [Test]
      public void GIVEN_any_page_WHEN_it_opens_THEN_only_the_players_page_has_the_section()
      {
         MisdeedsPagePolicy.Shows(true).Should().BeTrue();
         MisdeedsPagePolicy.Shows(false).Should().BeFalse();
      }

      // STAKE: the captive scenes stay Hardcore only, as the Trauma section; a player on Explicit or below never opted in.
      [TestCase(AdultContentLevel.Off)]
      [TestCase(AdultContentLevel.Mature)]
      [TestCase(AdultContentLevel.Explicit)]
      public void GIVEN_a_level_below_hardcore_WHEN_gathered_THEN_no_captive_scene_shows(AdultContentLevel level)
      {
         var records = new[] {
            Deed("a", MisdeedKind.TookByForce, 100), Deed("b", MisdeedKind.Tortured, 100), Deed("c", MisdeedKind.Interrogated, 100),
            Deed("d", MisdeedKind.Tormented, 100)
         };

         MisdeedsPagePolicy.Of(records, level).Should().BeEmpty();
         MisdeedsPagePolicy.Of(records, AdultContentLevel.Hardcore).Should().HaveCount(4);
      }

      // STAKE: Gabriel: "oui": a beating, a robbery, a murder, a betrayal or an adultery is no adult content; it shows at
      // every level, Off included.
      [Test]
      public void GIVEN_deeds_that_are_no_captive_scene_WHEN_gathered_with_adult_content_off_THEN_they_show()
      {
         var records = new[] {
            Deed("a", MisdeedKind.Beat, 100), Deed("b", MisdeedKind.Robbed, 100), Deed("c", MisdeedKind.Murdered, 100),
            Deed("d", MisdeedKind.Executed, 100), Deed("e", MisdeedKind.BrokeWord, 100), Deed("f", MisdeedKind.HandedToEnemy, 100),
            Deed("g", MisdeedKind.Unfaithful, 100), Deed("h", MisdeedKind.LayWithSpouseOf, 100), Deed("i", MisdeedKind.StoleSpouse, 100),
            Deed("j", MisdeedKind.ForsookRealm, 100)
         };

         MisdeedsPagePolicy.Of(records, AdultContentLevel.Off).Should().HaveCount(10);
      }

      // STAKE: Gabriel: "Les actes que le joueur a fait subir peuvent apparaitre": a prisoner the player took by force is
      // named, once per victim, counted and dated.
      [Test]
      public void GIVEN_the_player_took_a_prisoner_by_force_twice_WHEN_the_players_page_opens_THEN_one_line_tells_it()
      {
         IReadOnlyList<MisdeedFacts> deeds = MisdeedsPagePolicy.Of(new[] {Deed("phenoria", MisdeedKind.TookByForce, 100), Deed("phenoria", MisdeedKind.TookByForce, 200)},
                                                                   AdultContentLevel.Hardcore);

         deeds.Should().HaveCount(1);
         MisdeedsPagePolicy.Line(deeds[0], "Phenoria", true, When)
                           .Should().Be("You took Phenoria by force while she was your prisoner, twice, the first in spring 1091, the last in summer 1092.");
      }

      // STAKE: each deed in plain words, sober, in the player's second person.
      [TestCase(MisdeedKind.TookByForce, "You took Ira by force while she was your prisoner, in spring 1091.")]
      [TestCase(MisdeedKind.Tortured, "You tortured Ira while she was your prisoner, in spring 1091.")]
      [TestCase(MisdeedKind.Interrogated, "You interrogated Ira under duress while she was your prisoner, in spring 1091.")]
      [TestCase(MisdeedKind.Tormented, "You tormented Ira while she was your prisoner, in spring 1091.")]
      [TestCase(MisdeedKind.Beat, "You beat Ira, in spring 1091.")]
      [TestCase(MisdeedKind.Robbed, "You robbed Ira by force, in spring 1091.")]
      [TestCase(MisdeedKind.Murdered, "You killed Ira outside of battle, in spring 1091.")]
      [TestCase(MisdeedKind.Executed, "You had Ira put to death, in spring 1091.")]
      [TestCase(MisdeedKind.BrokeWord, "You broke your word to Ira on a task she gave you, in spring 1091.")]
      [TestCase(MisdeedKind.HandedToEnemy, "You handed Ira over to her enemies while she was your prisoner, in spring 1091.")]
      [TestCase(MisdeedKind.Unfaithful, "You were unfaithful to Ira, in spring 1091.")]
      [TestCase(MisdeedKind.LayWithSpouseOf, "You lay with Ira's husband, in spring 1091.")]
      [TestCase(MisdeedKind.StoleSpouse, "You took Ira's husband from her, in spring 1091.")]
      public void GIVEN_each_kind_of_deed_WHEN_its_line_is_written_THEN_it_says_what_the_player_did(MisdeedKind kind, string expected)
      {
         MisdeedsPagePolicy.Line(new MisdeedFacts {VictimId = "ira", Kind = kind, Days = new[] {100}}, "Ira", true, When).Should().Be(expected);
      }

      // STAKE: a realm forsaken is named as the realm, with no he or she.
      [Test]
      public void GIVEN_a_realm_forsaken_WHEN_its_line_is_written_THEN_it_names_the_oath()
      {
         MisdeedsPagePolicy.Line(new MisdeedFacts {VictimId = "vlandia", Kind = MisdeedKind.ForsookRealm, Days = new[] {200}}, "Vlandia", false, When)
                           .Should().Be("You forsook your oath to Vlandia, in summer 1092.");
      }

      // STAKE: a husband wronged reads "wife", never "husband" twice.
      [Test]
      public void GIVEN_a_wronged_husband_WHEN_the_line_is_written_THEN_it_speaks_of_his_wife()
      {
         MisdeedsPagePolicy.Line(new MisdeedFacts {VictimId = "lucon", Kind = MisdeedKind.LayWithSpouseOf, Days = new[] {100}}, "Lucon", false, When)
                           .Should().Be("You lay with Lucon's wife, in spring 1091.");
      }

      // STAKE: two different deeds to one character are two lines, the oldest first, each with its own count.
      [Test]
      public void GIVEN_a_prisoner_the_player_robbed_then_took_by_force_WHEN_gathered_THEN_two_entries_the_oldest_first()
      {
         MisdeedsPagePolicy.Of(new[] {Deed("ira", MisdeedKind.TookByForce, 200), Deed("ira", MisdeedKind.Robbed, 100)}, AdultContentLevel.Hardcore)
                           .Select(d => d.Kind).Should().Equal(MisdeedKind.Robbed, MisdeedKind.TookByForce);
      }

      // STAKE: a murder both the game's death log and the misdeeds ledger hold is one murder, not two.
      [Test]
      public void GIVEN_one_deed_from_two_sources_WHEN_gathered_THEN_it_is_counted_once()
      {
         MisdeedsPagePolicy.Of(new[] {Deed("lucon", MisdeedKind.Murdered, 100), Deed("lucon", MisdeedKind.Murdered, 100)}, AdultContentLevel.Off)
                           .Single().Days.Should().Equal(100);
      }

      // STAKE: Gabriel: "Les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur." A scene where
      // the captor forced the player's own hands is also the player's ordeal, not the player's act.
      [TestCase(TraumaAuthor.Captor)]
      [TestCase(TraumaAuthor.CaptorsMen)]
      [TestCase(TraumaAuthor.PlayerCoerced)]
      public void GIVEN_what_a_captor_did_or_forced_the_player_to_do_WHEN_read_from_the_trauma_ledger_THEN_it_is_no_deed(TraumaAuthor author)
      {
         MisdeedsPagePolicy.FromTrauma(new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = author, Day = 100}).Should().BeNull();
      }

      // STAKE: the player's own deeds in the Trauma ledger keep their kind and day.
      [TestCase(TraumaKind.Assaulted, MisdeedKind.TookByForce)]
      [TestCase(TraumaKind.Tortured, MisdeedKind.Tortured)]
      [TestCase(TraumaKind.Interrogated, MisdeedKind.Interrogated)]
      [TestCase(TraumaKind.Tormented, MisdeedKind.Tormented)]
      [TestCase(TraumaKind.Beaten, MisdeedKind.Beat)]
      [TestCase(TraumaKind.Robbed, MisdeedKind.Robbed)]
      public void GIVEN_the_players_own_deed_in_the_trauma_ledger_WHEN_read_THEN_it_keeps_its_kind_and_day(TraumaKind trauma, MisdeedKind expected)
      {
         MisdeedRecord r = MisdeedsPagePolicy.FromTrauma(new TraumaRecord {VictimId = "ira", Kind = trauma, Author = TraumaAuthor.Player, Day = 100});

         r.Kind.Should().Be(expected);
         r.Day.Should().Be(100);
      }

      // STAKE: Gabriel's "trahisons": a task given up or let lapse is a broken word to its giver, dated when it was given.
      [TestCase(QuestStatus.Abandoned)]
      [TestCase(QuestStatus.Expired)]
      public void GIVEN_a_task_given_up_or_lapsed_WHEN_read_THEN_it_is_a_broken_word(QuestStatus status)
      {
         MisdeedRecord r = MisdeedsPagePolicy.FromQuest("ira", new InformalQuest {Type = QuestType.AttackLord, Status = status, IssuedOnDay = 100});

         r.Kind.Should().Be(MisdeedKind.BrokeWord);
         r.VictimId.Should().Be("ira");
         r.Day.Should().Be(100);
      }

      // STAKE: the game already forgives a bandit lair that could not be found, and a task set aside without fault; the page
      // must not call them a broken word.
      [Test]
      public void GIVEN_a_forgiven_or_finished_task_WHEN_read_THEN_it_is_no_deed()
      {
         MisdeedsPagePolicy.FromQuest("ira", new InformalQuest {Type = QuestType.BanditHideout, Status = QuestStatus.Abandoned}).Should().BeNull();
         MisdeedsPagePolicy.FromQuest("ira", new InformalQuest {Type = QuestType.AttackLord, Status = QuestStatus.Cancelled}).Should().BeNull();
         MisdeedsPagePolicy.FromQuest("ira", new InformalQuest {Type = QuestType.AttackLord, Status = QuestStatus.Completed}).Should().BeNull();
      }

      // STAKE: Gabriel: "adultere est un misdeed": the player's partner wronged by a night elsewhere, the spouse of the one
      // the player lay with, the spouse of the one the player married away.
      [TestCase(JealousStake.PlayerPartner, RomanticActTier.Intimacy, MisdeedKind.Unfaithful)]
      [TestCase(JealousStake.PlayerPartner, RomanticActTier.Marriage, MisdeedKind.Unfaithful)]
      [TestCase(JealousStake.SpouseOfCourted, RomanticActTier.Intimacy, MisdeedKind.LayWithSpouseOf)]
      [TestCase(JealousStake.SpouseOfCourted, RomanticActTier.Marriage, MisdeedKind.StoleSpouse)]
      public void GIVEN_an_adultery_found_out_WHEN_judged_THEN_it_is_a_misdeed(JealousStake stake, RomanticActTier tier, MisdeedKind expected)
      {
         MisdeedsPagePolicy.AdulteryOf(stake, tier, false).Should().Be(expected);
      }

      // STAKE: a flirt is doubt, not adultery; a rival or a spurned admirer was no one's spouse; a partner whose people
      // share partners, or who accepted the arrangement, was not betrayed.
      [TestCase(JealousStake.PlayerPartner, RomanticActTier.Flirt, false)]
      [TestCase(JealousStake.SpouseOfCourted, RomanticActTier.Flirt, false)]
      [TestCase(JealousStake.RivalSuitor, RomanticActTier.Intimacy, false)]
      [TestCase(JealousStake.SpurnedAdmirer, RomanticActTier.Marriage, false)]
      [TestCase(JealousStake.SecretLover, RomanticActTier.Intimacy, false)]
      [TestCase(JealousStake.PlayerPartner, RomanticActTier.Intimacy, true)]
      public void GIVEN_no_marriage_bed_wronged_WHEN_judged_THEN_it_is_no_misdeed(JealousStake stake, RomanticActTier tier, bool shared)
      {
         MisdeedsPagePolicy.AdulteryOf(stake, tier, shared).Should().BeNull();
      }

      // STAKE: the misdeeds ledger survives a save and load, and a kind a later version adds is skipped, never thrown.
      [Test]
      public void GIVEN_a_ledger_WHEN_saved_and_read_back_THEN_it_is_the_same_and_an_unknown_kind_is_skipped()
      {
         var ledger = new[] {Deed("ira", MisdeedKind.Unfaithful, 100), Deed("vlandia", MisdeedKind.ForsookRealm, 200)};

         List<MisdeedRecord> back = MisdeedsPagePolicy.Read(MisdeedsPagePolicy.Write(ledger) + "\nlucon|Extorted|300\nbroken");

         back.Should().HaveCount(2);
         back[1].VictimId.Should().Be("vlandia");
         back[1].Kind.Should().Be(MisdeedKind.ForsookRealm);
         back[1].Day.Should().Be(200);
      }
   }
}
