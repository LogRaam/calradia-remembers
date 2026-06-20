// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 Phase 2b: a "Read the Chronicle" button on the player's OWN encyclopedia page, opening the
// illuminated chronicle viewer. Shown only on the player's page (where the per-hero panels hide).

#region

using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin adding a chronicle button to the hero encyclopedia page. Bound by the
    ///   prefab patch via <c>@IsPlayerPage</c> / <c>@ExecuteOpenChronicle</c>; visible only when the
    ///   page is the player's own (their chronicle, no one else's).
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageChronicleMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool _isPlayerPage;
        private string _chronicleButtonText = "Read the Chronicle";

        public EncyclopediaHeroPageChronicleMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        [DataSourceProperty]
        public bool IsPlayerPage
        {
            get => _isPlayerPage;
            set
            {
                if (_isPlayerPage == value) return;
                _isPlayerPage = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        [DataSourceProperty]
        public string ChronicleButtonText
        {
            get => _chronicleButtonText;
            set
            {
                if (_chronicleButtonText == value) return;
                _chronicleButtonText = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        public override void OnRefresh() => Refresh();

        [DataSourceMethod]
        public void ExecuteOpenChronicle() => Chronicle.ChronicleManager.Open();

        private void Refresh()
        {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            IsPlayerPage = hero != null && hero == Hero.MainHero;
        }
    }
}
