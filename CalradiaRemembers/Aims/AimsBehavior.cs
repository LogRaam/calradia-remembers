// Code written by Gabriel Mailhot, 15/10/2026.
// CrAims, engine side: the standing aims kept across saves, and the leave to raise one in a conversation. tashmetu
// (14/10/2026) asked for lords with aims of their own; Gabriel said build it, and on 15/10/2026 that the vassal war duty
// goes through this architecture. INTERNAL for now: the public CrAims door (Register / Retract / Raised) is the shape
// proposed in ROADMAP, shown to Gabriel and tashmetu before it becomes a contract in modder-api.md.
//
// The rules are pure (Logic/Aims: AimBook, AimConversationLeave, AimRaisePolicy, AimApproachWording); this persists the
// book under one additive key, decides the leave when a conversation opens (CrConversation.Started), forgets it when it
// ends, and hands the encounter context the lord's aim.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Logic.Aims;
using Newtonsoft.Json;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Aims
{
   /// <summary>What the encounter context is told of a lord's aim.</summary>
   internal sealed class AimForContext
   {
      public string Want { get; set; }
      public string PlayerPart { get; set; }
      public string Approach { get; set; }
      public bool MayRaise { get; set; }
   }

   /// <summary>Keeps the standing aims and the per-conversation leave to raise them.</summary>
   public sealed class AimsBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads no aims (save-migration rule).
      private const string SaveKey = "cr_standing_aims";

      /// <summary>The source tag of the aims Calradia Remembers seeds itself.</summary>
      internal const string CrSource = "CalradiaRemembers";

      private static bool _subscribed;

      public AimsBehavior()
      {
         Instance = this;

         // The conversation events are static and outlive a campaign: subscribe once, route to the live instance.
         if (_subscribed) return;
         _subscribed = true;
         CrConversation.Started += info => Instance?.OnConversationStarted(info);
         CrConversation.Ended += log => Instance?.OnConversationEnded(log?.SessionId);
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static AimsBehavior Instance { get; private set; }

      internal AimBook Book { get; private set; } = new AimBook();

      internal AimConversationLeave Leave { get; private set; } = new AimConversationLeave();

      /// <summary>
      ///   The lord's personal regard for the player (the mod's own ledger, NpcProfile.ReputationWithPlayer), or null when
      ///   he has no profile. Supplied by the submodule so this namespace does not reach into Behaviors (a cycle).
      /// </summary>
      internal static Func<Hero, int?> PersonalRegard;

      /// <summary>Raised when a lord is given the floor for his aim (internal until the public door is decided).</summary>
      internal static event Action<StandingAim, AimChannels> Raised;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Book.All, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[AIMS] serialize failed, saving no aims: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[AIMS] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            List<StandingAim> saved = null;
            try
            {
               if (bytes is {Length: > 0}) saved = JsonConvert.DeserializeObject<List<StandingAim>>(Encoding.UTF8.GetString(bytes));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[AIMS] load failed, reading no aims: {ex.GetType().Name}: {ex.Message}");
            }

            Book = new AimBook(saved);
            Leave = new AimConversationLeave();
         }
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>Registers or updates an aim (CR's own sources today; the public door later).</summary>
      internal AimAdmission Register(StandingAim aim)
      {
         AimAdmission result = Book.Register(aim, Today);
         ModLog.Info($"[AIMS] {aim?.SourceMod}/{aim?.Id} on {aim?.NpcId}: {result}.");

         return result;
      }

      /// <summary>Retracts one of a source's aims.</summary>
      internal bool Retract(string sourceMod, string id) => Book.Retract(sourceMod, id);

      /// <summary>Records a raise through a channel other than a conversation (a letter released, an audience granted).</summary>
      internal void MarkRaised(StandingAim aim, AimChannels channel)
      {
         if (Book.MarkRaised(aim, Today, channel)) RaiseRaised(aim, channel);
      }

      /// <summary>
      ///   What the encounter context is told of this lord's aim, or null when he holds none. An aim Calradia
      ///   Remembers seeds for a mechanic with its own section (the war duty's reckoning) is not voiced here: that
      ///   section states it with the terms the game will hold him to.
      /// </summary>
      internal AimForContext ForContext(Hero hero, string sessionId)
      {
         StandingAim aim = hero == null ? null : Book.ForNpc(hero.StringId);
         if (aim == null || string.Equals(aim.SourceMod, CrSource, StringComparison.OrdinalIgnoreCase)) return null;

         return new AimForContext {
            Want = aim.Want,
            PlayerPart = aim.PlayerPart,
            Approach = AimApproachWording.Describe(new AimApproachFacts {
               Honor = hero.GetTraitLevel(DefaultTraits.Honor),
               Calculating = hero.GetTraitLevel(DefaultTraits.Calculating),
               HasVerifiedService = !string.IsNullOrWhiteSpace(aim.MissionId)
            }),
            MayRaise = Leave.MayRaise(sessionId)
         };
      }

      /// <summary>Decides the leave as a conversation opens. Also the cr.testall seam.</summary>
      internal AimRaiseVerdict OnConversationStarted(CrConversationInfo info)
      {
         try
         {
            if (info == null) return AimRaiseVerdict.Spent;

            StandingAim aim = Book.ForNpc(info.NpcId);
            if (aim == null) return AimRaiseVerdict.Spent;

            Hero hero = Hero.FindFirst(h => h != null && h.StringId == info.NpcId);
            AimRaiseVerdict verdict = Leave.Open(new AimOpening {
               SessionId = info.SessionId,
               Aim = aim,
               Today = info.GameDay,
               Facts = new AimRaiseFacts {IsEnemyOfPlayer = IsEnemyOfPlayer(hero), IsCaptivity = hero?.IsPrisoner == true || Hero.MainHero?.IsPrisoner == true}
            }, Book);

            ModLog.Info($"[AIMS] conversation with {hero?.Name}: {aim.SourceMod}/{aim.Id} {verdict}.");
            if (verdict == AimRaiseVerdict.MayRaise) RaiseRaised(aim, AimChannels.Conversation);

            return verdict;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AIMS] opening failed: {ex.GetType().Name}: {ex.Message}");

            return AimRaiseVerdict.Spent;
         }
      }

      internal void OnConversationEnded(string sessionId) => Leave.Close(sessionId);

      private static bool IsEnemyOfPlayer(Hero hero)
      {
         if (hero == null || Hero.MainHero == null) return false;
         if (hero.MapFaction != null && Hero.MainHero.MapFaction != null && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction)) return true;

         int? regard = PersonalRegard?.Invoke(hero);

         return regard.HasValue && regard.Value <= VanillaRelation.EnemyTier;
      }

      private void OnDailyTick()
      {
         try
         {
            foreach (StandingAim gone in Book.PruneExpired(Today))
               ModLog.Info($"[AIMS] {gone.SourceMod}/{gone.Id} expired.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AIMS] daily prune failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static void RaiseRaised(StandingAim aim, AimChannels channel)
      {
         Action<StandingAim, AimChannels> evt = Raised;
         if (evt == null) return;

         foreach (Delegate handler in evt.GetInvocationList())
            try { ((Action<StandingAim, AimChannels>) handler)(aim, channel); }
            catch (Exception ex)
            {
               ModLog.Error($"[AIMS] a Raised listener threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }
   }
}
