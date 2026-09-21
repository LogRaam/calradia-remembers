// Code written by Gabriel Mailhot, 21/09/2026.
// What these pin: that the generated views of the system are the views of THIS code, not of last week's.
//
// Gabriel, 21/09/2026: "ces cartes doivent etre regenerees a chaque changement de code. Normalement ce serait un
// trigger dans un pipeline." There is no pipeline that can build this mod, so the suite is the trigger: each test
// regenerates one map from the source and fails if the committed copy differs. He also named the limit himself: "il
// se peut qu'un changement dans le code n'affecte pas les seuils mais un autre mecanisme". So the first two maps are
// the generic net (every type, every edge between namespaces), and the specialised ones come after.
//
// A failure here is never a bug in the code under test. It means the code moved and the map did not: regenerate
// (see ApprovedMap) and commit the map with the change. Its diff is the architectural summary of that change.

#region

using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class MapsAreCurrentTests
   {
      // THE GENERIC NET, first half: a concept that already exists is findable BEFORE a second one is written.
      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_inventory_is_regenerated_THEN_it_matches_the_committed_map()
         => ApprovedMap.Verify(InventoryMap.Path, Generated(InventoryMap.Generate));

      // THE GENERIC NET, second half: a namespace that starts to know another shows up as one new line.
      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_dependencies_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(DependencyMap.Path, Generated(DependencyMap.Generate));

      // THE BEHAVIOUR INDEX. A change inside a method leaves no structural trace, but a behaviour that matters is held
      // by a test, so it shows here. This file existed already and sat ten days stale, for want of exactly this test.
      [Test]
      public void GIVEN_the_mod_suite_as_it_stands_WHEN_the_rules_are_regenerated_THEN_they_match_RULES_md()
         => ApprovedMap.Verify(RulesMap.ModPath, SourceTree.IsReachable ? RulesMap.GenerateForMod(SourceTree.ModTests) : "");

      [Test]
      public void GIVEN_the_sdk_suite_as_it_stands_WHEN_the_rules_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(RulesMap.SdkPath, SourceTree.IsReachable ? RulesMap.GenerateForSdk(SourceTree.SdkTests) : "");

      // THE LENSES, one per family of bug this project has actually had. Each header says which report it answers.
      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_constants_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(ConstantsMap.Path, Generated(ConstantsMap.Generate));

      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_event_listeners_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(EventListenersMap.Path, Generated(EventListenersMap.Generate));

      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_prompt_sections_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(PromptSectionsMap.Path, Generated(PromptSectionsMap.Generate));

      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_verbs_are_regenerated_THEN_they_match_the_committed_map()
         => ApprovedMap.Verify(VerbsMap.Path, Generated(VerbsMap.Generate));

      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_shared_state_is_regenerated_THEN_it_matches_the_committed_map()
         => ApprovedMap.Verify(SharedStateMap.Path, Generated(SharedStateMap.Generate));

      // THE DECISION TABLE. Not a list of declarations but a sweep of the real rules across the whole scale, so a
      // changed cell is a changed behaviour. It is the view Gabriel asked for (one page, two minutes) and the safety
      // net for giving regard a type: see ROADMAP.md.
      [Test]
      public void GIVEN_the_rules_as_they_stand_WHEN_the_regard_scale_is_swept_THEN_every_decision_matches_the_committed_table()
         => ApprovedMap.Verify(RegardDecisionsMap.Path, SourceTree.IsReachable ? RegardDecisionsMap.Generate() : "");

      #region the two contracts

      // These two are not views of the code, they are promises made to people outside it: every player with a
      // campaign in progress, and every modder with a bridge already shipped.
      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_save_format_is_regenerated_THEN_it_matches_the_committed_map()
      {
         // The guard runs HERE as well as in its own test below, because in update mode this test rewrites the
         // committed map, and no runner promises which of the two goes first. A vanished key must be refused before
         // the evidence of it is overwritten.
         RefuseAVanishedSaveKey();
         ApprovedMap.Verify(SaveFormatMap.Path, Generated(SaveFormatMap.Generate));
      }

      [Test]
      public void GIVEN_the_source_as_it_stands_WHEN_the_modder_api_is_regenerated_THEN_it_matches_the_committed_map()
         => ApprovedMap.Verify(ModderApiMap.Path, Generated(ModderApiMap.Generate));

      // THE ONE CHANGE A REGENERATION MAY NOT ABSORB. Every other map accepts whatever the code now says; that is
      // what regenerating means. A save key that vanishes is a player's letters, grudges or marriage silently never
      // read again on their next load, and nothing else in the suite would notice. Gabriel's hard rule is additive
      // keys only, so this fails in update mode too, until the removal is declared on purpose.
      [Test]
      public void GIVEN_a_save_key_in_the_committed_map_WHEN_the_code_no_longer_writes_it_THEN_the_suite_refuses_it()
      {
         if (!SourceTree.IsReachable) Assert.Ignore("The source trees are not reachable from this runner.");

         RefuseAVanishedSaveKey();
      }

      private static void RefuseAVanishedSaveKey()
      {
         if (!SourceTree.IsReachable) return;

         string committedPath = Path.Combine(SourceTree.ModRepo!, SaveFormatMap.Path.Replace('/', Path.DirectorySeparatorChar));
         if (!File.Exists(committedPath)) return; // nothing committed yet, nothing to protect
         if (Environment.GetEnvironmentVariable(AllowSaveKeyRemoval) == "1") return; // declared on purpose

         HashSet<string> before = SaveFormatMap.KeysListedIn(File.ReadAllText(committedPath));
         HashSet<string> now = SaveFormatMap.KeysListedIn(SaveFormatMap.Generate(SourceTree.Production));
         List<string> gone = before.Where(k => !now.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

         Assert.That(gone, Is.Empty,
            "These save keys were written by a shipped version and are no longer written: " + string.Join(", ", gone)
          + $". Every campaign in progress loses that data on its next load. If that is truly intended, set {AllowSaveKeyRemoval}=1 "
          + "for the regeneration and say why in the commit.");
      }

      private const string AllowSaveKeyRemoval = "CR_ALLOW_SAVE_KEY_REMOVAL";

      #endregion

      #region private

      /// <summary>Under a runner that cannot see the sources there is nothing to generate; Verify turns that into Ignore.</summary>
      private static string Generated(Func<IReadOnlyList<SourceFile>, string> generate)
         => SourceTree.IsReachable ? generate(SourceTree.Production) : "";

      #endregion
   }
}
