// Code written by Gabriel Mailhot, 08/10/2026.
// Extension Surface: the romance door. tashmetu (Nexus, 08/10/2026): nights from another mod reached CR only as Intimacy
// memories, which move nothing, so CR's consent bar never learned of them; and that mod wanted to offer "spend the night"
// only when CR's chat would allow it. Gabriel ratified both: a read of CR's verdict, and an explicit call that records a
// night as if it had happened in the chat, held to the same bar (SharedIntimacyPolicy). A plain memory never unlocks it.
// LapitaPolata (Nexus, 22/10/2026): a secret lover made in CR's chat "only exists in my and her memory. Affairs of Calradia
// does not know about it". Gabriel (23/10/2026) approved the way back: the bond read as one word (GetStatus) and an event
// when it changes (StatusChanged), for a bridge to carry into its own mod (RomanceBondPolicy).
// tashmetu (Nexus, 03/11/2026), for Romance & Seduction: CR's hidden affairs between lords could give one lord an affair in
// each mod. Gabriel (03/11/2026): CR depends on no mod, so the other mod registers a veto CR asks before any affair
// (RegisterAffairVeto), hears each one begin and end (HiddenAffairChanged), and reads them (GetHiddenAffair).
// tashmetu (Nexus, 25/11/2026, Bridges to Remember): an Affairs wife promoted to the game's spouse makes the old one read
// "former_spouse" exactly as a divorce does; StatusChanged now says whether CR ended the marriage (EndedByCr).

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

      static CrRomance()
      {
         // Mapping raises its own event and never names this door, so the hidden affairs need not know the API exists.
         HiddenAffairBehavior.Changed += RaiseHiddenAffairChanged;
      }

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

      /// <summary>
      ///   Fired when a hidden affair between two lords begins or ends (a death, a marriage, the player telling it, or it
      ///   simply running its course). Never about the player: their own bonds are <see cref="StatusChanged" />. A loaded
      ///   save's affairs are not announced: read them with <see cref="GetHiddenAffairs" />. Each listener is called under
      ///   its own guard. Since 2.8.3.
      /// </summary>
      public static event Action<CrHiddenAffairChange> HiddenAffairChanged;

      /// <summary>
      ///   Registers a veto under <paramref name="source" /> (your mod's name, shown in the log). Before CR lets a married
      ///   lord take a hidden lover it calls <paramref name="veto" /> with the married lord, then the lover; return a few
      ///   words saying why (e.g. "already in an affair") to forbid it, or null to let it be. A veto that throws is logged
      ///   and never forbids. Registering the same source again replaces its veto. Not saved: register again when a game
      ///   is loaded. Since 2.8.3.
      /// </summary>
      public static void RegisterAffairVeto(string source, Func<Hero, Hero, string> veto)
      {
         if (HiddenAffairBehavior.Vetoes.Register(source, veto)) ModLog.Info($"[ROMANCE API] affair veto registered by '{source}'");
      }

      /// <summary>Withdraws the affair veto of <paramref name="source" />. Returns whether one was there.</summary>
      public static bool UnregisterAffairVeto(string source) => HiddenAffairBehavior.Vetoes.Unregister(source);

      /// <summary>
      ///   The hidden affair this lord is in, on either side, or null when none (or no campaign). Read-only, cheap. Since 2.8.3.
      /// </summary>
      public static CrHiddenAffair GetHiddenAffair(string heroId)
      {
         try
         {
            return string.IsNullOrWhiteSpace(heroId) ? null : CrHiddenAffair.Of(HiddenAffairBehavior.Instance?.Ledger.Of(heroId));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] GetHiddenAffair failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Every hidden affair standing between lords (empty outside a campaign). Copies, oldest first. Since 2.8.3.</summary>
      public static IReadOnlyList<CrHiddenAffair> GetHiddenAffairs()
      {
         try
         {
            var all = new List<CrHiddenAffair>();
            foreach (HiddenAffair a in HiddenAffairBehavior.Instance?.Ledger.Affairs ?? new List<HiddenAffair>()) all.Add(CrHiddenAffair.Of(a));

            return all;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROMANCE API] GetHiddenAffairs failed: {ex.GetType().Name}: {ex.Message}");

            return Array.Empty<CrHiddenAffair>();
         }
      }

      private static void RaiseHiddenAffairChanged(string subjectId, string loverId, bool began)
      {
         Action<CrHiddenAffairChange> handlers = HiddenAffairChanged;
         if (handlers == null) return;

         var change = new CrHiddenAffairChange {
            SubjectId = subjectId, LoverId = loverId, Began = began, SaveId = CrConversation.CurrentSaveId(),
            GameDay = (int) CampaignTime.Now.ToDays, DuringSelfTest = Diagnostics.CrSelfTest.IsRunning
         };
         foreach (Delegate handler in handlers.GetInvocationList())
            try { ((Action<CrHiddenAffairChange>) handler)(change); }
            catch (Exception ex)
            {
               ModLog.Error($"[ROMANCE API] a HiddenAffairChanged subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }

      /// <summary>Forgets the bonds seen: the next check only learns them (a campaign started or loaded).</summary>
      internal static void ResetWatch() => Watch.Reset();

      /// <summary>CR has just dissolved this couple's marriage; when one of them is the player, the next check says so.</summary>
      internal static void NoteMarriageEndedByCr(Hero a, Hero b)
      {
         Hero main = Hero.MainHero;
         if (main == null || a == null || b == null) return;
         if (a == main) Watch.NoteMarriageEndedByCr(b.StringId);
         else if (b == main) Watch.NoteMarriageEndedByCr(a.StringId);
      }

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
               ModLog.Info($"[ROMANCE API] {c.NpcId}: {c.Before.Bond} -> {c.After.Bond}{(c.After.OpenTerms ? " (open terms)" : "")}{(c.EndedByCr ? " (divorce pronounced in CR)" : "")}.");
               Raise(new CrRomanceStatusChange {
                  NpcId = c.NpcId, SaveId = CrConversation.CurrentSaveId(), GameDay = day, DuringSelfTest = Diagnostics.CrSelfTest.IsRunning,
                  EndedByCr = c.EndedByCr,
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

   /// <summary>A hidden affair between two lords (see <see cref="CrRomance.GetHiddenAffair" />). A copy: changing it changes nothing.</summary>
   public sealed class CrHiddenAffair
   {
      /// <summary>The married lord's hero StringId.</summary>
      public string SubjectId { get; set; }

      /// <summary>Their lover's hero StringId (unwed).</summary>
      public string LoverId { get; set; }

      /// <summary>The in-game day it began.</summary>
      public int SinceDay { get; set; }

      internal static CrHiddenAffair Of(HiddenAffair a) => a == null ? null : new CrHiddenAffair {SubjectId = a.SubjectId, LoverId = a.LoverId, SinceDay = a.Day};
   }

   /// <summary>A hidden affair beginning or ending (see <see cref="CrRomance.HiddenAffairChanged" />).</summary>
   public sealed class CrHiddenAffairChange
   {
      /// <summary>The married lord's hero StringId.</summary>
      public string SubjectId { get; set; }

      /// <summary>Their lover's hero StringId.</summary>
      public string LoverId { get; set; }

      /// <summary>True when it began, false when it ended.</summary>
      public bool Began { get; set; }

      /// <summary>The loaded campaign's unique id, as in the conversation events.</summary>
      public string SaveId { get; set; }

      /// <summary>The in-game day of the change.</summary>
      public int GameDay { get; set; }

      /// <summary>True when cr.test made it: ignore it.</summary>
      public bool DuringSelfTest { get; set; }
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

      /// <summary>
      ///   True when this change ends a marriage CR itself dissolved (a divorce pronounced in CR's chat or by its own rules).
      ///   False for a marriage ended any other way: by another mod, or because another wife became the game's spouse, which
      ///   puts the one before among the ex-spouses and reads her "former_spouse" exactly as a divorce does.
      /// </summary>
      public bool EndedByCr { get; set; }
   }
}
