// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Trauma section (Gabriel, 20/10/2026): at Hardcore, on a character's page and never the player's, the
// aggressions that character lived through before the player's eyes. The words are TraumaPagePolicy's and the reading of
// the game TraumaPageReader's (in Mapping, so cr.test all reads the same lines without the diagnostics depending on the
// encyclopedia).

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
    ///   UIExtenderEx mixin for the Trauma section of a hero page: <c>@CalradiaTraumaLabel</c> (cream header) and
    ///   <c>@CalradiaTraumaItems</c> (gold lines). Both empty below Hardcore, on the player's own page, or when nothing is recorded, so the
    ///   AutoHide widgets collapse.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageTraumaMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;
        private string _label;

        public EncyclopediaHeroPageTraumaMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Cream header, "Trauma:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaTraumaLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Gold lines, one per aggression, oldest first. Empty below Hardcore and on the player's own page.</summary>
        [DataSourceProperty]
        public string CalradiaTraumaItems
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
                IReadOnlyList<string> lines = TraumaPageReader.LinesFor(HeroField?.GetValue(ViewModel) as Hero);
                CalradiaTraumaLabel = lines.Count == 0 ? "" : "Trauma:";
                CalradiaTraumaItems = lines.Count == 0 ? "" : ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ENCYCLOPEDIA] trauma section failed (non-fatal, hidden): {ex.GetType().Name}: {ex.Message}");
                CalradiaTraumaLabel = "";
                CalradiaTraumaItems = "";
            }
        }

        #endregion
    }
}
