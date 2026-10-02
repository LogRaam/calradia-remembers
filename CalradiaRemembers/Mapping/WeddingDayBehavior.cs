// Code written by Gabriel Mailhot, 01/10/2026.
// The game keeps no wedding date. raphareish (Nexus, 01/10/2026): a character married days before spoke of her spouse as
// if they had been married for years, because her family line said "Spouse: X" and nothing else. This records the day
// of every wedding CR sees (both spouses, any marriage, the player's or not) so the family line can say how recent it is
// (SpouseTenurePolicy). Additive save key; a marriage CR never saw simply has no date.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The day each married hero was wed, as CR saw it happen.</summary>
   public sealed class WeddingDayBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_WeddingDays_v1";

      private static Dictionary<string, int> _weddedOn = new Dictionary<string, int>();

      /// <summary>Created for every campaign started or loaded: no wedding day leaks from the last one.</summary>
      public WeddingDayBehavior() => _weddedOn = new Dictionary<string, int>();

      public override void RegisterEvents()
         => CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);

      public override void SyncData(IDataStore dataStore)
      {
         // A newline-joined "heroId=day" list: tiny, well under the native string limit, absent on an old save.
         string blob = string.Join("\n", _weddedOn.Select(kv => kv.Key + "=" + kv.Value));
         dataStore.SyncData(SaveKey, ref blob);

         if (!dataStore.IsLoading) return;

         _weddedOn = new Dictionary<string, int>();
         foreach (string line in (blob ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            int eq = line.LastIndexOf('=');
            if (eq > 0 && int.TryParse(line.Substring(eq + 1), out int day)) _weddedOn[line.Substring(0, eq)] = day;
         }
      }

      /// <summary>The day this hero was wed, when CR saw it; null otherwise.</summary>
      public static int? WeddedOn(Hero hero)
         => hero?.StringId != null && _weddedOn.TryGetValue(hero.StringId, out int day) ? day : (int?) null;

      /// <summary>cr.testall seam: records (day) or forgets (null) a hero's wedding day.</summary>
      internal static void DebugSet(Hero hero, int? day)
      {
         if (hero?.StringId == null) return;
         if (day is int d) _weddedOn[hero.StringId] = d;
         else _weddedOn.Remove(hero.StringId);
      }

      private void OnBeforeHeroesMarried(Hero first, Hero second, bool showNotification)
      {
         try
         {
            var today = (int) CampaignTime.Now.ToDays;
            if (first?.StringId != null) _weddedOn[first.StringId] = today;
            if (second?.StringId != null) _weddedOn[second.StringId] = today;
         }
         catch (Exception ex) { ModLog.Error($"[MARRIAGE] wedding day not recorded: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
