// Code written by Gabriel Mailhot, 17/10/2026.
// The encyclopedia's Friends and Enemies with their reasons (docs/design/NPC_REGARD.md section 8). Gabriel, 17/10/2026:
// the player's own page listed as a friend a lord whose personal regard was a bitter grudge, and every friend or enemy
// shown must carry its reason. Route (b) of the design: the vanilla lists are refilled from this mixin's refresh, which
// UIExtenderEx runs after the page's own Refresh (the same path RelabelClanRelation already uses on 1.4.8), so the
// portraits, tooltips and links stay vanilla's. The reasons go in a "Why" block below the page's info.
//
// - The player's page: who holds the player dear or in contempt, by each character's personal regard (the mod's own
//   ledger), with the strongest known grudge or kindness as the reason.
// - A character's page: the vanilla lists as they were, less anyone what really happened says is the opposite, plus
//   anyone it makes a friend or enemy with a reason the player may know (PairRegardPolicy.PageReason). A page where
//   nothing derived applies is left exactly as vanilla built it.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>Refills a hero page's Friends and Enemies from real regard, and says why in a "Why" block.</summary>
   [ViewModelMixin]
   public sealed class EncyclopediaHeroPageRegardMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
   {
      /// <summary>Vanilla shows this many portraits before the "+N" overflow (EncyclopediaHeroPageVM._alliesEnemiesCapacity).</summary>
      public const int ShownPerList = 13;

      /// <summary>At most this many reasons in the "Why" block.</summary>
      public const int MaxReasons = 10;

      private static readonly FieldInfo HeroField =
         typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

      private static readonly MethodInfo OverflowUpdated =
         typeof(EncyclopediaHeroPageVM).GetMethod("OnAdditionalListsUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

      private string _label;
      private string _items;

      public EncyclopediaHeroPageRegardMixin(EncyclopediaHeroPageVM vm) : base(vm)
      {
         Refresh();
      }

      /// <summary>Cream header, "Why:" or empty (auto-hides).</summary>
      [DataSourceProperty]
      public string CalradiaRegardWhyLabel
      {
         get => _label;
         set
         {
            if (_label == value) return;
            _label = value;
            ViewModel?.OnPropertyChanged();
         }
      }

      /// <summary>Gold lines, one reason per listed friend or enemy. Empty when none (auto-hides).</summary>
      [DataSourceProperty]
      public string CalradiaRegardWhyItems
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
         CalradiaRegardWhyLabel = "";
         CalradiaRegardWhyItems = "";

         try
         {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            if (hero == null || ViewModel == null || ViewModel.IsInformationHidden) return;

            if (hero == Hero.MainHero) RefillThePlayersPage();
            else RefillACharactersPage(hero);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ENCYCLOPEDIA] friends and enemies by regard failed (non-fatal, vanilla lists kept): {ex.GetType().Name}: {ex.Message}");
         }
      }

      // The player has no ledger of their own regard: the lists say what others think of them (design 8.1).
      private void RefillThePlayersPage()
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return;

         var dear = new List<(Hero Hero, int Regard)>();
         var contempt = new List<(Hero Hero, int Regard)>();
         foreach (string id in store.ListIds())
         {
            NpcProfile p = store.Get(id);
            Hero h = p == null ? null : Hero.FindFirst(x => x.StringId == id);
            if (h == null || h == Hero.MainHero || !HeroPresence.IsInTheWorld(h) || h.IsChild) continue;

            if (p.ReputationWithPlayer >= Regard.WarmTier) dear.Add((h, p.ReputationWithPlayer));
            else if (p.ReputationWithPlayer <= Regard.WaryTier) contempt.Add((h, p.ReputationWithPlayer));
         }

         List<Hero> friends = dear.OrderByDescending(x => x.Regard).Select(x => x.Hero).ToList();
         List<Hero> enemies = contempt.OrderBy(x => x.Regard).Select(x => x.Hero).ToList();
         Fill(friends, enemies);
         ViewModel.AlliesText = "Hold you dear";
         ViewModel.EnemiesText = "Hold you in contempt";

         var lines = new List<string>();
         foreach ((Hero h, int regard) in contempt.OrderBy(x => x.Regard).Concat(dear.OrderByDescending(x => x.Regard)).Take(MaxReasons))
            lines.Add($"• {h.Name}: {WhyTowardThePlayer(h) ?? $"{RegardBands.Describe(regard)} ({regard:+#;-#;0})"}");

         Show(lines);
      }

      // The strongest grudge or kindness toward the player that the player knows of.
      private static string WhyTowardThePlayer(Hero h)
      {
         int today = (int) CampaignTime.Now.ToDays;
         string main = Hero.MainHero?.StringId;
         var grudge = GrudgeBehavior.Instance?.KnownGrudgesFor(h.StringId, today).FirstOrDefault(g => g.TargetId == main);
         if (grudge != null) return ShownText.Of(grudge.CauseSummary);

         var kindness = AppreciationBehavior.Instance?.KnownAppreciationsFor(h.StringId, today).FirstOrDefault(a => a.TargetId == main);

         return kindness == null ? null : ShownText.Of(kindness.CauseSummary);
      }

      // A character's page: vanilla's lists, corrected by what really happened, with the reasons.
      private void RefillACharactersPage(Hero hero)
      {
         List<Hero> vanillaFriends = ViewModel.Allies.Concat(ViewModel.AdditionalAllies).Select(h => h.Hero).Where(h => h != null).ToList();
         List<Hero> vanillaEnemies = ViewModel.Enemies.Concat(ViewModel.AdditionalEnemies).Select(h => h.Hero).Where(h => h != null).ToList();

         var derivedFriends = new List<(Hero Hero, string Why)>();
         var derivedEnemies = new List<(Hero Hero, string Why)>();
         var turned = new HashSet<Hero>();

         foreach (Hero other in vanillaFriends.Concat(vanillaEnemies).Concat(PairRegardReader.TiedTo(hero)).Distinct())
         {
            PairRegard r = PairRegardReader.Assess(hero, other);
            if (r.Verdict == PairRegardVerdict.Neither) continue;

            turned.Add(other);
            string why = PairRegardPolicy.PageReason(r);
            if (why == null) continue; // a reason the player may not read: dropped from the wrong list, listed nowhere

            if (r.Verdict == PairRegardVerdict.Friend) derivedFriends.Add((other, why));
            else derivedEnemies.Add((other, why));
         }

         if (turned.Count == 0) return; // nothing real applies: vanilla's page stands as built

         List<Hero> friends = derivedFriends.Select(x => x.Hero).Concat(vanillaFriends.Where(h => !turned.Contains(h))).ToList();
         List<Hero> enemies = derivedEnemies.Select(x => x.Hero).Concat(vanillaEnemies.Where(h => !turned.Contains(h))).ToList();
         Fill(friends, enemies);

         Show(derivedEnemies.Select(x => $"• {x.Hero.Name} (enemy): {x.Why}")
                            .Concat(derivedFriends.Select(x => $"• {x.Hero.Name} (friend): {x.Why}"))
                            .Take(MaxReasons).ToList());
      }

      private void Fill(List<Hero> friends, List<Hero> enemies)
      {
         FillOne(ViewModel.Allies, ViewModel.AdditionalAllies, friends);
         FillOne(ViewModel.Enemies, ViewModel.AdditionalEnemies, enemies);
         OverflowUpdated?.Invoke(ViewModel, null);
      }

      private static void FillOne(MBBindingList<HeroVM> shown, MBBindingList<HeroVM> overflow, List<Hero> heroes)
      {
         shown.Clear();
         overflow.Clear();
         for (int i = 0; i < heroes.Count; i++)
            (i < ShownPerList ? shown : overflow).Add(new HeroVM(heroes[i]));
      }

      private void Show(List<string> lines)
      {
         if (lines.Count == 0) return;

         CalradiaRegardWhyLabel = "Why:";
         CalradiaRegardWhyItems = string.Join("\n", lines);
      }

      #endregion
   }
}
