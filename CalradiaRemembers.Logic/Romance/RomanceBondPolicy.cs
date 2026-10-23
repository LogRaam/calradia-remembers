// Code written by Gabriel Mailhot, 23/10/2026.
// The romance status door. LapitaPolata (Nexus, 22/10/2026): "I managed to have a secret lover, the relationship only exists
// in my and her memory. Affairs of Calradia does not know about it [...] the next game changer for me would be a bridge FROM
// CR". CR cannot write into another mod's state, but a bridge can, if it can read the bond and hear when it changes. Gabriel
// (23/10/2026) approved the door: the bond with the player as one stable word, the day it began, and an event when it changes.
// The word follows the Heart page's reading (a spouse outranks the romantic state, which a divorce does not reset), so a
// bridge never hears something the player's own encyclopedia contradicts.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>A character's bond with the player, as another mod reads it.</summary>
   public sealed class RomanceBondReading
   {
      /// <summary>
      ///   One of "none", "curious", "courting", "lovers", "secret_lover", "committed", "consort", "spouse", "former_spouse",
      ///   "estranged", "broken".
      /// </summary>
      public string Bond { get; init; } = RomanceBondPolicy.None;

      /// <summary>The day the bond as told began, or null when the game recorded none (an older save).</summary>
      public int? SinceDay { get; init; }

      /// <summary>Open terms: each free to love others, neither wronged by it.</summary>
      public bool OpenTerms { get; init; }
   }

   /// <summary>One change of a bond, between two readings.</summary>
   public sealed class RomanceBondChange
   {
      /// <summary>The character's hero id.</summary>
      public string NpcId { get; init; }

      /// <summary>The bond before.</summary>
      public RomanceBondReading Before { get; init; }

      /// <summary>The bond now.</summary>
      public RomanceBondReading After { get; init; }
   }

   /// <summary>Reads the bond word from the Heart page's facts, and tells a change from the reading before.</summary>
   public static class RomanceBondPolicy
   {
      /// <summary>No bond with the player.</summary>
      public const string None = "none";

      /// <summary>
      ///   The bond in one word, in the Heart page's order: a marriage first (now or dissolved), then a consort, the player's
      ///   own secret lover, then the romantic state. A married character's affair with the player is a "secret_lover" too:
      ///   from outside, both are a love kept hidden.
      /// </summary>
      public static RomanceBondReading Read(HeartFacts f)
      {
         if (f == null) return new RomanceBondReading();

         string bond = f.IsSpouse ? "spouse"
            : f.IsExSpouse ? "former_spouse"
            : f.IsConsort ? "consort"
            : f.IsSecretLover ? "secret_lover"
            : f.Status switch
            {
               RomanticStatus.Curious => "curious",
               RomanticStatus.Courting => "courting",
               RomanticStatus.Intimate => "lovers",
               RomanticStatus.SecretLover => "secret_lover",
               RomanticStatus.Committed => "committed",
               RomanticStatus.Estranged => "estranged",
               RomanticStatus.Broken => "broken",
               _ => None
            };

         return new RomanceBondReading {Bond = bond, SinceDay = bond == None ? null : HeartPagePolicy.Dates(f), OpenTerms = f.IsOpenArrangement};
      }

      /// <summary>True when the bond word or the open terms differ. A date alone (an old bond dated later) is no change.</summary>
      public static bool Differs(RomanceBondReading before, RomanceBondReading after)
         => (before?.Bond ?? None) != (after?.Bond ?? None) || (before?.OpenTerms ?? false) != (after?.OpenTerms ?? false);
   }

   /// <summary>
   ///   Remembers the last reading of each character and tells what changed at the next pass. The first pass after a load
   ///   only learns (a save's bonds are not news); a character no longer read at all has gone back to "none".
   /// </summary>
   public sealed class RomanceBondWatch
   {
      private readonly Dictionary<string, RomanceBondReading> _last = new();
      private bool _seeded;

      /// <summary>Forgets everything: the next pass learns again (a load, a new campaign).</summary>
      public void Reset()
      {
         _last.Clear();
         _seeded = false;
      }

      /// <summary>The changes since the previous pass, given every character with a bond now. Empty on the first pass.</summary>
      public IReadOnlyList<RomanceBondChange> Pass(IReadOnlyDictionary<string, RomanceBondReading> now)
      {
         now ??= new Dictionary<string, RomanceBondReading>();
         var changes = new List<RomanceBondChange>();
         foreach (string id in _last.Keys.Union(now.Keys).ToList())
         {
            _last.TryGetValue(id, out RomanceBondReading before);
            now.TryGetValue(id, out RomanceBondReading after);
            after ??= new RomanceBondReading();
            if (_seeded && RomanceBondPolicy.Differs(before, after)) changes.Add(new RomanceBondChange {NpcId = id, Before = before ?? new RomanceBondReading(), After = after});

            if (after.Bond == RomanceBondPolicy.None && !after.OpenTerms) _last.Remove(id);
            else _last[id] = after;
         }

         _seeded = true;

         return changes;
      }
   }
}
