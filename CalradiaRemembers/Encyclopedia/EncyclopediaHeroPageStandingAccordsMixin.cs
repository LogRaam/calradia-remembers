// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 3: the encyclopedia's "Standing accords" section on a hero's page, mirroring the Grievances mixin's
// shape exactly (a cream header + a gold bullet list, both empty strings when there is nothing to show so the
// AutoHideRichTextWidget prefabs collapse and the block takes no space). Reads the four live accord behaviors plus
// the ward bond for THIS hero and renders each through the pure third-person CouncilAccordPanelText (never the
// first-person CouncilAccordClause, which is bound for the NPC's own prompt, not a page the player reads).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that displays this hero's still-standing council accords with the player (a tribute
    ///   they owe, a stipend they receive, a non-aggression pact, a hostage held or given, a ward bond) on their
    ///   encyclopedia hero page. Bound via <c>@CalradiaStandingAccordsLabel</c> (cream header) and
    ///   <c>@CalradiaStandingAccordsItems</c> (gold bullet list). Both are empty strings when nothing stands, so
    ///   the AutoHide widgets collapse and the block takes no space on pages with no accord.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageStandingAccordsMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;

        private string _label;

        private bool _hasStandingAccords;

        public EncyclopediaHeroPageStandingAccordsMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold bullet list, one standing accord per line ("• sentence"). Empty when none (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaStandingAccordsItems
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream header ("Standing accords:") or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaStandingAccordsLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>True when at least one accord renders this session (exposed for future binding/tests).</summary>
        [DataSourceProperty]
        public bool HasStandingAccords
        {
            get => _hasStandingAccords;
            set
            {
                if (_hasStandingAccords == value) return;
                _hasStandingAccords = value;
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
                var hero = HeroField?.GetValue(ViewModel) as Hero;
                if (hero == null || hero == Hero.MainHero)
                {
                    Clear();

                    return;
                }

                Hero player = Hero.MainHero;
                var lines = new List<string>();

                CouncilTribute tribute = CouncilTributeBehavior.Instance?.Get(hero.StringId);
                if (tribute != null)
                    lines.Add(CouncilAccordPanelText.TributeOwed(new CouncilAccordPanelText.TributeLine {AmountPerDay = tribute.AmountPerDay, DaysRemaining = tribute.DaysRemaining, MissedDays = tribute.MissedDays}));

                CouncilStipend stipend = CouncilStipendBehavior.Instance?.Get(hero.StringId);
                if (stipend != null)
                    lines.Add(CouncilAccordPanelText.StipendReceived(stipend.AmountPerDay, stipend.DaysRemaining));

                CouncilPact pact = CouncilPactBehavior.Instance?.Get(player?.MapFaction, hero.MapFaction);
                if (pact != null)
                    lines.Add(CouncilAccordPanelText.Pact(pact.DaysRemaining));

                CouncilHostage held = CouncilHostageBehavior.Instance?.GetByHostage(hero.StringId);
                if (held != null)
                    lines.Add(CouncilAccordPanelText.HeldAsHostage(held.DaysRemaining));

                CouncilHostage pledged = CouncilHostageBehavior.Instance?.GetByPledger(hero.StringId);
                if (pledged != null)
                    lines.Add(CouncilAccordPanelText.HostageGiven(ResolveName(pledged.HostageId), pledged.DaysRemaining));

                CouncilWard asProtege = CouncilWardBehavior.Instance?.GetAsProtege(hero.StringId);
                if (asProtege != null)
                    lines.Add(CouncilAccordPanelText.WardAsProtege(ResolveName(asProtege.MentorId)));

                CouncilWard asMentor = CouncilWardBehavior.Instance?.GetAsMentor(hero.StringId);
                if (asMentor != null)
                    lines.Add(CouncilAccordPanelText.WardAsMentor(ResolveName(asMentor.ProtegeId)));

                if (lines.Count == 0)
                {
                    Clear();

                    return;
                }

                CalradiaStandingAccordsLabel = "Standing accords:";
                CalradiaStandingAccordsItems = string.Join("\n", lines.Select(l => "• " + l));
                HasStandingAccords = true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COUNCIL] encyclopedia standing-accords refresh failed; showing nothing this session. {ex.GetType().Name}: {ex.Message}");
                Clear();
            }
        }

        private void Clear()
        {
            CalradiaStandingAccordsLabel = "";
            CalradiaStandingAccordsItems = "";
            HasStandingAccords = false;
        }

        private static string ResolveName(string heroId) => FindHero(heroId)?.Name?.ToString();

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        #endregion
    }
}
