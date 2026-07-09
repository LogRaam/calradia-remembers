// Code written by Gabriel Mailhot, 08/07/2026.
// Appearance pillar: the in-game surface for the appearance prose (replacing the cr.appearance console). The
// PLAYER'S OWN encyclopedia page SHOWS the full "how others see you" line (auto-derived build/age/colouring with
// any authored note appended) and a single button that opens a custom popup to author or edit the note. The popup
// (AppearanceEditorManager, a focus Gauntlet layer) is used instead of ShowTextInquiry so it can be larger and so
// Backspace edits the field instead of navigating the encyclopedia BACK. Scoped to the player ALONE, deliberately:
// an NPC note would be incoherent (an NPC you described who later left your clan would carry an orphaned note),
// and only the player's look is read by every NPC they meet.

#region

using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin: on the player's own encyclopedia page, shows the composed appearance and one button
    ///   that opens the note editor. Binds via <c>@IsAppearanceEditable</c> / <c>@AppearanceHeader</c> /
    ///   <c>@AppearanceText</c> / <c>@AppearanceButtonText</c> / <c>@ExecuteAddMoreInfo</c>.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageAppearanceMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool _isAppearanceEditable;
        private string _appearanceHeader = "How others see you";
        private string _appearanceText = "";
        private string _appearanceButtonText = "Add more info";

        public EncyclopediaHeroPageAppearanceMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        [DataSourceProperty]
        public bool IsAppearanceEditable
        {
            get => _isAppearanceEditable;
            set
            {
                if (_isAppearanceEditable == value) return;
                _isAppearanceEditable = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        [DataSourceProperty]
        public string AppearanceHeader
        {
            get => _appearanceHeader;
            set
            {
                if (_appearanceHeader == value) return;
                _appearanceHeader = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        [DataSourceProperty]
        public string AppearanceText
        {
            get => _appearanceText;
            set
            {
                if (_appearanceText == value) return;
                _appearanceText = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        [DataSourceProperty]
        public string AppearanceButtonText
        {
            get => _appearanceButtonText;
            set
            {
                if (_appearanceButtonText == value) return;
                _appearanceButtonText = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        public override void OnRefresh() => Refresh();

        [DataSourceMethod]
        public void ExecuteAddMoreInfo()
        {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            if (hero?.StringId == null || hero != Hero.MainHero || AppearanceProseBehavior.Instance == null) return;
            if (AppearanceEditorManager.IsOpen) return;

            string current = AppearanceProseBehavior.Instance.Get(hero.StringId);
            var facts = AppearanceFactsExtractor.Extract(hero);
            Func<string, string> preview = note => AppearanceComposer.Compose(facts, note, AppearanceVoice.Subject) ?? "";

            AppearanceEditorManager.Open(current, preview, note =>
            {
                AppearanceProseBehavior.Instance?.Set(hero.StringId, note ?? "");
                Refresh(); // the page line now shows the note appended to the derived description
            });
        }

        private void Refresh()
        {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            bool editable = hero is {IsAlive: true} && hero == Hero.MainHero; // player's own page only, deliberately
            IsAppearanceEditable = editable;

            if (!editable)
            {
                AppearanceText = "";

                return;
            }

            string prose = AppearanceProseBehavior.Instance?.Get(hero.StringId) ?? "";
            // The full line a speaking NPC reads: derived look with the authored note appended by the composer.
            AppearanceText = AppearanceComposer.Compose(AppearanceFactsExtractor.Extract(hero), prose, AppearanceVoice.Subject) ?? "";
            AppearanceButtonText = string.IsNullOrWhiteSpace(prose) ? "Add more info" : "Edit your note";
        }
    }
}
