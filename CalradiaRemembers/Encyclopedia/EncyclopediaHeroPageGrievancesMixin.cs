// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar: the final surfacing piece — the encyclopedia's "Grievances" section. Mirrors
// EncyclopediaHeroPageDiscoveryMixin's shape exactly (a cream header + a gold bullet list, both empty
// strings when there is nothing to show so the AutoHideRichTextWidget prefabs collapse and the block
// takes no space). The ONLY thing ever read here is GrudgeBehavior.KnownGrudgesFor — the knowledge gate
// (Grudge.KnownToPlayer) lives entirely in the engine store; this mixin never reasons about it itself.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that displays the grudges the player has EARNED knowledge of against this
    ///   NPC, on that NPC's encyclopedia hero page. Bound via <c>@CalradiaGrievancesLabel</c> (cream
    ///   header) and <c>@CalradiaGrievancesItems</c> (gold bullet list). Both are empty strings when
    ///   there is nothing known — the AutoHide widgets collapse and the block takes no space on pages
    ///   with no data. Console <c>cr.grudges</c> remains the dev-truth dump that shows every grudge
    ///   regardless of what the player has actually learned; this mixin never reads that full ledger.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageGrievancesMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;

        private string _label;

        private bool _hasGrievances;

        public EncyclopediaHeroPageGrievancesMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold bullet list, one known grievance per line ("• sentence"). Empty when none (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaGrievancesItems
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream header — "Grievances:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaGrievancesLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>True when at least one known grievance renders this session — exposed for future binding/tests.</summary>
        [DataSourceProperty]
        public bool HasGrievances
        {
            get => _hasGrievances;
            set
            {
                if (_hasGrievances == value) return;
                _hasGrievances = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        public override void OnRefresh()
        {
            Refresh();
        }

        #region private

        private void Refresh()
        {
            try
            {
                var hero = HeroField?.GetValue(ViewModel) as Hero;
                if (hero == null || hero == Hero.MainHero)
                {
                    Clear();

                    return;
                }

                GrudgeBehavior behavior = GrudgeBehavior.Instance;
                if (behavior == null)
                {
                    Clear();

                    return;
                }

                var today = (int) CampaignTime.Now.ToDays;
                IReadOnlyList<Grudge> known = behavior.KnownGrudgesFor(hero.StringId, today);

                var lines = new List<string>();
                foreach (Grudge grudge in known)
                {
                    string line = BuildLine(grudge, today);
                    if (!string.IsNullOrEmpty(line)) lines.Add(line);
                }

                if (lines.Count == 0)
                {
                    Clear();

                    return;
                }

                CalradiaGrievancesLabel = "Grievances:";
                CalradiaGrievancesItems = Mapping.ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
                HasGrievances = true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] encyclopedia grievances refresh failed — showing nothing this session. {ex.GetType().Name}: {ex.Message}");
                Clear();
            }
        }

        private void Clear()
        {
            CalradiaGrievancesLabel = "";
            CalradiaGrievancesItems = "";
            HasGrievances = false;
        }

        /// <summary>
        ///   One grudge, rendered through the same pure <see cref="GrudgeNarrator.DisplayLine" /> the tests
        ///   cover. Tolerant of a missing target/holder hero (a stale StringId after a save) — falls back
        ///   to "them" via <see cref="GrudgeNarrator.DisplayLine" /> rather than skip the line outright.
        /// </summary>
        private static string BuildLine(Grudge grudge, int today)
        {
            bool targetIsPlayer = grudge.TargetId == Hero.MainHero?.StringId;
            string targetName = targetIsPlayer ? null : ResolveName(grudge.TargetId);

            TraitFacts traits = GrudgeBridge.BuildTraitFacts(FindHero(grudge.HolderId));
            int effectiveMagnitude = GrudgePolicy.EffectiveMagnitude(grudge, today, traits);
            GrudgeResolutionKind kind = GrudgePolicy.ResolutionKindFor(grudge.Source, grudge.Rigidity);

            return GrudgeNarrator.DisplayLine(grudge, targetIsPlayer, targetName, effectiveMagnitude, kind);
        }

        private static string ResolveName(string heroId) => FindHero(heroId)?.Name?.ToString();

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        #endregion
    }
}
