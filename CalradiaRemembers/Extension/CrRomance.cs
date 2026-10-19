// Code written by Gabriel Mailhot, 08/10/2026.
// Extension Surface: the romance door. tashmetu (Nexus, 08/10/2026): nights from another mod reached CR only as Intimacy
// memories, which move nothing, so CR's consent bar never learned of them; and that mod wanted to offer "spend the night"
// only when CR's chat would allow it. Gabriel ratified both: a read of CR's verdict, and an explicit call that records a
// night as if it had happened in the chat, held to the same bar (SharedIntimacyPolicy). A plain memory never unlocks it.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Reads and records intimacy with the player, held to the bar CR's own chat holds.</summary>
   public static class CrRomance
   {
      /// <summary>
      ///   True when CR's chat would allow intimacy between this character and the player now (its consent bar: regard,
      ///   their nature, a marriage to another...); false when it would not, or when adult content is off; null when there
      ///   is no such hero or CR is not loaded. Read-only, cheap.
      /// </summary>
      public static bool? WouldAcceptIntimacy(string npcId)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            Hero hero = string.IsNullOrWhiteSpace(npcId) ? null : Hero.FindFirst(h => h.StringId == npcId);
            if (behavior?.Store == null || behavior.GameStateBridge == null || hero == null) return null;
            if (ModSettings.AdultContentLevel == AdultContentLevel.Off) return false;

            NpcProfile profile = behavior.Store.Get(npcId) ?? HeroProfileMapper.FromHero(hero);

            return behavior.GameStateBridge.PermitsIntimacyWith(hero, profile);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] WouldAcceptIntimacy failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Records a night the player and this character shared in another mod, as if it had happened in CR's chat: the
      ///   memory (in the character's words, as <paramref name="summary" />) and the romance arc, which moves exactly as
      ///   the chat would move it. Refused, writing nothing, when the chat would not have allowed it: ask
      ///   <see cref="WouldAcceptIntimacy" /> before offering the night. Regard is not moved.
      /// </summary>
      public static SharedIntimacyResult RecordSharedIntimacy(string npcId, string summary, string sourceMod)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            Hero hero = string.IsNullOrWhiteSpace(npcId) ? null : Hero.FindFirst(h => h.StringId == npcId);
            string line = ExternalEventRules.BoundSummary(summary);
            bool loaded = behavior?.Store != null && behavior.GameStateBridge != null;
            NpcProfile profile = loaded && hero != null ? behavior.Store.Get(npcId) ?? HeroProfileMapper.FromHero(hero) : null;
            bool romanceOn = ModSettings.AdultContentLevel != AdultContentLevel.Off;

            SharedIntimacyResult result = SharedIntimacyPolicy.Decide(new SharedIntimacyFacts {
               SummaryGiven = loaded && !string.IsNullOrWhiteSpace(line), HeroKnown = hero != null, RomanceOn = romanceOn,
               ChatWouldAllow = profile != null && romanceOn && behavior.GameStateBridge.PermitsIntimacyWith(hero, profile)
            });
            ModLog.Info($"[ROMANCE API] {sourceMod ?? "a mod"} reported a night with {hero?.Name?.ToString() ?? npcId}: {result}.");
            if (result != SharedIntimacyResult.Recorded) return result;

            bool orientationCompatible = profile.Romantic?.IsCompatibleWith(Hero.MainHero?.IsFemale ?? false) ?? true;
            ProfileMutator.Apply(profile, new ParsedResponse {Dialogue = "", NewEventData = new ParsedEventData(NotableEventType.Intimacy, line)},
               (int) CampaignTime.Now.ToDays, romanticContentEnabled: true, orientationCompatible: orientationCompatible,
               applyReputation: false, intimacyPermitted: true, playerName: Hero.MainHero?.Name?.ToString());
            behavior.Store.Set(profile);

            return result;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] RecordSharedIntimacy failed: {ex.GetType().Name}: {ex.Message}");

            return SharedIntimacyResult.RejectedInvalid;
         }
      }
   }
}
