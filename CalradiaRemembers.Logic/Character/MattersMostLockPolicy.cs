// Code written by Gabriel Mailhot, 22/11/2026.
// A "what matters most" chosen on purpose survives the refresh. Bhelogan (Discord 21/11/2026): "A Muh Prompts user set
// Isilflead the Huntress's what matters most to Kin and saved it. The value is written correctly, but CR changes it back
// at the next refresh: [MATTERS] Isilflead the Huntress: what matters most moved from Kin to Glory (its ground fell)."
// MattersMostPolicy.Resolve keeps a value only while the world grounds it (Kin needs a living parent or sibling), which
// is right for a rolled value and wrong for one a player wrote for the story ("a huntress who left her family behind and
// still thinks of them"). Bhelogan proposed a pin on the profile (for Muh Prompts) and a field in character_overrides.json
// (for hand-written campaigns); Gabriel (22/11/2026): "go".

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>What decides whether a character's "what matters most" is left to the world or held as written.</summary>
   public sealed class MattersMostLockFacts
   {
      /// <summary>The value on the profile now.</summary>
      public MattersMost Current { get; init; }

      /// <summary>The value written in character_overrides.json ("mattersMost"), or null when none (or custom traits are off).</summary>
      public MattersMost? Authored { get; init; }

      /// <summary>Another mod (Muh Prompts) pinned the value on the profile (NpcProfile.MattersMostPinned).</summary>
      public bool Pinned { get; init; }
   }

   /// <summary>The value held, when one is held, and why. Pure.</summary>
   public static class MattersMostLockPolicy
   {
      /// <summary>The value to hold without asking the world, or null to let MattersMostPolicy.Resolve decide as before.</summary>
      public static MattersMost? Held(MattersMostLockFacts f)
      {
         if (f == null) return null;

         // The author's file wins: it is read live, so taking the field out gives the character back to the world.
         if (f.Authored is { } authored && authored != MattersMost.None) return authored;

         // A pin keeps whatever was written, but a pin on nothing chosen yet holds nothing.
         return f.Pinned && f.Current != MattersMost.None ? f.Current : null;
      }

      /// <summary>The note the log gives a held value: "written in character_overrides.json" or "pinned".</summary>
      public static string Why(MattersMostLockFacts f)
         => f?.Authored is { } a && a != MattersMost.None ? "written in character_overrides.json" : "pinned";
   }
}
