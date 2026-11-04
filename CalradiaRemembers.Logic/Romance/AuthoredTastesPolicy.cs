// Code written by Gabriel Mailhot, 03/11/2026.
// Tene21 (Nexus, 03/11/2026): "I don't believe the "loves", "lessFond" and "dislikes" fields in character_overrides.json
// are applying. Setting a character's tastes in the file, then running cr.reload_lore followed by cr.acts Character Name
// still show what I assume are the character's generated tastes." The lock was copied onto the profile only when a chat
// message was sent, so cr.acts, the Heart line of the encyclopedia and every other reader saw the roll. Gabriel
// (03/11/2026): "une centralisation ou on injecte au bon moment en amont". This rule is what that one sync writes: the
// authored lists replace the rolled ones, and a gesture the author placed is taken out of every list the author left to
// the roll, so no gesture ever sits in two lists (a partial lock of "loves" alone used to leave a rolled less-fond copy).

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>A character's tastes as they stand, and the lists an author wrote for them.</summary>
   public sealed class AuthoredTastesFacts
   {
      /// <summary>The tastes on the profile now (rolled, or an earlier lock). Null lists read as empty.</summary>
      public IntimateTastes Current { get; init; } = new();

      /// <summary>The authored loved gestures; null when the author left the field out (the roll keeps it).</summary>
      public IReadOnlyList<IntimateAct> Loves { get; init; }

      /// <summary>The authored less-loved gestures; null when left out. Never one also authored loved or disliked.</summary>
      public IReadOnlyList<IntimateAct> LessFond { get; init; }

      /// <summary>The authored disliked gestures; null when left out. Never one also authored loved.</summary>
      public IReadOnlyList<IntimateAct> Dislikes { get; init; }
   }

   /// <summary>How an authored gesture lock lands on a character's rolled tastes. Pure.</summary>
   public static class AuthoredTastesPolicy
   {
      /// <summary>True when the author wrote at least one of the three lists (an empty list counts: a lock to none).</summary>
      public static bool HasLock(AuthoredTastesFacts f) => f != null && (f.Loves != null || f.LessFond != null || f.Dislikes != null);

      /// <summary>
      ///   The tastes the character has once the lock is in: each authored list replaces its rolled one, and every gesture
      ///   the author placed anywhere leaves the lists still rolled. Null when there is no lock (the profile stays as it is).
      /// </summary>
      public static IntimateTastes Apply(AuthoredTastesFacts f)
      {
         if (!HasLock(f)) return null;

         var placed = new HashSet<IntimateAct>((f.Loves ?? new List<IntimateAct>()).Concat(f.LessFond ?? new List<IntimateAct>()).Concat(f.Dislikes ?? new List<IntimateAct>()));
         List<IntimateAct> Pick(IReadOnlyList<IntimateAct> authored, List<IntimateAct> rolled)
            => authored != null ? authored.Distinct().ToList() : (rolled ?? new List<IntimateAct>()).Where(a => !placed.Contains(a)).ToList();

         return new IntimateTastes {
            Loved = Pick(f.Loves, f.Current?.Loved), LessFond = Pick(f.LessFond, f.Current?.LessFond), Disliked = Pick(f.Dislikes, f.Current?.Disliked)
         };
      }

      /// <summary>
      ///   True when the profile carries exactly the lists the author wrote, so the lock is visibly in place and lifting it
      ///   (the custom-traits toggle turned off) should give the character their own roll back. False when the author wrote
      ///   nothing, or when the profile's tastes are still the roll: those are never re-rolled, so a learned taste stays.
      /// </summary>
      public static bool LockIsInPlace(AuthoredTastesFacts f)
      {
         if (!HasLock(f)) return false;

         bool Same(IReadOnlyList<IntimateAct> authored, List<IntimateAct> now)
            => authored == null || new HashSet<IntimateAct>(authored).SetEquals(now ?? new List<IntimateAct>());

         return Same(f.Loves, f.Current?.Loved) && Same(f.LessFond, f.Current?.LessFond) && Same(f.Dislikes, f.Current?.Disliked);
      }
   }
}
