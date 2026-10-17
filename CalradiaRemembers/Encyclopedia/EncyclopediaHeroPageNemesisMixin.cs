// Code written by Gabriel Mailhot, 20/08/2026.
// Nemesis pillar, discoverability piece (player report, SuperGraham339): captured/tortured by a nemesis,
// escaped, killed the party, ran into him again (the pillar's designed payoff), then "tried to look him up
// in the encyclopedia but didn't see any mention." Mirrors EncyclopediaHeroPageGrievancesMixin's shape
// exactly (a cream header + a gold bullet list, both empty strings when there is nothing to show so the
// AutoHideRichTextWidget prefabs collapse and the block takes no space). The ONLY thing ever read here is
// NemesisDossierFactory.BuildFor: the encountered gate (NemesisIdentity.TimesEncountered > 0) lives
// entirely in that factory (shared with cr.nemesis so both prove the same code); this mixin never reasons
// about it itself.

#region

using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Nemesis;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that displays the shared history the player has with a nemesis they have
    ///   ALREADY ENCOUNTERED, on that nemesis's own encyclopedia hero page. Bound via
    ///   <c>@CalradiaNemesisLabel</c> (cream header) and <c>@CalradiaNemesisItems</c> (gold bullet list).
    ///   Both are empty strings when this hero is not an encountered nemesis (the AutoHide widgets
    ///   collapse and the block takes no space on every other hero's page). A LATENT nemesis (never
    ///   materialized into a real encounter) stays hidden: see NemesisDossierFactory for why
    ///   TimesEncountered is the correct "has this been revealed to the player" signal, not merely having
    ///   a bound Hero.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageNemesisMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;

        private string _label;

        private bool _hasNemesisDossier;

        public EncyclopediaHeroPageNemesisMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold bullet list: who he is, culture/region, and your shared history. Empty when none (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaNemesisItems
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream header, "Nemesis:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaNemesisLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>True when this hero is an encountered nemesis and the dossier renders this session (exposed for future binding/tests).</summary>
        [DataSourceProperty]
        public bool HasNemesisDossier
        {
            get => _hasNemesisDossier;
            set
            {
                if (_hasNemesisDossier == value) return;
                _hasNemesisDossier = value;
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

                NemesisDossierText dossier = NemesisDossierFactory.BuildFor(hero);
                if (!dossier.HasDossier)
                {
                    Clear();

                    return;
                }

                CalradiaNemesisLabel = dossier.Label;
                CalradiaNemesisItems = Mapping.ShownText.Of(dossier.Items);
                HasNemesisDossier = true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] encyclopedia dossier refresh failed, showing nothing this session. {ex.GetType().Name}: {ex.Message}");
                Clear();
            }
        }

        private void Clear()
        {
            CalradiaNemesisLabel = "";
            CalradiaNemesisItems = "";
            HasNemesisDossier = false;
        }

        #endregion
    }
}
