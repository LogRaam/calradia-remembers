// Code written by Gabriel Mailhot, 27/06/2026.
// "Stance with teeth", the proactive side: once a week, a lord whose EARNED posture has hardened into open
// enmity or genuine regard reaches out UNBIDDEN — a cold/menacing letter, or a warm offer of aid. Secret hatred
// (a schemer, a would-be assassin) writes nothing here; it would tip its hand. Its bite comes face to face (the
// conversation hint) and, later, through schemes / interception / a town ambush. The verdict is the pure
// StanceConsequencePolicy; this host only templates and queues the letter, rate-limited per NPC.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Stance
{
   /// <summary>Weekly: turns a hardened stance into an unbidden letter (open enmity, or genuine friendship).</summary>
   public sealed class StanceConsequenceBehavior : CampaignBehaviorBase
   {
      // A lord won't reach out more than once every three weeks, however the player keeps provoking them.
      private static int LetterCooldownDays => WorldCalendar.DaysPerSeason;

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

            // 3.4 (regard audit): resolve alive heroes ONCE into a dictionary (O(1) lookup, skips the dead),
            // instead of a per-profile Hero.FindFirst that made this weekly sweep O(n^2) and ran on corpses.
            var aliveById = new Dictionary<string, Hero>();
            foreach (Hero h in Hero.AllAliveHeroes)
               if (h?.StringId != null)
                  aliveById[h.StringId] = h;

            foreach (string id in store.ListIds())
            {
               if (!aliveById.TryGetValue(id, out Hero hero)) continue;

               NpcProfile profile = store.Get(id);
               if (profile == null) continue;
               if (!CanWrite(hero)) continue;

               TryWrite(store, profile, hero, today, ignoreCooldown: false);
            }
         }
         catch (Exception ex) { ModLog.Error($"[STANCE] consequence tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Debug seam for the console: arm a town ambush against the player by this lord, now.</summary>
      public static bool DebugArm(Hero hero)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || hero == null) return false;

         NpcProfile profile = store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         profile.StanceAmbushArmedDay = (int) CampaignTime.Now.ToDays;
         store.Set(profile);
         ModLog.Info($"[STANCE] {hero.Name} has armed an attempt on the player's life (debug).");

         return true;
      }

      /// <summary>
      ///   C4 (regard audit): does this hero's CURRENT stance STILL resolve to murder, by the exact policy that
      ///   armed the ambush? The town-ambush behavior calls this before springing an armed attempt, so a lord who
      ///   has since reconciled (regard/stance recovered) does not have his months-old knife spring regardless.
      /// </summary>
      public static bool IsMurderousNow(Hero hero, NpcProfile profile)
      {
         if (hero == null || profile == null) return false;

         return StanceConsequencePolicy.Resolve(StanceService.ToStance(profile), new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = TraitLevel(hero, DefaultTraits.Honor), Valor = TraitLevel(hero, DefaultTraits.Valor), Mercy = TraitLevel(hero, DefaultTraits.Mercy), Calculating = TraitLevel(hero, DefaultTraits.Calculating)}) == StanceConsequence.Assassinate;
      }

      private static bool TryWrite(BannerlordNpcMemoryStore store, NpcProfile profile, Hero hero, int today, bool ignoreCooldown)
      {
         if (!ignoreCooldown && today - profile.StanceLastConsequenceDay < LetterCooldownDays) return false;

         StanceConsequence verdict = StanceConsequencePolicy.Resolve(StanceService.ToStance(profile), new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = TraitLevel(hero, DefaultTraits.Honor), Valor = TraitLevel(hero, DefaultTraits.Valor), Mercy = TraitLevel(hero, DefaultTraits.Mercy), Calculating = TraitLevel(hero, DefaultTraits.Calculating)});

         // Murderous intent doesn't write a letter, it arms a knife in the dark (the town ambush, increment 3).
         if (verdict == StanceConsequence.Assassinate)
         {
            profile.StanceAmbushArmedDay = today;
            profile.StanceLastConsequenceDay = today;
            store.Set(profile);
            ModLog.Info($"[STANCE] {hero.Name} has armed an attempt on the player's life.");

            return true;
         }

         // A cold schemer's path (increment 2, "stance with teeth"): a real scheme against the PLAYER,
         // never a letter (it would tip the plotter's hand). SchemeStore.ForcePlot reuses any scheme
         // already afoot between this pair, so the weekly re-evaluation never doubles a plot.
         if (verdict == StanceConsequence.Plot)
         {
            SchemeKind kind = MBRandom.RandomInt(2) == 0
               ? SchemeKind.Slander
               : SchemeKind.Sabotage;
            string outcome = SchemeStore.Instance?.ForcePlot(hero, Hero.MainHero, kind);
            profile.StanceLastConsequenceDay = today;
            store.Set(profile);
            ModLog.Info($"[STANCE] {hero.Name} plots ({kind}) against the player. {outcome ?? "(scheme system inactive)"}");

            return true;
         }

         // A bold lord's path (increment 2): rides out for real, through InterceptionBehavior's own
         // per-lord cooldown and "one pursuer at a time" guard, so this can legitimately do nothing some
         // weeks. The accompanying letter below still rides out regardless, either ahead of him or as the
         // only word the player gets if his party could not be sent, but the real pursuit is the point.
         if (verdict == StanceConsequence.Intercept)
         {
            bool rode = InterceptionBehavior.Instance?.TriggerStanceIntercept(hero) ?? false;
            ModLog.Info($"[STANCE] {hero.Name} {(rode ? "rides out to" : "means to")} intercept the player.");
         }

         // M-S3 (regard audit): rotate the letter template by how many stance-hostility letters this NPC has
         // already sent, so a lasting enmity does not mail the player the SAME threat word for word every cycle.
         var variant = 0;
         foreach (PendingLetter l in profile.SentLetters)
            if (l.Reason == LetterReason.StanceHostility)
               variant++;

         string content = LetterFor(verdict, variant, out LetterReason reason);

         if (content == null) return false; // nothing warranted (neutral, mild, or a secret hatred biding its time)

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

      /// <summary>M-S3: threat-letter templates, rotated by send count so a lasting enmity varies its word.</summary>
      private static readonly string[] ThreatLetters =
      {
         "Word of you reaches me, and none of it warms me. I have not forgotten what you are, nor will I. " +
         "Keep from my path: cross me again and you will find I am no friend to trifle with.",
         "You have made an enemy of me, and I am not one who forgets. Do not mistake my silence for peace; it is " +
         "only patience. Give me cause once more, and I will give you reason to rue it.",
         "I have heard enough of your doings to take your measure. Stay well clear of what is mine and what is " +
         "dear to me. Should our paths cross on an ill day, do not look to me for mercy."
      };

      /// <summary>M-S3: intercept-letter templates, rotated by send count.</summary>
      private static readonly string[] InterceptLetters =
      {
         "You and I have a reckoning owed between us, and I am not one to let such a debt sit. Ride where " +
         "you will: our paths will cross, and when they do, I mean to settle it blade to blade.",
         "There is a matter unsettled between us, and I will not rest until it is answered. Look over your " +
         "shoulder as you ride, for I am coming, and I do not turn back.",
         "You thought distance would keep you safe from me. It will not. I am riding to find you, and when I " +
         "do, we will finish what you began."
      };

      /// <summary>The letter content for a verdict, and its reason; null when no letter is warranted.</summary>
      private static string LetterFor(StanceConsequence verdict, int variant, out LetterReason reason)
      {
         switch (verdict)
         {
            case StanceConsequence.Threat:
               reason = LetterReason.StanceHostility;

               return ThreatLetters[Math.Abs(variant) % ThreatLetters.Length];

            case StanceConsequence.Intercept:
               reason = LetterReason.StanceHostility;

               return InterceptLetters[Math.Abs(variant) % InterceptLetters.Length];

            case StanceConsequence.Warning:
               reason = LetterReason.StanceFavor;

               // The friendly-ladder payoff (increment 2): a trusted friend does not merely PROMISE to
               // warn you of a plot, they NAME one when a real one exists. SchemeStore is the single
               // source of truth for "is anyone actually plotting against the player right now."
               Hero schemer = SchemeStore.Instance?.GetSecretSchemerAgainst(Hero.MainHero);

               return schemer != null
                  ? $"Hear me, for I would not have you taken unawares: I have learned that {schemer.Name} means you harm, " +
                    "and moves quietly against you even now. Watch yourself, friend, and reckon with it before it ripens " +
                    "into a blow you cannot parry."
                  : GenericFriendlyLetter();

            case StanceConsequence.Favor:
               reason = LetterReason.StanceFavor;

               return GenericFriendlyLetter();

            default: // None / ColdShoulder / WarmWelcome (conversational) and Plot / Assassinate (secret) — no letter
               reason = default;

               return null;
         }
      }

      private static bool CanWrite(Hero hero)
         => hero is {IsAlive: true, IsPrisoner: false} && hero != Hero.MainHero && hero.Clan != Clan.PlayerClan;

      /// <summary>The generic warm letter shared by Favor and a Warning with nothing real to name.</summary>
      private static string GenericFriendlyLetter()
         => "I count you a friend, and I am not shy to say so. Should you ever have need of me, a sword, a coin, " +
            "a word in the right ear, you have only to ask. And were anything to move against you that reached " +
            "my ear, you would hear of it from me first.";

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      #endregion
   }
}
