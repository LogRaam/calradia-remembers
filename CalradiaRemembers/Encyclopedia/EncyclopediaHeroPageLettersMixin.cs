// Code written by Gabriel Mailhot, 07/08/2026.
// Player request: keep delivered letters and read them back, arranged by contact. This mixin adds a
// "Letters" button to a hero's encyclopedia page that opens the dated correspondence thread with THAT
// hero. Unlike the chronicle button (player's own page only), this is a per-contact entry point and is
// shown only when there is actually correspondence to read (LetterArchivePolicy.HasCorrespondence), so
// the button never opens an empty archive. Mirrors EncyclopediaHeroPageChronicleMixin.

#region

using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Letters;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin adding a per-contact "Letters" button to the hero encyclopedia page. Bound by
    ///   the prefab patch via <c>@HasLetters</c> / <c>@ExecuteOpenLetters</c>; visible only when the player
    ///   and this hero have exchanged at least one delivered letter.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageLettersMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool _hasLetters;
        private string _lettersButtonText = "Letters";

        public EncyclopediaHeroPageLettersMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        [DataSourceProperty]
        public bool HasLetters
        {
            get => _hasLetters;
            set
            {
                if (_hasLetters == value) return;
                _hasLetters = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        [DataSourceProperty]
        public string LettersButtonText
        {
            get => _lettersButtonText;
            set
            {
                if (_lettersButtonText == value) return;
                _lettersButtonText = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        public override void OnRefresh() => Refresh();

        [DataSourceMethod]
        public void ExecuteOpenLetters()
        {
            NpcProfile profile = ResolveProfile();
            if (profile == null) return;

            LetterArchiveManager.Open(profile);
        }

        #region private

        private void Refresh()
        {
            NpcProfile profile = ResolveProfile();
            HasLetters = profile != null
                         && LetterArchivePolicy.HasCorrespondence(profile.SentLetters, profile.ReceivedPlayerLetters);
        }

        private NpcProfile ResolveProfile()
        {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            if (hero == null || hero == Hero.MainHero) return null;

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;

            return behavior?.Store?.Get(hero.StringId);
        }

        #endregion
    }
}
