// Code written by Gabriel Mailhot, 28/08/2026.
// Extension Surface: the PUBLIC contract types for a third-party mod's own executable conversation action,
// registered in one call through CrActions.Register. Where CrConversation lets an addon LISTEN to a
// conversation and CrNpcMemory lets it read/write what CR remembers for an NPC, this is the door for a mod
// that wants the LLM to actually DO one of its own deeds mid-conversation (summon a caravan, open a shop,
// whatever the mod owns), taught to the interpreter and the 1:1 prose prompt and dispatched by CR exactly
// like a built-in verb, without any Harmony patch or reflection into CR's internals.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Extension;

#endregion

namespace CalradiaRemembers
{
   /// <summary>One named parameter an <see cref="ExternalActionSpec" /> accepts, taught to the LLM by name and meaning.</summary>
   public sealed class ExternalActionParam
   {
      public ExternalActionParam(string name, string meaning)
      {
         Name = name;
         Meaning = meaning;
      }

      /// <summary>The parameter's key, exactly as it will appear in the emitted [ACTION] block's parameters.</summary>
      public string Name { get; }

      /// <summary>A short human-readable explanation of what value belongs in this parameter, shown to the LLM.</summary>
      public string Meaning { get; }
   }

   /// <summary>The outcome of <see cref="CrActions.Register" />.</summary>
   public enum ActionRegistrationResult
   {
      /// <summary>The action was registered: it is now taught to the interpreter and 1:1 prose prompt, and CR dispatches it.</summary>
      Registered,

      /// <summary>The spec itself was malformed (null, a blank Name, or no Execute) and nothing was registered.</summary>
      RejectedInvalid,

      /// <summary>
      ///   The Name collides with an already-registered action (built-in or external) or with a chat-flow
      ///   control CR reserves for itself (see <see cref="CrActions.Register" />). Nothing was registered.
      /// </summary>
      RejectedDuplicate
   }

   /// <summary>
   ///   Everything a third-party mod supplies to register one executable conversation action through
   ///   <see cref="CrActions.Register" />.
   ///   <para>
   ///     <see cref="Name" /> is the action vocabulary key: the [ACTION] "type:" the LLM emits to invoke this
   ///     deed, exactly like CR's own built-in verbs (change_relation, give_gold, ...).
   ///   </para>
   ///   <para>
   ///     <see cref="WhenToEmit" /> and <see cref="WhenNotToEmit" /> teach the interpreter (and the 1:1 prose
   ///     prompt) what this deed looks like when it genuinely happens in the NPC's reply, and the false
   ///     positives to withhold on: the same "tells / anti-patterns" shape CR's own catalog uses for every
   ///     built-in verb.
   ///   </para>
   ///   <para>
   ///     <see cref="Execute" /> is where the registering mod does its OWN engine work: it receives
   ///     engine-agnostic <see cref="VerbFacts" /> (a plain Hero StringId as NpcId, never a live Hero, never
   ///     a CR type) and re-resolves whatever it needs, its own Hero, its own state, from that id on its own
   ///     side. CR never reaches into the mod's state to perform the deed, and the mod never reaches into
   ///     CR's; the two sides only ever exchange these plain, engine-agnostic shapes.
   ///   </para>
   ///   <para>
   ///     <see cref="IsEligible" /> and <see cref="TeachingText" /> are optional: leave them null when the
   ///     action should always be taught to an eligible NPC on its catalog entry alone, or supply them for
   ///     extra, per-turn conditional teaching (e.g. only while some condition of the mod's own holds).
   ///   </para>
   /// </summary>
   public sealed class ExternalActionSpec
   {
      /// <summary>The action vocabulary key: the [ACTION] "type:" the LLM emits to invoke this deed. REQUIRED.</summary>
      public string Name { get; set; }

      /// <summary>A one-line, factual description of what the deed IS, taught alongside its tells/anti-patterns.</summary>
      public string Description { get; set; }

      /// <summary>
      ///   Concept-level cues for how this deed READS in the NPC's prose (not lexical triggers): what the
      ///   interpreter should recognise as this action genuinely happening in the reply.
      /// </summary>
      public IReadOnlyList<string> WhenToEmit { get; set; }

      /// <summary>
      ///   Concept-level false positives to withhold on (not lexical triggers): what merely resembles this
      ///   deed, a future promise, a mere discussion, a neighbouring verb, but must NOT emit it.
      /// </summary>
      public IReadOnlyList<string> WhenNotToEmit { get; set; }

      /// <summary>The named parameters this action accepts, taught to the LLM so it fills them in the [ACTION] block.</summary>
      public IReadOnlyList<ExternalActionParam> Parameters { get; set; }

      /// <summary>
      ///   Optional. True when this action currently applies to the NPC described by the given
      ///   <see cref="VerbFacts" /> and should be taught / may be executed this turn. Null (the default)
      ///   means "always eligible", relying on the catalog entry alone.
      /// </summary>
      public Func<VerbFacts, bool> IsEligible { get; set; }

      /// <summary>Optional. Extra prompt text taught for this turn while the action is eligible, beyond the catalog entry.</summary>
      public Func<VerbFacts, string> TeachingText { get; set; }

      /// <summary>
      ///   REQUIRED. Does the mod's own engine work for this deed: re-resolves a live Hero (or whatever it
      ///   needs) from <see cref="VerbFacts.NpcId" /> in the request, applies whatever effect the mod owns,
      ///   and reports back whether it succeeded and a short chat-visible message. CR never performs this
      ///   work itself; it only routes the call here once the action is registered.
      /// </summary>
      public Func<ActionRequest, ActionResult> Execute { get; set; }

      /// <summary>The registering mod's own id, for provenance in the logs. Optional but encouraged.</summary>
      public string SourceMod { get; set; }
   }
}
