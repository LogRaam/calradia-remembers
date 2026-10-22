// Code written by Gabriel Mailhot, 22/10/2026.
// Gabriel (21/10/2026), after reading another LLM mod whose rulers write policies that the game turns into effects:
// "2 et 3 m'interesse", then "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre conteste." This is the CLOSED
// catalogue of edicts a ruler may decree. The model may name an edict and argue for it, but what it does comes only from
// here: each one a gain bought with a cost, small enough to sit beside the game's own daily drifts, and applied through
// the game's own settlement models so the town's tooltip shows it. Decided overnight; every number is a TUNE.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Edicts
{
   /// <summary>
   ///   The edicts a ruler may decree; what each does is in <see cref="EdictCatalog" />. APPEND-ONLY: a save stores the
   ///   number, so a reordered or removed member would load as another edict.
   /// </summary>
   public enum EdictKind
   {
      /// <summary>Lighter taxes: the people breathe, the treasury thins.</summary>
      TaxRelief,

      /// <summary>A heavier levy: more coin, a sullen town.</summary>
      HeavyLevy,

      /// <summary>The watch doubled: safer streets, paid out of the taxes.</summary>
      DoubledWatch,

      /// <summary>The militia mustered and drilled: more spears on the walls, fewer hands at work.</summary>
      MilitiaMuster,

      /// <summary>Grain requisitioned from the countryside: full granaries, angry farmers.</summary>
      GrainRequisition,

      /// <summary>A market charter: trade flows, and so do thieves.</summary>
      MarketCharter,

      /// <summary>A curfew: order kept, business and goodwill lost.</summary>
      Curfew,

      /// <summary>Feast days: a glad town eating into its stores.</summary>
      FeastDays
   }

   /// <summary>Where an edict holds: one town, or every town of a realm.</summary>
   public enum EdictScope
   {
      /// <summary>One town, decreed by the head of the house that holds it.</summary>
      Town,

      /// <summary>Every town of a realm, decreed by its ruler; a town's own edict comes first.</summary>
      Realm
   }

   /// <summary>What one edict does to a town each day, through the game's own models; 0 leaves a channel alone.</summary>
   public sealed class EdictEffect
   {
      /// <summary>The edict.</summary>
      public EdictKind Kind { get; init; }

      /// <summary>Its key as the model and the console write it ("tax_relief").</summary>
      public string Key { get; init; }

      /// <summary>Its plain name ("Tax relief").</summary>
      public string Title { get; init; }

      /// <summary>A factor on the town's tax (-0.3 is thirty percent less).</summary>
      public float TaxFactor { get; init; }

      /// <summary>Loyalty per day.</summary>
      public float Loyalty { get; init; }

      /// <summary>Security per day.</summary>
      public float Security { get; init; }

      /// <summary>Prosperity per day.</summary>
      public float Prosperity { get; init; }

      /// <summary>Militia per day.</summary>
      public float Militia { get; init; }

      /// <summary>Food stocks per day.</summary>
      public float Food { get; init; }

      /// <summary>The edict costs the people's goodwill (a loyalty loss or a heavier tax): the one the people may contest.</summary>
      public bool BurdensThePeople => Loyalty < 0f || TaxFactor > 0f;

      /// <summary>What it does, in words, gains first ("loyalty +1 a day, taxes -30%").</summary>
      public string Describe()
      {
         var parts = new List<string>();
         void Day(string what, float v)
         {
            if (v != 0f) parts.Add($"{what} {(v > 0 ? "+" : "")}{v:0.#} a day");
         }

         if (TaxFactor != 0f) parts.Add($"taxes {(TaxFactor > 0 ? "+" : "")}{TaxFactor * 100f:0}%");
         Day("loyalty", Loyalty);
         Day("security", Security);
         Day("prosperity", Prosperity);
         Day("militia", Militia);
         Day("food", Food);

         return string.Join(", ", parts);
      }
   }

   /// <summary>The closed list of edicts, and how a name the model writes finds one.</summary>
   public static class EdictCatalog
   {
      /// <summary>TUNE: the shortest and longest term an edict may be decreed for, in days.</summary>
      public const int MinDays = 30, MaxDays = 90;

      /// <summary>TUNE: the term when none is said.</summary>
      public const int DefaultDays = 60;

      /// <summary>TUNE: influence a ruler spends to decree an edict on one town, and on a whole realm.</summary>
      public const int TownInfluenceCost = 25, RealmInfluenceCost = 100;

      /// <summary>Every edict, and what it does.</summary>
      public static readonly IReadOnlyList<EdictEffect> All = new[] {
         new EdictEffect {Kind = EdictKind.TaxRelief, Key = "tax_relief", Title = "Tax relief", TaxFactor = -0.3f, Loyalty = 1f},
         new EdictEffect {Kind = EdictKind.HeavyLevy, Key = "heavy_levy", Title = "Heavy levy", TaxFactor = 0.25f, Loyalty = -1f},
         new EdictEffect {Kind = EdictKind.DoubledWatch, Key = "doubled_watch", Title = "Doubled watch", TaxFactor = -0.1f, Security = 1f},
         new EdictEffect {Kind = EdictKind.MilitiaMuster, Key = "militia_muster", Title = "Militia muster", Militia = 1f, Prosperity = -1f},
         new EdictEffect {Kind = EdictKind.GrainRequisition, Key = "grain_requisition", Title = "Grain requisition", Food = 4f, Loyalty = -1f},
         new EdictEffect {Kind = EdictKind.MarketCharter, Key = "market_charter", Title = "Market charter", Prosperity = 1f, Security = -0.5f},
         new EdictEffect {Kind = EdictKind.Curfew, Key = "curfew", Title = "Curfew", Security = 1.5f, Prosperity = -1f, Loyalty = -0.5f},
         new EdictEffect {Kind = EdictKind.FeastDays, Key = "feast_days", Title = "Feast days", Loyalty = 1.5f, Food = -3f}
      };

      /// <summary>The edict of this kind.</summary>
      public static EdictEffect Of(EdictKind kind) => All.First(e => e.Kind == kind);

      /// <summary>
      ///   The edict a written name means: its key or its title, any case, spaces or underscores ("Tax relief",
      ///   "tax_relief", "TAX RELIEF"); null for anything else, so a name the model invents decrees nothing.
      /// </summary>
      public static EdictEffect Find(string written)
      {
         if (string.IsNullOrWhiteSpace(written)) return null;
         string k = Normalize(written);

         return All.FirstOrDefault(e => Normalize(e.Key) == k || Normalize(e.Title) == k);
      }

      /// <summary>The term asked, brought within the catalogue's bounds; the default when none is said.</summary>
      public static int Term(int askedDays) => askedDays <= 0 ? DefaultDays : Math.Min(Math.Max(askedDays, MinDays), MaxDays);

      /// <summary>What decreeing costs in influence.</summary>
      public static int InfluenceCost(EdictScope scope) => scope == EdictScope.Realm ? RealmInfluenceCost : TownInfluenceCost;

      private static string Normalize(string s) => new string(s.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());
   }
}
