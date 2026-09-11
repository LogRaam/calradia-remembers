// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (SuperGraham339, 2026-07-10): NPCs have a poor sense of geography and invent where places sit
// (a lord described a Vlandian border castle as lying "toward Empire territory" when the game's own encyclopedia
// says its pass leads to Battania). The game already holds authoritative, always-current geography, so rather
// than a hand-authored lorebook that would drift, this grounds the NPC from the engine: the encyclopedia blurb
// of where they stand, plus, for any settlement the player names, its blurb and a bearing computed from the map.

#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>
   ///   Builds a ready-to-inject geography note for the LLM prompt from live map data. Includes the encyclopedia
   ///   blurb of the settlement the NPC currently stands in, and for each settlement named in the player's
   ///   message a compact fact line (culture, holder, bearing from the NPC) followed by its own blurb. Returns
   ///   null when nothing concrete can be said. The prompt header and the "treat as fact" rule are added by the
   ///   SDK's <c>PromptBuilder</c>; this returns only the body.
   /// </summary>
   public static class SettlementContextResolver
   {
      private const int MinTokenLength = 3;

      // Cap the mentioned-place facts so a message full of place names cannot bloat the prompt.
      private const int MaxMentionedPlaces = 3;

      // A place name rarely collides with a common word, but a few guards stop "town"/"ville" and the like
      // from matching every hamlet named that.
      private static readonly HashSet<string> StopWords =
         new(StringComparer.OrdinalIgnoreCase) {
            "the", "and", "for", "you", "your", "this", "that", "with", "from", "have", "are", "was",
            "les", "des", "que", "une", "est", "pour", "dans", "avec",
            "town", "castle", "village", "ville", "place", "here", "there"
         };

      /// <summary>
      ///   The geography-grounding body, or null when the NPC has no current settlement blurb and the player
      ///   named no recognisable place. <paramref name="playerMessage" /> may be null (only the self-blurb
      ///   is then produced).
      /// </summary>
      public static string Resolve(Hero npc, string playerMessage)
      {
         if (npc == null) return null;

         var sb = new StringBuilder();

         Settlement here = npc.CurrentSettlement;
         if (here != null)
         {
            string blurb = Blurb(here);
            if (!string.IsNullOrEmpty(blurb))
               sb.AppendLine($"You stand in {here.Name}: {blurb}");
         }

         foreach (Settlement s in MatchMentioned(playerMessage, here))
            sb.AppendLine($"- {FactLine(npc, s)}");

         string note = sb.ToString().TrimEnd();

         return string.IsNullOrWhiteSpace(note) ? null : note;
      }

      #region private

      /// <summary>A settlement's authoritative encyclopedia blurb, trimmed, or null when it has none.</summary>
      private static string Blurb(Settlement s)
      {
         string ency = s.EncyclopediaText?.ToString();

         return string.IsNullOrWhiteSpace(ency) ? null : ency.Trim();
      }

      /// <summary>Culture, kind and holder, a bearing from the NPC, then the settlement's own blurb.</summary>
      private static string FactLine(Hero npc, Settlement s)
      {
         string kind = s.IsTown ? "town" : s.IsCastle ? "castle" : s.IsVillage ? "village" : "holding";
         string culture = s.Culture?.Name?.ToString();
         string cultureClause = string.IsNullOrEmpty(culture) ? $"a {kind}" : $"a {culture} {kind}";

         string faction = s.MapFaction?.Name?.ToString();
         string holder = string.IsNullOrEmpty(faction) ? "" : $" held by {faction}";

         string bearing = Bearing(npc, s);
         string bearingClause = string.IsNullOrEmpty(bearing) ? "" : $", {bearing}";

         string blurb = Blurb(s);
         string blurbClause = string.IsNullOrEmpty(blurb) ? "" : $" {blurb}";

         return $"{s.Name}: {cultureClause}{holder}{bearingClause}.{blurbClause}";
      }

      /// <summary>
      ///   A human bearing (direction + coarse distance) of <paramref name="target" /> from where the NPC
      ///   stands. Null when the NPC is not in a settlement (no fixed reference point) or the target IS that
      ///   settlement.
      /// </summary>
      private static string Bearing(Hero npc, Settlement target)
      {
         Settlement basis = npc.CurrentSettlement;

         if (basis == null || basis == target) return null;

         Vec2 from = basis.GetPosition2D;
         Vec2 to = target.GetPosition2D;

         return GeographyDescriber.Bearing(new MapPoint(from.X, from.Y).StepTo(new MapPoint(to.X, to.Y)), from.Distance(to));
      }

      /// <summary>
      ///   Settlements whose name shares a token with the player's message (capped, deduped). Internal (not
      ///   private) so <c>ExternalVerbAdapter</c>'s pre-response entity resolution (Extension Surface actions
      ///   guidance) reuses this exact matching instead of reimplementing it.
      /// </summary>
      internal static List<Settlement> MatchMentioned(string playerMessage, Settlement exclude)
      {
         var result = new List<Settlement>();

         if (string.IsNullOrWhiteSpace(playerMessage)) return result;

         HashSet<string> tokens = ExtractTokens(playerMessage);

         if (tokens.Count == 0) return result;

         var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         foreach (Settlement s in Settlement.All)
         {
            if (s == null || s == exclude) continue;
            if (!(s.IsTown || s.IsCastle || s.IsVillage)) continue;

            var name = s.Name?.ToString();

            if (string.IsNullOrEmpty(name)) continue;
            if (!seen.Add(s.StringId)) continue;
            if (!NameSharesToken(name, tokens)) continue;

            result.Add(s);
            if (result.Count >= MaxMentionedPlaces) break;
         }

         return result;
      }

      private static HashSet<string> ExtractTokens(string message)
      {
         var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         foreach (string word in Regex.Split(message, @"[^\p{L}]+"))
         {
            if (word.Length < MinTokenLength) continue;
            if (StopWords.Contains(word)) continue;

            // Diacritic-folded so a place typed plainly still matches its accented map name (same folding
            // ContextualNameResolver applies to hero names).
            tokens.Add(NameNormalization.Fold(word));
         }

         return tokens;
      }

      private static bool NameSharesToken(string name, HashSet<string> tokens)
      {
         foreach (string part in name.Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries))
         {
            if (part.Length < MinTokenLength) continue;
            if (StopWords.Contains(part)) continue;
            if (tokens.Contains(NameNormalization.Fold(part))) return true;
         }

         return false;
      }

      #endregion
   }
}
