// Code written by Gabriel Mailhot, 27/06/2026.
// "Stance with teeth", the proactive side: once a week, a lord whose EARNED posture has hardened into open
// enmity or genuine regard reaches out UNBIDDEN — a cold/menacing letter, or a warm offer of aid. Secret hatred
// (a schemer, a would-be assassin) writes nothing here; it would tip its hand. Its bite comes face to face (the
// conversation hint) and, later, through schemes / interception / a town ambush. The verdict is the pure
// StanceConsequencePolicy; this host only templates and queues the letter, rate-limited per NPC.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Weekly: turns a hardened stance into an unbidden letter (open enmity, or genuine friendship).</summary>
   public sealed class StanceConsequenceBehavior : CampaignBehaviorBase
   {
      // A lord won't reach out more than once every three weeks, however the player keeps provoking them.
      private const int LetterCooldownDays = 21;

      // Courier time for an unbidden stance letter.
      private const int DeliveryDays = 3;

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      public override void SyncData(IDataStore dataStore) { } // state lives on each NpcProfile

      /// <summary>Debug seam for the self-test: evaluate one hero now and queue any warranted letter.</summary>
      public static bool DebugEvaluate(Hero hero)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || !CanWrite(hero)) return false;

         NpcProfile profile = store.Get(hero.StringId);

         return profile != null && TryWrite(store, profile, hero, (int) CampaignTime.Now.ToDays, ignoreCooldown: true);
      }

      #region private

      private void OnWeeklyTick()
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            int today = (int) CampaignTime.Now.ToDays;

            foreach (string id in store.ListIds())
            {
               NpcProfile profile = store.Get(id);
               if (profile == null) continue;

               Hero hero = Hero.FindFirst(h => h != null && h.StringId == id);
               if (!CanWrite(hero)) continue;

               TryWrite(store, profile, hero, today, ignoreCooldown: false);
            }
         }
         catch (Exception ex) { ModLog.Error($"[STANCE] consequence tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static bool TryWrite(BannerlordNpcMemoryStore store, NpcProfile profile, Hero hero, int today, bool ignoreCooldown)
      {
         if (!ignoreCooldown && today - profile.StanceLastConsequenceDay < LetterCooldownDays) return false;

         string content = LetterFor(StanceConsequencePolicy.Resolve(
            StanceService.ToStance(profile),
            TraitLevel(hero, DefaultTraits.Honor),
            TraitLevel(hero, DefaultTraits.Valor),
            TraitLevel(hero, DefaultTraits.Mercy),
            TraitLevel(hero, DefaultTraits.Calculating)), out LetterReason reason);

         if (content == null) return false; // nothing warranted (neutral, mild, or a secret hatred that stays silent)

         profile.SentLetters.Add(new PendingLetter {
            SenderId = hero.StringId,
            SenderName = hero.Name?.ToString(),
            Reason = reason,
            Content = content,
            SentOnDay = today,
            DeliveryDay = today + DeliveryDays,
            IsDelivered = false
         });
         profile.StanceLastConsequenceDay = today;
         store.Set(profile);

         ModLog.Info($"[STANCE] {reason} letter queued: {hero.Name} -> player.");

         return true;
      }

      /// <summary>The letter content for a verdict, and its reason; null when no letter is warranted.</summary>
      private static string LetterFor(StanceConsequence verdict, out LetterReason reason)
      {
         switch (verdict)
         {
            case StanceConsequence.Threat:
               reason = LetterReason.StanceHostility;

               return "Word of you reaches me, and none of it warms me. I have not forgotten what you are, nor will I. " +
                      "Keep from my path — cross me again and you will find I am no friend to trifle with.";

            case StanceConsequence.Intercept:
               reason = LetterReason.StanceHostility;

               return "You and I have a reckoning owed between us, and I am not one to let such a debt sit. Ride where " +
                      "you will — our paths will cross, and when they do, I mean to settle it blade to blade.";

            case StanceConsequence.Favor:
            case StanceConsequence.Warning:
               reason = LetterReason.StanceFavor;

               return "I count you a friend, and I am not shy to say so. Should you ever have need of me — a sword, a coin, " +
                      "a word in the right ear — you have only to ask. And were anything to move against you that reached " +
                      "my ear, you would hear of it from me first.";

            default: // None / ColdShoulder / WarmWelcome (conversational) and Plot / Assassinate (secret) — no letter
               reason = default;

               return null;
         }
      }

      private static bool CanWrite(Hero hero)
         => hero is {IsAlive: true, IsPrisoner: false} && hero != Hero.MainHero && hero.Clan != Clan.PlayerClan;

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      #endregion
   }
}
