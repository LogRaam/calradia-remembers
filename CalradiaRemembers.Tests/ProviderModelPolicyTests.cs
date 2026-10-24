// Written from the report, not from the code.
//
// Nexus, 2026-09-11: "spent the last few hours pulling my hair out trying to set up an interpret model, you
// have the endpoints flippy floppied and criss crossed. the only way it works is if you reverse the
// endpoints, but then the models are just doing each others jobs."
//
// What a player is owed here has nothing to do with which field holds which string. It is this: pointing a
// role at my own server and forgetting to name a model must not send that server a model id it has never
// heard of. The prose role has always been owed it. The other two were not getting it.

#region

using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ProviderModelPolicyTests
    {
        private const string Shipped = "x-ai/grok-4.20"; // what every model field holds out of the box

        private static ProviderModelPolicy.ModelChoice Choice(string id, ProviderKind kind)
            => new() {ConfiguredId = id, Kind = kind, ShippedDefaultId = Shipped};

        // THE REPORT. He pointed the interpreter at his own machine and never edited the model field, because
        // nothing told him he had to. The mod then asked a local LM Studio for an OpenRouter id, which it
        // refuses, while prose kept working - and that difference is what read as "criss crossed".
        [Test]
        public void GIVEN_a_local_server_and_a_model_field_never_touched_WHEN_resolving_THEN_nothing_is_sent_rather_than_an_id_it_cannot_know()
        {
            ProviderModelPolicy.Resolve(Choice(Shipped, ProviderKind.OpenAiCompatible)).Should().BeEmpty();
            ProviderModelPolicy.Resolve(Choice(Shipped, ProviderKind.NanoGpt)).Should().BeEmpty();
        }

        // The same field, on the provider the default was written for, IS a real choice. Treating it as unset
        // everywhere would break every ordinary OpenRouter player to fix one local one.
        [Test]
        public void GIVEN_openrouter_and_the_shipped_default_WHEN_resolving_THEN_it_is_a_real_choice_and_is_sent()
        {
            ProviderModelPolicy.Resolve(Choice(Shipped, ProviderKind.OpenRouter)).Should().Be(Shipped);
        }

        // A player who DID name a model for his own server must be obeyed, whatever it looks like. A local id
        // has no namespace convention at all ("qwen3-30b", a bare file name), so nothing may be inferred from
        // its shape.
        [Test]
        public void GIVEN_a_local_server_and_a_model_the_player_actually_named_WHEN_resolving_THEN_it_is_sent_verbatim()
        {
            ProviderModelPolicy.Resolve(Choice("qwen3-30b-a3b", ProviderKind.OpenAiCompatible))
                               .Should().Be("qwen3-30b-a3b");
            ProviderModelPolicy.Resolve(Choice("local-model", ProviderKind.NanoGpt)).Should().Be("local-model");
        }

        // A cleared field is the player saying "I have not chosen", on any provider. The callers read empty as
        // "fall back", so this is the one answer that always leaves them somewhere to go.
        [Test]
        public void GIVEN_a_field_the_player_cleared_WHEN_resolving_THEN_it_says_nothing_on_every_provider()
        {
            foreach (ProviderKind kind in new[]
                         {ProviderKind.OpenRouter, ProviderKind.NanoGpt, ProviderKind.OpenAiCompatible})
            {
                ProviderModelPolicy.Resolve(Choice("", kind)).Should().BeEmpty();
                ProviderModelPolicy.Resolve(Choice("   ", kind)).Should().BeEmpty();
                ProviderModelPolicy.Resolve(Choice(null, kind)).Should().BeEmpty();
            }
        }

        // A pasted id carries whatever whitespace the clipboard had. A stray space must not make an id "not the
        // default" and slip an OpenRouter name onto a local server through the side door.
        [Test]
        public void GIVEN_the_shipped_default_pasted_with_stray_spaces_WHEN_resolving_THEN_it_still_counts_as_untouched()
        {
            ProviderModelPolicy.Resolve(Choice("  " + Shipped + " ", ProviderKind.OpenAiCompatible))
                               .Should().BeEmpty();
        }

        // And the id that IS sent is clean, because a trailing space in a URL path is a 404 nobody can read.
        [Test]
        public void GIVEN_a_real_choice_pasted_with_stray_spaces_WHEN_resolving_THEN_the_id_sent_is_trimmed()
        {
            ProviderModelPolicy.Resolve(Choice("  qwen3-30b  ", ProviderKind.OpenAiCompatible)).Should().Be("qwen3-30b");
        }

        // STAKE (2.7.20): the Interpret field now ships DeepSeek V4.1 Flash. A player who points the interpreter at his
        // own server without touching the field must not have that OpenRouter id sent to it, the very report this
        // policy was written for.
        [Test]
        public void GIVEN_a_local_server_and_the_new_interpreter_default_untouched_WHEN_resolving_THEN_nothing_is_sent()
        {
            ProviderModelPolicy.Resolve(new ProviderModelPolicy.ModelChoice {
                ConfiguredId = "deepseek/deepseek-v4.1-flash", Kind = ProviderKind.OpenAiCompatible, ShippedDefaultId = "deepseek/deepseek-v4.1-flash"
            }).Should().BeEmpty();
        }

        // STAKE: a field saved before 2.7.20 still holds Grok, the id it shipped with then. On a local server it is
        // just as untouched as it always was, whatever the field ships today.
        [Test]
        public void GIVEN_a_local_server_and_an_older_shipped_default_saved_in_the_field_WHEN_resolving_THEN_nothing_is_sent()
        {
            ProviderModelPolicy.Resolve(new ProviderModelPolicy.ModelChoice {
                ConfiguredId = "x-ai/grok-4.20", Kind = ProviderKind.OpenAiCompatible, ShippedDefaultId = "deepseek/deepseek-v4.1-flash"
            }).Should().BeEmpty();
        }

        // STAKE: on OpenRouter the new default is a real model, and it is what the interpreter runs on out of the box.
        [Test]
        public void GIVEN_openrouter_and_the_new_interpreter_default_WHEN_resolving_THEN_it_is_sent()
        {
            ProviderModelPolicy.Resolve(new ProviderModelPolicy.ModelChoice {
                ConfiguredId = "deepseek/deepseek-v4.1-flash", Kind = ProviderKind.OpenRouter, ShippedDefaultId = "deepseek/deepseek-v4.1-flash"
            }).Should().Be("deepseek/deepseek-v4.1-flash");
        }

        // Unknown facts must refuse rather than throw: this is read on every single request.
        [Test]
        public void GIVEN_nothing_at_all_WHEN_resolving_THEN_it_says_nothing_instead_of_throwing()
        {
            ProviderModelPolicy.Resolve(null).Should().BeEmpty();
            ProviderModelPolicy.IsConfigured(null).Should().BeFalse();
        }

        // The distinction the whole fix rests on, stated on its own: OpenRouter is where the shipped id came
        // from, so it is the one provider for which finding it in a field proves nothing either way.
        [Test]
        public void GIVEN_each_provider_WHEN_asking_whose_namespace_the_ids_belong_to_THEN_only_openrouter_shares_ours()
        {
            ProviderModelPolicy.OwnsItsOwnModelNamespace(ProviderKind.OpenRouter).Should().BeFalse();
            ProviderModelPolicy.OwnsItsOwnModelNamespace(ProviderKind.NanoGpt).Should().BeTrue();
            ProviderModelPolicy.OwnsItsOwnModelNamespace(ProviderKind.OpenAiCompatible).Should().BeTrue();
        }
    }
}
