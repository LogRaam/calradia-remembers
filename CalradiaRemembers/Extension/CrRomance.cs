// Code written by Gabriel Mailhot, 08/10/2026.
// Extension Surface: the romance door. tashmetu (Nexus, 08/10/2026): nights from another mod reached CR only as Intimacy
// memories, which move nothing, so CR's consent bar never learned of them; and that mod wanted to offer "spend the night"
// only when CR's chat would allow it. Gabriel ratified both: a read of CR's verdict, and an explicit call that records a
// night as if it had happened in the chat, held to the same bar (SharedIntimacyPolicy). A plain memory never unlocks it.
// LapitaPolata (Nexus, 22/10/2026): a secret lover made in CR's chat "only exists in my and her memory. Affairs of Calradia
// does not know about it". Gabriel (23/10/2026) approved the way back: the bond read as one word (GetStatus) and an event
// when it changes (StatusChanged), for a bridge to carry into its own mod (RomanceBondPolicy).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Reads and records intimacy with the player, held to the bar CR's own chat holds; reads the bond and its changes.</summary>
   public static class CrRomance
   {
      private static readonly RomanceBondWatch Watch = new();

      /// <summary>
      ///   Fired when a character's bond with the player changes (a courtship begins, she becomes the player's secret lover,
      ///   a wedding, a divorce, a bond that breaks, open terms agreed or ended), with the bond before and after. Checked
      ///   when a conversation closes and every in-game hour, so it may come up to an hour after the change. A loaded save's
      ///   bonds are not announced: read them with <see cref="GetStatus" />. Each listener is called under its own guard.
      /// </summary>
      public static event Action<CrRomanceStatusChange> StatusChanged;

      /// <summary>
      ///   The character's bond with the player, as the encyclopedia's Heart section tells it (the game's marriage first,
      ///   then CR's own romance). Null when there is no such hero or no campaign. Read-only, cheap.
      /// </summary>
      public static CrRomanceStatus GetStatus(string npcId)
      {
         try
         {
            Hero hero = string.IsNullOrWhiteSpace(npcId) || Hero.MainHero == null ? null : Hero.FindFirst(h => h.StringId == npcId);
            if (hero == null || hero == Hero.MainHero) return null;

            return CrRomanceStatus.Of(npcId, HeartPageReader.BondOf(hero));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] GetStatus failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Forgets the bonds seen: the next check only learns them (a campaign started or loaded).</summary>
      internal static void ResetWatch() => Watch.Reset();

      /// <summary>Reads every bond with the player and raises <see cref="StatusChanged" /> for each that moved since the last check.</summary>
      internal static void CheckForChanges()
      {
         try
         {
            if (Hero.MainHero == null || NpcMemoryCampaignBehavior.Instance?.Store == null) return;

            IReadOnlyList<RomanceBondChange> changes = Watch.Pass(HeartPageReader.BondsNow());
            if (changes.Count == 0) return;

            var day = (int) CampaignTime.Now.ToDays;
            foreach (RomanceBondChange c in changes)
            {
               ModLog.Info($"[ROMANCE API] {c.NpcId}: {c.Before.Bond} -> {c.After.Bond}{(c.After.OpenTerms ? " (open terms)" : "")}.");
               Raise(new CrRomanceStatusChange {
                  NpcId = c.NpcId, SaveId = CrConversation.CurrentSaveId(), GameDay = day, DuringSelfTest = Diagnostics.CrSelfTest.IsRunning,
                  Before = CrRomanceStatus.Of(c.NpcId, c.Before), After = CrRomanceStatus.Of(c.NpcId, c.After)
               });
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] the bond check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static void Raise(CrRomanceStatusChange change)
      {
         Action<CrRomanceStatusChange> handlers = StatusChanged;
         if (handlers == null) return;

         foreach (Delegate handler in handlers.GetInvocationList())
            try { ((Action<CrRomanceStatusChange>) handler)(change); }
            catch (Exception ex)
            {
               ModLog.Error($"[ROMANCE API] a StatusChanged subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }

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

   /// <summary>A character's bond with the player (see <see cref="CrRomance.GetStatus" />). A copy: changing it changes nothing.</summary>
   public sealed class CrRomanceStatus
   {
      /// <summary>The character's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>
      ///   "none", "curious", "courting", "lovers", "secret_lover", "committed", "consort", "spouse", "former_spouse",
      ///   "estranged" or "broken". "secret_lover" is any love kept hidden: the player's own secret lover, or a married
      ///   character's affair with the player. New words may be added; treat an unknown one as "none".
      /// </summary>
      public string Bond { get; set; }

      /// <summary>The in-game day the bond began (the wedding, the day they became lovers...), or null when none was recorded.</summary>
      public int? SinceDay { get; set; }

      /// <summary>Open terms: each free to love others, neither wronged by it.</summary>
      public bool OpenTerms { get; set; }

      internal static CrRomanceStatus Of(string npcId, RomanceBondReading r)
         => new() {NpcId = npcId, Bond = r?.Bond ?? RomanceBondPolicy.None, SinceDay = r?.SinceDay, OpenTerms = r?.OpenTerms ?? false};
   }

   /// <summary>One change of a character's bond with the player (see <see cref="CrRomance.StatusChanged" />).</summary>
   public sealed class CrRomanceStatusChange
   {
      /// <summary>The character's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>The loaded campaign's unique id, as in the conversation events.</summary>
      public string SaveId { get; set; }

      /// <summary>The in-game day the change was seen.</summary>
      public int GameDay { get; set; }

      /// <summary>The bond before. Its <see cref="CrRomanceStatus.Bond" /> differs from After's, or the open terms do.</summary>
      public CrRomanceStatus Before { get; set; }

      /// <summary>The bond now.</summary>
      public CrRomanceStatus After { get; set; }

      /// <summary>True while cr.test all runs: a test's change, put back afterwards, which a bridge must not keep.</summary>
      public bool DuringSelfTest { get; set; }
   }
}
