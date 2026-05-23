// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Builds an <see cref="NpcProfile" /> from a Bannerlord <see cref="Hero" />.
    ///   Used the first time the player meets an NPC; the resulting profile is
    ///   then stored and reused across sessions.
    ///   The personality is derived from Bannerlord's own trait system
    ///   (Honor, Mercy, Valor, Generosity, Calculating) plus culture and rank,
    ///   so the LLM gets an NPC that already feels rooted in the game world.
    /// </summary>
    public static class HeroProfileMapper
    {
        public static NpcProfile FromHero(Hero hero) => new() {
            Id = hero.StringId,
            Name = hero.Name?.ToString() ?? "Unknown",
            Faction = hero.MapFaction?.Name?.ToString() ?? "Unaligned",
            Clan = hero.Clan?.Name?.ToString() ?? "No clan",
            Personality = BuildPersonality(hero)
        };

        #region private

        // ── Personality builder ───────────────────────────────────────────────

        private static string BuildPersonality(Hero hero)
        {
            var parts = new List<string>();

            var culture = hero.Culture?.Name?.ToString();
            if (!string.IsNullOrEmpty(culture))
                parts.Add($"A {culture} by culture.");

            parts.Add(DescribeRank(hero));
            parts.Add(DescribeTrait("Honor", hero.GetTraitLevel(DefaultTraits.Honor),
                "principled and honor-bound", "pragmatic in matters of honor", "self-serving, willing to break oaths"));
            parts.Add(DescribeTrait("Mercy", hero.GetTraitLevel(DefaultTraits.Mercy),
                "compassionate and merciful", "neither cruel nor especially gentle", "cruel and vindictive"));
            parts.Add(DescribeTrait("Valor", hero.GetTraitLevel(DefaultTraits.Valor),
                "brave, possibly reckless", "measured in danger", "cautious to the point of cowardice"));
            parts.Add(DescribeTrait("Generosity", hero.GetTraitLevel(DefaultTraits.Generosity),
                "generous with wealth and favor", "neither stingy nor lavish", "tight-fisted, even miserly"));
            parts.Add(DescribeTrait("Calculation", hero.GetTraitLevel(DefaultTraits.Calculating),
                "scheming, plays the long game", "straightforward in dealings", "blunt and direct, no patience for plots"));

            return string.Join(" ", parts);
        }

        private static string DescribeRank(Hero hero)
        {
            if (hero.IsKingdomLeader) return "Holds royal authority over a kingdom.";
            if (hero.IsFactionLeader) return "Leads a faction.";
            if (hero.IsClanLeader) return "Leads a noble clan.";
            if (hero.IsLord) return "A noble lord with land and retainers.";
            if (hero.IsMerchant) return "A merchant by trade, a notable of the town.";
            if (hero.IsRuralNotable) return "A respected village notable.";
            if (hero.IsArtisan) return "An artisan of the town.";
            if (hero.IsGangLeader) return "Leads an underworld gang.";

            return "A person of some standing in Calradia.";
        }

        private static string DescribeTrait(string traitName,
                                            int level,
                                            string positive,
                                            string neutral,
                                            string negative)
        {
            var descriptor = level >= 1 ? positive
                : level <= -1 ? negative
                : neutral;

            return $"{traitName.ToLower()}: {descriptor}.";
        }

        #endregion
    }
}