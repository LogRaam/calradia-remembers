// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: what each character is told, and the scene narrator's prompt
// (docs/design/AUTHORED_QUESTS.md). Gabriel's rulings: the text is a canvas the model rephrases while keeping its facts;
// a character is told only what they know at this stage (gating before declaration, 09/09/2026); characters never lie
// (20/09/2026); a scene's narrator speaks plainly (03/10/2026). The narrator reports what the game must check through
// markers ([[found:id]], [[hazard:id]], [[meet:id]]) that SceneMarkers strips from the text and the engine validates
// against the file: the model chooses the moment, the game decides whether it counts.

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
      ///   and facts; anyone the stage names as a knower has their own lines, framed as what they believe.
      /// </summary>
      public static string ForCharacter(AuthoredQuestRun run, AuthoredQuest q, string heroId, string localOfPlaceId, bool offering)
      {
         if (run == null || q == null || string.IsNullOrEmpty(heroId)) return null;

         // In the conversation that completed a stage (the report to the lord), the stage spoken of is that one.
         AuthoredStage stage = run.SpokenHeroId == heroId && run.SpokenStageId != null
            ? q.Stages.FirstOrDefault(s => s.Id == run.SpokenStageId)
            : AuthoredQuestEngine.Current(run, q);
         if (stage == null) return null;

         bool isGiver = heroId == AuthoredQuestEngine.Bound(run, "{giver}");
         List<string> lines = KnowerLines(run, stage, heroId, localOfPlaceId);
         bool speaksTheStage = isGiver && (offering || run.SpokenStageId == stage.Id);
         if (lines.Count == 0 && !speaksTheStage) return null;

         var sb = new StringBuilder();
         sb.AppendLine($"A MATTER YOU ARE PART OF ({AuthoredQuestEngine.Fill(run, q.Title)}):");
         foreach (string l in lines) sb.AppendLine("- " + AuthoredQuestEngine.Fill(run, l));

         if (speaksTheStage)
         {
            string canvas = string.Join(" ", AuthoredFlags.Said(stage.Canvas, run.Flags));
            if (!string.IsNullOrWhiteSpace(canvas)) sb.AppendLine("This moment: " + AuthoredQuestEngine.Fill(run, canvas));
            foreach (string f in AuthoredFlags.Said(stage.Facts, run.Flags)) sb.AppendLine("Fact: " + AuthoredQuestEngine.Fill(run, f));
            if (offering) sb.AppendLine("You raise this with the player now and ask for their help; they may accept or refuse.");
         }

         sb.Append(Rule);

         return sb.ToString();
      }

      /// <summary>
      ///   The narrator of an exploration scene: plain voice, second person; the opening; what is there to find (not yet
      ///   found), what the player's own choices could spring, who may be met; and how to report each.
      /// </summary>
      public static string Narrator(AuthoredQuestRun run, AuthoredQuest q)
      {
         AuthoredScene scene = AuthoredQuestEngine.Current(run, q)?.Objective?.Scene;
         if (scene == null) return null;

         var sb = new StringBuilder();
         sb.AppendLine("You are the NARRATOR of a scene in Calradia, not a character. Write in the second person (\"you\"), in plain, clear "
                       + "prose: short sentences, concrete things seen and heard, no flourish. Describe; never decide for the player. End each "
                       + "reply by letting the player choose what they do next.");
         sb.AppendLine();
         sb.AppendLine("THE SCENE: " + AuthoredQuestEngine.Fill(run, scene.Opening.Canvas));
         foreach (string f in scene.Opening.Facts ?? new List<string>()) sb.AppendLine("Fact: " + AuthoredQuestEngine.Fill(run, f));

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

         sb.AppendLine();
         sb.Append("Never invent a finding, a danger or a person not listed. Nothing in your narration gives gold, starts a fight or harms "
                   + "anyone beyond a listed danger; the game carries those out.");

         return sb.ToString();
      }

      private static List<string> KnowerLines(AuthoredQuestRun run, AuthoredStage stage, string heroId, string localOfPlaceId)
      {
         var lines = new List<string>();
         foreach (KeyValuePair<string, List<AuthoredLine>> k in stage.Knowers ?? new Dictionary<string, List<AuthoredLine>>())
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
      private static readonly Regex Marker = new(@"\[\[(found|hazard|meet):([A-Za-z0-9_]+)\]\]", RegexOptions.Compiled);

      public string Text { get; private set; } = "";
      public List<string> Found { get; } = new();
      public List<string> Hazards { get; } = new();
      public List<string> Met { get; } = new();

      /// <summary>The text the player reads, and the markers the engine checks.</summary>
      public static SceneMarkers Extract(string narration)
      {
         var m = new SceneMarkers();
         if (string.IsNullOrEmpty(narration)) return m;

         foreach (Match x in Marker.Matches(narration))
         {
            List<string> into = x.Groups[1].Value == "found" ? m.Found : x.Groups[1].Value == "hazard" ? m.Hazards : m.Met;
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
