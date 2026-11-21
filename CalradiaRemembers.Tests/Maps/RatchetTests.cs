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
      ///   Every namespace inside a cycle on 21/09/2026: 37 in the mod, 7 in the SDK, 2 in Logic; on 01/10/2026 the
      ///   corrected map (qualified names read too) showed 13 more that had always been there, and one new one. Nothing may join
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
         "CalradiaRemembers.RealmTidings", "CalradiaRemembers.Scenarios",
         "CalradiaRemembers.Services", "CalradiaRemembers.Stance", "CalradiaRemembers.Tournaments",
         "CalradiaRemembers.Verbs", "CalradiaRemembers.Witnesses", "CalradiaRemembers.WorldAlmanac",
         "CalradiaRemembers.WorldEvents",
         "NpcMemoryService.Core.Actions", "NpcMemoryService.Core.LlmClient",
         "NpcMemoryService.Core.LlmClient.OpenRouter", "NpcMemoryService.Core.Models",
         "NpcMemoryService.Core.Parsing", "NpcMemoryService.Core.Prompts", "NpcMemoryService.Core.Services",
         "CalradiaRemembers.Logic", "CalradiaRemembers.Logic.Assemblies",

         // INVISIBLE UNTIL 01/10/2026, NOT NEW. The dependency map read only `using` directives, so a namespace called by
         // QUALIFIED name (CrSettings, Violence.X) never showed its edge. Gabriel asked for the map review that
         // found it; the corrected map, run on the exact 2.6.7 sources (mod dc99b73, SDK 9977098), already showed every
         // name below in a cycle. Added as they were found, so the wall is honest from here on.
         "CalradiaRemembers", "CalradiaRemembers.Arrests", "CalradiaRemembers.Chronicle", "CalradiaRemembers.Map",
         "CalradiaRemembers.OpenPartnerLover", "CalradiaRemembers.Rescue",
         "CalradiaRemembers.Styles", "CalradiaRemembers.Violence", "CalradiaRemembers.WarCouncil",
         "NpcMemoryService.Core.Knowledge", "NpcMemoryService.Core.Memory", "NpcMemoryService.Core.Standing",

         // NEW on 30/09/2026, accepted (Gabriel, 01/10/2026): the self-esteem, clan warm start and perceived image readers.
         // They read the live game like Mapping (rank, presence, the day's mood) and the profile store (Behaviors), and
         // the chat, Mapping, Duels and Violence call them, so they sit in the mod's core group by nature. Folding them
         // into Mapping would remove the name without untangling anything.
         "CalradiaRemembers.Standing",

         // NEW on 15/10/2026, by Claude, accepted by Gabriel the same day: the vassal war duty. A campaign behavior that writes the liege's
         // letters (Letters), leaves him memories (Behaviors, Storage), releases a governor before a confiscation
         // (GameState) and reads presence (Mapping), and the console and cr.test all call it: the same shape as Arrests,
         // already in the group. Breaking it would take a hook per effect; Aims, which needed one edge, was kept out.
         "CalradiaRemembers.WarDuty",

         // NEW on 01/11/2026, by Claude, accepted by Gabriel the same day: the calls for help. The same shape as WarDuty: a
         // campaign behavior that sends letters (Letters), leaves memories and grudges (Behaviors, Grudges), opens quests
         // (Quests) and reads presence (Mapping), and the console, cr.test all and the submodule call it. The two edges
         // that could go did: Letters reaches it through ILetterPleaDesk, and it no longer touches MissionBoard in the root.
         "CalradiaRemembers.HelpPleas",

         // INVISIBLE UNTIL 19/10/2026, NOT NEW. The map did not resolve a BARE name to an enclosing namespace: inside
         // CalradiaRemembers.Travel, `ModLog` is CalradiaRemembers' own, with no using and no qualifier. Found while
         // splitting Logic (Logic.Nemesis read as "uses: nothing"). Every name below calls the mod root's foundations
         // (ModLog, SafeRun, ModSettings, ModulePaths, StoryLog) while the root's ModCheats and submodule call them back.
         // The same day those foundations moved to CalradiaRemembers.Foundation, which uses none of its callers, and
         // Appearance, GUI, Portraits, Retainer, Storage, Travel, UI, UI.Spike and Video left again. Aims stays: its
         // behavior raises the public CrConversation events, and the submodule registers it.
         "CalradiaRemembers.Aims"
      };

      /// <summary>
      ///   Regard thresholds still written as a BARE NUMBER on 21/09/2026: 74 of the 91 the decision table lists, the
      ///   other 17 already pointing at a named rung on <c>Regard</c> or <c>VanillaRelation</c>. A bare number is the
      ///   duplication itself: the same rung declared again under a local name, free to drift from every other copy.
      /// </summary>
      private const int BareNumberThresholdsOn21September = 74;

      /// <summary>
      ///   Pairs of namespaces that use each other DIRECTLY on 19/10/2026, the day the dependency map started listing them
      ///   (after reading how Graphify ranks its graph's edges). Finer than the cycle set above: a new cross-edge between
      ///   two namespaces already in the group never adds a name there, but it always adds a pair here. 99 when fitted;
      ///   84 the same day, once SafeRun left Diagnostics for the root, 47 imports of Diagnostics that nothing used were
      ///   removed, and RelationWarmth took its own project's namespace (Retainer left the cycle with them).
      ///   137 since the same day: the map started resolving a bare name to an enclosing namespace, which showed 53 pairs
      ///   that had always been there, every one between the mod root (ModLog, SafeRun, ModSettings beside ModCheats and
      ///   the submodule) and a namespace that calls ModLog without a using. Not new edges: a measurement fixed.
      ///   92 the same day, once ModLog, SafeRun, StoryLog, ModSettings, CrSettings, LocalContextState and
      ///   MainThreadDispatcher moved to CalradiaRemembers.Foundation (the MCM Test buttons now reach the tester
      ///   through a hook) and 102 imports nothing used were removed (Tools/Remove-DeadUsings.py).
      /// </summary>
      private const int MutualPairsOn19October = 92;

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
                               + string.Join("; ", joined.Select(n => $"{n}, through {string.Join(", ", DependencyMap.ShortestCycleThrough(SourceTree.Production, n))}"))
                               + ". Cutting any one hop of a cycle breaks it; docs/maps/dependencies.md shows the whole group. Break an edge rather than widening this "
                               + "baseline. If it truly had to happen, add the name here and say why in the commit.");
      }

      // Gabriel (20/11/2026, after the memory pictures: the count said 95 against 92 and the new pairs had to be found by
      // regenerating the map by hand): the failure names each pair the committed map does not list yet, with the files that
      // make each direction, so the edge to break is in the message.
      private static string NewPairsSinceTheCommittedMap(IReadOnlyList<DependencyMap.MutualPair> pairs)
      {
         string committed = System.IO.Path.Combine(SourceTree.ModRepo!, "docs", "maps", "dependencies.md");
         string text = System.IO.File.Exists(committed) ? System.IO.File.ReadAllText(committed) : "";
         List<DependencyMap.MutualPair> added = pairs.Where(p => !text.Contains($"#### `{p.First}` and `{p.Second}`")).ToList();
         if (added.Count == 0) return "(Every pair is already in the committed map: the count grew elsewhere.)";

         return "New: " + string.Join("; ", added.Select(p =>
            $"{p.First} and {p.Second} ({p.First} -> {p.Second} in {string.Join(", ", p.FirstUsesSecondIn)}; "
          + $"{p.Second} -> {p.First} in {string.Join(", ", p.SecondUsesFirstIn)})")) + ".";
      }

      // A NEW CROSS-EDGE INSIDE THE GROUP. The cycle set above cannot see one: both names are already in it. A pair that
      // now uses each other is a cut that has become harder to make.
      [Test]
      public void GIVEN_the_pairs_of_namespaces_that_use_each_other_WHEN_counted_THEN_there_are_no_more_than_before()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         IReadOnlyList<DependencyMap.MutualPair> pairs = DependencyMap.MutualPairs(SourceTree.Production);

         pairs.Count.Should().BeLessThanOrEqualTo(MutualPairsOn19October,
            "two namespaces have started to use each other directly. " + NewPairsSinceTheCommittedMap(pairs) + " "
          + "docs/maps/dependencies.md, \"Pairs that use each other "
          + "directly\", lists every pair with the files that create it: break the new edge rather than raising the baseline. "
          + "If it truly had to happen, raise it here and say why in the commit.");

         if (pairs.Count < MutualPairsOn19October)
            Assert.Warn($"{MutualPairsOn19October - pairs.Count} pair(s) untangled since the baseline: lower it to {pairs.Count}.");
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

      /// <summary>
      ///   Facts of the game compared with a bare number in the glue on 14/11/2026, the day the map started (Gabriel: maps that
      ///   put Tell Don't Ask into practice). docs/maps/glue-decisions.md lists them. 16 when fitted; 1 the same day, once the
      ///   seat and camp bands moved to Logic.Conditions.ConditionBands (the loyalty 50 written twice now has one home), the
      ///   captor's influence mark to PlayerImportancePolicy, the coming-of-age fallback to ComingOfAge, station 2 to
      ///   Stature.IsGreatHouseOrRuler and the flavour witness roll to WitnessPresencePolicy. The one left is the almanac's
      ///   podium (the top three shown as cards): presentation, not a rule of the game.
      /// </summary>
      private const int GlueDecisionsOn14November = 1;

      // TELL DON'T ASK, IN THIS PROJECT'S TERMS: a decision belongs in a pure policy where a test holds it and a second copy
      // shows. A fact compared with a number in the glue is the decision made again, out of reach, free to drift (the
      // affection bar's literal 50 in three places).
      [Test]
      public void GIVEN_facts_of_the_game_compared_with_a_bare_number_in_the_glue_WHEN_counted_THEN_there_are_no_more_than_the_baseline()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int count = GlueDecisionsMap.Count(SourceTree.Production);

         count.Should().BeLessThanOrEqualTo(GlueDecisionsOn14November,
            $"the glue has started deciding for itself ({count} against a baseline of {GlueDecisionsOn14November}): a fact of the game "
          + "compared with a number written in place. Put the decision in a Logic policy (a facts object, a named rung) and call it; "
          + "docs/maps/glue-decisions.md lists every one. If it truly belongs in the glue, raise the baseline here and say why.");

         if (count < GlueDecisionsOn14November)
            Assert.Warn($"{GlueDecisionsOn14November - count} decision(s) moved out of the glue since the baseline: lower it to {count}.");
      }

      /// <summary>
      ///   Public members of the Logic layer with three or more same-typed parameters in a row on 14/11/2026, the day the suite
      ///   started holding them. Tools/Find-SwapRisk.ps1 had taken them to zero on 10/09/2026, but nothing ran it; 33 had come
      ///   back by the time this test did (the script, run the same day, lists the same 33), one of them QuestRepeatPolicy.KeyOf,
      ///   widened that very day. 32 the same day, once KeyOf took a QuestTarget; 30 once the guard stopped counting public
      ///   members of private types (Ppmd7's range coder); 0 once every other one took a facts object, its positional form
      ///   kept private behind a one-line delegation (Gabriel: "oui"). At zero this is a wall: the next one fails.
      /// </summary>
      private const int SwapRiskRunsOn14November = 0;

      // A SWAP THE COMPILER CANNOT SEE: three same-typed parameters in a row in a pure rule is a call that will one day pass two
      // of them the wrong way round, compile, and give a plausible wrong answer (CanEscort cost seven rewritten assertions).
      [Test]
      public void GIVEN_the_pure_rules_WHEN_their_signatures_are_read_THEN_no_more_take_three_same_typed_parameters_in_a_row_than_before()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         IReadOnlyList<string> findings = SwapRiskGuard.Findings(SourceTree.Production);

         findings.Count.Should().BeLessThanOrEqualTo(SwapRiskRunsOn14November,
            "a public member of CalradiaRemembers.Logic now takes three or more parameters of one type in a row. Ask for the type "
          + "that already exists, or give it a facts object with { get; init; } (CLAUDE.md, \"The structural cure\"). All of them: "
          + string.Join("; ", findings));

         if (findings.Count < SwapRiskRunsOn14November)
            Assert.Warn($"{SwapRiskRunsOn14November - findings.Count} signature(s) given a facts object since the baseline: lower it to {findings.Count}.");
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

      // EVERY INITIATIVE ASKS THE ONE GATE (30/09/2026). Nexusrrr's baby son asked to discuss warfare because the fief
      // audience checked no age; an inventory found a dozen initiative paths and a dozen filters, two of them checking
      // age. Two walls keep the gate's reach from eroding. First: a kind of initiative declared and never asked is a
      // path someone meant to gate and did not.
      [Test]
      public void GIVEN_the_kinds_of_initiative_WHEN_the_game_code_is_read_THEN_every_kind_is_asked_somewhere()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         HashSet<string> asked = ActionsMap.KindSites(SourceTree.Production).Select(k => k.Kind).ToHashSet(StringComparer.Ordinal);
         List<string> neverAsked = Enum.GetNames(typeof(CalradiaRemembers.Logic.ActionKind)).Where(k => !asked.Contains(k)).ToList();

         neverAsked.Should().BeEmpty("these kinds of initiative are declared but no game code asks the gate for them: "
                                   + string.Join(", ", neverAsked) + ". Ask ActionGate where that initiative happens, or remove the kind.");
      }

      // Second: a letter written straight into a character's post, outside the letter pipeline, is an initiative the
      // pipeline's own gate never saw (the stance consequence did exactly that, from anyone alive and free). Every such
      // file must ask the gate itself.
      [Test]
      public void GIVEN_letters_written_straight_into_the_post_WHEN_read_THEN_every_such_file_asks_the_gate()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         List<string> unguarded = ActionsMap.DirectLetterFiles(SourceTree.Production).Where(f => !f.AsksGate).Select(f => f.File).ToList();

         unguarded.Should().BeEmpty("these files add a letter to a character's SentLetters without asking ActionGate, so a "
                                  + "child or a captive could write first: " + string.Join(", ", unguarded));
      }

      // IsAlive IS NOT "IN THE WORLD" (30/09/2026). Maifailun's dead father sat at a council; the inventory that followed
      // found the engine counts a DISABLED hero as alive, and a dozen paths checking IsAlive alone, among them the
      // chokepoint every letter the player sends goes through. The gated paths now ask ActionGate or HeroPresence; the
      // rest is a backlog, much of it bookkeeping where death alone is the question. A new bare read is how the next
      // one slips in: ask the gate for an act, HeroPresence for "is in the world", or raise the baseline and say why.
      private const int BareIsAliveReadsOn30September = 313;

      [Test]
      public void GIVEN_reads_of_IsAlive_outside_the_two_doors_WHEN_counted_THEN_there_are_no_more_than_the_baseline()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int bare = ActionsMap.BareIsAliveReads(SourceTree.Production).Sum(b => b.Count);

         bare.Should().BeLessThanOrEqualTo(BareIsAliveReadsOn30September,
            $"a hero's IsAlive is read on its own ({bare} against a baseline of {BareIsAliveReadsOn30September}), and a "
          + "disabled hero reads alive. Ask Mapping.ActionGate for an act, Mapping.HeroPresence.IsInTheWorld for presence; "
          + "if death alone is truly the question, raise the baseline here and say why. docs/maps/actions.md lists them.");

         if (bare < BareIsAliveReadsOn30September)
            Assert.Warn($"{BareIsAliveReadsOn30September - bare} bare IsAlive read(s) moved onto a door: lower the baseline to {bare}.");
      }

      // ONE DOOR FOR AN EXCHANGE (Gabriel, 01/11/2026: "centraliser tout ce qui est transactions"). Gold moved in 38 places and
      // goods in many more, each with its own rules, which is how the same bug reached tashmetu, 3chmidt and RaveN017Mod. The
      // reworked roads go through GameState/ExchangeService; the rest is a backlog being moved over in steps. A new direct
      // move is how the next one starts: pay through the door, or raise the baseline and say why.
      private const int DirectGoldMovesOn1November = 35;
      private const int DirectGoodsMovesOn1November = 9;

      private static int DirectMoves(string pattern)
         => SourceTree.Production
                      .Where(f => !f.RelativePath.EndsWith("ExchangeService.cs", StringComparison.Ordinal)
                                  && !("/" + f.RelativePath).Contains("/Diagnostics/")
                                  && !System.IO.Path.GetFileName(f.RelativePath).StartsWith("ModCheats", StringComparison.Ordinal))
                      .Sum(f => System.Text.RegularExpressions.Regex.Matches(f.Root.ToFullString(), pattern).Count);

      [Test]
      public void GIVEN_coin_moved_outside_the_exchange_door_WHEN_counted_THEN_there_are_no_more_than_the_baseline()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int direct = DirectMoves(@"GiveGoldAction\.Apply|ChangeHeroGold\(");

         direct.Should().BeLessThanOrEqualTo(DirectGoldMovesOn1November,
            "coin moves outside GameState/ExchangeService, with rules of its own and nothing written down: pay through the door.");
         if (direct < DirectGoldMovesOn1November) Assert.Warn($"{DirectGoldMovesOn1November - direct} move(s) came through the door: lower the baseline to {direct}.");
      }

      [Test]
      public void GIVEN_goods_moved_outside_the_exchange_door_WHEN_counted_THEN_there_are_no_more_than_the_baseline()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int direct = DirectMoves(@"ItemRoster\.AddToCounts");

         direct.Should().BeLessThanOrEqualTo(DirectGoodsMovesOn1November,
            "goods move outside GameState/ExchangeService: an exchange with a character goes through the door. Loot and travel do not count as one, and stay in this baseline.");
         if (direct < DirectGoodsMovesOn1November) Assert.Warn($"{DirectGoodsMovesOn1November - direct} move(s) came through the door: lower the baseline to {direct}.");
      }

      // RELATIONS BETWEEN NPCS THROUGH ONE READER (04/11/2026). tashmetu (Nexus, 04/11/2026): the game's Hero.GetRelation
      // answers for the two clan leaders, and its starting draw is blind; some of CR asked one, some read the pair's own
      // slot, and both trusted the draw. Every relation between two NPCs now goes through Mapping/PairBondReader (read) and
      // Mapping/RelationLedgerBehavior (write). What is left is the player's own standing, which the game's relation does
      // carry, and pairs of one house; a new direct read or write is how the next disagreement starts.
      private const int DirectRelationTouchesOn4November = 68;

      [Test]
      public void GIVEN_relations_read_or_written_outside_the_pair_bond_reader_WHEN_counted_THEN_there_are_no_more_than_the_baseline()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         int direct = SourceTree.Production
                                .Where(f => !f.RelativePath.EndsWith("Mapping/PairBondReader.cs", StringComparison.Ordinal)
                                            && !f.RelativePath.EndsWith("Mapping/RelationLedgerBehavior.cs", StringComparison.Ordinal)
                                            && !("/" + f.RelativePath).Contains("/Diagnostics/")
                                            && !System.IO.Path.GetFileName(f.RelativePath).StartsWith("ModCheats", StringComparison.Ordinal))
                                .Sum(f => Regex.Matches(f.Root.ToFullString(), @"\.GetRelation\(|GetHeroRelation\(|ApplyRelationChangeBetweenHeroes\(|SetPersonalRelation\(").Count);

         direct.Should().BeLessThanOrEqualTo(DirectRelationTouchesOn4November,
            "a relation is read or written outside Mapping/PairBondReader and RelationLedgerBehavior: between two NPCs, go through them.");
         if (direct < DirectRelationTouchesOn4November) Assert.Warn($"{DirectRelationTouchesOn4November - direct} touch(es) moved onto the reader: lower the baseline to {direct}.");
      }

      // REGARD CHANGES IN ONE PLACE (29/09/2026). CrNpcMemory.RegardChanged tells other mods of every change of a
      // character's regard for the player, and it can only do that because every change goes through
      // ProfileMutator.ApplyReputationDelta. On the day the event was built, six places wrote the value directly
      // (three of them without even clamping it to -100..100); all were routed through. A new direct write would be a
      // change no listener hears, and a bridge that mirrors regard would silently drift out of step. A profile being
      // built (an object initializer) is not a change, and the console's own test commands are left their freedom.
      [Test]
      public void GIVEN_the_code_that_changes_regard_WHEN_read_THEN_every_change_goes_through_the_one_place_listeners_hear()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");

         List<string> direct = SourceTree.Production
                                         .Where(f => !f.RelativePath.EndsWith("ProfileMutator.cs", StringComparison.Ordinal)
                                                     && !("/" + f.RelativePath).Contains("/Diagnostics/")
                                                     && !System.IO.Path.GetFileName(f.RelativePath).StartsWith("ModCheats", StringComparison.Ordinal))
                                         .SelectMany(f => f.Root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax>()
                                                           .Where(a => a.Left is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax m
                                                                       && m.Name.Identifier.Text == "ReputationWithPlayer")
                                                           .Select(a => $"{f.RelativePath}: {a}"))
                                         .ToList();

         direct.Should().BeEmpty("regard is changed here without going through ProfileMutator.ApplyReputationDelta, so "
                               + "CrNpcMemory.RegardChanged never hears it: " + string.Join("; ", direct));
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
