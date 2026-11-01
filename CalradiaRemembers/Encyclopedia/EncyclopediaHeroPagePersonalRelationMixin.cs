// Code written by Gabriel Mailhot, 01/06/2026.

#region

using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that surfaces the NPC's PERSONAL regard for the player
    ///   (our per-hero score, independent of the clan-level relation Bannerlord shows)
    ///   on the hero's encyclopedia page. Bound by the prefab patch via
    ///   <c>@CalradiaPersonalRelation</c>. Shows "unknown" until the player has a
    ///   stored profile for that hero (i.e. has spoken with them).
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPagePersonalRelationMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _label;
        private string _value;

        public EncyclopediaHeroPagePersonalRelationMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();

            // A later vanilla refresh runs OnRefresh BEFORE it rebuilds Stats, which undid the "Clan Relation" label
            // (found 17/10/2026 with the friends lists, EncyclopediaHeroPageRegardMixin). The page's Refresh ends with
            // IsLoadingOver = true: the label is set again then.
            if (vm != null) vm.PropertyChangedWithBoolValue += OnPagePropertyChanged;
        }

        public override void OnFinalize()
        {
            if (ViewModel != null) ViewModel.PropertyChangedWithBoolValue -= OnPagePropertyChanged;
            base.OnFinalize();
        }

        private void OnPagePropertyChanged(object sender, PropertyChangedWithBoolValueEventArgs e)
        {
            if (e.PropertyName == nameof(EncyclopediaHeroPageVM.IsLoadingOver) && e.Value) Refresh();
        }

        /// <summary>Gold value, e.g. "neutral (-5)" — description then the score in parentheses.</summary>
        [DataSourceProperty]
        public string CalradiaPersonalRelation
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream label, styled like the vanilla "Culture:" / "Relation:" definitions.</summary>
        [DataSourceProperty]
        public string CalradiaPersonalRelationLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
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
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            if (hero == null || hero == Hero.MainHero)
            {
                // Empty both → AutoHide widgets collapse (no row on the player's own page).
                CalradiaPersonalRelationLabel = "";
                CalradiaPersonalRelation = "";

                return;
            }

            CalradiaPersonalRelationLabel = "Personal regard:";

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile profile = behavior?.Store?.Get(hero.StringId);

            // The dominant known reason beside the band (NPC_REGARD.md Q2, 17/10/2026: proposed, taken as the default when
            // Gabriel said "continu"): "bitter grudge (-80): you executed his brother".
            string why = profile == null ? null : Mapping.DossierReader.WhyTowardThePlayer(hero, profile.ReputationWithPlayer);
            CalradiaPersonalRelation = profile == null
                ? "unknown"
                : $"{RegardBands.Describe(profile.ReputationWithPlayer)} ({profile.ReputationWithPlayer:+#;-#;0})" + (why == null ? "" : ": " + why);

            RelabelClanRelation();
        }

        /// <summary>
        ///   Renames the vanilla "Relation" stat on this page to "Clan Relation", so it reads as visibly distinct
        ///   from the "Personal regard:" row above (players confuse the clan-level relation Bannerlord shows with
        ///   CR's own personal ledger). Done from this mixin's own refresh, which UIExtenderEx already runs after
        ///   the VM rebuilds its Stats list, rather than a raw Harmony patch on <c>EncyclopediaHeroPageVM.Refresh</c>:
        ///   that patch hard-crashed on load under game 1.4.8 (a MonoMod detour fault), while this mixin path is
        ///   the same one the personal-regard row already uses safely. Mutates only the one Stats entry; the shared
        ///   localization string is untouched, so no other screen changes. Defensive: does nothing if the row is
        ///   absent (e.g. the player's own page, handled by the early return above).
        /// </summary>
        private void RelabelClanRelation()
        {
            try
            {
                string vanillaRelationLabel = GameTexts.FindText("str_enc_sf_relation")?.ToString();
                if (string.IsNullOrEmpty(vanillaRelationLabel)) return;

                MBBindingList<StringPairItemVM> stats = ViewModel?.Stats;
                if (stats == null) return;

                foreach (StringPairItemVM stat in stats)
                {
                    if (stat.Definition != vanillaRelationLabel) continue;

                    stat.Definition = "Clan Relation";

                    return;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ENCYCLOPEDIA] clan-relation relabel (mixin) failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}