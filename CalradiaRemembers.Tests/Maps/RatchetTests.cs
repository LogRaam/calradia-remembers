// Code written by Gabriel Mailhot, 21/09/2026.
// What these pin: that the two properties we have decided may only improve do not get worse.
//
// A critique of the maps write-up (21/09/2026) named the one risk the maps cannot answer themselves: approval
// fatigue. Every other test here ends in a human reading a diff, and with an AI producing structural change quickly,
// "regenerate and commit" becomes a reflex within weeks. A reflex is not a review.
//
// A RATCHET needs no reader. A snapshot asks "is this exactly as it was?" and only a person can answer that; a
// ratchet asks "has this got worse?" and answers itself. So the two properties that are monotonic by intention are
// pinned here rather than left to whoever happens to read dependencies.md that day:
//
//   - the set of namespaces caught in a dependency cycle may not GROW;
//   - the count of regard thresholds written as a bare number may only go DOWN.
//
// Both are baselines of a mess being worked off, not standards being met. Each is what the code measured on the day
// the ratchet was fitted, and the only edit either is allowed is a smaller one, with the commit saying what was
// untangled. That is the difference between a test that protects progress and a test that forbids it.

#region

using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class RatchetTests
   {
      /// <summary>
      ///   Every namespace inside a cycle on 21/09/2026: 37 in the mod, 7 in the SDK, 2 in Logic. Nothing may join
      ///   them. Held as a SET rather than a count on purpose: splitting one namespace in two would raise a count
      ///   innocently, while a genuinely new entanglement always brings a name that was not here before.
      /// </summary>
      private static readonly HashSet<string> TangledOn21September = new(StringComparer.Ordinal) {
         "CalradiaRemembers.Appreciations", "CalradiaRemembers.Assemblies", "CalradiaRemembers.Bastards",
         "CalradiaRemembers.Battles", "CalradiaRemembers.Behaviors", "CalradiaRemembers.Chat",
         "CalradiaRemembers.Commitments", "CalradiaRemembers.Commoners", "CalradiaRemembers.Companions",
         "CalradiaRemembers.Diagnostics", "CalradiaRemembers.Dialogue", "CalradiaRemembers.Duels",
         "CalradiaRemembers.Dynasty", "CalradiaRemembers.Escort", "CalradiaRemembers.GameState",
         "CalradiaRemembers.Grudges", "CalradiaRemembers.Interception", "CalradiaRemembers.Items",
         "CalradiaRemembers.Jealousy", "CalradiaRemembers.Letters", "CalradiaRemembers.Lore",
         "CalradiaRemembers.Mapping", "CalradiaRemembers.Marriage", "CalradiaRemembers.Memory",
         "CalradiaRemembers.Missions", "CalradiaRemembers.Nemesis", "CalradiaRemembers.Quests",
         "CalradiaRemembers.RealmTidings", "CalradiaRemembers.Retainer", "CalradiaRemembers.Scenarios",
         "CalradiaRemembers.Services", "CalradiaRemembers.Stance", "CalradiaRemembers.Tournaments",
         "CalradiaRemembers.Verbs", "CalradiaRemembers.Witnesses", "CalradiaRemembers.WorldAlmanac",
         "CalradiaRemembers.WorldEvents",
         "NpcMemoryService.Core.Actions", "NpcMemoryService.Core.LlmClient",
         "NpcMemoryService.Core.LlmClient.OpenRouter", "NpcMemoryService.Core.Models",
         "NpcMemoryService.Core.Parsing", "NpcMemoryService.Core.Prompts", "NpcMemoryService.Core.Services",
         "CalradiaRemembers.Logic", "CalradiaRemembers.Logic.Assemblies"
      };

      /// <summary>
      ///   Regard thresholds still written as a BARE NUMBER on 21/09/2026: 74 of the 91 the decision table lists, the
      ///   other 17 already pointing at a named rung on <c>Regard</c> or <c>VanillaRelation</c>. A bare number is the
      ///   duplication itself: the same rung declared again under a local name, free to drift from every other copy.
      /// </summary>
      private const int BareNumberThresholdsOn21September = 74;

      // A NEW ENTANGLEMENT IS THE ONE STRUCTURAL CHANGE THAT IS NEVER AN IMPROVEMENT. Inside a cycle nothing can be
      // understood, tested or moved alone, and 46 namespaces are in one already.
      [Test]
      public void GIVEN_the_namespaces_caught_in_a_cycle_WHEN_compared_with_the_baseline_THEN_no_new_one_has_joined()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         List<string> joined = Tangled().Where(n => !TangledOn21September.Contains(n))
                                        .OrderBy(n => n, StringComparer.Ordinal)
                                        .ToList();

         joined.Should().BeEmpty("a namespace that was free of cycles has been drawn into one: "
                               + string.Join(", ", joined)
                               + ". Read docs/maps/dependencies.md to see which group, and break the edge rather than widening this "
                               + "baseline. If it truly had to happen, add the name here and say why in the commit.");
      }

      // AND THE BASELINE MUST NOT QUIETLY BECOME A TARGET: when a namespace is untangled, this says so, so the list
      // shrinks with the work instead of drifting into decoration.
      [Test]
      public void GIVEN_a_namespace_that_has_been_untangled_WHEN_the_baseline_still_lists_it_THEN_the_list_is_out_of_date()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         HashSet<string> tangled = Tangled();
         List<string> freed = TangledOn21September.Where(n => !tangled.Contains(n))
                                                  .OrderBy(n => n, StringComparer.Ordinal)
                                                  .ToList();

         freed.Should().BeEmpty("these namespaces are in no cycle any more, which is the work paying off: "
                              + string.Join(", ", freed)
                              + ". Remove them from the baseline in the same commit, so it keeps meaning what it says.");
      }

      // THE MEASURE OF THE PRIMITIVE-OBSESSION WORK, ratcheted so it cannot drift back up while nobody is reading the
      // decision table's footer.
      [Test]
      public void GIVEN_the_thresholds_written_as_a_bare_number_WHEN_counted_THEN_there_are_no_more_than_before()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int bare = BareNumberThresholds();

         bare.Should().BeLessThanOrEqualTo(BareNumberThresholdsOn21September,
            $"a regard threshold has been declared as a bare number ({bare} against a baseline of "
          + $"{BareNumberThresholdsOn21September}). Point it at a rung on Regard or VanillaRelation instead, so the two ledgers "
          + "keep one definition each; if it is genuinely a new concept, raise the baseline here and say why. "
          + "docs/maps/decisions-regard.md lists them all.");

         if (bare < BareNumberThresholdsOn21September)
            Assert.Warn($"{BareNumberThresholdsOn21September - bare} threshold(s) have moved onto a named rung since the baseline "
                      + $"was set. Lower it to {bare} in this commit so the ratchet keeps holding.");
      }

      #region private

      /// <summary>
      ///   Regard compared INLINE against a bare number on 26/09/2026: 33 (20 through a ledger member, 13 through a value
      ///   named for regard); 32 the same day, once the trial's "below zero" pointed at the new Regard.NeutralTier;
      ///   28 once CompanionRapport.Band took a VanillaRelation and its four rungs were named; 27 once
      ///   ExternalMissionRules' reward stopped being called a "relation". The silent half of the bare-number measure above (Gabriel, 26/09/2026: a map that is
      ///   wrong without anyone noticing): that one counts CONSTANTS by their name, so a threshold written in place, with
      ///   no constant at all, was invisible to it. 0 later that day: every one points at a rung (six new ones, no value
      ///   moved), a quest reward's amount stopped being called a "relation", and a sign printed for display stopped
      ///   counting as a decision. At zero this is no longer a ratchet but a wall: the next inline threshold fails.
      /// </summary>
      private const int InlineRegardComparisonsOn26September = 0;

      // THE SILENT HALF, ratcheted the same way: a rung compared in place is the same duplication as a bare constant,
      // only harder to see.
      [Test]
      public void GIVEN_regard_compared_inline_against_a_bare_number_WHEN_counted_THEN_there_are_no_more_than_before()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int inline = RegardDecisionsMap.InlineRegardComparisons().Count();

         inline.Should().BeLessThanOrEqualTo(InlineRegardComparisonsOn26September,
            $"a regard value is compared in place with a bare number ({inline} against a baseline of "
          + $"{InlineRegardComparisonsOn26September}). Point it at a rung on Regard or VanillaRelation instead; "
          + "docs/maps/decisions-regard.md lists every one under \"Regard compared inline\".");

         if (inline < InlineRegardComparisonsOn26September)
            Assert.Warn($"{InlineRegardComparisonsOn26September - inline} inline comparison(s) moved onto a named rung: lower the baseline to {inline}.");
      }

      // A SAVE KEY THE MAP ONLY GUESSES AT (27/09/2026). The save-format map is a contract with every campaign in
      // progress, and 20 of its keys were listed only because their constant was NAMED *SaveKey*: nothing showed them
      // reaching the save. Following them through the helpers that make the call proved all 20. From here a key found
      // by its name alone means one of two things, both worth stopping for: a helper the map cannot follow (teach it),
      // or a constant whose data is no longer saved at all, which is a player's data silently dropped.
      [Test]
      public void GIVEN_the_save_keys_WHEN_mapped_THEN_every_one_is_shown_reaching_the_save()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         SaveFormatMap.KeysFoundOnlyByName(SourceTree.Production)
                      .Should().BeEmpty("the map can vouch for these keys only by their constant's name. Either they reach "
                                      + "SyncData through a helper the map does not follow (teach SaveFormatMap.SyncHelpers), or "
                                      + "nothing saves them any more, and a campaign in progress loses that data on its next load.");
      }

      /// <summary>Every namespace the dependency map puts in a cycle, read off the map the suite keeps current.</summary>
      private static HashSet<string> Tangled()
      {
         string cycles = DependencyMap.Generate(SourceTree.Production).Split("## Who knows the most")[0];

         return Regex.Matches(cycles, @"^- `([^`]+)`", RegexOptions.Multiline)
                     .Select(m => m.Groups[1].Value)
                     .ToHashSet(StringComparer.Ordinal);
      }

      /// <summary>
      ///   Regard-named thresholds whose value is a literal rather than a named rung, counted off the decision table's
      ///   own footer so this figure and the printed one can never disagree.
      /// </summary>
      private static int BareNumberThresholds()
      {
         string listed = RegardDecisionsMap.Generate().Split("The thresholds not yet represented")[^1];

         return Regex.Matches(listed, @"^- `[^`]+ = -?\d+`", RegexOptions.Multiline).Count;
      }

      #endregion
   }
}
