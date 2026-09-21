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

      #region private

      /// <summary>Under a runner that cannot see the sources there is nothing to generate; Verify turns that into Ignore.</summary>
      private static string Generated(Func<IReadOnlyList<SourceFile>, string> generate)
         => SourceTree.IsReachable ? generate(SourceTree.Production) : "";

      #endregion
   }
}
