// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar: the ONLY place that turns a live Hero + NpcProfile into the pure DTOs
// CalradiaRemembers.Logic.Grudges reasons over. No decision logic lives here — mirrors the
// TraitLevel/GetRelation helper pattern already used by DivorceProcessBehavior/SchemeStore callers
// throughout the mod (see their private static TraitLevel(Hero, TraitObject) helpers).

#region

using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Grudges
{
    /// <summary>Bridges a live <see cref="Hero" />/<see cref="NpcProfile" /> pair to the pure Grudges DTOs.</summary>
    internal static class GrudgeBridge
    {
        /// <summary>The hero's trait levels relevant to grudge formation, rigidity, and decay.</summary>
        public static TraitFacts BuildTraitFacts(Hero hero) => new TraitFacts
        {
            Mercy = TraitLevel(hero, DefaultTraits.Mercy),
            Honor = TraitLevel(hero, DefaultTraits.Honor),
            Valor = TraitLevel(hero, DefaultTraits.Valor),
            Calculating = TraitLevel(hero, DefaultTraits.Calculating),
            Generosity = TraitLevel(hero, DefaultTraits.Generosity)
        };

        /// <summary>
        ///   The full candidate-facts bundle a live <c>TryPlant</c> call reasons over. Every live planting
        ///   site names an unambiguous, attributable event by construction, so <c>IsAttributable</c> is
        ///   always true here — an unattributable candidate is simply never planted in the first place.
        /// </summary>
        public static GrudgeCandidateFacts BuildCandidateFacts(Hero holder, GrudgeSource source, int magnitude, bool possessiveAmplifier) => new GrudgeCandidateFacts
        {
            Source = source,
            Magnitude = magnitude,
            IsAttributable = true,
            PossessiveAmplifier = possessiveAmplifier,
            Traits = BuildTraitFacts(holder)
        };

        /// <summary>
        ///   True when the NPC's romantic preferences amplify a jealousy grudge — Possessive or strictly
        ///   monogamous, reusing the same layer other jealousy-adjacent code already reads
        ///   (<see cref="RomanticProfile.Preferences" />, see JealousyCulture for the sibling culture-level
        ///   amplifier). False for a profile with no romantic layer at all.
        /// </summary>
        public static bool HasPossessiveAmplifier(NpcProfile profile)
        {
            var prefs = profile?.Romantic?.Preferences;

            return prefs != null
                   && (prefs.Contains(RomanticPreference.Possessive) || prefs.Contains(RomanticPreference.MonogamousStrict));
        }

        private static int TraitLevel(Hero hero, TraitObject trait)
        {
            try { return hero?.GetTraitLevel(trait) ?? 0; }
            catch { return 0; }
        }
    }
}
