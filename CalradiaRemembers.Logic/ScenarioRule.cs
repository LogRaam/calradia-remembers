// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   One reactive rule: a <see cref="Trigger" />, optional <see cref="Guards" /> (eligibility, AND-combined),
   ///   a list of <see cref="Consequences" /> (declarative world mutations), and a <see cref="Message" />
   ///   for the World-Event store. <see cref="FireOnce" /> seals a one-shot once it has fired (anti-save-scum);
   ///   <see cref="Weight" /> biases the random draw when several rules share a trigger (default 1).
   /// </summary>
   public sealed class ScenarioRule
   {
      public ScenarioRule(
         string id,
         KeywordToken trigger,
         IReadOnlyList<KeywordToken> guards,
         IReadOnlyList<KeywordToken> consequences,
         string message,
         bool fireOnce = false,
         int weight = 1,
         string group = null)
      {
         Id = id;
         Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
         Trigger = trigger;
         Guards = guards ?? Array.Empty<KeywordToken>();
         Consequences = consequences ?? Array.Empty<KeywordToken>();
         Message = message ?? string.Empty;
         FireOnce = fireOnce;
         Weight = weight < 1
            ? 1
            : weight; // a non-positive weight is meaningless — treat it as the default 1
      }

      /// <summary>The declarative world mutations to apply, in order, when this rule is chosen.</summary>
      public IReadOnlyList<KeywordToken> Consequences { get; }

      /// <summary>
      ///   The alternatives this rule belongs to, or null. Rules of one group exclude each other: one is drawn by weight
      ///   (the four outcomes of marrying Rhagaea); a rule with no group always plays when ready (Gabriel, 11/10/2026).
      /// </summary>
      public string Group { get; }

      /// <summary>True for a one-shot rule: once it has fired it is sealed and never drawn again.</summary>
      public bool FireOnce { get; }

      /// <summary>Eligibility predicates, AND-combined; empty means "always eligible once the trigger matches".</summary>
      public IReadOnlyList<KeywordToken> Guards { get; }

      /// <summary>Stable identifier — the key the fired-once bookkeeping persists.</summary>
      public string Id { get; }

      /// <summary>The situation text dropped into the World-Event store, told in-game by the existing channels.</summary>
      public string Message { get; }

      /// <summary>The event that arms this rule (e.g. <c>ON_MARRIAGE:Rhagaea,Player</c>).</summary>
      public KeywordToken Trigger { get; }

      /// <summary>Relative draw weight among the alternatives of a group. Clamped to a minimum of 1.</summary>
      public int Weight { get; }

      /// <summary>
      ///   Builds a rule from raw DSL strings — the form a JSON pack carries. Documents the language:
      ///   pass keyword strings for the trigger / guards / consequences and they are parsed into tokens.
      /// </summary>
      public static ScenarioRule Parse(
         string id,
         string trigger,
         IEnumerable<string> guards,
         IEnumerable<string> consequences,
         string message,
         bool fireOnce = false,
         int weight = 1,
         string group = null) => new(
         id,
         KeywordToken.Parse(trigger),
         ParseEach(guards),
         ParseEach(consequences),
         message,
         fireOnce,
         weight,
         group);

      #region private

      private static IReadOnlyList<KeywordToken> ParseEach(IEnumerable<string> raw)
      {
         var tokens = new List<KeywordToken>();

         if (raw == null) return tokens;
         foreach (string s in raw)
         {
            if (string.IsNullOrWhiteSpace(s)) continue;
            tokens.Add(KeywordToken.Parse(s));
         }

         return tokens;
      }

      #endregion
   }
}