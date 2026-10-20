// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Heart section (Gabriel, 20/10/2026: love stories were kept and shown nowhere). On the page of a
// character engaged with the player, the story of the bond in the player's second person; on the player's own page, one
// recap line per such person. The words are HeartPagePolicy's and the reading of the game HeartPageReader's (in Mapping, so
// cr.testall reads the same lines without the diagnostics depending on the encyclopedia).

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
    ///   UIExtenderEx mixin for the Heart section of a hero page: <c>@CalradiaHeartLabel</c> (cream header) and
    ///   <c>@CalradiaHeartItems</c> (gold lines). Both empty when the character is not engaged with the player, so the
    ///   AutoHide widgets collapse.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageHeartMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;
        private string _label;

        public EncyclopediaHeroPageHeartMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Cream header, "Heart:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaHeartLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Gold lines: the bond told to the player, or on the player's own page one line per person.</summary>
        [DataSourceProperty]
        public string CalradiaHeartItems
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
                IReadOnlyList<string> lines = HeartPageReader.LinesFor(HeroField?.GetValue(ViewModel) as Hero);
                CalradiaHeartLabel = lines.Count == 0 ? "" : "Heart:";
                CalradiaHeartItems = lines.Count == 0 ? "" : ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ENCYCLOPEDIA] heart section failed (non-fatal, hidden): {ex.GetType().Name}: {ex.Message}");
                CalradiaHeartLabel = "";
                CalradiaHeartItems = "";
            }
        }

        #endregion
    }
}
