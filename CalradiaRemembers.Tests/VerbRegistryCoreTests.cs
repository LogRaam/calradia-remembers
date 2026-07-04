// Code written by Gabriel Mailhot, 03/07/2026.

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

         public FakeVerb(string name, bool eligible, string teaching, string executeResult = null)
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

      [Test]
      public void GIVEN_one_eligible_and_one_ineligible_verb_WHEN_composing_teachings_THEN_only_the_eligible_one_is_included()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("eligible_verb", true, "TEACH_A\n"));
         registry.Register(new FakeVerb("ineligible_verb", false, "TEACH_B\n"));

         string result = registry.ComposeTeachings("ctx");

         result.Should().Be("TEACH_A\n");
      }

      [Test]
      public void GIVEN_multiple_eligible_verbs_WHEN_composing_without_an_explicit_order_THEN_they_concatenate_in_registration_order()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("first", true, "A\n"));
         registry.Register(new FakeVerb("second", true, "B\n"));

         string result = registry.ComposeTeachings("ctx");

         result.Should().Be("A\nB\n");
      }

      [Test]
      public void GIVEN_no_eligible_verbs_WHEN_composing_teachings_THEN_the_result_is_null()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("ineligible_verb", false, "TEACH\n"));

         registry.ComposeTeachings("ctx").Should().BeNull();
      }

      [Test]
      public void GIVEN_an_eligible_verb_whose_teaching_text_is_empty_WHEN_composing_THEN_nothing_is_appended_for_it()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("silent_verb", true, ""));
         registry.Register(new FakeVerb("vocal_verb", true, "TEACH\n"));

         registry.ComposeTeachings("ctx").Should().Be("TEACH\n");
      }

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

      [Test]
      public void GIVEN_an_action_name_with_no_matching_verb_WHEN_executing_THEN_the_result_is_null()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH", "OUTCOME"));

         registry.Execute("unknown_verb", "the-action", "ctx").Should().BeNull();
      }

      [Test]
      public void GIVEN_an_action_name_differing_only_by_case_WHEN_executing_THEN_it_still_routes_to_the_matching_verb()
      {
         var registry = new VerbRegistryCore<string, string, string>();
         registry.Register(new FakeVerb("give_influence", true, "TEACH", "OUTCOME"));

         registry.Execute("GIVE_INFLUENCE", "the-action", "ctx").Should().Be("OUTCOME");
      }
   }
}
