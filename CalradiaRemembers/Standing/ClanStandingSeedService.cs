// Code written by Gabriel Mailhot, 30/09/2026.
// A clan's standing warms a first meeting (tashmetu, Nexus, 30/09/2026). The rules are ClanStandingSeedPolicy's (pure,
// tested); this applies them to a speaker's profile when a conversation turn syncs the clan's standing, on the game
// thread, before the prompt is built, so the character already speaks from the warmer start.

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Standing
{
   /// <summary>Gives a character the one-time warm start their clan's standing with the player is worth.</summary>
   public static class ClanStandingSeedService
   {
      /// <summary>
      ///   Seeds <paramref name="profile" />'s regard from <paramref name="clanStanding" /> when the seed is due, and
      ///   records it as spent. Returns the regard added (0 when nothing moved). Call on the game thread; the caller
      ///   persists the profile.
      /// </summary>
      public static int Apply(NpcProfile profile, int clanStanding, int today)
      {
         if (profile == null) return 0;

         ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(new ClanStandingSeedFacts {
            ClanStanding = clanStanding,
            CurrentRegard = profile.ReputationWithPlayer,
            AlreadySeeded = profile.ClanStandingSeedDay != null,
            Enabled = ModSettings.ClanStandingWarmsFirstMeetings
         });

         if (!seed.Consumed) return 0;

         profile.ClanStandingSeedDay = today;
         if (seed.Shift > 0) ProfileMutator.ApplyReputationDelta(profile, seed.Shift, RegardCause.ClanStanding);

         ModLog.Info($"[REGARD] {profile.Name}: clan standing {clanStanding:+#;-#;0} warms the start by {seed.Shift:+#;-#;0} (regard now {profile.ReputationWithPlayer}); given once.");

         return seed.Shift;
      }
   }
}
