// Code written by Gabriel Mailhot, 19/09/2026.
// What these pin: that a grievance the player has ANSWERED is remembered as answered, and that answering it is
// not mending it.
//
// Zivkyn (Nexus, 18/09/2026): a lord kept "asking why i killed their Kinsmen, that i did months ago, even though
// ive explained it 10 times now". The explanation lived only in the conversation it was spoken in; the grudge never
// learned it had been answered, so every later conversation met it fresh, and the lord kept riding out every thirty
// days to confront the player about it. Gabriel's ruling (19/09/2026), which these tests hold the code to:
//   - a vendetta stays unforgivable, answered or not;
//   - an answered grievance no longer brings its holder out to confront the player;
//   - an explanation does not soften a grievance: only amends, gifts, deeds and time do that.

#region

using Newtonsoft.Json;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
   [TestFixture]
   public sealed class AnsweredGrievanceTests
   {
      private const int Today = 400;

      // THE REPORTED CASE, end to end in words: once answered, the holder is told what was said and told not to
      // ask again. Without this line the model reads "you may name it openly" every time and asks afresh.
      [Test]
      public void GIVEN_an_answered_grievance_WHEN_the_holder_reads_it_THEN_they_know_the_account_and_are_told_not_to_ask_again()
      {
         Grudge grudge = Vendetta();
         GrudgePolicy.RecordAnswer(grudge, "Your brother raised his banner against his own liege", Today);

         string line = GrudgeNarrator.PromptLine(grudge, true, 90);

         line.Should().Contain("Your brother raised his banner against his own liege");
         line.Should().Contain("Do not ask them again why");
         line.Should().NotContain("openly", "the permissive wording is what let the question come back every time");
      }

      // A VENDETTA STAYS A VENDETTA. The first of Gabriel's three rulings: an answer is not forgiveness.
      [Test]
      public void GIVEN_a_vendetta_WHEN_it_is_answered_THEN_it_is_exactly_as_heavy_and_exactly_as_unforgivable()
      {
         Grudge grudge = Vendetta();

         GrudgePolicy.RecordAnswer(grudge, "It was war.", Today).Should().BeTrue();

         grudge.Magnitude.Should().Be(90);
         grudge.Rigidity.Should().Be(GrudgePolicy.VendettaRigidity);
         grudge.IsResolved.Should().BeFalse();
         GrudgePolicy.IsResolvable(grudge).Should().BeFalse();
      }

      // AN EXPLANATION IS NOT AMENDS, the third ruling, for a grievance that could otherwise be mended.
      [Test]
      public void GIVEN_a_mendable_grievance_WHEN_it_is_answered_THEN_nothing_about_its_weight_changes()
      {
         Grudge grudge = Mendable();

         GrudgePolicy.RecordAnswer(grudge, "I had no choice.", Today);

         grudge.Magnitude.Should().Be(60);
         grudge.Rigidity.Should().Be(40);
         grudge.IsResolved.Should().BeFalse();
      }

      // THE SECOND RULING: the confrontation has happened. Before this, a vendetta sent its holder after the
      // player every thirty days, forever, with the same opener.
      [Test]
      public void GIVEN_an_answered_grievance_WHEN_asking_whether_it_still_drives_a_confrontation_THEN_it_does_not()
      {
         Grudge grudge = Vendetta();
         GrudgePolicy.StillDrivesConfrontation(grudge).Should().BeTrue("unanswered, it still brings the lord out");

         GrudgePolicy.RecordAnswer(grudge, "It was war.", Today);

         GrudgePolicy.StillDrivesConfrontation(grudge).Should().BeFalse();
      }

      // A resolved grievance has nothing left to answer and never drives anything.
      [Test]
      public void GIVEN_a_resolved_grievance_WHEN_answered_THEN_it_is_refused_and_it_drives_nothing()
      {
         Grudge grudge = Mendable();
         GrudgePolicy.Resolve(grudge, Today);

         GrudgePolicy.RecordAnswer(grudge, "Anyway.", Today).Should().BeFalse();
         GrudgePolicy.IsAnswered(grudge).Should().BeFalse();
         GrudgePolicy.StillDrivesConfrontation(grudge).Should().BeFalse();
      }

      // An [ACTION] emitted with nothing in it is not an answer, and must not silence the question.
      [Test]
      public void GIVEN_an_empty_account_WHEN_recorded_THEN_the_grievance_stays_unanswered()
      {
         Grudge grudge = Vendetta();

         GrudgePolicy.RecordAnswer(grudge, "   ", Today).Should().BeFalse();
         GrudgePolicy.RecordAnswer(grudge, null, Today).Should().BeFalse();

         GrudgePolicy.IsAnswered(grudge).Should().BeFalse();
      }

      // The account rides in the prompt every turn afterwards, so it stays one line however much the model wrote.
      [Test]
      public void GIVEN_a_long_multiline_account_WHEN_recorded_THEN_it_is_kept_as_one_bounded_line()
      {
         Grudge grudge = Vendetta();

         GrudgePolicy.RecordAnswer(grudge, "First line.\nSecond line.\r\n" + new string('x', 500), Today);

         grudge.PlayerAccount.Should().NotContain("\n").And.NotContain("\r");
         grudge.PlayerAccount.Length.Should().BeLessThanOrEqualTo(GrudgePolicy.MaxAccountChars + 3);
         grudge.AnsweredDay.Should().Be(Today);
      }

      // Answering something is knowing of it; a grievance answered can no longer be the holder's secret.
      [Test]
      public void GIVEN_a_grievance_the_player_did_not_know_of_WHEN_answered_THEN_it_becomes_known()
      {
         Grudge grudge = Mendable();
         grudge.KnownToPlayer = false;

         GrudgePolicy.RecordAnswer(grudge, "I heard what you think I did.", Today);

         grudge.KnownToPlayer.Should().BeTrue();
      }

      // SAVE MIGRATION: every grudge in a save written before 2.5.16 has no AnsweredDay. It must read as never
      // answered, which is the truth about all of them, and not as answered on day 0.
      [Test]
      public void GIVEN_a_grudge_saved_before_the_field_existed_WHEN_loaded_THEN_it_reads_as_never_answered()
      {
         const string oldSave = "{\"Id\":\"g1\",\"HolderId\":\"lord_1\",\"TargetId\":\"main_hero\",\"Magnitude\":80,\"Rigidity\":100,\"ResolvedDay\":-1}";

         var grudge = JsonConvert.DeserializeObject<Grudge>(oldSave);

         GrudgePolicy.IsAnswered(grudge).Should().BeFalse();
         grudge.PlayerAccount.Should().BeNull();
      }

      #region hardened, for the duel

      // Harden is Resolve's missing counterpart; it deepens, capped at 100.
      [Test]
      public void GIVEN_a_live_grievance_WHEN_hardened_THEN_it_deepens_and_never_past_100()
      {
         Grudge grudge = Mendable();

         GrudgePolicy.Harden(grudge, 15).Should().BeTrue();
         grudge.Magnitude.Should().Be(75);

         GrudgePolicy.Harden(grudge, 50);
         grudge.Magnitude.Should().Be(100);
      }

      // Worse is not unforgivable: one refusal must not turn a quarrel into a vendetta.
      [Test]
      public void GIVEN_a_mendable_grievance_WHEN_hardened_THEN_it_stays_mendable()
      {
         Grudge grudge = Mendable();

         GrudgePolicy.Harden(grudge, 15);

         grudge.Rigidity.Should().Be(40);
         GrudgePolicy.IsResolvable(grudge).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_resolved_grievance_WHEN_hardened_THEN_it_stays_resolved_and_unchanged()
      {
         Grudge grudge = Mendable();
         GrudgePolicy.Resolve(grudge, Today);

         GrudgePolicy.Harden(grudge, 15).Should().BeFalse();
         grudge.Magnitude.Should().Be(60);
      }

      #endregion

      #region private

      private static Grudge Vendetta() => new() {
         Id = "v", HolderId = "lord_1", TargetId = "main_hero", Source = GrudgeSource.ExecutedKin,
         CauseSummary = "the execution of their brother", Magnitude = 90, Rigidity = GrudgePolicy.VendettaRigidity,
         KnownToPlayer = true, EventDay = 100
      };

      private static Grudge Mendable() => new() {
         Id = "m", HolderId = "lord_1", TargetId = "main_hero", Source = GrudgeSource.Jealousy,
         CauseSummary = "the attentions you paid their wife", Magnitude = 60, Rigidity = 40, KnownToPlayer = true, EventDay = 100
      };

      #endregion
   }
}
