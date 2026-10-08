// Code written by Gabriel Mailhot, 08/10/2026.
// Extension Surface: read-only readings of CR's cultures. tashmetu's Regard and Standing (Nexus, 08/10/2026) weighed
// affairs by CR's jealousy cultures by calling CalradiaRemembers.Jealousy.JealousyCultureLoader, an internal type that
// any rename would have broken without warning. This is the stable door to the same answer.

#region

using System;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Read-only readings of the cultures CR plays by (its ModuleData jealousy_cultures.json, or a Custom copy).</summary>
   public static class CrCultures
   {
      /// <summary>
      ///   How strictly a culture holds marriage: Strictness 0 (low), 1 (medium), 2 (high), and whether a house of the
      ///   given clan tier (0..6) shares partners. An unknown culture reads as CR's fallback: strict, never sharing.
      /// </summary>
      public static MarriageCustomReading MarriageCustom(string cultureName, int clanTier)
      {
         try
         {
            return MarriageCustomReading.Of(JealousyCultureLoader.GetCulture(cultureName), clanTier);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CULTURES] marriage custom of '{cultureName}' could not be read: {ex.GetType().Name}: {ex.Message}");

            return MarriageCustomReading.Of(JealousyCulture.Monogamous, clanTier);
         }
      }
   }
}
