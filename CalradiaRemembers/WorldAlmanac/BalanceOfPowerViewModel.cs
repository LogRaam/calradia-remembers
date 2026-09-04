// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac Inc 3, the data layer of the "Balance of Power" panel: binds a computed PowerIndexResult (+ the
// raw facts each card shows) into the observable properties the Gauntlet prefab renders. Pure presentation
// shaping, no engine calls of its own: the manager gathers the facts (KingdomPowerFactsBuilder), scores them
// (PowerIndexPolicy) and hands both here. The prefab follows THESE property names; positions and art are
// iterated on Gabriel's panel background (cr_BalanceOfPower.jpg). Momentum and the Strategic Assessment are
// later increments, so their fields are placeholders for now.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>The whole "Balance of Power" dashboard as bindable data: the top-3 podium, the full ranked list, the key indicators, and the field totals.</summary>
   public sealed class BalanceOfPowerViewModel : ViewModel
   {
      private MBBindingList<PowerCardViewModel> _podium;
      private MBBindingList<PowerRowViewModel> _ranked;

      public BalanceOfPowerViewModel(PowerIndexResult result, IReadOnlyList<KingdomPowerFacts> facts, string dateText = "", string politicalMapPath = "")
      {
         _podium = new MBBindingList<PowerCardViewModel>();
         _ranked = new MBBindingList<PowerRowViewModel>();

         result ??= new PowerIndexResult();
         DateText = dateText ?? "";
         PoliticalMapImagePath = politicalMapPath ?? "";
         KingdomPowerScore leader = result.Ranked.FirstOrDefault();
         LeaderText = leader == null
            ? ""
            : $"The {leader.KingdomName} currently holds the strongest position in Calradia.";
         Dictionary<string, KingdomPowerFacts> byId = (facts ?? new List<KingdomPowerFacts>())
                                                     .Where(f => f?.KingdomId != null)
                                                     .GroupBy(f => f.KingdomId)
                                                     .ToDictionary(g => g.Key, g => g.First());

         foreach (KingdomPowerScore s in result.Ranked)
         {
            byId.TryGetValue(s.KingdomId, out KingdomPowerFacts f);
            _ranked.Add(new PowerRowViewModel(s, f));
            if (s.Rank <= 3) _podium.Add(new PowerCardViewModel(s, f));
         }

         KeyIndicators ki = result.Indicators ?? new KeyIndicators();
         MostPowerfulArmy = $"{NameOf(byId, ki.MostPowerfulArmyKingdomId)} ({ki.MostPowerfulArmyTroops:N0})";
         LargestTerritory = $"{NameOf(byId, ki.LargestTerritoryKingdomId)} ({ki.LargestTerritorySettlements} settlements)";
         StrongestEconomy = $"{NameOf(byId, ki.StrongestEconomyKingdomId)} ({ki.StrongestEconomyWealth:N0} denars)";
         MostInfluentialLeader = string.IsNullOrEmpty(ki.MostInfluentialLeaderName)
            ? NameOf(byId, ki.MostInfluentialLeaderKingdomId)
            : $"{ki.MostInfluentialLeaderName} ({ki.MostInfluentialLeaderInfluence})";
         BestWarRecord = $"{NameOf(byId, ki.BestWarRecordKingdomId)} ({ki.BestWarRecordVictories}W / {ki.BestWarRecordDefeats}L)";

         PowerFieldTotals t = result.Totals ?? new PowerFieldTotals();
         TotalTroops = t.TotalTroops.ToString("N0");
         TotalSettlements = t.TotalSettlements.ToString();
         TotalClans = t.TotalClans.ToString();
         TotalWealth = t.TotalWealth.ToString("N0");
      }

      /// <summary>Invoked by the close button; the manager wires it to tear the layer down.</summary>
      public System.Action OnCloseRequested { get; set; }

      [DataSourceProperty] public string Title => "BALANCE OF POWER";

      /// <summary>The in-game date line under the painted title ("Day 12 of Spring, 1092"). Gathered by the manager (this view model makes no engine calls of its own).</summary>
      [DataSourceProperty] public string DateText { get; }

      /// <summary>The line under the date naming the faction in first place; empty when no kingdom was scored.</summary>
      [DataSourceProperty] public string LeaderText { get; }

      [DataSourceProperty] public string Tagline => "Power is not given. It is remembered.";

      /// <summary>Full path to the panel's leather background, for the prefab's CrRuntimeImageWidget (same recipe as the council book).</summary>
      [DataSourceProperty]
      public string BackgroundPath => System.IO.Path.Combine(
         BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", "cr_BalanceOfPower.jpg");

      /// <summary>
      ///   Full path to the political minimap PNG rendered for THIS open (empty when it could not be built). The
      ///   manager renders it and passes the path in, so the view model makes no engine calls; the prefab's central
      ///   CrRuntimeImageWidget binds it and hides itself via <see cref="HasPoliticalMap" /> when it is empty.
      /// </summary>
      [DataSourceProperty] public string PoliticalMapImagePath { get; }

      /// <summary>True when a political map was rendered, so the central image widget shows only when there is one.</summary>
      [DataSourceProperty] public bool HasPoliticalMap => !string.IsNullOrEmpty(PoliticalMapImagePath);

      [DataSourceProperty] public string BalanceHeader => "Balance of Power";
      [DataSourceProperty] public string AssessmentHeader => "Strategic Assessment";
      [DataSourceProperty] public string IndicatorsHeader => "Key Indicators";

      [DataSourceProperty]
      public MBBindingList<PowerCardViewModel> Podium
      {
         get => _podium;
         set
         {
            if (value == _podium) return;
            _podium = value;
            OnPropertyChangedWithValue(value);
         }
      }

      // The top three as three INDEPENDENT bindings, so the prefab can place each card absolutely over its own
      // painted frame instead of flowing all three through one ListPanel (which cannot align each to a fixed
      // frame). Null when fewer than three kingdoms exist; the card's widget then simply binds to nothing.
      [DataSourceProperty] public PowerCardViewModel First => _podium.Count > 0 ? _podium[0] : null;
      [DataSourceProperty] public PowerCardViewModel Second => _podium.Count > 1 ? _podium[1] : null;
      [DataSourceProperty] public PowerCardViewModel Third => _podium.Count > 2 ? _podium[2] : null;

      [DataSourceProperty]
      public MBBindingList<PowerRowViewModel> Ranked
      {
         get => _ranked;
         set
         {
            if (value == _ranked) return;
            _ranked = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty] public string MostPowerfulArmy { get; }
      [DataSourceProperty] public string LargestTerritory { get; }
      [DataSourceProperty] public string StrongestEconomy { get; }
      [DataSourceProperty] public string MostInfluentialLeader { get; }
      [DataSourceProperty] public string BestWarRecord { get; }

      [DataSourceProperty] public string TotalTroops { get; }
      [DataSourceProperty] public string TotalSettlements { get; }
      [DataSourceProperty] public string TotalClans { get; }
      [DataSourceProperty] public string TotalWealth { get; }

      // Placeholder until the persisted battle ledger lands (Strategic Assessment is LLM-written, cached).
      [DataSourceProperty] public string StrategicAssessment => "The realms of Calradia hold their ground; no chronicler's assessment has been drawn up yet.";

      public void ExecuteClose() => OnCloseRequested?.Invoke();

      private static string NameOf(IReadOnlyDictionary<string, KingdomPowerFacts> byId, string id)
         => id != null && byId.TryGetValue(id, out KingdomPowerFacts f) ? f.KingdomName : id ?? "(none)";
   }

   /// <summary>One top-3 podium card: the kingdom, its Power Index, its six pillars (0..100 + 0..1 bar fill), and the display facts strip.</summary>
   public sealed class PowerCardViewModel : ViewModel
   {
      public PowerCardViewModel(KingdomPowerScore s, KingdomPowerFacts f)
      {
         Rank = s.Rank;
         KingdomName = s.KingdomName;
         PowerIndexText = s.PowerIndex.ToString("F1");

         // Text bindings must be strings: a Gauntlet TextWidget bound to an int throws at RefreshBinding
         // ("Int32 cannot be converted to String"). The numeric fills below stay double for the bars.
         Military = System.Math.Round(s.Military).ToString("0");
         Territory = System.Math.Round(s.Territory).ToString("0");
         Economy = System.Math.Round(s.Economy).ToString("0");
         Political = System.Math.Round(s.Political).ToString("0");
         WarPerformance = System.Math.Round(s.WarPerformance).ToString("0");
         Diplomacy = System.Math.Round(s.Diplomatic).ToString("0");

         MilitaryFill = (float) (s.Military / 100.0);
         TerritoryFill = (float) (s.Territory / 100.0);
         EconomyFill = (float) (s.Economy / 100.0);
         PoliticalFill = (float) (s.Political / 100.0);
         WarFill = (float) (s.WarPerformance / 100.0);
         DiplomacyFill = (float) (s.Diplomatic / 100.0);

         if (f != null)
         {
            Troops = (f.FieldTroops + f.GarrisonTroops).ToString("N0");
            Armies = f.ArmyCount.ToString();
            Towns = f.Towns.ToString();
            Castles = f.Castles.ToString();
            Villages = f.Villages.ToString();
            Clans = f.ClanCount.ToString();
            Vassals = f.VassalCount.ToString();
            Wealth = f.EstimatedWealth.ToString("N0");
            LeaderName = f.LeaderName;
            LeaderInfluence = f.LeaderInfluence.ToString();

            BannerId = f.BannerId ?? "";
            BannerTextureProvider = f.BannerTextureProvider ?? "";
            BannerArgs = f.BannerArgs ?? "";
            LeaderPortraitId = f.LeaderPortraitId ?? "";
            LeaderPortraitTextureProvider = f.LeaderPortraitTextureProvider ?? "";
            LeaderPortraitArgs = f.LeaderPortraitArgs ?? "";
         }
      }

      [DataSourceProperty] public int Rank { get; }
      [DataSourceProperty] public string KingdomName { get; }
      [DataSourceProperty] public string PowerIndexText { get; }

      [DataSourceProperty] public string Military { get; }
      [DataSourceProperty] public string Territory { get; }
      [DataSourceProperty] public string Economy { get; }
      [DataSourceProperty] public string Political { get; }
      [DataSourceProperty] public string WarPerformance { get; }
      [DataSourceProperty] public string Diplomacy { get; }

      // 0..1 fills for the pillar bars (FillBarWidget.InitialAmountAsFloat binds a float; a double would not
      // convert). The readable 0..100 scores stay in the string properties above.
      [DataSourceProperty] public float MilitaryFill { get; }
      [DataSourceProperty] public float TerritoryFill { get; }
      [DataSourceProperty] public float EconomyFill { get; }
      [DataSourceProperty] public float PoliticalFill { get; }
      [DataSourceProperty] public float WarFill { get; }
      [DataSourceProperty] public float DiplomacyFill { get; }

      // The kingdom banner, left of the name (flat id/provider/args, the chat's portrait recipe).
      [DataSourceProperty] public string BannerId { get; } = "";
      [DataSourceProperty] public string BannerTextureProvider { get; } = "";
      [DataSourceProperty] public string BannerArgs { get; } = "";
      [DataSourceProperty] public bool HasBanner => !string.IsNullOrEmpty(BannerId);

      // The ruler's portrait, right of the PowerIndex. Three tableaus total (one per podium card), created at
      // movie load and freed with it on close (bounded), so no portrait-guard bookkeeping is needed here.
      [DataSourceProperty] public string LeaderPortraitId { get; } = "";
      [DataSourceProperty] public string LeaderPortraitTextureProvider { get; } = "";
      [DataSourceProperty] public string LeaderPortraitArgs { get; } = "";
      [DataSourceProperty] public bool HasLeaderPortrait => !string.IsNullOrEmpty(LeaderPortraitId);

      [DataSourceProperty] public string Troops { get; } = "0";
      [DataSourceProperty] public string Armies { get; } = "0";
      [DataSourceProperty] public string Towns { get; } = "0";
      [DataSourceProperty] public string Castles { get; } = "0";
      [DataSourceProperty] public string Villages { get; } = "0";
      [DataSourceProperty] public string Clans { get; } = "0";
      [DataSourceProperty] public string Vassals { get; } = "0";
      [DataSourceProperty] public string Wealth { get; } = "0";
      [DataSourceProperty] public string LeaderName { get; } = "";
      [DataSourceProperty] public string LeaderInfluence { get; } = "0";
   }

   /// <summary>One row of the full "Balance of Power" list: the kingdom, its Power Index (number + 0..1 bar fill), and its momentum (placeholder until history exists).</summary>
   public sealed class PowerRowViewModel : ViewModel
   {
      public PowerRowViewModel(KingdomPowerScore s, KingdomPowerFacts f)
      {
         Rank = s.Rank;
         KingdomName = s.KingdomName;
         PowerIndex = System.Math.Round(s.PowerIndex).ToString("0"); // string: a Text binding to an int throws
         PowerFill = (float) (s.PowerIndex / 100.0); // FillBarWidget.InitialAmountAsFloat binds a float; a double would not bind
         MomentumText = "";       // filled once the persisted Power Index history lands
         MomentumIsUp = false;
      }

      [DataSourceProperty] public int Rank { get; }
      [DataSourceProperty] public string KingdomName { get; }
      [DataSourceProperty] public string PowerIndex { get; }
      [DataSourceProperty] public float PowerFill { get; }
      [DataSourceProperty] public string MomentumText { get; }
      [DataSourceProperty] public bool MomentumIsUp { get; }
   }
}
