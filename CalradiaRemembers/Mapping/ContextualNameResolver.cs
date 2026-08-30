// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

      // Common words filtered out of both hero names and player messages.
      private static readonly HashSet<string> StopWords =
         new(StringComparer.OrdinalIgnoreCase) {
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
      public static string Resolve(Hero npc, string playerMessage, IGameStateBridge bridge = null)
      {
         List<(Hero Hero, string FullName, string Relationship)> mentioned = ResolveMentionedHeroes(npc, playerMessage);

         if (mentioned.Count == 0) return null;

         var matches = new List<(string FullName, string Gender, string Relationship, string Whereabouts, string Prisoner)>();

         foreach (var (other, fullName, relationship) in mentioned)
            matches.Add((fullName, ThirdPartyDescriptor.GenderNoun(other.IsFemale),
               relationship, ResolveWhereabouts(npc, other, bridge), PrisonerStatusClause(npc, other, fullName)));

         return BuildHint(matches);
      }

      /// <summary>
      ///   The heroes named in <paramref name="playerMessage" />, each paired with the display name and
      ///   relation category <see cref="Resolve" /> itself uses. Exposed so <c>ExternalVerbAdapter</c>'s
      ///   pre-response entity resolution (Extension Surface actions guidance) reuses the SAME
      ///   tokenisation/matching this class already does, rather than reimplementing it. Never null; empty
      ///   when <paramref name="npc" /> is null, the message is blank, or nothing matches.
      /// </summary>
      internal static List<(Hero Hero, string FullName, string Relationship)> ResolveMentionedHeroes(Hero npc, string playerMessage)
      {
         var result = new List<(Hero Hero, string FullName, string Relationship)>();

         if (npc == null || string.IsNullOrWhiteSpace(playerMessage)) return result;

         HashSet<string> messageTokens = ExtractTokens(playerMessage);
         if (messageTokens.Count == 0) return result;

         var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         foreach (Hero other in Hero.AllAliveHeroes)
         {
            if (other == npc || other.IsHumanPlayerCharacter) continue;

            var fullName = HeroLabel.Describe(other.Name?.ToString(), other.Clan?.Name?.ToString(), other.Culture?.Name?.ToString());

            if (string.IsNullOrEmpty(fullName)) continue;
            if (seen.Contains(other.StringId)) continue;
            if (!NameSharesTokenWithMessage(fullName, messageTokens)) continue;

            seen.Add(other.StringId);
            result.Add((other, fullName, Categorize(npc, other)));
         }

         return result;
      }

      #region private

      private static string BuildHint(List<(string FullName, string Gender, string Relationship, string Whereabouts, string Prisoner)> matches)
      {
         var sb = new StringBuilder();
         sb.AppendLine("NAMES MENTIONED IN THE PLAYER'S MESSAGE:");

         if (matches.Count == 1)
         {
            sb.Append(
               $"The player appears to be referring to {matches[0].FullName}" +
               $", {matches[0].Gender}, {matches[0].Relationship}.");
            if (!string.IsNullOrEmpty(matches[0].Whereabouts))
               sb.Append($" Whereabouts: {matches[0].Whereabouts}.");
            if (!string.IsNullOrEmpty(matches[0].Prisoner))
               sb.Append($" {matches[0].Prisoner}");
         }
         else
         {
            sb.AppendLine(
               "The following people may be relevant to what the player just said:");
            foreach (var (name, gender, rel, where, prisoner) in matches)
            {
               sb.AppendLine(string.IsNullOrEmpty(where)
                  ? $"  - {name}, {gender}, {rel}"
                  : $"  - {name}, {gender}, {rel}. Whereabouts: {where}");
               if (!string.IsNullOrEmpty(prisoner))
                  sb.AppendLine($"    {prisoner}");
            }

            if (HasAmbiguousFirstName(matches))
               sb.AppendLine(
                  "If uncertain which person the player means, ask for clarification in character.");
         }

         // Anti-correction reminder: the gender stated above is fact. Names in Calradia are often
         // gender-ambiguous, and the NPC must not guess gender from a name or "correct" the player.
         sb.Append(
            "\nThe gender given for each person above is a fact you know. Do not infer gender from a name, " +
            "and never correct the player about someone's gender.");

         // One anti-fabrication reminder when any whereabouts is offered: it is hearsay,
         // possibly stale, and the NPC must not invent a place they were not told.
         if (matches.Any(m => !string.IsNullOrEmpty(m.Whereabouts)))
            sb.Append(
               "\nIf the player asks where someone is, you may share the whereabouts noted above — " +
               "but only as hearsay (you may well be out of date), and never invent a location you were not given.");

         return sb.ToString().TrimEnd();
      }

      private static string Categorize(Hero npc, Hero other)
         => ThirdPartyDescriptor.Categorize(
            CharacterRelationManager.GetHeroRelation(npc, other),
            npc.Spouse == other,
            npc.Father == other,
            npc.Mother == other,
            npc.Children != null && npc.Children.Any(c => c == other),
            npc.Clan != null && npc.Clan == other.Clan,
            AreRivalGangLeaders(npc, other));

      /// <summary>
      ///   Two DIFFERENT gang leaders whose turf is the same settlement are structural rivals, so the NPC is
      ///   told so rather than left to hallucinate a same-culture friendship (player report). Both must be
      ///   gang-leader notables sharing a current settlement.
      /// </summary>
      private static bool AreRivalGangLeaders(Hero npc, Hero other)
      {
         if (npc == null || other == null || npc == other) return false;
         if (npc.Occupation != Occupation.GangLeader) return false;
         if (other.Occupation != Occupation.GangLeader) return false;

         return npc.CurrentSettlement != null && npc.CurrentSettlement == other.CurrentSettlement;
      }

      private static HashSet<string> ExtractTokens(string message)
      {
         var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         foreach (string word in Regex.Split(message, @"[^\p{L}]+"))
         {
            if (word.Length < MinTokenLength) continue;
            if (IsStopWord(word)) continue;

            // Diacritic-folded so "Heloise" (as typed) matches a hero named "Héloïse" — the same
            // folding applied to the hero name parts below in NameSharesTokenWithMessage.
            tokens.Add(NameNormalization.Fold(word));
         }

         return tokens;
      }

      private static bool HasAmbiguousFirstName(
         List<(string FullName, string Gender, string Relationship, string Whereabouts, string Prisoner)> matches)
      {
         List<string> firstNames = matches
                                   .Select(m => m.FullName.Split(' ')[0])
                                   .ToList();

         return firstNames.Count !=
                firstNames
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .Count();
      }

      private static bool IsStopWord(string word) => StopWords.Contains(word);

      // ── Whereabouts (Sprint: third-party location as plausible hearsay) ────
      // An NPC should not omnisciently know where everyone is. We gate the knowledge by
      // the SAME social tie the resolver already computes: a kinsman or close friend can
      // place someone; a faction-mate knows rough military movements; a stranger or distant
      // party does not. Phrasing is hearsay so a stale position reads as honest uncertainty
      // rather than a lie. Live query — no persistence (that is the v1.2 rumor-store layer).

      /// <summary>
      ///   How plausibly <paramref name="npc" /> would know where <paramref name="other" /> is,
      ///   from their social tie alone (Option A — close ties only). Gathers the engine facts and
      ///   delegates the tiering to the pure <see cref="ThirdPartyDescriptor.KnowledgeOf" />.
      /// </summary>
      private static KnowledgeTier KnowledgeOf(Hero npc, Hero other)
         => ThirdPartyDescriptor.KnowledgeOf(
            npc.CurrentSettlement != null && npc.CurrentSettlement == other.CurrentSettlement,
            npc.Spouse == other || npc.Father == other || npc.Mother == other || npc.Children != null && npc.Children.Any(c => c == other) || npc.Clan != null && npc.Clan == other.Clan,
            CharacterRelationManager.GetHeroRelation(npc, other),
            npc.MapFaction != null && npc.MapFaction == other.MapFaction);

      /// <summary>
      ///   A readable location for <paramref name="other" />: the settlement they are in, or
      ///   what they are doing on the map (<see cref="IGameStateBridge.GetCurrentActivity" />,
      ///   which itself stays null when it cannot read a place with confidence). Null when
      ///   nothing concrete can be said.
      /// </summary>
      private static string LocationOf(Hero other, IGameStateBridge bridge)
      {
         Settlement s = other.CurrentSettlement;

         if (s?.Name != null) return $"at {s.Name}";

         string activity = bridge?.GetCurrentActivity(other);

         return string.IsNullOrWhiteSpace(activity)
            ? null
            : activity;
      }

      private static bool NameSharesTokenWithMessage(string heroName, HashSet<string> messageTokens)
      {
         foreach (string part in heroName.Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries))
         {
            if (part.Length < MinTokenLength) continue;
            if (IsStopWord(part)) continue;

            // Folded the same way as the message tokens, so "Heloise the Finger" (typed plain)
            // still matches "Héloïse the Finger" (stored accented).
            if (messageTokens.Contains(NameNormalization.Fold(part))) return true;
         }

         return false;
      }

      /// <summary>
      ///   The whereabouts clause the NPC could plausibly relay about <paramref name="other" />,
      ///   framed by how they would know it, or null when they would not know at all.
      /// </summary>
      private static string ResolveWhereabouts(Hero npc, Hero other, IGameStateBridge bridge)
      {
         KnowledgeTier tier = KnowledgeOf(npc, other);
         string place = tier == KnowledgeTier.None || tier == KnowledgeTier.Present
            ? null
            : LocationOf(other, bridge);

         return ThirdPartyDescriptor.WhereaboutsClause(tier, place, npc.CurrentSettlement?.Name?.ToString());
      }

      /// <summary>
      ///   A CURRENT, hard fact when a mentioned person is a prisoner: whether THIS NPC (or their party/clan) holds
      ///   them, the player holds them, or a third party does. Given so a captor who has just taken someone states
      ///   it plainly instead of inventing a false capture-and-sale backstory (player report 2026-08-16). Null when
      ///   the person is free.
      /// </summary>
      private static string PrisonerStatusClause(Hero npc, Hero other, string fullName)
      {
         if (other == null || !other.IsPrisoner) return null;

         PartyBase party = other.PartyBelongedToAsPrisoner;
         Hero captor = party?.LeaderHero ?? party?.Owner;

         ThirdPartyDescriptor.PrisonerHolder holder;
         string captorLabel = null;

         if (captor == Hero.MainHero || (party != null && party.MobileParty == MobileParty.MainParty))
         {
            holder = ThirdPartyDescriptor.PrisonerHolder.Player;
         }
         else if (captor == npc || (npc?.Clan != null && captor?.Clan == npc.Clan))
         {
            holder = ThirdPartyDescriptor.PrisonerHolder.ThisNpc;
         }
         else
         {
            holder = ThirdPartyDescriptor.PrisonerHolder.Other;
            captorLabel = captor != null
               ? HeroLabel.Describe(captor.Name?.ToString(), captor.Clan?.Name?.ToString(), captor.Culture?.Name?.ToString())
               : party?.Name?.ToString();
         }

         return ThirdPartyDescriptor.PrisonerClause(holder, captorLabel, fullName);
      }

      #endregion
   }
}