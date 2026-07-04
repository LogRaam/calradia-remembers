// Code written by Gabriel Mailhot, 01/06/2026.

#region

using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
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

            CalradiaPersonalRelation = profile == null
                ? "unknown"
                : $"{RegardBands.Describe(profile.ReputationWithPlayer)} ({profile.ReputationWithPlayer:+#;-#;0})";
        }

        #endregion
    }
}