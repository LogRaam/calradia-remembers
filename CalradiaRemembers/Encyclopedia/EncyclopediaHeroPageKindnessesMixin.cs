// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar: the final surfacing piece — the encyclopedia's "Kindnesses" section, the positive
// mirror of EncyclopediaHeroPageGrievancesMixin. Mirrors its shape exactly (a cream header + a gold bullet
// list, both empty strings when there is nothing to show so the AutoHideRichTextWidget prefabs collapse
// and the block takes no space). The ONLY thing ever read here is AppreciationBehavior.KnownAppreciationsFor
// — the knowledge gate (Appreciation.KnownToPlayer) lives entirely in the engine store; this mixin never
// reasons about it itself. UNLIKE Grievances, the TARNISH (a live grudge souring a standing warmth) is
// applied at render time here too, so a soured kindness shows dimmer, or not at all once fully tarnished —
// the same discipline the prompt's ResolveAppreciationNote already applies.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   UIExtenderEx mixin that displays the kindnesses the player has EARNED knowledge of from this NPC,
    ///   on that NPC's encyclopedia hero page. Bound via <c>@CalradiaKindnessesLabel</c> (cream header) and
    ///   <c>@CalradiaKindnessesItems</c> (gold bullet list). Both are empty strings when there is nothing
    ///   known — the AutoHide widgets collapse and the block takes no space on pages with no data. Console
    ///   <c>cr.appreciations</c> remains the dev-truth dump that shows every appreciation regardless of what
    ///   the player has actually learned; this mixin never reads that full ledger.
    /// </summary>
    [ViewModelMixin]
    public sealed class EncyclopediaHeroPageKindnessesMixin
        : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private static readonly FieldInfo HeroField =
            typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _items;

        private string _label;

        private bool _hasKindnesses;

        public EncyclopediaHeroPageKindnessesMixin(EncyclopediaHeroPageVM vm) : base(vm)
        {
            Refresh();
        }

        /// <summary>Gold bullet list, one known kindness per line ("• sentence"). Empty when none (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaKindnessesItems
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>Cream header — "Kindnesses:" or empty (auto-hides).</summary>
        [DataSourceProperty]
        public string CalradiaKindnessesLabel
        {
            get => _label;
            set
            {
                if (_label == value) return;
                _label = value;
                ViewModel?.OnPropertyChanged();
            }
        }

        /// <summary>True when at least one known kindness renders this session — exposed for future binding/tests.</summary>
        [DataSourceProperty]
        public bool HasKindnesses
        {
            get => _hasKindnesses;
            set
            {
                if (_hasKindnesses == value) return;
                _hasKindnesses = value;
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

                Appreciations.AppreciationBehavior behavior = Appreciations.AppreciationBehavior.Instance;
                if (behavior == null)
                {
                    Clear();

                    return;
                }

                var today = (int) CampaignTime.Now.ToDays;
                IReadOnlyList<Appreciation> known = behavior.KnownAppreciationsFor(hero.StringId, today);

                int activeGrudgeMagnitude = ResolveDominantLiveGrudgeMagnitude(hero, today);
                CalradiaRemembers.Logic.Appreciations.TraitFacts traits = Appreciations.AppreciationBridge.BuildTraitFacts(hero);

                var lines = new List<string>();
                foreach (Appreciation appreciation in known)
                {
                    string line = BuildLine(appreciation, today, traits, activeGrudgeMagnitude);
                    if (!string.IsNullOrEmpty(line)) lines.Add(line);
                }

                if (lines.Count == 0)
                {
                    Clear();

                    return;
                }

                CalradiaKindnessesLabel = "Kindnesses:";
                CalradiaKindnessesItems = Mapping.ShownText.Of(string.Join("\n", lines.Select(l => "• " + l)));
                HasKindnesses = true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[APPRECIATION] encyclopedia kindnesses refresh failed — showing nothing this session. {ex.GetType().Name}: {ex.Message}");
                Clear();
            }
        }

        private void Clear()
        {
            CalradiaKindnessesLabel = "";
            CalradiaKindnessesItems = "";
            HasKindnesses = false;
        }

        /// <summary>
        ///   One appreciation, rendered through the same pure <see cref="AppreciationNarrator.DisplayLine" />
        ///   the tests cover, with the effective magnitude first TARNISHED by the holder's own dominant live
        ///   grudge — a soured kindness shows dimmer (or, fully tarnished, not at all: DisplayLine returns
        ///   null at magnitude 0). Tolerant of a missing target hero (a stale StringId after a save) —
        ///   falls back to "them" via <see cref="AppreciationNarrator.DisplayLine" /> rather than skip the
        ///   line outright.
        /// </summary>
        private static string BuildLine(Appreciation appreciation, int today, CalradiaRemembers.Logic.Appreciations.TraitFacts traits, int activeGrudgeMagnitude)
        {
            bool targetIsPlayer = appreciation.TargetId == Hero.MainHero?.StringId;
            string targetName = targetIsPlayer ? null : ResolveName(appreciation.TargetId);

            int effective = AppreciationPolicy.EffectiveMagnitude(appreciation, today, traits);
            int tarnished = AppreciationPolicy.TarnishedMagnitude(effective, activeGrudgeMagnitude);

            return AppreciationNarrator.DisplayLine(appreciation, targetIsPlayer, targetName, tarnished);
        }

        /// <summary>The holder's dominant live grudge's TODAY's effective magnitude (0 when none) — the same lookup the prompt's ResolveAppreciationNote uses, kept independent here so this mixin stays a pure read of the two engine stores.</summary>
        private static int ResolveDominantLiveGrudgeMagnitude(Hero hero, int today)
        {
            IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);
            if (live == null || live.Count == 0) return 0;

            CalradiaRemembers.Logic.Grudges.TraitFacts grudgeTraits = GrudgeBridge.BuildTraitFacts(hero);

            return GrudgePolicy.EffectiveMagnitude(live[0], today, grudgeTraits);
        }

        private static string ResolveName(string heroId) => FindHero(heroId)?.Name?.ToString();

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        #endregion
    }
}
