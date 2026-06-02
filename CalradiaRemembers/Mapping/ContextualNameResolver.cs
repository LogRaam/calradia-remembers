// Code written by Gabriel Mailhot, 01/06/2026. Sprint 10.5 — contextual name injection.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Scans the player's message for hero names and produces a ready-to-inject
    ///   hint block for the LLM system prompt. Each match is categorised from the
    ///   NPC's perspective (close friend, enemy, family, clanmate, or stranger) so
    ///   the NPC can answer questions about third parties accurately. When multiple
    ///   heroes share the same first name the hint invites the NPC to ask which one
    ///   the player means — producing natural in-character disambiguation.
    /// </summary>
    public static class ContextualNameResolver
    {
        private const int MinTokenLength = 3;
        private const int FriendThreshold = 10;
        private const int EnemyThreshold = -10;

        // Common words filtered out of both hero names and player messages.
        private static readonly HashSet<string> StopWords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // English
                "the", "and", "but", "for", "you", "your", "him", "her", "his",
                "she", "they", "their", "who", "what", "how", "why", "when",
                "where", "this", "that", "with", "from", "have", "been", "not",
                "are", "was", "were", "will", "can", "did", "has", "had", "out",
                "know", "met", "seen", "hear", "tell", "talk", "about",
                // French
                "les", "des", "que", "qui", "une", "est", "pas", "sur", "par",
                "pour", "dans", "avec", "son", "ses", "lui", "elle", "ils", "eux",
                "ces", "notre", "votre", "leur", "tout", "bien", "mais", "donc",
                "aussi", "même", "plus", "très", "chez", "comme", "encore", "déjà",
                "connu", "connais", "rencontré", "parler",
                // Bannerlord name connectors
                "ibn", "bint", "of", "de", "al", "van", "von"
            };

        /// <summary>
        ///   Returns a hint block string (multi-line, ready for prompt injection) when
        ///   hero names are found in <paramref name="playerMessage" />, or null when none
        ///   are found. The block is labelled <c>NAMES MENTIONED IN THE PLAYER'S MESSAGE:</c>.
        /// </summary>
        public static string Resolve(Hero npc, string playerMessage)
        {
            if (npc == null || string.IsNullOrWhiteSpace(playerMessage)) return null;

            HashSet<string> messageTokens = ExtractTokens(playerMessage);
            if (messageTokens.Count == 0) return null;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matches = new List<(string FullName, string Relationship)>();

            foreach (Hero other in Hero.AllAliveHeroes)
            {
                if (other == npc || other.IsHumanPlayerCharacter) continue;

                string fullName = other.Name?.ToString();
                if (string.IsNullOrEmpty(fullName)) continue;
                if (seen.Contains(other.StringId)) continue;

                if (NameSharesTokenWithMessage(fullName, messageTokens))
                {
                    seen.Add(other.StringId);
                    matches.Add((fullName, Categorize(npc, other)));
                }
            }

            return matches.Count == 0 ? null : BuildHint(matches);
        }

        #region private

        private static bool NameSharesTokenWithMessage(string heroName, HashSet<string> messageTokens)
        {
            foreach (string part in heroName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length < MinTokenLength) continue;
                if (IsStopWord(part)) continue;
                if (messageTokens.Contains(part)) return true;
            }
            return false;
        }

        private static string Categorize(Hero npc, Hero other)
        {
            if (npc.Spouse == other) return "your spouse";
            if (npc.Father == other) return "your father";
            if (npc.Mother == other) return "your mother";
            if (npc.Children != null && npc.Children.Any(c => c == other)) return "your child";

            int relation = CharacterRelationManager.GetHeroRelation(npc, other);

            if (relation >= 30) return $"a very close friend ({DescribeRelation(relation)})";
            if (relation >= FriendThreshold) return $"a friend ({DescribeRelation(relation)})";
            if (relation <= -30) return $"a bitter enemy ({DescribeRelation(relation)})";
            if (relation <= EnemyThreshold) return $"an enemy ({DescribeRelation(relation)})";
            if (npc.Clan != null && npc.Clan == other.Clan) return "a member of your clan";
            if (relation != 0) return $"an acquaintance ({DescribeRelation(relation)})";

            return "not personally known to you";
        }

        private static string BuildHint(List<(string FullName, string Relationship)> matches)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NAMES MENTIONED IN THE PLAYER'S MESSAGE:");

            if (matches.Count == 1)
            {
                sb.Append(
                    $"The player appears to be referring to {matches[0].FullName}" +
                    $" — {matches[0].Relationship}.");
            }
            else
            {
                sb.AppendLine(
                    "The following people may be relevant to what the player just said:");
                foreach (var (name, rel) in matches)
                    sb.AppendLine($"  - {name} — {rel}");

                if (HasAmbiguousFirstName(matches))
                    sb.Append(
                        "If uncertain which person the player means, ask for clarification in character.");
            }

            return sb.ToString().TrimEnd();
        }

        private static bool HasAmbiguousFirstName(
            List<(string FullName, string Relationship)> matches)
        {
            var firstNames = matches
                .Select(m => m.FullName.Split(' ')[0])
                .ToList();
            return firstNames.Count != firstNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static HashSet<string> ExtractTokens(string message)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string word in Regex.Split(message, @"[^\p{L}]+"))
            {
                if (word.Length < MinTokenLength) continue;
                if (IsStopWord(word)) continue;
                tokens.Add(word);
            }
            return tokens;
        }

        private static bool IsStopWord(string word) => StopWords.Contains(word);

        private static string DescribeRelation(int r) => r switch
        {
            >=  30 => "deep trust",
            >=  10 => "genuine regard",
            >=  -9 => "neutral",
            >= -29 => "quiet distrust",
            _      => "resentment"
        };

        #endregion
    }
}
