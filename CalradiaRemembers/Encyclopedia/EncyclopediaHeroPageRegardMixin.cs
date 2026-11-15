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
//
// LapitaPolata (Discord, 15/11/2026): "The longer I play the game, the longer the lists have become." Each section's header
// is a switch that folds or shows its rows (Logic.FoldableSectionHeader writes it, with the count); the choice holds for
// every page until the game is closed.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
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
      private string _housesLabel;

      // The section labels before the count and the switch are added (FoldableSectionHeader), kept to rewrite the header on a click.
      private string _whyWords;
      private string _housesWords;

      // LapitaPolata (15/11/2026): whether each section's rows are shown. Static: the player's choice holds on every page
      // opened afterwards, until the game is closed.
      private static bool _whyShown = true;
      private static bool _housesShown = true;

      // Gabriel (22/10/2026): the reasons and the houses as rows, portrait or banners, status, reason, a coloured marker.
      private readonly MBBindingList<RegardPersonRowVM> _people = new();
      private readonly MBBindingList<RegardHouseRowVM> _houseRows = new();

      // cr.test all reaches a page's mixin through the page it built.
      private static readonly ConditionalWeakTable<EncyclopediaHeroPageVM, EncyclopediaHeroPageRegardMixin> ByPage = new();

      // cr.test npc_regard_reason reads the rows through hooks set here, the first time a page is built: Diagnostics
      // naming the encyclopedia (or the submodule doing it for them) would draw it into the namespace cycle.
      static EncyclopediaHeroPageRegardMixin()
      {
         Diagnostics.CrSelfTest.RegardPeopleOf = page => Of(page as EncyclopediaHeroPageVM)?.PeopleForDiagnostics
            .Select(r => (r.Person?.Hero, r.IsFriend, r.Reason)).ToList();
         Diagnostics.CrSelfTest.RegardHouseRowsOf = page => Of(page as EncyclopediaHeroPageVM)?.HouseRowsForDiagnostics
            .Select(r => (r.IsFriend, (IReadOnlyList<(IFaction, string)>) r.Houses.Select(h => (h.Faction?.Faction, h.Note)).ToList())).ToList();
      }

      /// <summary>cr.test all: the mixin UIExtenderEx attached to this page, or null.</summary>
      internal static EncyclopediaHeroPageRegardMixin Of(EncyclopediaHeroPageVM page)
         => page != null && ByPage.TryGetValue(page, out EncyclopediaHeroPageRegardMixin m) ? m : null;

      // The characters whose reason the Why block gives, so their house is named through them rather than said twice.
      private readonly HashSet<Hero> _explained = new();

      public EncyclopediaHeroPageRegardMixin(EncyclopediaHeroPageVM vm) : base(vm)
      {
         if (vm != null)
         {
            ByPage.Remove(vm); // net472 has no AddOrUpdate
            ByPage.Add(vm, this);
         }
         Refresh();

         // A later vanilla refresh (RefreshValues) runs this mixin's OnRefresh BEFORE it refills the lists, so the
         // vanilla lists won (cr.test npc_regard_reason, 17/10/2026: stillAfterTheVanillaRefresh=False). The page
         // says when its own fill is done: Refresh ends with IsLoadingOver = true. The lists are corrected then.
         if (vm != null) vm.PropertyChangedWithBoolValue += OnPagePropertyChanged;
      }

      public override void OnFinalize()
      {
         if (ViewModel != null) ViewModel.PropertyChangedWithBoolValue -= OnPagePropertyChanged;
         ClearRows();
         base.OnFinalize();
      }

      private void OnPagePropertyChanged(object sender, PropertyChangedWithBoolValueEventArgs e)
      {
         if (e.PropertyName == nameof(EncyclopediaHeroPageVM.IsLoadingOver) && e.Value) Refresh();
      }

      /// <summary>Cream header: "Why:" on the player's page, "Friends and enemies, and why:" on a character's, or empty (auto-hides).</summary>
      [DataSourceProperty]
      public string CalradiaRegardWhyLabel
      {
         get => _label;
         set
         {
            if (_label == value) return;
            _label = value;
            ViewModel?.OnPropertyChanged();
            ViewModel?.OnPropertyChanged(nameof(CalradiaRegardHasWhy));
         }
      }

      /// <summary>Whether the friends-and-enemies rows are shown (the player folds them by clicking the header).</summary>
      [DataSourceProperty]
      public bool CalradiaRegardWhyShown => _whyShown;

      /// <summary>Whether the house rows are shown (the player folds them by clicking the header).</summary>
      [DataSourceProperty]
      public bool CalradiaRegardHousesShown => _housesShown;

      /// <summary>Whether the friends-and-enemies header has anything to head (it is hidden with its section otherwise).</summary>
      [DataSourceProperty]
      public bool CalradiaRegardHasWhy => !string.IsNullOrEmpty(_label);

      /// <summary>Whether the houses header has anything to head.</summary>
      [DataSourceProperty]
      public bool CalradiaRegardHasHouses => !string.IsNullOrEmpty(_housesLabel);

      /// <summary>The friends-and-enemies header was clicked: fold or show its rows, here and on every page after.</summary>
      [DataSourceMethod]
      public void ExecuteToggleRegardWhy()
      {
         _whyShown = !_whyShown;
         CalradiaRegardWhyLabel = FoldableSectionHeader.Text(_whyWords, _people.Count, _whyShown);
         ViewModel?.OnPropertyChanged(nameof(CalradiaRegardWhyShown));
      }

      /// <summary>The houses header was clicked: fold or show its rows, here and on every page after.</summary>
      [DataSourceMethod]
      public void ExecuteToggleRegardHouses()
      {
         _housesShown = !_housesShown;
         CalradiaRegardHousesLabel = FoldableSectionHeader.Text(_housesWords, _houseRows.Count, _housesShown);
         ViewModel?.OnPropertyChanged(nameof(CalradiaRegardHousesShown));
      }

      /// <summary>One row per listed friend or enemy: portrait, status, reason, marker. Empty when none.</summary>
      [DataSourceProperty]
      public MBBindingList<RegardPersonRowVM> CalradiaRegardPeople => _people;

      /// <summary>One row per reason the houses share: their banners, the reason, marker. Empty when none.</summary>
      [DataSourceProperty]
      public MBBindingList<RegardHouseRowVM> CalradiaRegardHouseRows => _houseRows;

      /// <summary>Cream header, "Houses:" or empty (auto-hides).</summary>
      [DataSourceProperty]
      public string CalradiaRegardHousesLabel
      {
         get => _housesLabel;
         set
         {
            if (_housesLabel == value) return;
            _housesLabel = value;
            ViewModel?.OnPropertyChanged();
            ViewModel?.OnPropertyChanged(nameof(CalradiaRegardHasHouses));
         }
      }

      /// <summary>cr.test all: the person rows as shown (status, reason, friend), in order.</summary>
      internal IReadOnlyList<RegardPersonRowVM> PeopleForDiagnostics => _people.ToList();

      /// <summary>cr.test all: the house rows as shown, in order.</summary>
      internal IReadOnlyList<RegardHouseRowVM> HouseRowsForDiagnostics => _houseRows.ToList();

      public override void OnRefresh()
      {
         Refresh();
      }

      #region private

      private void Refresh()
      {
         CalradiaRegardWhyLabel = "";
         CalradiaRegardHousesLabel = "";
         _whyWords = null;
         _housesWords = null;
         ClearRows();
         _explained.Clear();

         try
         {
            Hero hero = HeroField?.GetValue(ViewModel) as Hero;
            if (hero == null || ViewModel == null || ViewModel.IsInformationHidden) return;

            if (hero == Hero.MainHero) RefillThePlayersPage();
            else RefillACharactersPage(hero);

            ShowHouses(hero);
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

         var rows = new List<RegardPersonRowVM>();
         foreach ((Hero h, int regard) in contempt.OrderBy(x => x.Regard).Concat(dear.OrderByDescending(x => x.Regard)).Take(MaxReasons))
            rows.Add(new RegardPersonRowVM(h, $"{Capitalised(RegardBands.Describe(regard))} ({regard:+#;-#;0})", DossierReader.WhyTowardThePlayer(h, regard), regard >= Regard.WarmTier));

         Show(rows, "Why:");
      }

      // The other houses, by their lord as this house's lord sees them (design 5.4; Gabriel's answer to Q1: listed by
      // regard, a war marked). On the player's page, by each lord's personal regard for the player.
      private void ShowHouses(Hero hero)
      {
         Hero lord = hero.Clan?.Leader ?? hero;
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         var houses = new List<ClanRegardCandidate>();

         foreach (Clan c in Clan.All)
         {
            Hero other = c?.Leader;
            if (c == null || c.IsEliminated || c.IsBanditFaction || c == hero.Clan || other == null || other == lord || !HeroPresence.IsInTheWorld(other)) continue;

            bool atWar = hero.MapFaction != null && c.MapFaction != null && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, c.MapFaction);
            if (hero == Hero.MainHero)
            {
               NpcProfile p = store?.Get(other.StringId);
               if (p == null) continue;
               PairRegardVerdict v = p.ReputationWithPlayer >= Regard.WarmTier ? PairRegardVerdict.Friend
                                   : p.ReputationWithPlayer <= Regard.WaryTier ? PairRegardVerdict.Enemy
                                   : PairRegardVerdict.Neither;
               if (v == PairRegardVerdict.Neither) continue;
               houses.Add(new ClanRegardCandidate {
                  Name = c.Name?.ToString(), Id = c.StringId, Derived = v, AtWar = atWar,
                  DerivedReason = DossierReader.WhyTowardThePlayer(other, p.ReputationWithPlayer) ?? $"{RegardBands.Describe(p.ReputationWithPlayer)} ({p.ReputationWithPlayer:+#;-#;0})"
               });
               continue;
            }

            PairRegard r = PairRegardReader.Assess(lord, other);
            houses.Add(new ClanRegardCandidate {
               Name = c.Name?.ToString(), Id = c.StringId, Derived = r.Verdict,
               DerivedReason = PairRegardPolicy.WithoutThePair(new PairReasonNames {Reason = PairRegardPolicy.PageReason(r), OneName = lord.Name?.ToString(), OtherName = other.Name?.ToString()}),
               VanillaRelation = Mapping.PairBondReader.Points(lord, other), AtWar = atWar,
               Through = lord == hero && _explained.Contains(other) ? other.Name?.ToString() : null,
               Lord = lord.Name?.ToString(), OtherLord = other.Name?.ToString()
            });
         }

         IReadOnlyList<ClanRegardGroup> groups = ClanRegardListPolicy.Groups(houses);
         if (groups.Count == 0) return;

         // Gabriel (22/10/2026): one row per reason, the houses' banners side by side, pale green for affinity and red
         // for odds (the colour replaces the "At odds" / "Of affinity" subtitles of 20/10); the war under its house.
         // Gabriel (23/10/2026): a house on the vanilla number alone has its own row, the banner then the line naming
         // both heads of house, the war on that line.
         // tashmetu (2/11/2026): on a member's page the heads' lines read as the member's own; say whose standing it is.
         _housesWords = ClanRegardListPolicy.HousesLabel(hero == Hero.MainHero || lord == hero, hero.Clan?.Name?.ToString(), lord.Name?.ToString());
         foreach (ClanRegardGroup g in groups)
         {
            var row = new RegardHouseRowVM(g.Reason == null ? "Named above, through their lord" : Capitalised(g.Reason), g.Affinity, g.OneHouse);
            foreach (ClanRegardHouse h in g.Houses)
            {
               Clan clan = Clan.All.FirstOrDefault(c => c?.StringId == h.Id);
               if (clan != null) row.Houses.Add(new RegardHouseVM(clan, g.OneHouse ? "" : HouseNote(h)));
            }

            if (row.Houses.Count > 0) _houseRows.Add(row);
         }

         CalradiaRegardHousesLabel = FoldableSectionHeader.Text(_housesWords, _houseRows.Count, _housesShown);
      }

      // Under a house's banner: the war, and the lord through whom it is explained above.
      private static string HouseNote(ClanRegardHouse h)
      {
         var parts = new List<string>();
         if (h.AtWar) parts.Add("at war");
         if (!string.IsNullOrWhiteSpace(h.Through)) parts.Add("through " + h.Through);

         return string.Join(", ", parts);
      }

      // A character's page: vanilla's lists, corrected by what really happened, with the reasons.
      private void RefillACharactersPage(Hero hero)
      {
         List<Hero> vanillaFriends = ViewModel.Allies.Concat(ViewModel.AdditionalAllies).Select(h => h.Hero).Where(h => h != null).ToList();
         List<Hero> vanillaEnemies = ViewModel.Enemies.Concat(ViewModel.AdditionalEnemies).Select(h => h.Hero).Where(h => h != null).ToList();

         var derivedFriends = new List<(Hero Hero, string Why)>();
         var derivedEnemies = new List<(Hero Hero, string Why)>();
         var turned = new HashSet<Hero>();

         // The page is looked at: the pairs it would show that have no story yet may draw their shared past (design 7.1),
         // the house lords of the realm and the neighbours too, at most six new pairs per opening.
         WorldEvents.SharedPastBehavior.Instance?.DrawFor(hero, vanillaFriends.Concat(vanillaEnemies).Concat(RealmLords(hero)));

         foreach (Hero other in vanillaFriends.Concat(vanillaEnemies).Concat(PairRegardReader.TiedTo(hero)).Distinct())
         {
            PairRegard r = PairRegardReader.Assess(hero, other);
            if (r.Verdict == PairRegardVerdict.Neither)
            {
               // kurushimi00 (06/11/2026): made peace and holding nothing since, they leave the game's own list too.
               if (r.Reconciled) turned.Add(other);

               continue;
            }

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

         var shown = derivedEnemies.Select(x => (x.Hero, x.Why, Friend: false)).Concat(derivedFriends.Select(x => (x.Hero, x.Why, Friend: true))).Take(MaxReasons).ToList();
         foreach (var s in shown) _explained.Add(s.Hero);
         // Gabriel (20/10/2026): under "Personal regard: unknown", a bare "Why:" read as the reason for the regard toward
         // the player, while these lines are the character's own friends and enemies. The label says whose they are.
         Show(shown.Select(x => new RegardPersonRowVM(x.Hero, $"{x.Hero.Name}, {(x.Friend ? "friend" : "enemy")}",
                                                      Capitalised(PairRegardPolicy.WithoutThePair(new PairReasonNames {Reason = x.Why, OneName = hero.Name?.ToString(), OtherName = x.Hero.Name?.ToString()})), x.Friend))
                   .ToList(), "Friends and enemies, and why:");
      }

      // The other house lords of the hero's realm: the people a lord would have crossed before the campaign.
      private static IEnumerable<Hero> RealmLords(Hero hero)
         => hero.MapFaction is Kingdom k
            ? k.Clans.Where(c => c != null && !c.IsEliminated && c != hero.Clan && c.Leader != null).Select(c => c.Leader)
            : Enumerable.Empty<Hero>();

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

      private static string Capitalised(string line)
         => string.IsNullOrEmpty(line) ? line : char.ToUpperInvariant(line[0]) + line.Substring(1);

      private void Show(List<RegardPersonRowVM> rows, string label)
      {
         if (rows.Count == 0) return;

         _whyWords = label;
         foreach (RegardPersonRowVM row in rows) _people.Add(row);
         CalradiaRegardWhyLabel = FoldableSectionHeader.Text(label, _people.Count, _whyShown);
      }

      private void ClearRows()
      {
         foreach (RegardPersonRowVM row in _people) row.OnFinalize();
         foreach (RegardHouseRowVM row in _houseRows) row.OnFinalize();
         _people.Clear();
         _houseRows.Clear();
      }

      #endregion
   }
}
