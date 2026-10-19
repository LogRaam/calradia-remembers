// Code written by Gabriel Mailhot, 28/08/2026.
// Extension Surface, Prompt Variables volet (increment 4): CrPrompt.RegisterVariable is the PUBLIC door a
// third-party mod uses to contribute one LIVE {{name}} prompt variable, computed per conversation, that the
// post-history prompt can reference. Replaces the only alternative that existed before this: a Harmony
// patch on the SDK's own PromptBuilder.BuildPromptVariables to splice a value in after the fact. Nothing
// here does any engine work itself; the provider re-resolves whatever engine state it needs from the
// NpcId on its own PromptVarFacts.

#region

using System;
using NpcMemoryService.Core.Extension;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   The public facade a third-party mod calls to register (or unregister) one live prompt variable.
   ///   The provider is called once per prompt build with engine-agnostic <see cref="PromptVarFacts" />
   ///   (an NpcId to re-resolve its own Hero from, and the personal relation the host already tracks) and
   ///   must return the variable's current text; a built-in variable (<c>{{user}}</c>, <c>{{char}}</c>, and
   ///   so on) ALWAYS wins a name collision, so a third-party name can never shadow a core token.
   /// </summary>
   public static class CrPrompt
   {
      /// <summary>
      ///   Registers (or replaces) the provider for a <c>{{name}}</c> prompt variable. A null/blank name or
      ///   a null <paramref name="provider" /> is silently ignored. Registering the same name again replaces
      ///   the prior provider.
      /// </summary>
      public static void RegisterVariable(string name, Func<PromptVarFacts, string> provider)
      {
         if (string.IsNullOrWhiteSpace(name) || provider == null) return;

         PromptVariableRegistry.Register(name, provider);

         ModLog.Info($"[EXT PROMPT VAR] registered '{name}'");
      }

      /// <summary>Unregisters the provider for <paramref name="name" />. Returns whether one was present.</summary>
      public static bool Unregister(string name) => PromptVariableRegistry.Unregister(name);
   }
}
