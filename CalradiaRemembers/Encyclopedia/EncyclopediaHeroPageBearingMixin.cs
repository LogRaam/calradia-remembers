// Code written by Gabriel Mailhot, 04/07/2026.
// The "Bearing" encyclopedia line (owner ruling, affect-coherence reform P2): the NPC's stance toward the
// player, in words only, numberless — mirrors EncyclopediaHeroPagePersonalRelationMixin's single-line
// shape exactly (one label + one value on the same row), since StanceBearingText composes a single short
// clause rather than a bullet list. Gated on a CR profile existing for the hero (met at least once) — a
// never-met hero has no stance to speak of.

#region

using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that surfaces the NPC's STANCE toward the player (Trust/Respect/Fear, NOT
    ///   Affection — that is the existing "Personal regard:" line) on the hero's encyclopedia page, in
    ///   words only, never a number. Bound via <c>@CalradiaBearing</c> / <c>@CalradiaBearingLabel</c>. Both
    ///   empty when the NPC has never been met (no stored profile) or every axis reads neutral — the
    ///   AutoHide widgets collapse and the row takes no space.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageBearingMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _label;
        private string _value;

        public EncyclopediaHeroPageBearingMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold value, e.g. "trusts your word, fears you" — empty when nothing to voice.</summary>
        [DataSourceProperty]
        public string CalradiaBearing
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
        public string CalradiaBearingLabel
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
            var hero = HeroField?.GetValue(ViewModel) as Hero;
            // 3.4 (regard audit): a dead hero holds no living bearing toward the player; do not voice one
            // posthumously on their encyclopedia page.
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive)
            {
                Clear();

                return;
            }

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile profile = behavior?.Store?.Get(hero.StringId);

            if (profile == null)
            {
                Clear(); // never met — nothing to voice

                return;
            }

            string bearing = StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = profile.StanceTrust, Respect = profile.StanceRespect, Fear = profile.StanceFear});

            if (string.IsNullOrEmpty(bearing))
            {
                Clear(); // wholly neutral

                return;
            }

            CalradiaBearingLabel = "Bearing:";
            CalradiaBearing = bearing;
        }

        private void Clear()
        {
            CalradiaBearingLabel = "";
            CalradiaBearing = "";
        }

        #endregion
    }
}
