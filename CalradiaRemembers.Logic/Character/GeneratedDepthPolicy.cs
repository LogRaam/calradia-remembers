// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 4 of the group-scene plan, "generated depth" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain". In the simulator a character known only from its game sheet
// spoke direct and flat beside a hand-written one; one call that gave it a voice, a mask, a fear, a limit and a private secret,
// from that sheet, brought back the subtext (GroupConversationSimulation.GenerateDepth, state-memory-2026-11-28.md).
//
// Gabriel on the secret (27/11/2026): "Il peut y avoir des secrets personnels complémentaires au jeu.. si ça ne touche pas à une
// mécanique du jeu", with his example: once a lover wanted to be with Cindy, I talked him out of it, and Cindy never knew. And:
// "Le problème arrive lorsque le LLM invente quelque chose que le jeu peut contredire." So the secret is private life the game
// cannot check; the generator is told so, a word screen drops the plainest misses for free, and a cheap check (the interpreter
// model) drops the rest. Only the secret goes: the rest of the depth stays.
//
// Generated ONCE and kept, never regenerated (a review of the plan: otherwise the same character changes nature between two
// meetings). It FILLS what is empty and COMPLEMENTS what exists: the game's own fear (NpcProfile.Fear) is kept and no second
// one is asked for; an authored backstory's voice is kept and no voice is asked for; the drawn habit of speech (YOUR VOICE,
// CharacterVoicePolicy) is shown to the generator, which writes a manner beneath it and invents no tic (VonLemberg, 15/11/2026:
// "explicitly asking an LLM to invent recurring words or phrases ... is counterproductive").
//
// This file holds the pure half: whether to generate, the sheet the two calls read, the two prompts, and the reading of both
// replies. The calls are GeneratedDepthGenerator; the engine glue is CalradiaRemembers/Chat/GeneratedDepthService.cs.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>What a chat turn knows when it decides whether to give its character a depth.</summary>
   public sealed class GeneratedDepthFacts
   {
      /// <summary>The MCM switch "Deeper characters".</summary>
      public bool SwitchOn { get; init; }

      /// <summary>The turn is the main character's, in an ordinary one-to-one conversation (not a council, captive scene or witness).</summary>
      public bool OrdinaryConversation { get; init; }

      /// <summary>The conversation is an envoy audience (the lord is not the one speaking with the player).</summary>
      public bool EnvoyAudience { get; init; }

      /// <summary>The conversation is a companion briefing.</summary>
      public bool CompanionBriefing { get; init; }

      /// <summary>The character is a hero with a profile, and not the player.</summary>
      public bool HasCharacter { get; init; }

      /// <summary>The profile already holds a depth: it is never regenerated.</summary>
      public bool HasDepth { get; init; }

      /// <summary>A generation for this character is already running.</summary>
      public bool InFlight { get; init; }

      /// <summary>A generation for this character already failed in this session (a refusing or broken model is not asked at every chat).</summary>
      public bool FailedThisSession { get; init; }
   }

   /// <summary>Whether to generate a character's depth now. Pure.</summary>
   public static class GeneratedDepthPolicy
   {
      /// <summary>The check's time limit, in seconds: one short verdict from the interpreter model.</summary>
      public const int CheckTimeoutSeconds = 30;

      /// <summary>
      ///   True when the switch is on, the turn is the main character's in an ordinary one-to-one conversation (no envoy, no
      ///   briefing), the character has none yet, and no generation is running or already failed this session.
      /// </summary>
      public static bool ShouldGenerate(GeneratedDepthFacts facts)
         => facts != null && facts.SwitchOn && facts.OrdinaryConversation && !facts.EnvoyAudience && !facts.CompanionBriefing && facts.HasCharacter
            && !facts.HasDepth && !facts.InFlight && !facts.FailedThisSession;

      /// <summary>The meeting-record anchor a seeded secret carries (no meeting has this id, so a replayed meeting never replaces it).</summary>
      public const string SeedAnchor = "generated_depth";

      /// <summary>
      ///   The meeting record with the generated secret added as one still kept from the player (Stage 2's record tracks told and
      ///   untold from then on), or null when there is nothing to store: no secret, the record already holds it, or it is full. A
      ///   record about another player (an heir) starts fresh, as the meeting update does. A copy: the record a reader holds is
      ///   never changed (the caller stores the result in one reference write).
      /// </summary>
      public static MeetingMemory SeedSecret(MeetingMemory existing, string secret, string playerName, int day)
      {
         if (string.IsNullOrWhiteSpace(secret)) return null;

         MeetingMemory record = MeetingMemory.About(existing, playerName)?.Clone() ?? new MeetingMemory {AboutPlayer = playerName};
         record.Secrets ??= new List<MeetingSecret>();
         if (record.Secrets.Any(s => string.Equals((s.Text ?? "").Trim(), secret.Trim(), StringComparison.OrdinalIgnoreCase))) return null;
         if (record.Secrets.Count >= MeetingMemory.MaxSecrets) return null;

         if (string.IsNullOrWhiteSpace(record.AboutPlayer)) record.AboutPlayer = playerName;
         record.Secrets.Add(new MeetingSecret {Text = secret.Trim(), SinceDay = day, AnchorId = SeedAnchor});

         return record;
      }
   }

   /// <summary>The character's sheet as the two calls read it, and what the generator is asked for.</summary>
   public sealed class GeneratedDepthSheet
   {
      /// <summary>The character's name.</summary>
      public string NpcName { get; init; }

      /// <summary>The character's own prompt sections, as the prompt shows them: who they are, their ties, their past with the player.</summary>
      public string Sheet { get; init; }

      /// <summary>Ask for a voice: false when an authored backstory gives one (the character keeps it).</summary>
      public bool AskVoice { get; init; } = true;

      /// <summary>Ask for a fear: false when the game gave the character a fear of its own (the game's wins).</summary>
      public bool AskFear { get; init; } = true;
   }

   /// <summary>What the generator answered, read.</summary>
   public sealed class GeneratedDepthReply
   {
      /// <summary>How they speak; empty when not asked or not given.</summary>
      public string Voice { get; init; } = "";

      /// <summary>How they want to be seen.</summary>
      public string Mask { get; init; } = "";

      /// <summary>What they fear most; empty when not asked or not given.</summary>
      public string Fear { get; init; } = "";

      /// <summary>What they will never do.</summary>
      public string Limit { get; init; } = "";

      /// <summary>Their private secret.</summary>
      public string Secret { get; init; } = "";

      /// <summary>Whether the reply is worth keeping for good: a mask and a limit at the least.</summary>
      public bool IsUsable => Mask.Length > 0 && Limit.Length > 0;
   }

   /// <summary>The checker's verdict on the secret.</summary>
   public sealed class GeneratedDepthVerdict
   {
      /// <summary>True when the secret may be kept.</summary>
      public bool Passes { get; init; }

      /// <summary>Why it was dropped, for the log.</summary>
      public string Reason { get; init; } = "";
   }

   /// <summary>The two prompts of a generated depth and the reading of their replies. Pure.</summary>
   public static class GeneratedDepthPrompt
   {
      /// <summary>Longest sheet sent, in characters (the calls are background and cheap; a long history is cut from its start).</summary>
      public const int MaxSheetChars = 6000;

      /// <summary>
      ///   What the game tracks, which a secret must never touch (Gabriel, 27/11/2026: the game can contradict it). Named in both
      ///   prompts, so the generator and the checker read one list.
      /// </summary>
      public const string TrackedFacts =
         "a marriage, betrothal, child, kinship, death, killing, fief, battle, war, alliance, treason, money, troops or prisoners";

      // The plainest misses, dropped without a call: words that can only name a tracked fact. Kept narrow on purpose (a lover, a
      // friend, a father remembered are private life); the checker reads the rest.
      private static readonly Regex TrackedWords = new(
         @"\b(marri\w*|wed(ding|ded)?|betroth\w*|bastard\w*|pregnan\w*|sired|murder\w*|kill(ed|s|ing)?|slew|slain|poison(ed)?|treason|traitor\w*|"
         + @"denars?|gold|ransom\w*|prisoners?|captives?|troops|soldiers|battles?|wars?|alliances?|fiefs?|castles?)\b",
         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

      /// <summary>The generator's system prompt (the simulator's GenerateDepth, fitted to what the game already gives).</summary>
      public static string GeneratorSystem(GeneratedDepthSheet sheet)
      {
         var sb = new StringBuilder();
         sb.AppendLine("You deepen a character of a medieval world for a novelist. From their sheet, write who they are beneath it, consistent with every line of the sheet:");
         sb.AppendLine("what the player wrote for them (BACKSTORY AND VOICE, WHAT YOU HOLD TRUE) above all. Answer exactly these lines:");
         if (sheet?.AskVoice != false)
            sb.AppendLine("voice: <how they speak: rhythm, register, what they never say; one sentence. It deepens the habit under YOUR VOICE in the sheet and keeps how they curse and how their piety shows; no signature word, no recurring phrase or tic>");
         sb.AppendLine("mask: <how they want to be seen by others, a few words>");
         if (sheet?.AskFear != false) sb.AppendLine("fear: <what they fear most about themselves or others, a few words>");
         sb.AppendLine("limit: <one thing they will never do, a few words>");
         sb.AppendLine("secret: <one personal secret nobody knows, two sentences>");
         sb.AppendLine("THE SECRET MUST BE PRIVATE LIFE THE GAME CANNOT CHECK: a feeling, a choice, a small deed, a thing seen or said, between people");
         sb.AppendLine("(example: once a lover wanted to be with her friend Cindy, and she talked him out of it; Cindy never knew).");
         sb.AppendLine($"It must NEVER touch what the game tracks: no {TrackedFacts}, and nothing that contradicts the sheet.");
         sb.AppendLine("Mask, fear and limit grow out of the sheet's nature and history, not against it. Write in English. No preamble, no comment.");

         return sb.ToString();
      }

      /// <summary>The generator's user message: the sheet.</summary>
      public static string GeneratorUser(GeneratedDepthSheet sheet)
         => $"THE SHEET OF {(sheet?.NpcName ?? "THE CHARACTER").ToUpperInvariant()}:\n{Cut(sheet?.Sheet)}";

      /// <summary>The checker's system prompt: one secret, read against what the game tracks and against the sheet.</summary>
      public const string CheckSystem =
         "You check one private secret written for a character of a medieval game. The game tracks " + TrackedFacts + "."
         + " The secret must touch none of these and must contradict nothing in the character's sheet."
         + " Answer one line: 'ok' when it touches none and contradicts nothing, or 'touches <what>' when it does.";

      /// <summary>The checker's user message: the sheet, then the secret.</summary>
      public static string CheckUser(GeneratedDepthSheet sheet, string secret)
         => $"THE SHEET OF {(sheet?.NpcName ?? "THE CHARACTER").ToUpperInvariant()}:\n{Cut(sheet?.Sheet)}\n\nTHE SECRET:\n{secret}";

      /// <summary>
      ///   Reads the generator's reply. Labels may carry markdown; a field answered "none" or "nothing" reads as empty; a field not
      ///   asked for is ignored even if given; each is cut at its cap. The whole reply is refused (an empty reply) when it is the
      ///   model thinking aloud or a refusal.
      /// </summary>
      public static GeneratedDepthReply Parse(string content, GeneratedDepthSheet sheet)
      {
         if (string.IsNullOrWhiteSpace(content) || MetaReasoningGuard.IsMetaReasoning(content) || DialogueHeuristics.LooksLikeRefusal(content))
            return new GeneratedDepthReply();

         string Field(string name, int cap)
         {
            Match m = Regex.Match(content, $@"(?im)^[\s\*\-#>]*{name}[\s\*]*:[\s\*]*(.*)$");

            return m.Success ? Clean(m.Groups[1].Value, cap) : "";
         }

         return new GeneratedDepthReply {
            Voice = sheet?.AskVoice == false ? "" : Field("voice", GeneratedDepth.MaxVoiceChars),
            Mask = Field("mask", GeneratedDepth.MaxTraitChars),
            Fear = sheet?.AskFear == false ? "" : Field("fear", GeneratedDepth.MaxTraitChars),
            Limit = Field("limit", GeneratedDepth.MaxTraitChars),
            Secret = Field("secret", GeneratedDepth.MaxSecretChars)
         };
      }

      /// <summary>
      ///   Whether a secret plainly names what the game tracks (a marriage, a killing, money, troops...): dropped without asking the
      ///   checker. Narrow on purpose; the checker reads what this lets through.
      /// </summary>
      public static bool NamesATrackedFact(string secret) => !string.IsNullOrWhiteSpace(secret) && TrackedWords.IsMatch(secret);

      /// <summary>
      ///   Reads the checker's verdict. Only a plain "ok" keeps the secret: "touches ..." anywhere drops it (unless it touches
      ///   nothing), and an empty or unreadable reply drops it too (a check that did not run is no check).
      /// </summary>
      public static GeneratedDepthVerdict ReadVerdict(string content)
      {
         string line = (content ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim().Trim('*', '"', '\'', '`', '.', ' ')).FirstOrDefault(l => l.Length > 0) ?? "";
         int touches = line.IndexOf("touch", StringComparison.OrdinalIgnoreCase);
         if (touches >= 0)
         {
            string after = line.Substring(touches).Split(new[] {' '}, 2).Skip(1).FirstOrDefault()?.Trim() ?? "";
            bool nothing = after.StartsWith("nothing", StringComparison.OrdinalIgnoreCase) || after.StartsWith("none", StringComparison.OrdinalIgnoreCase);
            if (!nothing) return new GeneratedDepthVerdict {Passes = false, Reason = after.Length == 0 ? "it touches a tracked fact" : "it touches " + after};
         }

         if (line.Equals("ok", StringComparison.OrdinalIgnoreCase) || (line.Length > 2 && line.StartsWith("ok", StringComparison.OrdinalIgnoreCase) && !char.IsLetter(line[2])))
            return new GeneratedDepthVerdict {Passes = true};

         return new GeneratedDepthVerdict {Passes = false, Reason = line.Length == 0 ? "the check gave no verdict" : "the check gave no readable verdict"};
      }

      /// <summary>The depth to keep, from a usable reply: the secret only when it was kept.</summary>
      public static GeneratedDepth ToDepth(GeneratedDepthReply reply, bool keepSecret, int day)
         => new() {
            Voice = reply?.Voice ?? "", Mask = reply?.Mask ?? "", Fear = reply?.Fear ?? "", Limit = reply?.Limit ?? "",
            Secret = keepSecret ? reply?.Secret ?? "" : "", GeneratedOnDay = day
         };

      private static string Cut(string sheet)
      {
         string s = (sheet ?? "").Trim();

         return s.Length <= MaxSheetChars ? s : s.Substring(0, MaxSheetChars).TrimEnd() + "\n(...)";
      }

      private static string Clean(string text, int cap)
      {
         string t = (text ?? "").Trim().Trim('*', '"', '“', '”', ' ').Trim();
         if (t.StartsWith("<", StringComparison.Ordinal) && t.EndsWith(">", StringComparison.Ordinal)) t = t.Substring(1, t.Length - 2).Trim();
         if (t.Equals("none", StringComparison.OrdinalIgnoreCase) || t.Equals("nothing", StringComparison.OrdinalIgnoreCase)
             || t.Equals("none.", StringComparison.OrdinalIgnoreCase) || t.Equals("nothing.", StringComparison.OrdinalIgnoreCase)) return "";
         if (t.Length <= cap) return t;

         int cut = t.LastIndexOf(' ', cap);

         return (cut > cap / 2 ? t.Substring(0, cut) : t.Substring(0, cap)).TrimEnd(' ', ',', ';');
      }
   }
}
