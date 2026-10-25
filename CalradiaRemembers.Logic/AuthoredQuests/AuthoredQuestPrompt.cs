// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: what each character is told, and the scene narrator's prompt
// (docs/design/AUTHORED_QUESTS.md). Gabriel's rulings: the text is a canvas the model rephrases while keeping its facts;
// a character is told only what they know at this stage (gating before declaration, 09/09/2026); characters never lie
// (20/09/2026); a scene's narrator speaks plainly (03/10/2026). The narrator reports what the game must check through
// markers ([[found:id]], [[hazard:id]], [[meet:id]]) that SceneMarkers strips from the text and the engine validates
// against the file: the model chooses the moment, the game decides whether it counts.
// 04/10/2026, branching tales: a line under a flag reaches the model only while it holds; the narrator is told the
// open ways out of its scene (never a closed one), each with its marker; and the character whose conversation took a
// choice speaks from that choice's own lines while it lasts. No one is ever told the choices still ahead.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The prompt text of an authored quest. Pure.</summary>
   public static class AuthoredQuestPrompt
   {
      private const string Rule = "Say this in your own words and your own voice. Keep every fact; add none; speak only of what you know here; "
                                  + "never speak of what is still to come.";

      /// <summary>
      ///   The block for one character, or null when they have no part in the quest now. The giver, while offering
      ///   (<paramref name="offering" />) or in the conversation completing a stage of theirs, has that stage's canvas
      ///   and facts; anyone the stage names as a knower has their own lines, framed as what they believe. The one whose
      ///   conversation took a choice speaks from that choice's lines when it has any (the lord hearing the truth in the
      ///   very conversation that exposes it), and otherwise from the stage, as before.
      /// </summary>
      public static string ForCharacter(AuthoredQuestRun run, AuthoredQuest q, string heroId, string localOfPlaceId, bool offering)
      {
         if (run == null || q == null || string.IsNullOrEmpty(heroId)) return null;

         // In the conversation that completed a stage (the report to the lord), the stage spoken of is that one.
         bool inTheirConversation = run.SpokenHeroId == heroId && run.SpokenStageId != null;
         if (run.Status != AuthoredQuestStatus.Active && !inTheirConversation) return null; // a tale that ended is told no more
         AuthoredStage stage = inTheirConversation ? q.Stages.FirstOrDefault(s => s.Id == run.SpokenStageId) : AuthoredQuestEngine.Current(run, q);
         if (stage == null) return null;

         AuthoredChoice choice = inTheirConversation && run.SpokenChoiceId != null ? stage.Choices?.FirstOrDefault(c => c?.Id == run.SpokenChoiceId) : null;
         List<AuthoredLine> canvasLines = stage.Canvas, factLines = stage.Facts;
         List<string> lines = KnowerLines(run, stage.Knowers, heroId, localOfPlaceId);
         // Gabriel (10/10/2026): the giver also speaks the moment of a stage decided by words with them, the conversation
         // that decides it being theirs (stages that are states of one conversation).
         bool decidedWithThem = !inTheirConversation && AuthoredQuestEngine.SaidChoicesFor(run, q, heroId).Count > 0;
         // Gabriel (24/10/2026, the night in Rhagaea's chamber): a talk that completes as the conversation ends is that
         // conversation, so the one it names speaks the stage's moment all through it (nothing was completed on opening).
         bool inTheirClosingTalk = !inTheirConversation && run.Status == AuthoredQuestStatus.Active
                                   && AuthoredQuestEngine.Ways(run, q).Any(w => w.Objective.Type == "talk_to" && AuthoredQuestEngine.EndsOnClose(w.Objective)
                                                                               && heroId == AuthoredQuestEngine.Bound(run, w.Objective.Hero));
         bool speaks = (heroId == AuthoredQuestEngine.Bound(run, "{giver}") && (offering || run.SpokenStageId == stage.Id || decidedWithThem)) || inTheirClosingTalk;

         if (choice != null && (choice.Canvas?.Count > 0 || choice.Facts?.Count > 0 || choice.Knowers?.Count > 0))
         {
            canvasLines = choice.Canvas;
            factLines = choice.Facts;
            lines = KnowerLines(run, choice.Knowers, heroId, localOfPlaceId);
            speaks = AuthoredFlags.Said(choice.Canvas, run.Flags).Any() || AuthoredFlags.Said(choice.Facts, run.Flags).Any();
         }

         if (lines.Count == 0 && !speaks) return null;

         var sb = new StringBuilder();
         sb.AppendLine($"A MATTER YOU ARE PART OF ({AuthoredQuestEngine.Fill(run, q.Title)}):");
         foreach (string l in lines) sb.AppendLine("- " + AuthoredQuestEngine.Fill(run, l));

         if (speaks)
         {
            string canvas = string.Join(" ", AuthoredFlags.Said(canvasLines, run.Flags));
            if (!string.IsNullOrWhiteSpace(canvas)) sb.AppendLine("This moment: " + AuthoredQuestEngine.Fill(run, canvas));
            foreach (string f in AuthoredFlags.Said(factLines, run.Flags)) sb.AppendLine("Fact: " + AuthoredQuestEngine.Fill(run, f));
            if (offering) sb.AppendLine("You raise this with the player now and ask for their help; they may accept or refuse.");
         }

         sb.Append(Rule);

         return sb.ToString();
      }

      /// <summary>
      ///   The narrator of an exploration scene: plain voice, second person; the opening; what is there to find (not yet
      ///   found), what the player's own choices could spring, who may be met, the ways out of the scene open now; and
      ///   how to report each.
      /// </summary>
      public static string Narrator(AuthoredQuestRun run, AuthoredQuest q, SceneSurroundingsFacts surroundings = null)
      {
         AuthoredScene scene = AuthoredQuestEngine.Current(run, q)?.Objective?.Scene;
         if (scene == null) return null;

         var sb = new StringBuilder();
         sb.AppendLine("You are the NARRATOR of a scene in Calradia, not a character. Write in the second person (\"you\"), in plain, clear "
                       + "prose: short sentences, concrete things seen and heard, no flourish. Describe; never decide for the player. End each "
                       + "reply by letting the player choose what they do next.");
         // fkasad (07/10/2026): "the dark wood to your left", four times, while he followed the tracks on the road.
         sb.AppendLine("Never steer the player toward what is to be found: they go where they choose, and may miss it. Follow what "
                       + "they do; mention a place or a sign once, and do not bring it up again unless they turn to it.");
         sb.AppendLine();
         sb.AppendLine("THE SCENE: " + AuthoredQuestEngine.Fill(run, scene.Opening.Canvas));
         foreach (string f in scene.Opening.Facts ?? new List<string>()) sb.AppendLine("Fact: " + AuthoredQuestEngine.Fill(run, f));
         // fkasad (07/10/2026): the same wood reads differently to an Aserai at night and a Battanian at noon.
         string around = SceneSurroundingsText.Compose(surroundings);
         if (around != null) sb.AppendLine(around);

         List<AuthoredFinding> left = scene.Findings.Where(f => !run.Findings.Contains(f.Id)).ToList();
         if (left.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("WHAT IS THERE TO BE FOUND (reveal each only when the player's own actions bring them to it, and write its marker on its own when you do):");
            foreach (AuthoredFinding f in left) sb.AppendLine($"- {AuthoredQuestEngine.Fill(run, f.Text)} Marker: [[found:{f.Id}]]");
         }

         List<AuthoredHazard> armed = scene.Hazards.Where(h => !run.HazardsSprung.Contains(h.Id)).ToList();
         if (armed.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("DANGERS (spring one ONLY if the player does what triggers it; a careful player avoids it; never invent another):");
            foreach (AuthoredHazard h in armed) sb.AppendLine($"- If the player is {AuthoredQuestEngine.Fill(run, h.Trigger)}. Marker: [[hazard:{h.Id}]]");
         }

         List<AuthoredEncounter> unmet = scene.Encounters.Where(e => !run.Met.Contains(e.Id)).ToList();
         if (unmet.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("WHO MAY BE MET (bring them in only when it follows from what the player does):");
            foreach (AuthoredEncounter e in unmet)
               sb.AppendLine($"- {AuthoredQuestEngine.Fill(run, e.Name)}: {AuthoredQuestEngine.Fill(run, e.Persona)}. Marker: [[meet:{e.Id}]]");
         }

         List<AuthoredEncounter> met = scene.Encounters.Where(e => run.Met.Contains(e.Id)).ToList();
         if (met.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("WHO IS WITH THE PLAYER NOW (give them their own voice, in quotes, as themselves; they say only what they know):");
            foreach (AuthoredEncounter e in met)
            {
               sb.AppendLine($"- {AuthoredQuestEngine.Fill(run, e.Name)}: {AuthoredQuestEngine.Fill(run, e.Persona)}.");
               foreach (string k in e.Knows ?? new List<string>()) sb.AppendLine("  They know: " + AuthoredQuestEngine.Fill(run, k));
            }
         }

         IReadOnlyList<AuthoredChoice> ways = AuthoredQuestEngine.SceneChoices(run, q);
         if (ways.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("WAYS THE PLAYER MAY TAKE OUT OF THIS SCENE (take one ONLY when the player's own words choose it, never for them; "
                          + "when they do, narrate their first steps on it, write its marker on its own, and end there: the scene closes):");
            foreach (AuthoredChoice c in ways) sb.AppendLine($"- {AuthoredQuestEngine.Fill(run, c.Label)}. Marker: [[choice:{c.Id}]]");
         }

         // tashmetu (07/10/2026): once everything required was found, the narrator kept asking "What do you do?" forever.
         if (AuthoredQuestEngine.SceneMayEnd(run, q))
         {
            sb.AppendLine();
            sb.AppendLine("EVERYTHING THE PLAYER NEEDED HERE HAS BEEN FOUND. Do not keep asking what they do next: bring the scene to a "
                          + "natural pause and tell them, in a line, that they may leave when they wish (ending the scene is theirs to do).");
         }

         sb.AppendLine();
         sb.Append("Never invent a finding, a danger, a person or a way out not listed. Nothing in your narration gives gold, starts a fight or harms "
                   + "anyone beyond a listed danger; the game carries those out.");

         return sb.ToString();
      }

      /// <summary>The [ACTION] type the interpreter reports a choice by words with.</summary>
      public const string StoryChoiceVerb = "story_choice";

      /// <summary>
      ///   For whoever reads the turn's actions (branching tales, 04/10/2026): the choices by words open in a conversation
      ///   with this character, each id with what the player's words must mean, and how to report one. Null when none is
      ///   open: a conversation with no such choice teaches nothing. In Enhanced only the interpreter reads it; in Standard
      ///   the character's own call does, told not to announce or steer toward them (Gabriel, 25/10/2026). The game
      ///   accepts only a declared, open id said to this very character.
      /// </summary>
      public static string ChoicesForInterpreter(AuthoredQuestRun run, AuthoredQuest q, string heroId)
      {
         IReadOnlyList<AuthoredChoice> open = AuthoredQuestEngine.SaidChoicesFor(run, q, heroId);
         if (open.Count == 0) return null;

         var sb = new StringBuilder();
         sb.AppendLine("STORY CHOICES THE PLAYER MAY TAKE BY WORDS IN THIS CONVERSATION (a written tale; only these count):");
         // The id alone opens the line: "- choice voice:" was read back as "choice_voice" (tashmetu, 07/10/2026).
         foreach (AuthoredChoice c in open) sb.AppendLine($"- {c.Id}: {AuthoredQuestEngine.Fill(run, c.Said.Meaning)}");
         sb.AppendLine("Report one ONLY when the PLAYER's own words in this exchange plainly say or agree to what it means and the reply "
                       + "accepts it; never for an offer still weighed, a question, a refusal, a jest or a promise to think about it; at most "
                       + "one per reply. Report it as:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: " + StoryChoiceVerb);
         sb.AppendLine("choice: <the choice id>");
         sb.Append("[/ACTION]");

         return sb.ToString();
      }

      private static List<string> KnowerLines(AuthoredQuestRun run, Dictionary<string, List<AuthoredLine>> knowers, string heroId, string localOfPlaceId)
      {
         var lines = new List<string>();
         foreach (KeyValuePair<string, List<AuthoredLine>> k in knowers ?? new Dictionary<string, List<AuthoredLine>>())
         {
            string key = k.Key ?? "";
            bool matches = key.StartsWith("locals_of:")
               ? localOfPlaceId != null && localOfPlaceId == AuthoredQuestEngine.Bound(run, key.Substring("locals_of:".Length))
               : heroId == AuthoredQuestEngine.Bound(run, key);
            if (matches) lines.AddRange(AuthoredFlags.Said(k.Value, run.Flags));
         }

         return lines;
      }
   }

   /// <summary>A narration with its game markers taken out.</summary>
   public sealed class SceneMarkers
   {
      private static readonly Regex Marker = new(@"\[\[(found|hazard|meet|choice):([A-Za-z0-9_]+)\]\]", RegexOptions.Compiled);

      public string Text { get; private set; } = "";
      public List<string> Found { get; } = new();
      public List<string> Hazards { get; } = new();
      public List<string> Met { get; } = new();

      /// <summary>The ways out of the scene the narration took (branching tales, 04/10/2026); the engine accepts only the first open one.</summary>
      public List<string> Choices { get; } = new();

      /// <summary>The text the player reads, and the markers the engine checks.</summary>
      public static SceneMarkers Extract(string narration)
      {
         var m = new SceneMarkers();
         if (string.IsNullOrEmpty(narration)) return m;

         foreach (Match x in Marker.Matches(narration))
         {
            List<string> into = x.Groups[1].Value switch {
               "found" => m.Found,
               "hazard" => m.Hazards,
               "choice" => m.Choices,
               _ => m.Met
            };
            into.Add(x.Groups[2].Value);
         }

         string clean = Marker.Replace(narration, "");
         clean = Regex.Replace(clean, @"[ \t]{2,}", " ");
         clean = Regex.Replace(clean, @"[ \t]+\n", "\n");
         m.Text = clean.Trim();

         return m;
      }
   }
}
