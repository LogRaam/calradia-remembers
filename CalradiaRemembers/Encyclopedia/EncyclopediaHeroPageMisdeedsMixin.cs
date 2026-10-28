// Code written by Gabriel Mailhot, 23/10/2026.
// The encyclopedia's Misdeeds section (Gabriel, 23/10/2026: "Heart doit etre une section sur l'amour et le desir.
// L'evenement 'take by force' devrait etre une autre section avec des actions reprehensibles."): at Hardcore, on the
// player's own page, what the player did of their own will to a character. The words are MisdeedsPagePolicy's and the
// reading of the game MisdeedsPageReader's (in Mapping, so cr.test all reads the same lines).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Mapping;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin for the Misdeeds section of the player's page: <c>@CalradiaMisdeedsLabel</c> (cream header) and
    ///   <c>@CalradiaMisdeedsItems</c> (gold lines). Both empty below Hardcore, on a character's page, or when nothing is recorded, so the
    ///   AutoHide widgets collapse.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageMisdeedsMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;
        private string _label;

        public EncyclopediaHeroPageMisdeedsMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Cream header, "Misdeeds:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaMisdeedsLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Gold lines, one per deed and character, oldest first. Empty below Hardcore and on a character's page.</summary>
        [DataSourceProperty]
        public string CalradiaMisdeedsItems
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
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
                IReadOnlyList<string> lines = MisdeedsPageReader.LinesFor(HeroField?.GetValue(ViewModel) as Hero);
                CalradiaMisdeedsLabel = lines.Count == 0 ? "" : "Misdeeds:";
                CalradiaMisdeedsItems = lines.Count == 0 ? "" : ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ENCYCLOPEDIA] misdeeds section failed (non-fatal, hidden): {ex.GetType().Name}: {ex.Message}");
                CalradiaMisdeedsLabel = "";
                CalradiaMisdeedsItems = "";
            }
        }

        #endregion
    }
}
