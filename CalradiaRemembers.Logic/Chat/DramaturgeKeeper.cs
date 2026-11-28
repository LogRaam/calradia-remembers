// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 6 of the group-scene plan: the scene keeper (docs/design/group-chat/integration-plan-2026-11-28.md), ported from the
// simulator's Inspect and Keep (CalradiaRemembers.Tests/Conversation/GroupConversationSimulation.cs). One cheap reading of
// each reply before it is shown: a LIE against what the character knows (the mod's rule: NPCs never lie; a claim not yet
// proven, a sincere belief, an evasion is no lie), a REPEAT within this chat, a CONTRADICTION of what was established, an
// INVENTION (a fact the game tracks that no fact holds; private life is free; Gabriel, 27/11/2026: "le probleme arrive
// lorsque le LLM invente quelque chose que le jeu peut contredire"). The verdict comes last, after the reasoning (a review of
// 27/11/2026: the keeper wrote "problem: lie" first, then cleared the line in its own reasoning, and it was rewritten anyway).
// It also keeps the record of the chat: what was established and where things are. NOT A/B-validated.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>What the keeper found in a reply.</summary>
   public enum KeeperProblem
   {
      /// <summary>Nothing: the reply stands.</summary>
      None,

      /// <summary>A statement the character knows to be false.</summary>
      Lie,

      /// <summary>A gesture, image or demand already used in this chat.</summary>
      Repeat,

      /// <summary>A conflict with what this chat established or the world's facts.</summary>
      Contradiction,

      /// <summary>A fact the game tracks that nothing holds.</summary>
      Invention
   }

   /// <summary>The keeper's reading of one reply.</summary>
   public sealed class KeeperVerdict
   {
      /// <summary>The final verdict.</summary>
      public KeeperProblem Problem { get; init; }

      /// <summary>The false statement, the repeat or the record contradicted, with the keeper's reasoning; or empty.</summary>
      public string Detail { get; init; } = "";

      /// <summary>What the reply made true for both present.</summary>
      public IReadOnlyList<string> Facts { get; init; } = new List<string>();

      /// <summary>Things the reply moved, as "thing: where it is now", separated by '|'.</summary>
      public string Objects { get; init; } = "";

      /// <summary>False when the reply held no verdict line at all (then it passes, and the log says so).</summary>
      public bool HadVerdict { get; init; }
   }

   /// <summary>The record of one chat: what was established and where things stand. Thread-safe.</summary>
   public sealed class DramaturgeRecord
   {
      /// <summary>The most facts kept (the oldest drop first).</summary>
      public const int MaxFacts = 25;

      private readonly List<string> _facts = new();
      private readonly Dictionary<string, string> _objects = new(StringComparer.OrdinalIgnoreCase);

      /// <summary>Adds what a reply established (facts and objects of a verdict that passed, or of the reply shown).</summary>
      public void Establish(KeeperVerdict verdict)
      {
         if (verdict == null) return;

         lock (_facts)
         {
            _facts.AddRange(verdict.Facts);
            if (_facts.Count > MaxFacts) _facts.RemoveRange(0, _facts.Count - MaxFacts);
            foreach (string part in (verdict.Objects ?? "").Split('|'))
            {
               int colon = part.IndexOf(':');
               if (colon <= 0) continue;
               string name = Regex.Replace(part.Substring(0, colon).Trim().ToLowerInvariant(), @"^(the|a|an|his|her|their)\s+", "");
               if (name.Length > 0 && name != "none") _objects[name] = part.Substring(colon + 1).Trim();
            }
         }
      }

      /// <summary>The record as the readers see it, or "(nothing yet)".</summary>
      public string Text()
      {
         lock (_facts)
         {
            if (_facts.Count == 0 && _objects.Count == 0) return "(nothing yet)";

            return (_objects.Count == 0 ? "" : "Where things are now:\n" + string.Join("\n", _objects.Select(o => $"- {o.Key}: {o.Value}")) + "\nEstablished:\n")
                   + string.Join("\n", _facts.Select(f => "- " + f));
         }
      }
   }

   /// <summary>The keeper's prompt, its parser and the reason handed to a rewrite.</summary>
   public static class DramaturgeKeeper
   {
      /// <summary>The keeper's instructions for <paramref name="npcName" />'s reply.</summary>
      public static string System(string npcName)
      {
         string name = string.IsNullOrWhiteSpace(npcName) ? "the character" : npcName.Trim();
         string upper = name.ToUpperInvariant();

         return "You keep the record of a scene. You never write the story. A NEW LINE has just been written. Answer exactly five lines, in this order;"
                + " decide the problem LAST, after weighing the detail:"
                + "\ndetail: <the false statement, the repeated gesture or image, the record it contradicts, or the invented fact, with your reasoning; or none>"
                + "\nfacts: <what this line makes true for both present: objects moved, who holds what, what was shown, admitted or revealed; separated by ' | '; or none>"
                + "\nobjects: <each thing this line moves or changes, as 'thing: where it is and how it stands now', separated by ' | '; or none>"
                + "\nrevealed: <a secret of WHAT " + upper + " KNOWS that this line tells, in a few words; or none>"
                + "\nproblem: <none | lie | repeat | contradiction | invention> (your final verdict on the detail above; if your reasoning cleared the line, none)"
                + $"\nA LIE: a statement of fact that WHAT {upper} KNOWS shows to be false, or an excuse {name} invents and knows is untrue."
                + " Not a lie: a claim not yet established in the record, something they may well have seen, an inference, a sincere belief, an"
                + " evasion, a silence, a literally true half-answer, an opinion, a demand. Two facts that can both be true are no lie. An unproven claim is"
                + " not a lie. When unsure, it is not a lie."
                + $"\nA REPEAT: a gesture, a way of handling an object, or an image that {name} already used in THE SCENE SO FAR, or a demand already made there"
                + " that this line only makes again. Answering a question again is not a repeat. Only THE SCENE SO FAR counts: a gesture from an earlier meeting,"
                + " told in their history or memories, is not a repeat, and echoing it on purpose is a callback, which is welcome."
                + "\nA CONTRADICTION: the line conflicts with the record, with where things are now, or with the world's facts."
                + "\nAN INVENTION: the line states as fact something a game of lords and wars keeps track of, and that is in none of the world's facts,"
                + $" WHAT {upper} KNOWS, or the record: a marriage or betrothal, a child or kinship, the death or capture of a lord, a battle or raid,"
                + " a war, peace or alliance, a fief changing hands, a plan of attack, a sum of money, a count of troops. Private life is free and is NOT"
                + " an invention: a memory, a feeling, a habit, a small deed between people, a detail of an event already known."
                + "\nRecord a claim as a claim ('she says he held the left'), never as a fact. Record only what both present could see or hear.";
      }

      /// <summary>The keeper's message: what the character knows, the world's facts, the record, this chat so far, and the new line.</summary>
      public static string User(DramaturgeSheet sheet, DramaturgeInnerState state, DramaturgeRecord record, string sceneText, string reply)
      {
         string name = sheet?.NpcName ?? "the character";
         var sb = new StringBuilder();
         sb.Append($"WHAT {name.ToUpperInvariant()} KNOWS:\n{sheet?.Knows}");
         if (!string.IsNullOrWhiteSpace(state?.Hiding)) sb.Append($"\nHidden right now: {state.Hiding}.");
         if (sheet?.WorldFacts?.Count > 0) sb.Append("\n\nTHE WORLD'S FACTS (true):\n" + string.Join("\n", sheet.WorldFacts.Select(f => "- " + f)));
         sb.Append($"\n\nTHE RECORD (established in this scene):\n{record?.Text() ?? "(nothing yet)"}");
         sb.Append($"\n\nTHE SCENE SO FAR:\n{(string.IsNullOrWhiteSpace(sceneText) ? "(nothing yet)" : sceneText)}");
         sb.Append($"\n\nTHE NEW LINE ({name}):\n{reply}");

         return sb.ToString();
      }

      /// <summary>
      ///   Reads the keeper's reply. The verdict is the LAST "problem:" line (the reasoning before it may say the word); no
      ///   verdict at all passes the line (<see cref="KeeperVerdict.HadVerdict" /> false).
      /// </summary>
      public static KeeperVerdict Parse(string content)
      {
         string text = content ?? "";
         string Last(string name)
         {
            MatchCollection all = Regex.Matches(text, $@"(?im)^\s*\**{name}\**\s*:[ \t]*(.+)$");

            return all.Count == 0 ? "" : DramaturgeInnerLife.Clean(all[all.Count - 1].Groups[1].Value);
         }

         string verdict = Last("problem").ToLowerInvariant().TrimStart('<', ' ');
         KeeperProblem problem = verdict.StartsWith("lie") ? KeeperProblem.Lie
            : verdict.StartsWith("repeat") ? KeeperProblem.Repeat
            : verdict.StartsWith("contradiction") ? KeeperProblem.Contradiction
            : verdict.StartsWith("invention") ? KeeperProblem.Invention
            : KeeperProblem.None;
         string detail = Last("detail");
         // A keeper that opens its detail on the next line ("detail:" alone, then the reasoning) left it empty: Gabriel's chat with
         // Monchug (28/11/2026) logged "found a invention: ;" and the rewrite was told nothing. Read the block up to the next field.
         if (detail.Length == 0)
         {
            MatchCollection blocks = Regex.Matches(text, @"(?ims)^\s*\**detail\**\s*:\s*$(.*?)(?=^\s*\**(?:facts|objects|revealed|problem)\**\s*:|\z)");
            if (blocks.Count > 0) detail = DramaturgeInnerLife.Clean(Regex.Replace(blocks[blocks.Count - 1].Groups[1].Value, @"\s+", " "));
         }

         List<string> facts = Last("facts").Split('|').Select(f => f.Trim()).Where(f => f.Length > 0 && !f.StartsWith("none", StringComparison.OrdinalIgnoreCase)).ToList();
         string objects = Last("objects");

         return new KeeperVerdict {
            Problem = problem,
            Detail = detail.StartsWith("none", StringComparison.OrdinalIgnoreCase) ? "" : detail,
            Facts = facts,
            Objects = objects.StartsWith("none", StringComparison.OrdinalIgnoreCase) ? "" : objects,
            HadVerdict = verdict.Length > 0
         };
      }

      /// <summary>The reason the rewrite is given (the simulator's words), naming the fault and its detail.</summary>
      public static string RewriteReason(KeeperVerdict verdict)
      {
         string detail = string.IsNullOrWhiteSpace(verdict?.Detail) ? "" : verdict.Detail.Trim();
         switch (verdict?.Problem ?? KeeperProblem.None)
         {
            case KeeperProblem.Lie:
               return $"You said something you know is false: \"{detail}\". Do not say it: evade, deflect, go silent, or tell the truth. You never lie.";
            case KeeperProblem.Repeat:
               return $"You repeated a gesture, an image or a demand already made in this conversation ({detail}). Do something different.";
            case KeeperProblem.Invention:
               return $"You stated as fact something the world does not hold ({detail}). Never invent a marriage, a kinship, a death, a battle, a"
                      + " plan of war, a sum or a count: keep to what is known, or keep your inventions to private life.";
            case KeeperProblem.Contradiction:
               return $"You contradicted what is established ({detail}). Keep to what is established.";
            default:
               return null;
         }
      }

      /// <summary>The verdict's name for the log ("lie", "none", ...).</summary>
      public static string Name(KeeperProblem problem) => problem.ToString().ToLowerInvariant();

      /// <summary>The detail as the logs show it: the keeper's own words, or a plain note that it gave none.</summary>
      public static string DetailForLog(KeeperVerdict verdict) => string.IsNullOrWhiteSpace(verdict?.Detail) ? "it gave no detail" : verdict.Detail.Trim();

      /// <summary>
      ///   Added to every rewrite's reason: the rewrite keeps the form of the character's other replies. Gabriel's chat with Monchug
      ///   (28/11/2026, Novel style): the one rewritten reply came back with no quotation marks, speech and gesture run together.
      /// </summary>
      public const string KeepTheForm = "Write the whole reply again in the same form as your other replies in this conversation, marking what is said"
                                        + " and what is done the same way.";
   }

   /// <summary>The facts the keeper's rewrite is judged on, once it came back.</summary>
   public sealed class KeeperRewriteFacts
   {
      /// <summary>The rewrite call answered.</summary>
      public bool RewriteAnswered { get; init; }

      /// <summary>The rewrite holds a spoken line or a narration.</summary>
      public bool RewriteHasText { get; init; }

      /// <summary>The rewrite reads as the model refusing the scene.</summary>
      public bool RewriteLooksLikeRefusal { get; init; }
   }

   /// <summary>When the keeper asks for a rewrite, and which reply is shown after it.</summary>
   public static class DramaturgeRewritePolicy
   {
      /// <summary>Only a verdict of a problem asks for the one rewrite; none, or no verdict, lets the reply stand.</summary>
      public static bool ShouldRewrite(KeeperVerdict verdict) => verdict != null && verdict.Problem != KeeperProblem.None;

      /// <summary>
      ///   Whether the rewrite is shown. It is, whatever its second reading says (the brief of 28/11/2026: one rewrite, read once
      ///   more; if it still fails, the second rewrite is shown). It is not when it never came, came back empty, or reads as a
      ///   refusal: the first reply is shown then, as it was.
      /// </summary>
      public static bool ShowsRewrite(KeeperRewriteFacts facts)
         => facts != null && facts.RewriteAnswered && facts.RewriteHasText && !facts.RewriteLooksLikeRefusal;
   }
}
