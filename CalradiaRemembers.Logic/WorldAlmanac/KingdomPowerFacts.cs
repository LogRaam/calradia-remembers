// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, pure foundation: the raw, engine-gathered facts about ONE kingdom that feed the
// "Balance of Power" dashboard, plus the scored result the policy computes from a whole field of them. This
// carries EVERYTHING a card on the panel shows (the six pillar inputs and the display strip: troops, armies,
// settlements, clans, vassals, wealth, the leader and their influence), so the UI, when it comes, only renders
// what is here and never re-derives from the engine. The mod's builder fills these live; the policy turns a
// field of them into normalized pillar scores and a Power Index. Momentum and a real war record need history,
// so those facts are placeholders here (zero) until the persisted layer lands.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
   /// <summary>The raw facts about one kingdom, gathered live from the campaign, that feed the Power Index.</summary>
   public sealed class KingdomPowerFacts
   {
      /// <summary>Stable kingdom id (Bannerlord StringId), the key the result is matched back by.</summary>
      public string KingdomId { get; set; }

      /// <summary>Display name of the kingdom.</summary>
      public string KingdomName { get; set; }

      /// <summary>The ruler's display name (shown on the card).</summary>
      public string LeaderName { get; set; }

      /// <summary>The ruler's personal clan influence (Political pillar + the Most Influential Leader indicator).</summary>
      public int LeaderInfluence { get; set; }

      // ── Military ──────────────────────────────────────────────────────────
      /// <summary>Troops in the kingdom's mobile field parties.</summary>
      public int FieldTroops { get; set; }

      /// <summary>Troops standing in settlement garrisons.</summary>
      public int GarrisonTroops { get; set; }

      /// <summary>Number of armies currently mustered.</summary>
      public int ArmyCount { get; set; }

      /// <summary>
      ///   Tier-weighted fighting strength of the field parties (Bannerlord's own party strength, which counts a
      ///   veteran far above a recruit). This, not the raw headcount, is what the Military pillar scores from:
      ///   Gabriel's call, it is military STRENGTH that matters, not numbers. The headcounts above stay for display.
      /// </summary>
      public double FieldStrength { get; set; }

      /// <summary>Tier-weighted fighting strength standing in garrisons (same basis as <see cref="FieldStrength" />).</summary>
      public double GarrisonStrength { get; set; }

      // ── Territory ─────────────────────────────────────────────────────────
      public int Towns { get; set; }
      public int Castles { get; set; }
      public int Villages { get; set; }

      /// <summary>Summed prosperity across the kingdom's settlements (Territory + Economy).</summary>
      public double TotalProsperity { get; set; }

      // ── Economy ───────────────────────────────────────────────────────────
      /// <summary>Estimated total wealth in denars (summed clan gold), the card's "denars" figure.</summary>
      public long EstimatedWealth { get; set; }

      // ── Political ─────────────────────────────────────────────────────────
      public int ClanCount { get; set; }

      /// <summary>Number of lords (vassals) in the kingdom.</summary>
      public int VassalCount { get; set; }

      /// <summary>Summed clan influence across the kingdom.</summary>
      public int CumulativeInfluence { get; set; }

      // ── War Performance (needs the persisted battle ledger; 0 in V1) ──────
      public int RecentVictories { get; set; }
      public int RecentDefeats { get; set; }
      public int SettlementsCaptured { get; set; }
      public int SettlementsLost { get; set; }

      // ── Diplomatic ────────────────────────────────────────────────────────
      /// <summary>Number of wars the kingdom is currently fighting (fewer is stronger).</summary>
      public int ActiveWars { get; set; }

      /// <summary>Number of standing alliances/tributes in the kingdom's favour.</summary>
      public int Alliances { get; set; }

      /// <summary>Total settlements (towns + castles + villages), the Largest Territory indicator and the card strip.</summary>
      public int TotalSettlements => Towns + Castles + Villages;

      // ── Display bindings (flat strings the prefab's ImageIdentifierWidgets render from) ──
      // The engine seams (BannerImageIdentifierVM, PortraitTextureGuard) are crossed once in the builder, so
      // the UI never re-derives from the engine, the same reason every other display field lives here.

      /// <summary>The kingdom banner's image id (empty when it could not be resolved).</summary>
      public string BannerId { get; set; } = "";

      /// <summary>The kingdom banner's texture provider name.</summary>
      public string BannerTextureProvider { get; set; } = "";

      /// <summary>The kingdom banner's additional args.</summary>
      public string BannerArgs { get; set; } = "";

      /// <summary>The ruler's portrait image id (empty when the ruler has none, so the card hides the widget).</summary>
      public string LeaderPortraitId { get; set; } = "";

      /// <summary>The ruler's portrait texture provider name.</summary>
      public string LeaderPortraitTextureProvider { get; set; } = "";

      /// <summary>The ruler's portrait additional args.</summary>
      public string LeaderPortraitArgs { get; set; } = "";

      /// <summary>
      ///   Full path to a player-dropped custom portrait for the ruler (ModuleData/ChatPortraits, the same source the
      ///   chat uses), or empty when none. When set, the card shows this image (aspect-preserved) instead of the 3D
      ///   render.
      /// </summary>
      public string LeaderCustomPortraitPath { get; set; } = "";
   }

   /// <summary>One kingdom's scored standing: each pillar on 0..100 and the weighted, normalized Power Index.</summary>
   public sealed class KingdomPowerScore
   {
      public string KingdomId { get; set; }
      public string KingdomName { get; set; }

      public double Military { get; set; }
      public double Territory { get; set; }
      public double Economy { get; set; }
      public double Political { get; set; }
      public double WarPerformance { get; set; }
      public double Diplomatic { get; set; }

      /// <summary>The weighted blend of the six pillars, 0..100.</summary>
      public double PowerIndex { get; set; }

      /// <summary>1-based standing in the field, 1 = strongest.</summary>
      public int Rank { get; set; }
   }

   /// <summary>The "at a glance" leaders on single measures, shown in the Key Indicators panel.</summary>
   public sealed class KeyIndicators
   {
      public string MostPowerfulArmyKingdomId { get; set; }
      public int MostPowerfulArmyTroops { get; set; }

      public string LargestTerritoryKingdomId { get; set; }
      public int LargestTerritorySettlements { get; set; }

      public string StrongestEconomyKingdomId { get; set; }
      public long StrongestEconomyWealth { get; set; }

      public string MostInfluentialLeaderKingdomId { get; set; }
      public string MostInfluentialLeaderName { get; set; }
      public int MostInfluentialLeaderInfluence { get; set; }

      public string BestWarRecordKingdomId { get; set; }
      public int BestWarRecordVictories { get; set; }
      public int BestWarRecordDefeats { get; set; }
   }

   /// <summary>Field-wide totals for the "Calradia Overview" strip.</summary>
   public sealed class PowerFieldTotals
   {
      public int TotalTroops { get; set; }
      public int TotalSettlements { get; set; }
      public int TotalClans { get; set; }
      public long TotalWealth { get; set; }
   }

   /// <summary>The full computed dashboard state: kingdoms ranked, the key indicators, and the field totals.</summary>
   public sealed class PowerIndexResult
   {
      public IReadOnlyList<KingdomPowerScore> Ranked { get; set; } = new List<KingdomPowerScore>();
      public KeyIndicators Indicators { get; set; } = new KeyIndicators();
      public PowerFieldTotals Totals { get; set; } = new PowerFieldTotals();
   }
}
