// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that surfaces a player COMPANION's contentment in your service on the hero
    ///   encyclopedia page — a sibling of the personal-relation line. Shown only for your own companions;
    ///   empty (the AutoHide widgets collapse) for everyone else. Bound by the prefab patch via
    ///   <c>@CalradiaContentment</c>.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageHappinessMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _label;
        private string _value;

        public EncyclopediaHeroPageHappinessMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold value, e.g. "content in your service (62)".</summary>
        [DataSourceProperty]
        public string CalradiaContentment
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream label, styled like the vanilla definition rows.</summary>
        [DataSourceProperty]
        public string CalradiaContentmentLabel
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
            if (!IsOwnCompanion(hero))
            {
                // Empty both → the AutoHide widgets collapse (no row for non-companions / the player).
                CalradiaContentmentLabel = "";
                CalradiaContentment = "";

                return;
            }

            int score = CompanionHappinessService.Get(hero);
            CalradiaContentmentLabel = "Contentment:";
            CalradiaContentment = $"{CompanionHappiness.Describe(score)} ({score})";
        }

        private static bool IsOwnCompanion(Hero hero)
            => hero != null && hero != Hero.MainHero && hero.IsAlive
               && (hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan);

        #endregion
    }
}
