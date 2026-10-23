// Code written by Gabriel Mailhot, 23/10/2026.
// The prisoners the player left in a dungeon, and where (chmidt, 23/10/2026: lords "should also know who I put in their
// dungeons"). Written by Battles.PrisonerMemoryBehavior, which also saves it; read by the encounter builder so a lord's
// seat says who sits in its dungeon and that the player left them there. Kept here, in Mapping, so the encounter builder
// does not reach into Battles (which already reads Mapping).

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Where the player left each prisoner, by hero id.</summary>
   public static class PlayerDungeons
   {
      private static Dictionary<string, (string Place, int Day)> _left = new Dictionary<string, (string, int)>();

      /// <summary>Forgets everything (a campaign started or loaded).</summary>
      public static void Reset() => _left = new Dictionary<string, (string, int)>();

      /// <summary>The player left <paramref name="captive" /> in <paramref name="place" />'s dungeon today.</summary>
      public static void Left(Hero captive, Settlement place)
      {
         if (captive != null && place != null) _left[captive.StringId] = (place.StringId, (int) CampaignTime.Now.ToDays);
      }

      /// <summary>Where the player left this prisoner, or null; and forgets it (his captivity ended).</summary>
      public static string Release(string heroId)
      {
         if (heroId == null || !_left.TryGetValue(heroId, out (string Place, int Day) left)) return null;

         _left.Remove(heroId);

         return left.Place;
      }

      /// <summary>True when the player left this prisoner in the dungeon where he sits now.</summary>
      public static bool LeftThereByThePlayer(Hero prisoner)
      {
         Settlement where = prisoner?.PartyBelongedToAsPrisoner?.Settlement;

         return where != null && _left.TryGetValue(prisoner.StringId, out (string Place, int Day) left) && left.Place == where.StringId;
      }

      /// <summary>The save form: "hero|place|day" per line.</summary>
      public static string Write() => string.Join("\n", _left.Select(kv => $"{kv.Key}|{kv.Value.Place}|{kv.Value.Day.ToString(CultureInfo.InvariantCulture)}"));

      /// <summary>Reads the save form back; a line it cannot read is skipped.</summary>
      public static void Read(string saved)
      {
         Reset();
         foreach (string line in (saved ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            string[] f = line.Split('|');
            if (f.Length == 3 && f[0].Length > 0 && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) _left[f[0]] = (f[1], day);
         }
      }
   }
}
