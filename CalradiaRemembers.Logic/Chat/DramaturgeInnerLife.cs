// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 6 of the group-scene plan: the inner life (docs/design/group-chat/integration-plan-2026-11-28.md), ported from the
// simulator's UpdateMind and LinePrompt (CalradiaRemembers.Tests/Conversation/GroupConversationSimulation.cs). Its review of
// 27/11/2026: "each character must be able to be transformed by the conversation he takes part in"; "the model respects his
// desire, but not his strategy". After each line a cheap reader says how the character stands NOW (feeling, level, shift, aim,
// tactic, what shows, what is hidden, whether anything leaks, the pull to speak, and the four-part bond), and the next reply is
// written from it. NOT A/B-validated: in the simulator a baseline without it equalled the full system on the drama.
//
// The bond is prompt texture only (understands, forgives, loves, trusts: "four different states"); it is never a regard ledger
// and never written back. The state lives for one chat and is never saved. With the meeting record of stage 2, the bond starts
// from it.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>Where a character stands inside, now. Immutable once built: an update makes a new one.</summary>
   public sealed class DramaturgeInnerState
   {
      /// <summary>The feeling now, a few words.</summary>
      public string Feeling { get; init; } = "";

      /// <summary>How deep the last change went: none, fluctuation, reevaluation, tactic, aim or lasting.</summary>
      public string Level { get; init; } = "none";

      /// <summary>What changed in them and which line caused it, or "none" (only reevaluation and above count).</summary>
      public string Shift { get; init; } = "none";

      /// <summary>What they want now.</summary>
      public string Aim { get; init; } = "";

      /// <summary>How they will try for it next.</summary>
      public string Tactic { get; init; } = "";

      /// <summary>What they let the other see.</summary>
      public string Showing { get; init; } = "";

      /// <summary>What they keep hidden now.</summary>
      public string Hiding { get; init; } = "";

      /// <summary>Whether anything shows outside: nothing, a detail, or words.</summary>
      public string Visible { get; init; } = "nothing";

      /// <summary>How strongly they feel they must speak now, and why, or "none".</summary>
      public string Urge { get; init; } = "none";

      /// <summary>The four-part bond toward the player ("Name: understands partly, forgives no, ..."), or empty.</summary>
      public string Bond { get; init; } = "";

      /// <summary>The tactics already tried and dropped this chat.</summary>
      public IReadOnlyList<string> Tried { get; init; } = new List<string>();

      /// <summary>True once a reader has answered at least once (or the bond was seeded).</summary>
      public bool HasContent => Feeling.Length > 0 || Aim.Length > 0 || Bond.Length > 0;
   }

   /// <summary>The inner-life reader's prompt, its parser and the note the next reply is written from.</summary>
   public static class DramaturgeInnerLife
   {
      /// <summary>The longest field kept from the reader (a runaway reply must not grow the prompt).</summary>
      public const int MaxFieldChars = 300;

      /// <summary>
      ///   The bond the meeting record of stage 2 left toward the player, as the reader's starting point
      ///   ("Arwa: understands partly, forgives no, loves no, trusts partly"); empty when there is none.
      /// </summary>
      public static string SeedBond(MeetingBond bond, string playerName)
      {
         if (bond == null || !bond.HasContent) return "";

         string Part(string word, string value) => string.IsNullOrWhiteSpace(value) ? null : $"{word} {value.Trim()}";
         string parts = string.Join(", ", new[] {Part("understands", bond.Understands), Part("forgives", bond.Forgives), Part("loves", bond.Loves), Part("trusts", bond.Trusts)}.Where(p => p != null));
         if (parts.Length == 0) return "";

         return string.IsNullOrWhiteSpace(playerName) ? parts : $"{playerName.Trim()}: {parts}";
      }

      /// <summary>The reader's instructions for <paramref name="sheet" />'s character.</summary>
      public static string System(DramaturgeSheet sheet)
      {
         string name = sheet?.NpcName ?? "the character";
         string player = string.IsNullOrWhiteSpace(sheet?.PlayerName) ? "the visitor" : sheet.PlayerName;

         return $"You keep the inner life of {name}, a character in a scene. You never write dialogue. After the last line of the scene, say how {name} stands NOW."
                + " People change: a revelation, a slight, a kindness, a defeat, a proof can turn a feeling, change a tactic, even make them give up what they wanted"
                + " or want something new. But nothing changes without a cause in the scene; a line that does not touch them changes little, and 'none' is the usual shift."
                + " A tactic that has failed twice is abandoned for another. A tactic never includes a lie or an invented excuse: when they will"
                + " not tell the truth they evade, deflect, stay silent or confess."
                + $"\nA secret is the last thing {name} gives up: before giving it up they try at least two different ways of keeping it (evade, deflect, a true"
                + " half-answer, silence, turning the question back). A single question, however painful, is not enough. When the ways run out, the choice is"
                + " silence or the truth, never a false account."
                + $"\n\nWHO {name.ToUpperInvariant()} IS AND WHAT THEY KNOW:\n{sheet?.Knows}"
                + "\n\nAnswer exactly ten lines:\nfeeling: <their feeling now, 1 to 4 words>"
                + "\nlevel: <none | fluctuation | reevaluation | tactic | aim | lasting> (fluctuation: a passing mood; reevaluation: they now understand the situation"
                + " differently; tactic: they change how they go about it; aim: they want something else; lasting: it changes how they see themselves or someone, beyond"
                + " tonight. Most lines are none or fluctuation.)"
                + "\nshift: <none, or for reevaluation and above, what changed in them and which line caused it, one short sentence>"
                + "\naim: <what they want now: the same aim, or the new one if it changed>\ntactic: <how they will try for it next, one short phrase>"
                + "\nshowing: <what they let the other see, 1 to 6 words>\nhiding: <what they keep hidden now, one short phrase>"
                + "\nvisible: <nothing | a detail | words> (does anything show outside? someone holding a mask shows nothing; nothing is usual)"
                + "\nurge: <none, or low or high followed by why: answer, interest, emotion, duty, morality, opportunity> (how strongly they feel they must speak now)"
                + $"\nbond: <toward {player}: {player}: understands yes/partly/no, forgives yes/partly/no, loves yes/partly/no, trusts yes/partly/no> (four different"
                + " things: one can love without trusting, understand without forgiving, forgive without forgetting; each moves only for its own cause, and a"
                + " confession of weakness earns understanding, not trust)";
      }

      /// <summary>The reader's message: the state before the last line, and the scene so far (this chat only).</summary>
      public static string User(DramaturgeSheet sheet, DramaturgeInnerState before, string sceneText)
      {
         string name = (sheet?.NpcName ?? "the character").ToUpperInvariant();
         DramaturgeInnerState s = before ?? new DramaturgeInnerState();
         var sb = new StringBuilder();
         sb.Append($"HOW {name} STOOD BEFORE THE LAST LINE:\nfeeling: {Or(s.Feeling)}\naim: {Or(s.Aim)}\ntactic: {Or(s.Tactic)}\nshowing: {Or(s.Showing)}\nhiding: {Or(s.Hiding)}");
         if (s.Tried.Count > 0) sb.Append("\nTactics already tried and dropped: " + string.Join("; ", s.Tried));
         if (s.Bond.Length > 0) sb.Append($"\nTheir bond before the last line: {s.Bond}");
         sb.Append($"\n\nTHE SCENE SO FAR:\n{(string.IsNullOrWhiteSpace(sceneText) ? "(nothing yet)" : sceneText)}");
         sb.Append($"\n\nHow {sheet?.NpcName ?? "the character"} stands now:");

         return sb.ToString();
      }

      /// <summary>
      ///   The state after the reader's reply. A field the reply leaves out keeps its old value; a shift counts only at the
      ///   level of a re-evaluation or above (a passing mood is not a change); a dropped tactic joins the tried ones.
      ///   Returns <paramref name="before" /> unchanged when the reply holds no field at all.
      /// </summary>
      public static DramaturgeInnerState Apply(DramaturgeInnerState before, string content)
      {
         DramaturgeInnerState s = before ?? new DramaturgeInnerState();
         string text = content ?? "";
         string Field(string name, string old)
         {
            Match f = Regex.Match(text, $@"(?im)^\s*\**{name}\**\s*:\s*(.+)$");
            string v = f.Success ? Clean(f.Groups[1].Value) : "";

            return v.Length > 0 ? v : old;
         }

         if (!Regex.IsMatch(text, @"(?im)^\s*\**(feeling|aim|tactic|showing|hiding|bond)\**\s*:")) return s;

         string level = Field("level", "none").ToLowerInvariant();
         string shift = Field("shift", "none");
         bool counts = level.StartsWith("reevaluation") || level.StartsWith("re-evaluation") || level.StartsWith("tactic") || level.StartsWith("aim") || level.StartsWith("lasting");
         if (!counts || shift.StartsWith("none", StringComparison.OrdinalIgnoreCase)) shift = "none";
         string tactic = Field("tactic", s.Tactic);
         var tried = s.Tried.ToList();
         if (s.Tactic.Length > 0 && !string.Equals(tactic, s.Tactic, StringComparison.OrdinalIgnoreCase) && !tried.Contains(s.Tactic)) tried.Add(s.Tactic);

         return new DramaturgeInnerState {
            Feeling = Field("feeling", s.Feeling),
            Level = level,
            Shift = shift,
            Aim = Field("aim", s.Aim),
            Tactic = tactic,
            Showing = Field("showing", s.Showing),
            Hiding = Field("hiding", s.Hiding),
            Visible = Field("visible", "nothing").ToLowerInvariant(),
            Urge = Field("urge", "none").ToLowerInvariant(),
            Bond = Field("bond", s.Bond),
            Tried = tried.Skip(Math.Max(0, tried.Count - 4)).ToList()
         };
      }

      /// <summary>
      ///   "Where you stand now", the lines the next reply is written from (the simulator's LinePrompt). Null when the state
      ///   holds nothing yet, so a first turn before any reading shows nothing.
      /// </summary>
      public static string Note(DramaturgeInnerState state, string playerName)
      {
         if (state == null || !state.HasContent) return null;

         var lines = new List<string>();
         void Add(string label, string value)
         {
            if (!string.IsNullOrWhiteSpace(value)) lines.Add($"- {label}: {value.Trim()}");
         }

         Add("you feel", state.Feeling);
         if (!state.Shift.StartsWith("none", StringComparison.OrdinalIgnoreCase)) Add("what just changed in you", state.Shift);
         Add("what you want now", state.Aim);
         Add("how you go about it now", state.Tactic);
         Add("what you let them see", state.Showing);
         Add("what you keep hidden", state.Hiding);
         Add(string.IsNullOrWhiteSpace(playerName) ? "where you stand with them" : $"where you stand with {playerName.Trim()}", state.Bond);
         if (!state.Urge.StartsWith("none", StringComparison.OrdinalIgnoreCase)) Add("your pull to speak", state.Urge);

         return lines.Count == 0 ? null : string.Join("\n", lines);
      }

      /// <summary>One line for the conversation log: the feeling and level, and the shift when there is one.</summary>
      public static string Describe(DramaturgeInnerState state)
      {
         if (state == null || !state.HasContent) return "no reading";

         return $"feels {Or(state.Feeling)} ({state.Level})" + (state.Shift.StartsWith("none", StringComparison.OrdinalIgnoreCase) ? "" : $"; shift: {state.Shift}")
                + (state.Tactic.Length > 0 ? $"; tactic: {state.Tactic}" : "") + (state.Bond.Length > 0 ? $"; bond: {state.Bond}" : "");
      }

      internal static string Clean(string s)
      {
         string v = (s ?? "").Replace("\u2014", ", ").Replace("\u2013", "-").Trim().Trim('`', '*', '"').Trim();

         return v.Length > MaxFieldChars ? v.Substring(0, MaxFieldChars).TrimEnd() : v;
      }

      private static string Or(string v) => string.IsNullOrWhiteSpace(v) ? "(not yet known)" : v;
   }

   /// <summary>What the cheap readers are told about the character: who they are and what they know, read once per chat.</summary>
   public sealed class DramaturgeSheet
   {
      /// <summary>The character's name.</summary>
      public string NpcName { get; init; }

      /// <summary>The player's name.</summary>
      public string PlayerName { get; init; }

      /// <summary>Who the character is and what they know (the stock sections of their own prompt).</summary>
      public string Knows { get; init; } = "";

      /// <summary>The world's facts the character holds as true (the talk of the realm), one per line.</summary>
      public IReadOnlyList<string> WorldFacts { get; init; } = new List<string>();
   }
}
