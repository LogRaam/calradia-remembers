// Code written by Gabriel Mailhot, 03/07/2026.
// The pure dispatch/composition engine behind CalradiaRemembers.Verbs.VerbRegistry, which registers BOTH the
// mod's own conversation verbs and any ExternalVerbAdapter wrapping a third-party mod's ExternalVerbContract
// in that very same list. So the crash-containment and lookup rules pinned here are what stands between one
// faulty external verb and every player's conversation window: the v1.21.0 regression (a verb's IsEligible
// touching a missing game API closed EVERY chat) is exactly the failure mode a hostile or outdated
// third-party verb could reproduce, by accident or otherwise. Case-insensitive name routing, the null-vs-
// nothing-composed sentinel, and explicit-order composition are the mod's own dispatch guarantees this
// class has to keep honest for external verbs too, not just its own.

#region

using System;
using CalradiaRemembers.Logic.Verbs;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   /// <summary>
   ///   Exercises <see cref="VerbRegistryCore{TContext,TAction,TResult}" />: the pure dispatch/composition
   ///   engine behind the mod's VerbRegistry (CalradiaRemembers.Verbs), which merely closes it over the
   ///   engine-coupled VerbContext/GameAction/ActionOutcome types and cannot itself be unit-tested (Hero is
   ///   an engine type). Plain strings stand in for TContext/TAction/TResult here, so the algorithm the mod
   ///   actually runs is protected by tests without any TaleWorlds dependency.
   /// </summary>
   [TestFixture]
   public sealed class VerbRegistryCoreTests
   {
      private sealed class FakeVerb : IVerb<string, string, string>
      {
         private readonly bool _eligible;
         private readonly string _teaching;
         private readonly string _executeResult;

         public FakeVerb(string name, bool eligible, string teaching, string executeResult = null!)
         {
            Name = name;
            _eligible = eligible;
            _teaching = teaching;
            _executeResult = executeResult;
         }

         public string Name { get; }
         public int ExecuteCallCount { get; private set; }

         public bool IsEligible(string ctx) => _eligible;

         public string TeachingText(string ctx) => _teaching;

         public string Execute(string action, string ctx)
         {
            ExecuteCallCount++;

            return _executeResult;
         }
      }

      // ── ComposeTeachings ─────────────────────────────────────────────────

      // The eligibility gate itself: a verb that says "not applicable right now" (an external mod's own
      // guard, e.g. facts.IsPrisoner) must never leak its teaching text into the prompt, or the LLM would be
      // taught an action the player can't actually take.
      [Test]
      public void GIVEN_one_eligible_and_one_ineligible_verb_WHEN_composing_teachings_THEN_only_the_eligible_one_is_included()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("eligible_verb", true, "TEACH_A\n"));
         registry.Register(new FakeVerb("ineligible_verb", false, "TEACH_B\n"));

         string result = registry.ComposeTeachings("ctx");

         result.Should().Be("TEACH_A\n");
      }

      // With no explicit order, teachings must concatenate in the order verbs were Register()ed, not some
      // other order (alphabetical, reversed): an external verb registered after CR's own built-ins should
      // append after them, never reshuffle the whole teaching block a modder didn't ask to touch.
      [Test]
      public void GIVEN_multiple_eligible_verbs_WHEN_composing_without_an_explicit_order_THEN_they_concatenate_in_registration_order()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("first", true, "A\n"));
         registry.Register(new FakeVerb("second", true, "B\n"));

         string result = registry.ComposeTeachings("ctx");

         result.Should().Be("A\nB\n");
      }

      // Null, not empty string, is the "nothing to teach" sentinel: the prompt builder relies on that
      // distinction to skip the section entirely rather than gluing in a blank block.
      [Test]
      public void GIVEN_no_eligible_verbs_WHEN_composing_teachings_THEN_the_result_is_null()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("ineligible_verb", false, "TEACH\n"));

         registry.ComposeTeachings("ctx").Should().BeNull();
      }

      // A verb (internal or external) that returns "" rather than null for its teaching must not pad the
      // prompt with a blank line, and must not by itself flip the null "nothing composed" sentinel above.
      [Test]
      public void GIVEN_an_eligible_verb_whose_teaching_text_is_empty_WHEN_composing_THEN_nothing_is_appended_for_it()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("silent_verb", true, ""));
         registry.Register(new FakeVerb("vocal_verb", true, "TEACH\n"));

         registry.ComposeTeachings("ctx").Should().Be("TEACH\n");
      }

      // Explicit order is how EncounterContextBuilder threads a fixed teaching sequence around an untouched
      // block (pledge_against here). An unrelated registered verb sitting between the named ones in the
      // registry must not sneak into the composed text just because it happens to be registered.
      [Test]
      public void GIVEN_an_explicit_order_naming_a_subset_WHEN_composing_teachings_THEN_only_those_names_are_composed_in_that_order()
      {
         //Arrange: mirrors EncounterContextBuilder splitting the court verbs around the untouched
         //pledge_against block, so an unrelated verb registered between them is skipped here.
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "GIVE\n"));
         registry.Register(new FakeVerb("pledge_against", true, "PLEDGE\n"));
         registry.Register(new FakeVerb("release_prisoner", true, "RELEASE\n"));

         string result = registry.ComposeTeachings("ctx", new[] {"release_prisoner", "give_influence"});

         result.Should().Be("RELEASE\nGIVE\n");
      }

      // A caller's ordered list can name a verb that isn't registered (an external mod's contract was
      // removed, or a name was typo'd): that must be a silent skip, never a lookup crash that takes the rest
      // of the prompt build down with it.
      [Test]
      public void GIVEN_an_explicit_order_naming_an_unregistered_verb_WHEN_composing_teachings_THEN_that_name_is_skipped()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("known_verb", true, "KNOWN\n"));

         string result = registry.ComposeTeachings("ctx", new[] {"unknown_verb", "known_verb"});

         result.Should().Be("KNOWN\n");
      }

      private sealed class ThrowingVerb : IVerb<string, string, string>
      {
         public ThrowingVerb(string name) => Name = name;
         public string Name { get; }
         public bool IsEligible(string ctx) => throw new MissingMethodException("simulates a game API absent on this build");
         public string TeachingText(string ctx) => "SHOULD_NOT_APPEAR\n";
         public string Execute(string action, string ctx) => null!;
      }

      // The crash-containment guarantee itself, and the one an external, unvetted verb could trip hardest:
      // a third-party mod's IsEligible is arbitrary code CR does not control, so one throwing verb must
      // degrade to "unavailable" for that turn while its neighbours still teach normally.
      [Test]
      public void GIVEN_a_verb_whose_eligibility_throws_WHEN_composing_teachings_THEN_it_is_skipped_and_the_others_still_compose()
      {
         // The v1.21.0 crash: a verb's IsEligible touched a game API missing on the player's build and the
         // exception closed EVERY conversation. One faulty verb must degrade to "unavailable", never a crash.
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("healthy_before", true, "BEFORE\n"));
         registry.Register(new ThrowingVerb("faulty_verb"));
         registry.Register(new FakeVerb("healthy_after", true, "AFTER\n"));

         string result = registry.ComposeTeachings("ctx");

         result.Should().Be("BEFORE\nAFTER\n");
      }

      // Swallowing the exception above must not mean losing it: an external mod's broken verb needs to
      // surface SOMEWHERE (a compat trace), or Gabriel has no way to ever learn a third-party contract is
      // faulting on a player's build.
      [Test]
      public void GIVEN_a_faulting_verb_and_a_fault_sink_WHEN_composing_teachings_THEN_the_exception_is_reported_to_the_sink()
      {
         // The host routes this to an always-on compat trace, so a missing game API never degrades silently.
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new ThrowingVerb("faulty_verb"));
         Exception? reported = null;

         registry.ComposeTeachings("ctx", ex => reported = ex);

         reported.Should().BeOfType<MissingMethodException>();
      }

      // ── Execute ──────────────────────────────────────────────────────────

      // The routing itself: BannerlordGameStateBridge.ExecuteAction dispatches an LLM-emitted [ACTION] to a
      // verb purely by name, so the wrong verb firing (or firing twice) here would mean the wrong side
      // effect lands, or a reward/consequence gets granted more than once.
      [Test]
      public void GIVEN_an_action_name_matching_a_registered_verb_WHEN_executing_THEN_that_verbs_Execute_runs_and_its_result_is_returned()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         var verb = new FakeVerb("give_influence", true, "TEACH", "OUTCOME");
         registry.Register(verb);

         string result = registry.Execute("give_influence", "the-action", "ctx");

         result.Should().Be("OUTCOME");
         verb.ExecuteCallCount.Should().Be(1);
      }

      // An unknown action name (a hallucinated action, or one whose external verb was since unregistered)
      // must resolve to a harmless null the bridge can no-op on, never an exception that aborts handling the
      // player's turn.
      [Test]
      public void GIVEN_an_action_name_with_no_matching_verb_WHEN_executing_THEN_the_result_is_null()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH", "OUTCOME"));

         registry.Execute("unknown_verb", "the-action", "ctx").Should().BeNull();
      }

      // The LLM's emitted [ACTION] "type:" text is free-form model output, not a guaranteed-exact literal:
      // routing must tolerate a case mismatch or a valid action silently fails to execute for no reason the
      // player or Gabriel could diagnose.
      [Test]
      public void GIVEN_an_action_name_differing_only_by_case_WHEN_executing_THEN_it_still_routes_to_the_matching_verb()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH", "OUTCOME"));

         registry.Execute("GIVE_INFLUENCE", "the-action", "ctx").Should().Be("OUTCOME");
      }

      // ── Unregister ───────────────────────────────────────────────────────

      // The coming public "register an external action" API needs a clean way to undo a registration (API
      // hygiene, and an in-game self-test that must clean up after itself): once unregistered, the verb must be
      // gone from both dispatch (Execute) and teaching (ComposeTeachings), not merely hidden from one of them.
      [Test]
      public void GIVEN_a_registered_verb_WHEN_unregistered_by_name_THEN_it_no_longer_executes_or_teaches()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         var verb = new FakeVerb("bc_bridge_summon_caravan", true, "TEACH\n", "OUTCOME");
         registry.Register(verb);

         bool result = registry.Unregister("bc_bridge_summon_caravan");

         result.Should().BeTrue();
         registry.Execute("bc_bridge_summon_caravan", "the-action", "ctx").Should().BeNull();
         registry.ComposeTeachings("ctx").Should().BeNull();
      }

      // The LLM's/mod's own "type:"/name text is not guaranteed to be an exact-case literal, so Unregister must
      // tolerate the same case mismatch Execute and FindByName already tolerate, or a legitimate teardown call
      // silently fails to remove anything.
      [Test]
      public void GIVEN_a_registered_verb_WHEN_unregistered_with_a_differently_cased_name_THEN_it_still_removes_it()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH\n", "OUTCOME"));

         bool result = registry.Unregister("GIVE_INFLUENCE");

         result.Should().BeTrue();
         registry.Execute("give_influence", "the-action", "ctx").Should().BeNull();
      }

      // An unknown name, or a null/empty one, must be a safe no-op, never an exception that could take down a
      // mod's own teardown path (an in-game self-test cleaning up after itself).
      [Test]
      public void GIVEN_an_unknown_or_blank_name_WHEN_Unregister_is_called_THEN_it_returns_false_without_throwing()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH\n", "OUTCOME"));

         registry.Unregister("no_such_verb").Should().BeFalse();
         registry.Unregister(null).Should().BeFalse();
         registry.Unregister("").Should().BeFalse();
         registry.Execute("give_influence", "the-action", "ctx").Should().Be("OUTCOME");
      }
   }
}
