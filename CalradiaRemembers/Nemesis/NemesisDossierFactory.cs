// Code written by Gabriel Mailhot, 20/08/2026.
// Nemesis pillar, discoverability piece (player report, SuperGraham339): resolves a Hero down to the plain
// facts CalradiaRemembers.Logic.Nemesis.NemesisDossierBuilder needs (name/culture/region strings, counters, history
// sentences), then calls it. Shared by EncyclopediaHeroPageNemesisMixin (the actual page) and cr.nemesis
// (the dev-truth preview) so both prove the exact same code, mirroring how NemesisResolutionBehavior.Resolve
// backs both the real HeroKilledEvent hook and its own console test commands.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Nemesis;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Nemesis
{
    /// <summary>The rendered dossier text for one hero: empty strings when he is not an encountered nemesis.</summary>
    public sealed class NemesisDossierText
    {
        public string Label = "";
        public string Items = "";

        /// <summary>True once both the label and the items rendered something this call.</summary>
        public bool HasDossier => !string.IsNullOrEmpty(Label) && !string.IsNullOrEmpty(Items);
    }

    /// <summary>
    ///   Resolves a Hero to the encyclopedia nemesis dossier text, or empty when he is not an ENCOUNTERED
    ///   nemesis. See NemesisIdentity.TimesEncountered's own doc for why that is the correct gate, not
    ///   merely having a bound Hero. Pure formatting lives in NemesisDossierBuilder; this class only owns
    ///   the engine-side resolution (Settlement lookups, NpcProfile.Events) that a pure class cannot do.
    /// </summary>
    public static class NemesisDossierFactory
    {
        /// <summary>Cap on extra history sentences pulled from a nemesis's own NpcProfile.Events, so a long-lived vendetta never floods the page.</summary>
        public const int MaxHistorySentences = 6;

        /// <summary>
        ///   The dossier text for <paramref name="hero" />, or an empty <see cref="NemesisDossierText" /> when
        ///   he is null, not a tracked nemesis, or a LATENT one the player has never actually encountered.
        /// </summary>
        public static NemesisDossierText BuildFor(Hero hero)
        {
            var result = new NemesisDossierText();
            if (hero == null) return result;

            NemesisBehavior behavior = NemesisBehavior.Instance;
            if (behavior == null) return result;

            NemesisIdentity nem = behavior.GetByHero(hero.StringId);
            // The encountered gate: TimesEncountered only rises on a real capture or an active-hunt
            // materialize (never at mere minting), so this is exactly "has the player met him": a
            // latent nemesis stays hidden here by construction, never leaked before he materializes.
            if (nem == null || nem.TimesEncountered <= 0) return result;

            string displayName = $"{nem.Name} {nem.Epithet}";
            string cultureDisplay = HumanizeCultureKey(nem.BandCultureKey);
            string regionDisplay = ResolveRegionDisplay(nem.RegionId);
            List<string> history = CollectHistorySentences(hero.StringId);

            result.Label = NemesisDossierBuilder.BuildLabel(nem.TimesEncountered);
            result.Items = NemesisDossierBuilder.BuildItems(new NemesisDossierBuilder.NemesisDossierFacts {DisplayName = displayName, CultureDisplay = cultureDisplay, RegionDisplay = regionDisplay, TimesEncountered = nem.TimesEncountered, RecaptureCount = nem.RecaptureCount, HistorySentences = history});

            return result;
        }

        /// <summary>"forest_bandits" -> "forest bandits" (the band-culture StringId is the only "culture" fact this pillar carries).</summary>
        private static string HumanizeCultureKey(string bandCultureKey)
            => string.IsNullOrEmpty(bandCultureKey) ? null : bandCultureKey.Replace('_', ' ');

        /// <summary>
        ///   A real settlement's display name when RegionId resolves to one, "the wilds" for that documented
        ///   fallback, or a humanized raw key otherwise (mirrors NemesisBehavior.ResolveRegionSettlement's own
        ///   tolerance of a culture/"wilds" anchor that is not a real place). Internal (not private) so
        ///   InterceptionBehavior.HasNemesisBountyToOffer can resolve the SAME region display for its opener
        ///   detail, rather than a second, diverging read of the same RegionId.
        /// </summary>
        internal static string ResolveRegionDisplay(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return null;
            if (regionId == "wilds") return "the wilds";

            Settlement settlement = Settlement.All?.FirstOrDefault(s => s != null && s.StringId == regionId);

            return settlement != null ? settlement.Name?.ToString() : regionId.Replace('_', ' ');
        }

        /// <summary>
        ///   Extra shared-history sentences beyond the counters: this nemesis Hero's own NotableEvents, the
        ///   escape/release memory NemesisBehavior.RecordCaptivityEndMemory writes, captive-scene summaries,
        ///   and so on. His profile carries no OTHER history, since he was minted purpose-built for this
        ///   vendetta. Oldest-first, capped so a long-lived rivalry never floods the page.
        /// </summary>
        private static List<string> CollectHistorySentences(string heroStringId)
        {
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(heroStringId);
            if (profile == null || profile.Events.Count == 0) return null;

            List<string> ordered = profile.Events
                .OrderBy(ev => ev.gameDay)
                .Select(ev => ev.summary)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            int skip = Math.Max(0, ordered.Count - MaxHistorySentences);

            return ordered.Skip(skip).ToList();
        }
    }
}
