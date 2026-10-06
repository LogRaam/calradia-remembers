// Code written by Gabriel Mailhot, 06/10/2026.
// A tale can leave two lords at odds. The branching run left it open (DECISIONS W2: "no rule yet on how long two lords
// may hate each other"); Gabriel (06/10/2026): "a quest can influence the regard between lords; CR already handles the
// time for that." So an ending may carry a "grudge" consequence: one role holds it against another, through CR's own
// grudge ledger, which already fades with time. If this breaks, the lord who learns his neighbour burned his village
// greets him at the next council as if nothing happened, or holds it against him for ever.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredQuestGrudgeTests
   {
      private static AuthoredQuestRun Run() => new() {
         QuestId = "t",
         BoundIds = new Dictionary<string, string> {["giver"] = "lord_giver", ["rival"] = "lord_rival"},
         BoundNames = new Dictionary<string, string> {["giver"] = "Lucon", ["rival"] = "Crotor"}
      };

      private static AuthoredConsequence Grudge(string who = "{giver}", string against = "{rival}", int magnitude = 30)
         => new() {Type = "grudge", Who = who, Against = against, Text = "{rival}'s riders burned my village", Magnitude = magnitude};

      // The giver holds it against the rival, for the cause the file gives, filled with their names.
      [Test]
      public void GIVEN_an_ending_that_sets_two_lords_at_odds_WHEN_carried_out_THEN_the_one_holds_it_against_the_other()
      {
         TaleGrudge g = AuthoredQuestEngine.GrudgeOf(Run(), Grudge());

         g.HolderId.Should().Be("lord_giver");
         g.TargetId.Should().Be("lord_rival");
         g.Cause.Should().Be("Crotor's riders burned my village");
         g.Magnitude.Should().Be(30);
      }

      // A role the tale never bound, or a lord set against himself, plants nothing rather than a grudge against nobody.
      [Test]
      public void GIVEN_a_role_not_bound_or_a_lord_against_himself_WHEN_carried_out_THEN_no_grudge_is_planted()
      {
         AuthoredQuestEngine.GrudgeOf(Run(), Grudge(against: "{village}")).Should().BeNull();
         AuthoredQuestEngine.GrudgeOf(Run(), Grudge(against: "{giver}")).Should().BeNull();
      }

      // A grudge is 0 to 100 like every other; a file that writes 500 or nothing still plants a sane one.
      [Test]
      public void GIVEN_a_magnitude_out_of_range_or_missing_WHEN_carried_out_THEN_it_is_kept_within_a_grudge()
      {
         AuthoredQuestEngine.GrudgeOf(Run(), Grudge(magnitude: 500)).Magnitude.Should().Be(100);
         AuthoredQuestEngine.GrudgeOf(Run(), Grudge(magnitude: 0)).Magnitude.Should().Be(AuthoredQuestEngine.DefaultTaleGrudgeMagnitude);
      }

      // Gabriel: "CR already handles the time for that." A lords' quarrel born of a tale mends with time, unless it has
      // hardened into a vendetta, the same as every other grudge.
      [Test]
      public void GIVEN_a_grudge_born_of_a_tale_WHEN_asked_how_it_mends_THEN_it_is_time()
      {
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Tale, rigidity: 10).Should().Be(GrudgeResolutionKind.Time);
         GrudgePolicy.ResolutionKindFor(GrudgeSource.Tale, rigidity: GrudgePolicy.VendettaRigidity).Should().Be(GrudgeResolutionKind.Unforgivable);
      }

      // The loader knows the new consequence: a file that uses it loads without being told it is skipped.
      [Test]
      public void GIVEN_a_tale_with_a_grudge_consequence_WHEN_loaded_THEN_the_author_is_not_told_it_is_skipped()
      {
         AuthoredQuestLoader.ConsequenceTypes.Should().Contain("grudge");
      }
   }
}
