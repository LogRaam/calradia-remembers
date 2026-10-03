// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
    /// <summary>Who is voicing the assessment: a trained companion strategist, or a common soldier standing in when no companion is at hand.</summary>
    public enum AdvisorKind
    {
        CompanionStrategist,
        CommonSoldier
    }

    /// <summary>The voice of the Strategic Assessment: their name, culture, Tactics skill (which sets the counsel's quality), and whether they are a trained strategist or a plain soldier.</summary>
    public sealed class StrategicAdvisor
    {
        public StrategicAdvisor(string name, string culture, int tacticsSkill, AdvisorKind kind = AdvisorKind.CompanionStrategist, string persona = "")
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? kind == AdvisorKind.CommonSoldier ? "a grizzled veteran of your warband" : "your war council"
                : name.Trim();
            Culture = culture?.Trim() ?? "";
            TacticsSkill = tacticsSkill;
            Kind = kind;
            Persona = persona?.Trim() ?? "";
        }

        public string Name { get; }
        public string Culture { get; }
        public int TacticsSkill { get; }
        public AdvisorKind Kind { get; }

        /// <summary>
        ///   The companion's own character in their own words (their trait archetype, culture/rank, and any authored
        ///   backstory / conviction), so the counsel is voiced as THAT person, not a faceless oracle. Empty for a
        ///   common soldier, who speaks only as a plain veteran.
        /// </summary>
        public string Persona { get; }

        public bool HasPersona => !string.IsNullOrWhiteSpace(Persona);

        public AdvisorTacticsTier Tier => TacticsAdvicePolicy.TierFor(TacticsSkill);
    }

    /// <summary>One war under way between two realms, named, so the advisor can reason about the actual conflicts rather than inventing them.</summary>
    public sealed class WarPair
    {
        public WarPair(string a, string b)
        {
            A = a?.Trim() ?? "";
            B = b?.Trim() ?? "";
        }

        public string A { get; }
        public string B { get; }
    }

    /// <summary>The wars presently under way across Calradia (who fights whom): the ONLY conflicts the advisor may speak of.</summary>
    public sealed class CalradiaWarState
    {
        public CalradiaWarState(IReadOnlyList<WarPair> wars) => Wars = wars ?? new List<WarPair>();

        public IReadOnlyList<WarPair> Wars { get; }

        public bool Any => Wars.Count > 0;
    }

    /// <summary>The player's own standing, so the advisor counsels FOR the player's realm rather than describing the world neutrally.</summary>
    public sealed class PlayerStrategicContext
    {
        public PlayerStrategicContext(string playerKingdomName, int playerKingdomRank, IReadOnlyList<string> atWarWith)
        {
            PlayerKingdomName = playerKingdomName?.Trim() ?? "";
            PlayerKingdomRank = playerKingdomRank;
            AtWarWith = atWarWith ?? new List<string>();
        }

        /// <summary>The kingdom the player belongs to, or empty when the player is landless / holds no realm.</summary>
        public string PlayerKingdomName { get; }

        /// <summary>1-based rank of the player's kingdom in the field, or 0 when they hold no realm.</summary>
        public int PlayerKingdomRank { get; }

        /// <summary>Names of the kingdoms the player's realm is at war with (may be empty).</summary>
        public IReadOnlyList<string> AtWarWith { get; }

        public bool HasRealm => !string.IsNullOrEmpty(PlayerKingdomName);
    }

    /// <summary>
    ///   The world the counsel is given in, from the player's own lore files (fkasad, 01/10/2026: the setting's name, World.txt,
    ///   the post-history instructions he uses to give his characters a style). All optional: empty reads as Calradia.
    /// </summary>
    public sealed class StrategicWorld
    {
        /// <summary>The world's name (the setting profile's), or empty for Calradia.</summary>
        public string Name { get; set; } = "";

        /// <summary>The world description (World.txt), trimmed in the prompt.</summary>
        public string Description { get; set; } = "";

        /// <summary>The player's post-history instructions, trimmed in the prompt.</summary>
        public string PostHistoryInstructions { get; set; } = "";
    }

    /// <summary>
    ///   Builds the one-shot LLM prompt for the World Almanac's Strategic Assessment: a companion strategist counsels
    ///   the player on how the realms stand, what is likely to come, and what to do. Pure so the load-bearing parts
    ///   are unit-tested: the advisor persona is present, the counsel quality scales with Tactics, the facts are the
    ///   REAL computed standings, the player's own position frames the advice, and the model is told never to invent
    ///   a fact. The engine seam (pick the advisor, gather the player's wars, fire the async call) is a later
    ///   increment; this only shapes the text.
    /// </summary>
    public static class StrategicAssessmentPromptBuilder
    {
        /// <summary>How many kingdoms to lay before the advisor: enough to judge the field, not so many the prompt bloats.</summary>
        public const int MaxKingdomsListed = 8;

        /// <summary>The global narrative style is trimmed to this in the counsel prompt, so a long style file cannot crowd out the facts.</summary>
        public const int MaxNarrativeStyleChars = 1200;

        /// <summary>The world description and the post-history instructions are each trimmed to this, so they colour the counsel without crowding out the facts.</summary>
        public const int MaxLoreChars = 800;

        public static string Build(PowerIndexResult result, PlayerStrategicContext player, StrategicAdvisor advisor,
            CalradiaWarState wars = null, string narrativeStyle = null, StrategicWorld world = null)
        {
            string worldName = string.IsNullOrWhiteSpace(world?.Name) ? "Calradia" : world.Name.Trim();

            result ??= new PowerIndexResult();
            player ??= new PlayerStrategicContext("", 0, null);
            advisor ??= new StrategicAdvisor("", "", 0);
            wars ??= new CalradiaWarState(null);

            var sb = new StringBuilder();

            // Persona + the quality dial: WHO is speaking and how keen their counsel is. A companion counsels as
            // THEMSELVES, in their own character (their trait archetype + any authored backstory/conviction), so the
            // voice matches how they speak in the chatroom; a common soldier stands in when no companion is at hand,
            // and speaks plainly, without a strategist's polish. Either way the Tactics tier sets how keen the counsel is.
            if (advisor.Kind == AdvisorKind.CommonSoldier)
            {
                sb.Append($"You are {advisor.Name}, a common soldier in your lord's warband, no trained strategist, but you have marched and fought across {worldName}. Speak plainly, as a soldier would, not with an officer's polish. ");
            }
            else if (advisor.HasPersona)
            {
                sb.Append($"You are {advisor.Name}, the companion your lord trusts to read the war. This is who you are: {advisor.Persona} Counsel in your own voice, true to that character. You are {TacticsAdvicePolicy.LabelFor(advisor.Tier)}. ");
            }
            else
            {
                string cultureClause = string.IsNullOrEmpty(advisor.Culture) ? "" : $" a {advisor.Culture} officer,";
                sb.Append($"You are {advisor.Name},{cultureClause} the strategist counselling your lord. You are {TacticsAdvicePolicy.LabelFor(advisor.Tier)}. ");
            }

            sb.Append(TacticsAdvicePolicy.GuidanceFor(advisor.Tier));
            AppendNarrativeStyle(sb, narrativeStyle);
            sb.AppendLine();
            sb.AppendLine();

            // Grounded facts: the REAL standings. Everything the advisor may cite lives here.
            AppendWorldDescription(sb, world);
            sb.AppendLine($"THE STATE OF {worldName.ToUpperInvariant()} (verified; cite ONLY what is written here, never invent a number, battle, or event):");
            AppendStandings(sb, result);
            AppendKeyIndicators(sb, result.Indicators);
            AppendTotals(sb, result.Totals);
            AppendWars(sb, wars);
            sb.AppendLine();

            // The player's own position, so the counsel is FOR them.
            sb.AppendLine("YOUR LORD'S POSITION:");
            AppendPlayerPosition(sb, player, result);
            sb.AppendLine();

            // What the advisor DOES NOT know: there is no battle ledger yet, so any talk of a realm being "war-weary"
            // or "bled dry" would be pure invention. Fence it off explicitly, or the model fills the silence with a war
            // history it has no basis for (an early counsel claimed a realm's "wars have bled them dry" from nothing).
            // fkasad (01/10/2026): "A low number of victories does not necessarily mean a high number of defeats. Some
            // factions are at war without actually engaging in battle... the advisor's analysis 'no victories = you are
            // losing'." The recent counts are now in the standings; what they do NOT mean is said here.
            sb.AppendLine("WHAT YOU DO NOT KNOW: beyond the recent battle counts above, you keep no record of past battles, "
                        + "casualties, or how a war began. Few or no victories does NOT mean a realm is losing: it may simply "
                        + "not have fought, for want of a border, through distance, or with nothing worth besieging. A war goes "
                        + "badly only where defeats outnumber victories or holdings are lost. Never claim a realm is war-weary, "
                        + "bled dry, exhausted, or has won or lost a war. The active wars listed above are the only conflicts you "
                        + "know of; whether one goes well is yours to GUESS from the forces, holdings and those counts, and you "
                        + "must voice it as a guess, not a fact.");
            sb.AppendLine();

            // The task: COUNSEL, not a recital. The player can already read the figures off this very panel, so the
            // advisor must add judgment they could not: the danger, the opening, the move, weighed for the player's own
            // realm, including the hard options (find a strong ally, or leave a failing banner). Grounded throughout.
            sb.Append("Your lord can already SEE these figures on the table before them, so do NOT merely recite the rankings or numbers. ");
            sb.Append("Give COUNSEL they could not read off the panel themselves: name the real danger, the opening, and the move to make. ");
            sb.Append("Weigh your lord's OWN realm honestly: if it is strong, how to press the advantage; if it is weak or losing ground, say so plainly and offer real choices, whether to seek a powerful ally against a shared enemy, or to leave for a stronger banner before this one falls. ");
            sb.Append("Speak in three to five sentences, in the second person and in character. ");
            // The narrative style may add *stage directions*; keep the grammatical person straight, or the model
            // writes "*You tap the map*" about ITSELF and addresses the advisor (player report 2026-09-04).
            sb.Append("The word 'you' always means your lord and no one else. Any stage direction or description of your OWN gesture or expression must be written in the THIRD PERSON (name yourself, or use he or she), never 'you' about yourself. ");
            sb.Append("Every judgment and recommendation must rest on a force, holding, or active war listed above; your reading of them is YOUR judgment, be as incisive as your skill allows and no more. ");
            sb.Append("Never state a number, war, or event that is not written above.");
            AppendPostHistory(sb, world);

            return sb.ToString();
        }

        #region private

        private static void AppendStandings(StringBuilder sb, PowerIndexResult result)
        {
            IReadOnlyList<KingdomPowerScore> ranked = result.Ranked ?? new List<KingdomPowerScore>();
            if (ranked.Count == 0)
            {
                sb.AppendLine("- No kingdom holds the field; Calradia is without a reigning power.");

                return;
            }

            var shown = 0;
            foreach (KingdomPowerScore k in ranked)
            {
                if (k == null) continue;
                if (shown >= MaxKingdomsListed) break;

                (string highName, double highVal) = TopPillar(k);
                (string lowName, double lowVal) = BottomPillar(k);
                sb.AppendLine(
                    $"- {k.Rank}. {NameOr(k.KingdomName)}: power {Round(k.PowerIndex)}. " +
                    $"Strongest in {highName} ({Round(highVal)}), weakest in {lowName} ({Round(lowVal)}). {BattleRecord(k)}");
                shown++;
            }
        }

        private static void AppendKeyIndicators(StringBuilder sb, KeyIndicators ind)
        {
            if (ind == null) return;

            var parts = new List<string>();
            if (ind.MostPowerfulArmyTroops > 0) parts.Add($"largest army holds {ind.MostPowerfulArmyTroops:N0} soldiers");
            if (ind.LargestTerritorySettlements > 0) parts.Add($"largest realm holds {ind.LargestTerritorySettlements} settlements");
            if (ind.StrongestEconomyWealth > 0) parts.Add($"richest treasury near {ind.StrongestEconomyWealth:N0} denars");
            if (!string.IsNullOrWhiteSpace(ind.MostInfluentialLeaderName))
                parts.Add($"most influential ruler is {ind.MostInfluentialLeaderName} ({ind.MostInfluentialLeaderInfluence} influence)");
            if (ind.BestWarRecordVictories > 0 || ind.BestWarRecordDefeats > 0)
                parts.Add($"best war record stands at {ind.BestWarRecordVictories}-{ind.BestWarRecordDefeats}");

            if (parts.Count > 0) sb.AppendLine($"- At a glance: {string.Join("; ", parts)}.");
        }

        private static void AppendTotals(StringBuilder sb, PowerFieldTotals totals)
        {
            if (totals == null) return;
            if (totals.TotalTroops <= 0 && totals.TotalSettlements <= 0) return;

            sb.AppendLine(
                $"- Across all realms: {totals.TotalTroops:N0} soldiers, {totals.TotalSettlements} settlements, " +
                $"{totals.TotalClans} clans.");
        }

        // The wars actually under way: the single most decisive fact for a strategic reading, and the one the model
        // most wants to invent when it is absent. Naming them lets the advisor reason about real fronts and shared
        // enemies instead of imagining a "clash between X and Y" from nothing.
        private static void AppendWars(StringBuilder sb, CalradiaWarState wars)
        {
            if (!wars.Any)
            {
                sb.AppendLine("- No realms are presently at war with one another.");

                return;
            }

            // One line, each war named once, grouped by realm (fkasad, 01/10/2026: as the conversation prompt lists them).
            sb.AppendLine($"- Wars under way (the ONLY wars there are): {CompactWars(wars)}.");
        }

        /// <summary>
        ///   "Sturgia with Vlandia and Battania; Aserai with Khuzait Khanate": each war once, grouped under the realm that
        ///   first appears in it, in the order the wars were given.
        /// </summary>
        internal static string CompactWars(CalradiaWarState wars)
        {
            var order = new List<string>();
            var enemies = new Dictionary<string, List<string>>();
            var named = new HashSet<string>();

            foreach (WarPair w in wars?.Wars ?? new List<WarPair>())
            {
                if (w == null || string.IsNullOrWhiteSpace(w.A) || string.IsNullOrWhiteSpace(w.B)) continue;

                string key = string.CompareOrdinal(w.A, w.B) < 0 ? w.A + "|" + w.B : w.B + "|" + w.A;
                if (!named.Add(key)) continue;

                string lead = enemies.ContainsKey(w.B) && !enemies.ContainsKey(w.A) ? w.B : w.A;
                string other = lead == w.A ? w.B : w.A;
                if (!enemies.ContainsKey(lead))
                {
                    enemies[lead] = new List<string>();
                    order.Add(lead);
                }

                enemies[lead].Add(other);
            }

            var groups = new List<string>();
            foreach (string realm in order)
            {
                List<string> foes = enemies[realm];
                string list = foes.Count == 1 ? foes[0] : string.Join(", ", foes.GetRange(0, foes.Count - 1)) + " and " + foes[foes.Count - 1];
                groups.Add($"{realm} with {list}");
            }

            return string.Join("; ", groups);
        }

        /// <summary>The recent battles as they are: a realm that fought none is said to have none, never to be losing.</summary>
        private static string BattleRecord(KingdomPowerScore k)
            => k.RecentVictories + k.RecentDefeats == 0
                ? "No battles recorded lately."
                : $"Recent battles: {k.RecentVictories} won, {k.RecentDefeats} lost.";

        private static void AppendWorldDescription(StringBuilder sb, StrategicWorld world)
        {
            if (string.IsNullOrWhiteSpace(world?.Description)) return;

            sb.AppendLine($"THE WORLD (as its own chroniclers tell it): {Trim(world.Description)}");
            sb.AppendLine();
        }

        private static void AppendPostHistory(StringBuilder sb, StrategicWorld world)
        {
            if (string.IsNullOrWhiteSpace(world?.PostHistoryInstructions)) return;

            sb.Append(" Also: ").Append(Trim(world.PostHistoryInstructions));
        }

        private static string Trim(string text)
        {
            string t = text.Trim();

            return t.Length > MaxLoreChars ? t.Substring(0, MaxLoreChars) : t;
        }

        // The player's chosen narrative style (global, the same voice the chatroom uses), so the counsel reads in the
        // world's register rather than a flat report. Trimmed so a long style file cannot crowd out the facts.
        private static void AppendNarrativeStyle(StringBuilder sb, string narrativeStyle)
        {
            if (string.IsNullOrWhiteSpace(narrativeStyle)) return;

            string style = narrativeStyle.Trim();
            if (style.Length > MaxNarrativeStyleChars) style = style.Substring(0, MaxNarrativeStyleChars);

            sb.Append(" Speak in this narrative style: ").Append(style);
        }

        private static void AppendPlayerPosition(StringBuilder sb, PlayerStrategicContext player, PowerIndexResult result)
        {
            int fieldSize = result.Ranked?.Count ?? 0;

            if (player.HasRealm)
            {
                string rankClause = player.PlayerKingdomRank > 0
                    ? $", standing {player.PlayerKingdomRank}{(fieldSize > 0 ? $" of {fieldSize}" : "")} among the powers"
                    : "";
                sb.AppendLine($"- Your lord's realm is {player.PlayerKingdomName}{rankClause}.");
            }
            else
            {
                sb.AppendLine("- Your lord holds no realm of their own; you counsel a landless captain whose fortunes turn on whom they serve and fight.");
            }

            if (player.AtWarWith != null && player.AtWarWith.Count > 0)
                sb.AppendLine($"- At war with: {string.Join(", ", player.AtWarWith)}.");
            else if (player.HasRealm)
                sb.AppendLine("- Presently at war with no one.");
        }

        // The kingdom's single strongest / weakest pillar, named, so the advisor can speak of "strong armies" or a
        // "brittle economy" without the raw pillar table.
        private static (string name, double value) TopPillar(KingdomPowerScore k)
        {
            (string, double)[] pillars = Pillars(k);
            (string name, double value) best = pillars[0];
            foreach ((string name, double value) p in pillars)
                if (p.value > best.value) best = p;

            return best;
        }

        private static (string name, double value) BottomPillar(KingdomPowerScore k)
        {
            (string, double)[] pillars = Pillars(k);
            (string name, double value) worst = pillars[0];
            foreach ((string name, double value) p in pillars)
                if (p.value < worst.value) worst = p;

            return worst;
        }

        // War performance is NOT named strongest or weakest: it is a spread across the field, so a realm that simply has
        // not fought reads as the weakest and the counsel concluded "you are losing" (fkasad, 01/10/2026). The real
        // counts are stated instead (BattleRecord).
        private static (string, double)[] Pillars(KingdomPowerScore k) => new[]
        {
            ("military", k.Military),
            ("territory", k.Territory),
            ("economy", k.Economy),
            ("political weight", k.Political),
            ("diplomacy", k.Diplomatic)
        };

        private static string NameOr(string name) => string.IsNullOrWhiteSpace(name) ? "an unnamed realm" : name.Trim();

        private static int Round(double v) => (int) Math.Round(v);

        #endregion
    }
}
