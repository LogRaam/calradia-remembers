// Code written by Gabriel Mailhot, 23/10/2026.
// The romance status door (LapitaPolata, 22/10/2026: a secret lover made in CR's chat "only exists in my and her memory.
// Affairs of Calradia does not know about it"). Another mod reads a character's bond with the player as one word and hears
// when it changes, so a bridge can carry it into its own mod. If this is wrong, a bridge makes an ex-wife a wife again, misses
// the secret lover entirely, or replays every bond of a loaded save as if it had just happened.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RomanceBondPolicyTests
   {
      private static Dictionary<string, RomanceBondReading> Bonds(params (string Id, string Bond)[] bonds)
      {
         var d = new Dictionary<string, RomanceBondReading>();
         foreach ((string id, string bond) in bonds) d[id] = new RomanceBondReading {Bond = bond};

         return d;
      }

      // STAKE: LapitaPolata's own case: the secret lover made in conversation must reach the bridge as one, with her day.
      [Test]
      public void GIVEN_a_companion_become_the_players_secret_lover_WHEN_another_mod_reads_her_bond_THEN_it_is_secret_lover_since_that_day()
      {
         RomanceBondReading r = RomanceBondPolicy.Read(new HeartFacts {Status = RomanticStatus.SecretLover, IsSecretLover = true, SecretLoverSinceDay = 100});

         r.Bond.Should().Be("secret_lover");
         r.SinceDay.Should().Be(100);
      }

      // STAKE: a married lord's affair with the player is a love kept hidden too; a bridge must not read it as plain lovers.
      [Test]
      public void GIVEN_a_married_lord_in_an_affair_with_the_player_WHEN_another_mod_reads_her_bond_THEN_it_is_secret_lover()
      {
         RomanceBondPolicy.Read(new HeartFacts {Status = RomanticStatus.SecretLover, LoversSinceDay = 50}).Bond.Should().Be("secret_lover");
      }

      // STAKE: a divorce does not reset the romantic state (it may still read Committed); a bridge must not make her a wife again.
      [Test]
      public void GIVEN_an_ex_wife_whose_state_still_reads_committed_WHEN_another_mod_reads_her_bond_THEN_it_is_former_spouse()
      {
         RomanceBondPolicy.Read(new HeartFacts {Status = RomanticStatus.Committed, IsExSpouse = true, WeddedOnDay = 200})
                          .Should().BeEquivalentTo(new RomanceBondReading {Bond = "former_spouse", SinceDay = 200});
      }

      // STAKE: the game's marriage outranks whatever the chat last said, as on the Heart page.
      [Test]
      public void GIVEN_the_players_wife_WHEN_another_mod_reads_her_bond_THEN_it_is_spouse_dated_by_the_wedding()
      {
         RomanceBondPolicy.Read(new HeartFacts {Status = RomanticStatus.Estranged, IsSpouse = true, WeddedOnDay = 300})
                          .Should().BeEquivalentTo(new RomanceBondReading {Bond = "spouse", SinceDay = 300});
      }

      // STAKE: a stranger has no bond, and no date that would read as one.
      [Test]
      public void GIVEN_a_character_with_no_romance_WHEN_another_mod_reads_her_bond_THEN_it_is_none_without_a_date()
      {
         RomanceBondPolicy.Read(new HeartFacts {StatusSinceDay = 10}).Should().BeEquivalentTo(new RomanceBondReading {Bond = "none"});
      }

      // STAKE: open terms change what a bridge may do (no jealousy), so they are read with the bond.
      [Test]
      public void GIVEN_a_consort_on_open_terms_WHEN_another_mod_reads_her_bond_THEN_it_is_consort_with_open_terms()
      {
         RomanceBondReading r = RomanceBondPolicy.Read(new HeartFacts {Status = RomanticStatus.Committed, IsConsort = true, IsOpenArrangement = true});

         r.Bond.Should().Be("consort");
         r.OpenTerms.Should().BeTrue();
      }

      // STAKE: loading a save is not news: a bridge must not hear every bond of the campaign as if it had just begun.
      [Test]
      public void GIVEN_a_save_just_loaded_with_a_secret_lover_WHEN_the_first_pass_runs_THEN_nothing_is_announced()
      {
         var watch = new RomanceBondWatch();

         watch.Pass(Bonds(("leontia", "secret_lover"))).Should().BeEmpty();
      }

      // STAKE: the event a bridge waits for: the courtship became a secret love.
      [Test]
      public void GIVEN_a_courtship_WHEN_she_becomes_the_players_secret_lover_THEN_the_change_is_announced_from_courting_to_secret_lover()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("leontia", "courting")));

         IReadOnlyList<RomanceBondChange> changes = watch.Pass(Bonds(("leontia", "secret_lover")));

         changes.Should().ContainSingle();
         changes[0].NpcId.Should().Be("leontia");
         changes[0].Before.Bond.Should().Be("courting");
         changes[0].After.Bond.Should().Be("secret_lover");
      }

      // STAKE: a bond that begins from nothing is announced from "none", once.
      [Test]
      public void GIVEN_a_stranger_WHEN_she_begins_courting_the_player_THEN_it_is_announced_once_from_none()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds());

         watch.Pass(Bonds(("ylva", "courting"))).Should().ContainSingle().Which.Before.Bond.Should().Be("none");
         watch.Pass(Bonds(("ylva", "courting"))).Should().BeEmpty();
      }

      // STAKE: a character no longer read at all has lost her bond: the bridge must hear it end, or keep her a lover forever.
      [Test]
      public void GIVEN_a_lover_WHEN_she_is_no_longer_read_with_a_bond_THEN_the_end_is_announced_as_none()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("ylva", "lovers")));

         watch.Pass(Bonds()).Should().ContainSingle().Which.After.Bond.Should().Be("none");
      }

      // STAKE: a date found later for an old bond is not a new bond; a bridge must not be told twice.
      [Test]
      public void GIVEN_the_same_bond_WHEN_only_its_date_appears_THEN_nothing_is_announced()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(new Dictionary<string, RomanceBondReading> {["ylva"] = new() {Bond = "lovers"}});

         watch.Pass(new Dictionary<string, RomanceBondReading> {["ylva"] = new() {Bond = "lovers", SinceDay = 40}}).Should().BeEmpty();
      }

      // STAKE: a new campaign loaded in the same session starts over, silently.
      [Test]
      public void GIVEN_a_watch_with_bonds_WHEN_it_is_reset_THEN_the_next_pass_only_learns()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("ylva", "lovers")));
         watch.Reset();

         watch.Pass(Bonds(("leontia", "spouse"))).Should().BeEmpty();
      }

      // ── Whose divorce: tashmetu (Nexus, 25/11/2026, Bridges to Remember) ends the marriage in Affairs of Calradia when CR
      // divorces; with Affairs' polygamy, another wife promoted to the game's spouse reads the old one "former_spouse" too.

      // STAKE: a divorce pronounced in CR not marked as CR's: the bridge ignores it and Affairs weds her back (bdionysis80's Lara).
      [Test]
      public void GIVEN_a_wife_WHEN_CR_dissolves_the_marriage_THEN_the_change_is_marked_ended_by_CR()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("lara", "spouse")));
         watch.NoteMarriageEndedByCr("lara");

         RomanceBondChange change = watch.Pass(Bonds(("lara", "former_spouse"))).Single();

         change.EndedByCr.Should().BeTrue();
      }

      // STAKE: another wife promoted to the game's spouse read as CR's divorce: the bridge ends a marriage Affairs still holds.
      [Test]
      public void GIVEN_a_wife_WHEN_she_reads_former_spouse_without_CR_dissolving_it_THEN_it_is_not_marked_ended_by_CR()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("lara", "spouse")));

         watch.Pass(Bonds(("lara", "former_spouse"), ("chara", "spouse"))).Should().OnlyContain(c => !c.EndedByCr);
      }

      // STAKE: a note left over from one divorce must not mark a later, unrelated change of the same character.
      [Test]
      public void GIVEN_a_note_the_next_pass_did_not_use_WHEN_a_later_pass_ends_her_marriage_THEN_it_is_not_marked_ended_by_CR()
      {
         var watch = new RomanceBondWatch();
         watch.Pass(Bonds(("lara", "courting")));
         watch.NoteMarriageEndedByCr("lara");
         watch.Pass(Bonds(("lara", "spouse")));

         watch.Pass(Bonds(("lara", "former_spouse"))).Single().EndedByCr.Should().BeFalse();
      }
   }
}
