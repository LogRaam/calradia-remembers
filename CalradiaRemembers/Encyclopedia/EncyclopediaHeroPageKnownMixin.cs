// Code written by Gabriel Mailhot, 27/10/2026.
// The encyclopedia's "What You Know" section (Gabriel, 27/10/2026: "il faudra penser a les indiquer dans des sections de
// l'encyclopedia"): on a character's page, what the player has learned of who they are, in the game's own words, never
// before it was learned: what matters most to them, and what the later increments of the pillar add. Unlike the
// Discoveries section, whose lines the model writes, every line here is the game's, read by KnownPageReader (in Mapping,
// so cr.test reads the same lines without the diagnostics depending on the encyclopedia).

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
    ///   UIExtenderEx mixin for the What You Know section of a hero page: <c>@CalradiaKnownLabel</c> (cream header) and
    ///   <c>@CalradiaKnownItems</c> (gold lines). Both empty while the player has learned nothing of them, so the AutoHide
    ///   widgets collapse.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageKnownMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;
        private string _label;

        public EncyclopediaHeroPageKnownMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Cream header, "What You Know:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaKnownLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Gold lines: what the player has learned of who they are.</summary>
        [DataSourceProperty]
        public string CalradiaKnownItems
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
                IReadOnlyList<string> lines = KnownPageReader.LinesFor(HeroField?.GetValue(ViewModel) as Hero);
                CalradiaKnownLabel = lines.Count == 0 ? "" : "What You Know:";
                CalradiaKnownItems = lines.Count == 0 ? "" : ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ENCYCLOPEDIA] what-you-know section failed (non-fatal, hidden): {ex.GetType().Name}: {ex.Message}");
                CalradiaKnownLabel = "";
                CalradiaKnownItems = "";
            }
        }

        #endregion
    }
}
