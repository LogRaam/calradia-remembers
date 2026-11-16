// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Relations;
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
         List<(Hero Hero, string FullName, string Tie)> lost = DeadKinMentioned(npc, playerMessage);

         if (mentioned.Count == 0 && lost.Count == 0) return null;

         var matches = new List<(string FullName, string Gender, string Relationship, string Standing, string Allegiance, string Whereabouts, string Prisoner)>();

         // The speaker's own dead first: the one a grieving father means when a living namesake shares the name.
         foreach (var (dead, fullName, tie) in lost)
            matches.Add((fullName, ThirdPartyDescriptor.GenderNoun(dead.IsFemale), tie, null, null, null, null));

         // VonLemberg / Goeth (16/11/2026): one shared word admitted every hero bearing it, uncapped. The prompt now carries
         // the most likely meant (MentionedPeoplePolicy) and counts the rest; the verbs' entity resolution still reads the
         // full list from ResolveMentionedHeroes.
         MentionSelection chosen = SelectForPrompt(npc, mentioned, playerMessage);
         var byKey = mentioned.GroupBy(m => m.Hero.StringId).ToDictionary(g => g.Key, g => g.First());
         foreach (string key in chosen.Shown)
         {
            var (other, fullName, relationship) = byKey[key];
            matches.Add((fullName, ThirdPartyDescriptor.GenderNoun(other.IsFemale), relationship, TradeOf(npc, other), AllegianceOf(other),
               ResolveWhereabouts(npc, other, bridge), PrisonerStatusClause(npc, other, fullName)));
         }

         if (matches.Count == 0) return null;

         string hint = BuildHint(matches, chosen.Omitted);
         if (lost.Count > 0 && chosen.Shown.Count > 0)
            hint += $"\n{lost[0].FullName} is {lost[0].Tie.Split('(')[0].Trim()}: unless the player says otherwise, that is who they mean; never ask which one.";

         return hint;
      }

      /// <summary>
      ///   Maifailun via tashmetu (15/10/2026): the player's wife Debana died in childbirth and her father asked "Which
      ///   Debana?", because this scan read living heroes only and found just a living namesake. The speaker's own dead kin
      ///   (children, parents, siblings, spouses), and the player's dead spouses when the speaker is the player's partner, are
      ///   matched too, named with the tie and how they died. Only for this hint: entity resolution for verbs never targets
      ///   the dead.
      /// </summary>
      private static List<(Hero Hero, string FullName, string Tie)> DeadKinMentioned(Hero npc, string playerMessage)
      {
         var result = new List<(Hero Hero, string FullName, string Tie)>();
         if (npc == null || string.IsNullOrWhiteSpace(playerMessage)) return result;

         HashSet<string> messageTokens = ExtractTokens(playerMessage);
         if (messageTokens.Count == 0) return result;

         var kin = new List<(Hero Hero, string Word)>();
         if (npc.Father != null) kin.Add((npc.Father, "father"));
         if (npc.Mother != null) kin.Add((npc.Mother, "mother"));
         foreach (Hero c in npc.Children ?? Enumerable.Empty<Hero>()) kin.Add((c, c.IsFemale ? "daughter" : "son"));
         foreach (Hero s in npc.Siblings ?? Enumerable.Empty<Hero>()) kin.Add((s, s.IsFemale ? "sister" : "brother"));
         foreach (Hero s in npc.ExSpouses ?? Enumerable.Empty<Hero>()) kin.Add((s, s.IsFemale ? "wife" : "husband"));

         Hero player = Hero.MainHero;
         bool partner = player != null && (player.Spouse == npc || player.ExSpouses?.Contains(npc) == true);
         var playerLate = new HashSet<Hero>(partner ? player.ExSpouses?.Where(s => s != npc) ?? Enumerable.Empty<Hero>() : Enumerable.Empty<Hero>());

         List<string> speakerNameTokens = NameTokens(npc.Name?.ToString());
         var seen = new HashSet<Hero>();
         foreach (var (h, word) in kin.Concat(playerLate.Select(h => (h, "")))
                                     .Where(k => k.Item1 is {IsDead: true} && k.Item1 != npc && !k.Item1.IsHumanPlayerCharacter))
         {
            if (!seen.Add(h)) continue;
            string fullName = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h.Name?.ToString(), ClanName = h.Clan?.Name?.ToString(), CultureName = h.Culture?.Name?.ToString()});
            if (string.IsNullOrEmpty(fullName)) continue;

            List<string> matched = MatchedTokens(fullName, messageTokens);
            if (matched.Count == 0 || SpeakerNamesakePolicy.IsMerelyANamesake(matched, speakerNameTokens)) continue;

            string tie = word.Length > 0 ? $"your late {word}" : (h.IsFemale ? "the player's late wife" : "the player's late husband");
            if (word.Length > 0 && playerLate.Contains(h)) tie += h.IsFemale ? ", the player's wife" : ", the player's husband";
            result.Add((h, fullName, tie + RelationshipsBuilder.DeathOnly(h)));
         }

         return result;
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

         // The speaker's OWN name, so their namesakes can be told apart from genuine third parties below.
         // Personal name only, never clan or culture: matching on a clan name is how a player asks about a
         // house, and that question must still find the house.
         List<string> speakerNameTokens = NameTokens(npc.Name?.ToString());

         foreach (Hero other in Hero.AllAliveHeroes)
         {
            if (other == npc || other.IsHumanPlayerCharacter) continue;

            var fullName = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = other.Name?.ToString(), ClanName = other.Clan?.Name?.ToString(), CultureName = other.Culture?.Name?.ToString()});

            if (string.IsNullOrEmpty(fullName)) continue;
            if (seen.Contains(other.StringId)) continue;

            List<string> matched = MatchedTokens(fullName, messageTokens);
            if (matched.Count == 0) continue;

            // Found only through words that also name the person being spoken to: the player was addressing
            // them, not naming somebody else (fkasad, Nexus, 15/09/2026 — a companion named Erevan was
            // answered with two other Erevans, and invited to ask which one was meant).
            if (SpeakerNamesakePolicy.IsMerelyANamesake(matched, speakerNameTokens)) continue;

            seen.Add(other.StringId);
            result.Add((other, fullName, Categorize(npc, other)));
         }

         return result;
      }

      #region private

      /// <summary>The facts MentionedPeoplePolicy weighs for each person found: how they were found and what ties them to the speaker.</summary>
      private static MentionSelection SelectForPrompt(Hero npc, List<(Hero Hero, string FullName, string Relationship)> mentioned, string playerMessage)
      {
         HashSet<string> tokens = ExtractTokens(playerMessage ?? "");
         var candidates = new List<MentionCandidate>();
         foreach (var (other, fullName, _) in mentioned)
            candidates.Add(new MentionCandidate {
               Key = other.StringId,
               IsKin = npc.Spouse == other || npc.Father == other || npc.Mother == other || npc.Children != null && npc.Children.Any(c => c == other),
               IsKnown = npc.Clan != null && npc.Clan == other.Clan || PairBondReader.Points(npc, other) != 0,
               MatchedPersonalName = MatchedTokens(other.Name?.ToString(), tokens).Count > 0,
               MatchedHouse = MatchedTokens(other.Clan?.Name?.ToString(), tokens).Count > 0,
               MatchedCulture = MatchedTokens(other.Culture?.Name?.ToString(), tokens).Count > 0,
               MatchedWords = MatchedTokens(fullName, tokens).Count
            });

         return MentionedPeoplePolicy.Select(candidates);
      }

      /// <summary>The realm the person's house serves now (VonLemberg / Goeth, 16/11/2026: a culture is not an allegiance).</summary>
      private static string AllegianceOf(Hero other)
      {
         Clan clan = other?.Clan;
         if (clan == null) return null;

         return MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {
            HouseName = clan.Name?.ToString(),
            RealmName = clan.Kingdom?.Name?.ToString(),
            IsMercenary = clan.IsUnderMercenaryService,
            RulesTheRealm = clan.Kingdom != null && clan.Kingdom.Leader == other,
            IsFreeCompany = clan.IsMinorFaction,
            IsOutlawBand = clan.IsBanditFaction
         });
      }

      /// <summary>
      ///   A notable's trade and home, said only to a speaker who could know them (VonLemberg / Goeth, 16/11/2026; Gabriel: his
      ///   town and those tied to him): the speaker lives in or stands in that place, or a tie lets them place the notable.
      /// </summary>
      private static string TradeOf(Hero npc, Hero other)
      {
         if (other == null || !other.IsNotable) return null;

         Settlement home = other.HomeSettlement;
         bool couldKnow = home != null && (npc.CurrentSettlement == home || npc.HomeSettlement == home || KnowledgeOf(npc, other) != KnowledgeTier.None);

         return MentionedPeoplePolicy.TradeClause(new NotableTradeFacts {
            OccupationName = other.Occupation.ToString(), HomeName = home?.Name?.ToString(), SpeakerCouldKnow = couldKnow
         });
      }

      private static string BuildHint(List<(string FullName, string Gender, string Relationship, string Standing, string Allegiance, string Whereabouts, string Prisoner)> matches, int omitted)
      {
         var sb = new StringBuilder();
         sb.AppendLine("NAMES MENTIONED IN THE PLAYER'S MESSAGE:");

         if (matches.Count == 1)
         {
            sb.Append(
               $"The player appears to be referring to {matches[0].FullName}" +
               $", {matches[0].Gender}, {matches[0].Relationship}" +
               (string.IsNullOrEmpty(matches[0].Standing) ? "" : $"; {matches[0].Standing}") +
               (string.IsNullOrEmpty(matches[0].Allegiance) ? "." : $"; {matches[0].Allegiance}."));
            if (!string.IsNullOrEmpty(matches[0].Whereabouts))
               sb.Append($" Whereabouts: {matches[0].Whereabouts}.");
            if (!string.IsNullOrEmpty(matches[0].Prisoner))
               sb.Append($" {matches[0].Prisoner}");
         }
         else
         {
            sb.AppendLine(
               "The following people may be relevant to what the player just said:");
            foreach (var (name, gender, rel, standing, allegiance, where, prisoner) in matches)
            {
               string who = $"  - {name}, {gender}, {rel}" + (string.IsNullOrEmpty(standing) ? "" : $"; {standing}") + (string.IsNullOrEmpty(allegiance) ? "" : $"; {allegiance}");
               sb.AppendLine(string.IsNullOrEmpty(where)
                  ? who
                  : $"{who}. Whereabouts: {where}");
               if (!string.IsNullOrEmpty(prisoner))
                  sb.AppendLine($"    {prisoner}");
            }

            if (HasAmbiguousFirstName(matches))
               sb.AppendLine(
                  "If uncertain which person the player means, ask for clarification in character.");

            string left = MentionedPeoplePolicy.OmittedLine(omitted);
            if (left != null) sb.AppendLine(left);
         }

         if (matches.Any(m => !string.IsNullOrEmpty(m.Allegiance)))
            sb.Append("\n" + MentionedPeoplePolicy.CultureIsNotAllegiance);

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

      // What lies between the speaker and the person named, with its reason, wins over the vanilla number when it
      // gives a verdict (NPC_REGARD.md section 9; Gabriel, 17/10/2026: every friend or enemy carries its reason). Kin
      // keep their word and gain the reason; a pair with nothing between them reads as before.
      // tashmetu (Nexus, 04/11/2026): the number is believed only with a reason (PairBondPolicy); a start drawn blind
      // leaves a vague feeling with no cause, and the houses' standing is said apart (Gabriel, 04/11/2026).
      private static string Categorize(Hero npc, Hero other)
      {
         PairBond bond = other == Hero.MainHero ? new PairBond {Points = npc.GetRelation(other)} : PairBondReader.Of(npc, other);
         string label = VanillaCategory(npc, other, bond.Points);
         string clause = PairBondText.Clause(bond);
         if (clause != null) label = $"{label}; {clause}";

         string derived = PairRegardPolicy.Describe(PairRegardReader.Assess(npc, other));
         bool kin = npc.Spouse == other || npc.Father == other || npc.Mother == other || npc.Children != null && npc.Children.Any(c => c == other);
         string said = derived == null ? label : kin ? $"{label}, and {derived}" : derived;

         PairBond houses = other == Hero.MainHero ? null : PairBondReader.OfHouses(npc, other);
         string house = houses == null ? null : PairBondText.HouseClause(houses.Points, npc.Clan?.Leader?.Name?.ToString(), other.Clan?.Leader?.Name?.ToString());

         return house == null ? said : $"{said}; {house}";
      }

      private static string VanillaCategory(Hero npc, Hero other, int believed)
         => ThirdPartyDescriptor.Categorize(new ThirdPartyFacts {
            Relation = believed,
            IsSpouse = npc.Spouse == other,
            IsFather = npc.Father == other,
            IsMother = npc.Mother == other,
            IsChild = npc.Children != null && npc.Children.Any(c => c == other),
            SameClan = npc.Clan != null && npc.Clan == other.Clan,
            AreRivalGangLeaders = AreRivalGangLeaders(npc, other)
         });

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
         List<(string FullName, string Gender, string Relationship, string Standing, string Allegiance, string Whereabouts, string Prisoner)> matches)
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
            PairBondReader.Points(npc, other),
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

      /// <summary>
      ///   WHICH words of a hero's name the player actually used — the evidence for the match, not merely
      ///   that there was one. SpeakerNamesakePolicy needs the words themselves to tell a third party from
      ///   the speaker's own namesake. Empty means no match at all.
      /// </summary>
      private static List<string> MatchedTokens(string heroName, HashSet<string> messageTokens)
      {
         var matched = new List<string>();

         foreach (string part in NameTokens(heroName))
            // Folded the same way as the message tokens, so "Heloise the Finger" (typed plain)
            // still matches "Héloïse the Finger" (stored accented).
            if (messageTokens.Contains(part))
               matched.Add(part);

         return matched;
      }

      /// <summary>The significant, folded words of a name, by the same rules the message is tokenised with.</summary>
      private static List<string> NameTokens(string name)
      {
         var tokens = new List<string>();

         if (string.IsNullOrWhiteSpace(name)) return tokens;

         foreach (string part in name.Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries))
         {
            if (part.Length < MinTokenLength) continue;
            if (IsStopWord(part)) continue;

            tokens.Add(NameNormalization.Fold(part));
         }

         return tokens;
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
               ? HeroLabel.Describe(new HeroLabel.HeroNaming {Name = captor.Name?.ToString(), ClanName = captor.Clan?.Name?.ToString(), CultureName = captor.Culture?.Name?.ToString()})
               : party?.Name?.ToString();
         }

         return ThirdPartyDescriptor.PrisonerClause(holder, captorLabel, fullName);
      }

      #endregion
   }
}