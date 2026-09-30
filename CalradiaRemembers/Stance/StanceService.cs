// Code written by Gabriel Mailhot, 21/06/2026.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>
   ///   The single place stance is read from / written to an <see cref="NpcProfile" />. Affection
   ///   mirrors <see cref="NpcProfile.ReputationWithPlayer" />: the word/deed STANCE writes here only ever move
   ///   Trust/Respect/Fear and the word-cooldown. An Affection-axis DEED is routed to the regard ledger
   ///   (<see cref="ProfileMutator.ApplyReputationDelta" />, regard audit C3) rather than dropped, so a duel or
   ///   companion-abuse swing lands; conversational Affection still flows through change_relation / RelationGate.
   /// </summary>
   public static class StanceService
   {
      /// <summary>Applies a proven deed to an in-memory profile (the caller persists it).</summary>
      public static void ApplyDeed(NpcProfile p, StanceAxis axis, int delta)
      {
         if (p == null || delta == 0) return;

         // C3 (regard audit): Affection is not a moveable stance axis, it mirrors ReputationWithPlayer. A deed
         // targeting it was computed into the transient NpcStance and then silently dropped by Write, so a duel's
         // affection swing and a companion-abuse crater never landed. Route it to the regard ledger instead (a
         // proven deed, so it bypasses the conversational gate exactly as a quest reward does). Trust/Respect/Fear
         // still flow through StanceGate below.
         if (axis == StanceAxis.Affection)
         {
            ProfileMutator.ApplyReputationDelta(p, delta, NpcMemoryService.Core.Standing.RegardCause.Stance);

            return;
         }

         NpcStance s = ToStance(p);
         StanceGate.ApplyDeed(s, axis, delta);
         Write(p, s);
      }

      /// <summary>
      ///   Applies a proven deed to the hero's stored profile and saves it. No-op when the player has
      ///   never met the hero (no profile) — there is no relationship to move yet.
      /// </summary>
      public static void ApplyDeed(Hero hero, StanceAxis axis, int delta)
      {
         if (hero == null || delta == 0) return;
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile p = store?.Get(hero.StringId);

         if (p == null) return;
         ApplyDeed(p, axis, delta);
         store.Set(p);
      }

      /// <summary>
      ///   Applies the LLM's spoken stance nudges for this exchange, in place — capped hard and
      ///   rate-limited by <see cref="StanceGate.TryApplyWordRound" /> (words only colour a standing).
      ///   A no-op when nothing was emitted or the cooldown blocks it. The caller persists the profile.
      /// </summary>
      public static void ApplyWordShift(NpcProfile p, StanceShiftData shift)
      {
         if (p == null || shift == null) return;
         NpcStance s = ToStance(p);
         bool applied = StanceGate.TryApplyWordRound(
            s,
            new StanceShift {Trust = shift.Trust, Respect = shift.Respect, Fear = shift.Fear},
            CampaignTime.Now.ToHours);
         if (applied) Write(p, s);
      }

      /// <summary>
      ///   Fades a profile's fear over <paramref name="days" /> in place. Returns true when it
      ///   actually moved, so a daily sweep only persists the profiles that changed.
      /// </summary>
      public static bool DecayFear(NpcProfile p, int days)
      {
         if (p is not {StanceFear: > 0} || days <= 0) return false;
         NpcStance s = ToStance(p);
         StanceGate.ApplyFearDecay(s, days);
         Write(p, s);

         return true;
      }

      /// <summary>
      ///   M-S2 (regard audit): fades Trust and Respect one step toward zero in place, so neither becomes a
      ///   permanent gate. Returns true only when it actually moved, so a daily sweep persists just the profiles
      ///   that changed. The host calls this on a multi-day interval, never every day (deeds prove; memory of them
      ///   fades slowly).
      /// </summary>
      public static bool DecayStanding(NpcProfile p)
      {
         if (p == null) return false;
         NpcStance s = ToStance(p);
         if (!StanceGate.ApplyStandingDecay(s)) return false;
         Write(p, s);

         return true;
      }

      /// <summary>The posture block for the prompt, or null when the NPC is wholly neutral.</summary>
      public static string Describe(NpcProfile p)
         => p == null
            ? null
            : StanceDescriptor.Summarize(ToStance(p));

      // ── Named deeds (the proven acts that move perception). Magnitudes are tuning Gabriel can
      //    adjust; words move far less (StanceGate.WordCap 2 vs these). Profile overloads let a
      //    caller that already holds and saves the profile avoid an extra store round-trip.

      /// <summary>An enemy lord the player beat in the field: respect for the victor, and fear.</summary>
      public static void OnDefeatedInBattle(NpcProfile p)
      {
         ApplyDeed(p, StanceAxis.Respect, 12);
         ApplyDeed(p, StanceAxis.Fear, 12);
      }

      /// <summary>A lord who fought at the player's side in a victory: a measure of respect.</summary>
      public static void OnFoughtBesideYou(NpcProfile p)
      {
         ApplyDeed(p, StanceAxis.Respect, 6);
      }

      /// <summary>A freely given gift: a small proof of goodwill that builds trust.</summary>
      public static void OnGiftReceived(Hero npc)
      {
         ApplyDeed(npc, StanceAxis.Trust, 4);
      }

      /// <summary>A broken word / abandoned quest: trust spent.</summary>
      public static void OnPromiseBroken(NpcProfile p)
      {
         ApplyDeed(p, StanceAxis.Trust, -18);
      }

      /// <summary>A kept promise / completed quest: trust earned, and a little respect.</summary>
      public static void OnPromiseKept(NpcProfile p)
      {
         ApplyDeed(p, StanceAxis.Trust, 15);
         ApplyDeed(p, StanceAxis.Respect, 5);
      }

      /// <summary>The player took this lord prisoner: real fear, and grudging respect for the captor.</summary>
      public static void OnTakenPrisoner(Hero lord)
      {
         ApplyDeed(lord, StanceAxis.Fear, 18);
         ApplyDeed(lord, StanceAxis.Respect, 6);
      }

      /// <summary>Word reached the target that the player had a hand in a plot against them: trust collapses.</summary>
      public static void OnTreacheryDiscovered(Hero target)
      {
         ApplyDeed(target, StanceAxis.Trust, -15);
      }

      /// <summary>Builds the pure stance view of a profile (Affection sourced from reputation).</summary>
      public static NpcStance ToStance(NpcProfile p)
         => new() {
            Affection = p.ReputationWithPlayer,
            Trust = p.StanceTrust,
            Respect = p.StanceRespect,
            Fear = p.StanceFear,
            LastWordInfluenceHour = p.StanceLastWordHour
         };

      /// <summary>Writes the moveable axes back to the profile (Affection is owned elsewhere).</summary>
      public static void Write(NpcProfile p, NpcStance s)
      {
         p.StanceTrust = s.Trust;
         p.StanceRespect = s.Respect;
         p.StanceFear = s.Fear;
         p.StanceLastWordHour = s.LastWordInfluenceHour;
      }
   }
}