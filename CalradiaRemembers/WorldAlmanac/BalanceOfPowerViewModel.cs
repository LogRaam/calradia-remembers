// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.Portraits;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   The whole "Balance of Power" dashboard as bindable data: the top-3 podium, the full ranked list, the key
   ///   indicators, and the field totals.
   /// </summary>
   public sealed class BalanceOfPowerViewModel : ViewModel
   {
      // The strategist voicing the assessment, shown beside their counsel (like the chat avatar). Set once by the
      // manager at open via ApplyAdvisorPortrait, before the movie binds, so plain fields suffice. Only a COMPANION
      // has a portrait; a soldier or the alone/offline path leaves HasAdvisor false and the block hides.
      private MBBindingList<PowerCardViewModel> _podium;
      private MBBindingList<PowerRowViewModel> _ranked;

      // The companion strategist's counsel. Settable so the manager can drop in a "the council debates.." line at
      // open and swap in the real assessment when the async LLM call lands (or set an offline summary directly when
      // the player travels alone). Fires OnPropertyChanged so the open panel updates live.
      private string _strategicAssessment = "";

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

      [DataSourceProperty]
      public string AdvisorCustomPortraitPath { get; private set; } = "";

      [DataSourceProperty]
      public string AdvisorName { get; private set; } = "";

      [DataSourceProperty]
      public string AdvisorPortraitArgs { get; private set; } = "";

      [DataSourceProperty]
      public float AdvisorPortraitHeight { get; private set; } = 120f;

      [DataSourceProperty]
      public string AdvisorPortraitId { get; private set; } = "";

      [DataSourceProperty]
      public string AdvisorPortraitTextureProvider { get; private set; } = "";

      [DataSourceProperty]
      public float AdvisorPortraitWidth { get; private set; } = 240f;

      [DataSourceProperty]
      public string AssessmentHeader => "Strategic Assessment";

      /// <summary>
      ///   Full path to the panel's leather background, for the prefab's CrRuntimeImageWidget (same recipe as the council
      ///   book).
      /// </summary>
      [DataSourceProperty]
      public string BackgroundPath => Path.Combine(
         CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_BalanceOfPower.jpg");

      [DataSourceProperty]
      public string BalanceHeader => "Balance of Power";

      [DataSourceProperty]
      public string BestWarRecord { get; }

      /// <summary>
      ///   The in-game date line under the painted title ("Day 12 of Spring, 1092"). Gathered by the manager (this view
      ///   model makes no engine calls of its own).
      /// </summary>
      [DataSourceProperty]
      public string DateText { get; }

      // The top three as three INDEPENDENT bindings, so the prefab can place each card absolutely over its own
      // painted frame instead of flowing all three through one ListPanel (which cannot align each to a fixed
      // frame). Null when fewer than three kingdoms exist; the card's widget then simply binds to nothing.
      [DataSourceProperty]
      public PowerCardViewModel First => _podium.Count > 0
         ? _podium[0]
         : null;

      [DataSourceProperty]
      public bool HasAdvisor { get; private set; }

      [DataSourceProperty]
      public bool HasAdvisorCustomPortrait { get; private set; }

      /// <summary>True when a political map was rendered, so the central image widget shows only when there is one.</summary>
      [DataSourceProperty]
      public bool HasPoliticalMap => !string.IsNullOrEmpty(PoliticalMapImagePath);

      [DataSourceProperty]
      public string IndicatorsHeader => "Key Indicators";

      [DataSourceProperty]
      public string LargestTerritory { get; }

      /// <summary>The line under the date naming the faction in first place; empty when no kingdom was scored.</summary>
      [DataSourceProperty]
      public string LeaderText { get; }

      [DataSourceProperty]
      public string MostInfluentialLeader { get; }

      [DataSourceProperty]
      public string MostPowerfulArmy { get; }

      /// <summary>Invoked by the close button; the manager wires it to tear the layer down.</summary>
      public Action OnCloseRequested { get; set; }

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

      /// <summary>
      ///   Full path to the political minimap PNG rendered for THIS open (empty when it could not be built). The
      ///   manager renders it and passes the path in, so the view model makes no engine calls; the prefab's central
      ///   CrRuntimeImageWidget binds it and hides itself via <see cref="HasPoliticalMap" /> when it is empty.
      /// </summary>
      [DataSourceProperty]
      public string PoliticalMapImagePath { get; }

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

      [DataSourceProperty]
      public PowerCardViewModel Second => _podium.Count > 1
         ? _podium[1]
         : null;

      [DataSourceProperty]
      public bool ShowAdvisorTableau { get; private set; }

      [DataSourceProperty]
      public string StrategicAssessment
      {
         get => _strategicAssessment;
         set
         {
            if (value == _strategicAssessment) return;
            _strategicAssessment = value ?? "";
            OnPropertyChangedWithValue(_strategicAssessment);
         }
      }

      [DataSourceProperty]
      public string StrongestEconomy { get; }

      [DataSourceProperty]
      public string Tagline => "Power is not given. It is remembered.";

      [DataSourceProperty]
      public PowerCardViewModel Third => _podium.Count > 2
         ? _podium[2]
         : null;

      [DataSourceProperty]
      public string Title => "BALANCE OF POWER";

      [DataSourceProperty]
      public string TotalClans { get; }

      [DataSourceProperty]
      public string TotalSettlements { get; }

      [DataSourceProperty]
      public string TotalTroops { get; }

      [DataSourceProperty]
      public string TotalWealth { get; }

      /// <summary>
      ///   Sets the strategist's portrait beside the assessment (the chat's recipe): a custom image if the hero has
      ///   one, else the 3D render, never both (the tableau fields are left empty when a custom exists, so only one
      ///   draws). Called by the manager at open; a soldier / alone path never calls it, so no portrait shows.
      /// </summary>
      public void ApplyAdvisorPortrait(string name, string tableauId, string tableauProvider, string tableauArgs, string customPath, float customWidth, float customHeight)
      {
         AdvisorName = name ?? "";

         bool hasCustom = !string.IsNullOrEmpty(customPath);
         AdvisorCustomPortraitPath = hasCustom
            ? customPath
            : "";
         AdvisorPortraitId = hasCustom
            ? ""
            : tableauId ?? "";
         AdvisorPortraitTextureProvider = hasCustom
            ? ""
            : tableauProvider ?? "";
         AdvisorPortraitArgs = hasCustom
            ? ""
            : tableauArgs ?? "";

         HasAdvisorCustomPortrait = hasCustom;
         ShowAdvisorTableau = !hasCustom && !string.IsNullOrEmpty(AdvisorPortraitId);
         HasAdvisor = hasCustom || ShowAdvisorTableau || !string.IsNullOrEmpty(AdvisorName);

         if (!hasCustom || !(customWidth > 0) || !(customHeight > 0)) return;

         AdvisorPortraitWidth = customWidth;
         AdvisorPortraitHeight = customHeight;
      }

      public void ExecuteClose() => OnCloseRequested?.Invoke();

      // The four painted buttons along the bottom-left (Detailed View / Diplomacy / History / Rumors & Events). Each
      // is a future screen; for now every one raises the same "Available Soon" popup, named per button so filling one
      // in later is just replacing its body.
      public void ExecuteDetailedView() => ShowComingSoon("Detailed View");
      public void ExecuteDiplomacy() => ShowComingSoon("Diplomacy");
      public void ExecuteHistory() => ShowComingSoon("History");
      public void ExecuteRumors() => ShowComingSoon("Rumors & Events");

      #region private

      private static string NameOf(IReadOnlyDictionary<string, KingdomPowerFacts> byId, string id)
         => id != null && byId.TryGetValue(id, out KingdomPowerFacts f)
            ? f.KingdomName
            : id ?? "(none)";

      // A simple modal popup. Works over this pushed panel the same way the chat window's own inquiries do. Guarded so
      // a popup failure can never take the panel down.
      private static void ShowComingSoon(string feature)
      {
         try
         {
            InformationManager.ShowInquiry(new InquiryData(
               feature, "Available Soon.",
               true, false,
               "OK", null,
               () => { }, null));
         }
         catch (Exception ex)
         {
            ModLog.Error($"BalanceOfPower ShowComingSoon({feature}) threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }

   /// <summary>
   ///   One top-3 podium card: the kingdom, its Power Index, its six pillars (0..100 + 0..1 bar fill), and the display
   ///   facts strip.
   /// </summary>
   public sealed class PowerCardViewModel : ViewModel
   {
      private const float PortraitBoxHeight = 165f;

      // A player-dropped custom portrait wins over the 3D render (the chat's recipe). PortraitBox* is the slot the
      // image is fitted into; keep it in sync with the prefab's portrait widget size.
      private const float PortraitBoxWidth = 265f;

      public PowerCardViewModel(KingdomPowerScore s, KingdomPowerFacts f)
      {
         Rank = s.Rank;
         KingdomName = s.KingdomName;
         PowerIndexText = s.PowerIndex.ToString("F1");

         // Text bindings must be strings: a Gauntlet TextWidget bound to an int throws at RefreshBinding
         // ("Int32 cannot be converted to String"). The numeric fills below stay double for the bars.
         Military = Math.Round(s.Military).ToString("0");
         Territory = Math.Round(s.Territory).ToString("0");
         Economy = Math.Round(s.Economy).ToString("0");
         Political = Math.Round(s.Political).ToString("0");
         WarPerformance = Math.Round(s.WarPerformance).ToString("0");
         Diplomacy = Math.Round(s.Diplomatic).ToString("0");

         MilitaryFill = (float) (s.Military / 100.0);
         TerritoryFill = (float) (s.Territory / 100.0);
         EconomyFill = (float) (s.Economy / 100.0);
         PoliticalFill = (float) (s.Political / 100.0);
         WarFill = (float) (s.WarPerformance / 100.0);
         DiplomacyFill = (float) (s.Diplomatic / 100.0);

         if (f == null) return;

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
         // A player-dropped custom portrait (if any) REPLACES the 3D render. When one exists, the render fields
         // are left EMPTY so the tableau widget has no character to draw at all: that is what actually stops the
         // two portraits overlapping (the tableau is a render-target widget that kept drawing even with its
         // IsVisible bound false). Its aspect is fitted inside the portrait box so a non-square image is never
         // stretched; unknown dimensions fall back to a square.
         LeaderCustomPortraitPath = f.LeaderCustomPortraitPath ?? "";
         HasCustomLeaderPortrait = !string.IsNullOrEmpty(LeaderCustomPortraitPath);

         if (!HasCustomLeaderPortrait)
         {
            LeaderPortraitId = f.LeaderPortraitId ?? "";
            LeaderPortraitTextureProvider = f.LeaderPortraitTextureProvider ?? "";
            LeaderPortraitArgs = f.LeaderPortraitArgs ?? "";
         }

         ShowLeaderTableau = !string.IsNullOrEmpty(LeaderPortraitId) && !HasCustomLeaderPortrait;

         if (!HasCustomLeaderPortrait || !RuntimePortraitFactory.TryGetDimensions(LeaderCustomPortraitPath, out int pw, out int ph) || pw <= 0 || ph <= 0) return;

         float scale = Math.Min(PortraitBoxWidth / pw, PortraitBoxHeight / ph);
         CustomPortraitWidth = pw * scale;
         CustomPortraitHeight = ph * scale;
      }

      [DataSourceProperty]
      public string Armies { get; } = "0";

      [DataSourceProperty]
      public string BannerArgs { get; } = "";

      // The kingdom banner, left of the name (flat id/provider/args, the chat's portrait recipe).
      [DataSourceProperty]
      public string BannerId { get; } = "";

      [DataSourceProperty]
      public string BannerTextureProvider { get; } = "";

      [DataSourceProperty]
      public string Castles { get; } = "0";

      [DataSourceProperty]
      public string Clans { get; } = "0";

      [DataSourceProperty]
      public float CustomPortraitHeight { get; } = PortraitBoxHeight;

      // The custom portrait's on-screen size, fitted to PortraitBox* at the image's own aspect so it is never
      // stretched (defaults to a square when the image dimensions are unknown).
      [DataSourceProperty]
      public float CustomPortraitWidth { get; } = PortraitBoxHeight;

      [DataSourceProperty]
      public string Diplomacy { get; }

      [DataSourceProperty]
      public float DiplomacyFill { get; }

      [DataSourceProperty]
      public string Economy { get; }

      [DataSourceProperty]
      public float EconomyFill { get; }

      [DataSourceProperty]
      public bool HasBanner => !string.IsNullOrEmpty(BannerId);

      /// <summary>True when a custom portrait was found, so the card shows the image instead of the render (set in the ctor).</summary>
      [DataSourceProperty]
      public bool HasCustomLeaderPortrait { get; }

      [DataSourceProperty]
      public bool HasLeaderPortrait => !string.IsNullOrEmpty(LeaderPortraitId);

      [DataSourceProperty]
      public string KingdomName { get; }

      /// <summary>Full path to the ruler's custom portrait, or empty; when set it replaces the 3D render.</summary>
      [DataSourceProperty]
      public string LeaderCustomPortraitPath { get; } = "";

      [DataSourceProperty]
      public string LeaderInfluence { get; } = "0";

      [DataSourceProperty]
      public string LeaderName { get; } = "";

      [DataSourceProperty]
      public string LeaderPortraitArgs { get; } = "";

      // The ruler's portrait, right of the PowerIndex. Three tableaus total (one per podium card), created at
      // movie load and freed with it on close (bounded), so no portrait-guard bookkeeping is needed here.
      [DataSourceProperty]
      public string LeaderPortraitId { get; } = "";

      [DataSourceProperty]
      public string LeaderPortraitTextureProvider { get; } = "";

      [DataSourceProperty]
      public string Military { get; }

      // 0..1 fills for the pillar bars (FillBarWidget.InitialAmountAsFloat binds a float; a double would not
      // convert). The readable 0..100 scores stay in the string properties above.
      [DataSourceProperty]
      public float MilitaryFill { get; }

      [DataSourceProperty]
      public string Political { get; }

      [DataSourceProperty]
      public float PoliticalFill { get; }

      [DataSourceProperty]
      public string PowerIndexText { get; }

      [DataSourceProperty]
      public int Rank { get; }

      /// <summary>
      ///   Show the 3D render only when there is one AND no custom portrait overrides it, so the two never overlap (set
      ///   in the ctor; mirrors the chat's ShowVanillaPortrait).
      /// </summary>
      [DataSourceProperty]
      public bool ShowLeaderTableau { get; }

      [DataSourceProperty]
      public string Territory { get; }

      [DataSourceProperty]
      public float TerritoryFill { get; }

      [DataSourceProperty]
      public string Towns { get; } = "0";

      [DataSourceProperty]
      public string Troops { get; } = "0";

      [DataSourceProperty]
      public string Vassals { get; } = "0";

      [DataSourceProperty]
      public string Villages { get; } = "0";

      [DataSourceProperty]
      public float WarFill { get; }

      [DataSourceProperty]
      public string WarPerformance { get; }

      [DataSourceProperty]
      public string Wealth { get; } = "0";
   }

   /// <summary>
   ///   One row of the full "Balance of Power" list: the kingdom, its Power Index (number + 0..1 bar fill), and its
   ///   momentum (placeholder until history exists).
   /// </summary>
   public sealed class PowerRowViewModel : ViewModel
   {
      public PowerRowViewModel(KingdomPowerScore s, KingdomPowerFacts f)
      {
         Rank = s.Rank;
         KingdomName = s.KingdomName;
         PowerIndex = Math.Round(s.PowerIndex).ToString("0"); // string: a Text binding to an int throws
         PowerFill = (float) (s.PowerIndex / 100.0); // FillBarWidget.InitialAmountAsFloat binds a float; a double would not bind
         MomentumText = ""; // filled once the persisted Power Index history lands
         MomentumIsUp = false;
      }

      [DataSourceProperty]
      public string KingdomName { get; }

      [DataSourceProperty]
      public bool MomentumIsUp { get; }

      [DataSourceProperty]
      public string MomentumText { get; }

      [DataSourceProperty]
      public float PowerFill { get; }

      [DataSourceProperty]
      public string PowerIndex { get; }

      [DataSourceProperty]
      public int Rank { get; }
   }
}