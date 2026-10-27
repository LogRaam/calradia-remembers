// Code written by Gabriel Mailhot, 27/10/2026.
// The engine side of a character's tastes in bed (Logic.Romance.IntimateActReading; Gabriel, 26/10/2026: "un geste
// impose que le NPC n'aime pas devrait reduire le personal regard"): what the turn's reader is taught, and what a
// reported gesture does to the character's personal regard, memory and what the player knows of them.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Teaches the reader a character's tastes in bed, and carries out a reported gesture.</summary>
   internal static class IntimateActApplier
   {
      // "npc|act" -> the day it was last charged: a night is one offence, not one per reply. Not saved: a reload
      // forgets it, which costs at most one more charge.
      private static readonly Dictionary<string, int> ChargedOn = new(StringComparer.Ordinal);

      /// <summary>The reader's block for this character this turn, or null.</summary>
      internal static string ReaderBlock(Hero hero, NpcProfile profile, IGameStateBridge bridge, bool forcedScene)
      {
         try
         {
            if (hero == null || profile?.Romantic == null) return null;
            bool hardcore = ModSettings.AdultContentLevel == AdultContentLevel.Hardcore;
            if (!hardcore || forcedScene) return null;

            return IntimateActReading.ForReader(new IntimateActReaderFacts {
               Hardcore = true, ForcedScene = false, IntimacyPermitted = bridge?.PermitsIntimacyWith(hero, profile) == true,
               NpcName = hero.Name?.ToString(), Loved = profile.Romantic.LovedActs ?? new List<IntimateAct>(),
               Disliked = profile.Romantic.DislikedActs ?? new List<IntimateAct>()
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[INTIMACY] reader block failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Carries out a reported gesture: regard, memory, what the player now knows.</summary>
      internal static ActionOutcome Apply(GameAction action, Hero hero, NpcProfile profile, IGameStateBridge bridge)
      {
         try
         {
            if (hero == null || profile?.Romantic == null) return ActionOutcome.Refused("");
            string actId = action?.Parameters != null && action.Parameters.TryGetValue("act", out string a) ? a : null;
            string done = action?.Parameters != null && action.Parameters.TryGetValue("done", out string d) ? d : null;
            IntimateAct act = IntimateActReading.Parse(actId);
            bool performed = !string.Equals((done ?? "").Trim(), "proposed", StringComparison.OrdinalIgnoreCase);
            var today = (int) CampaignTime.Now.ToDays;
            string key = hero.StringId + "|" + act;
            List<IntimateAct> known = profile.Romantic.KnownActs ?? new List<IntimateAct>();

            IntimateActVerdict v = IntimateActReading.Judge(new IntimateActJudgeFacts {
               Act = act, Performed = performed, Hardcore = ModSettings.AdultContentLevel == AdultContentLevel.Hardcore,
               IntimacyPermitted = bridge?.PermitsIntimacyWith(hero, profile) == true,
               Loved = profile.Romantic.LovedActs ?? new List<IntimateAct>(), Disliked = profile.Romantic.DislikedActs ?? new List<IntimateAct>(),
               AlreadyKnown = known.Contains(act), AlreadyChargedToday = ChargedOn.TryGetValue(key, out int day) && day == today
            });

            if (v.Discovered && !known.Contains(act))
            {
               known.Add(act);
               profile.Romantic.KnownActs = known;
            }

            if (v.RegardDelta != 0) ProfileMutator.ApplyReputationDelta(profile, v.RegardDelta, RegardCause.Deed);
            if (v.Imposed)
            {
               ChargedOn[key] = today;
               ProfileMutator.ApplyNotableEvent(profile, NotableEventType.Conflict, IntimateActReading.ImposedMemory(act, Hero.MainHero?.Name?.ToString()), today,
                  Hero.MainHero?.Name?.ToString());
            }

            ModLog.Info($"[INTIMACY] {hero.Name}: '{actId}' {(performed ? "performed" : "proposed")} -> regard {v.RegardDelta:+0;-0;0}"
                        + $"{(v.Imposed ? ", imposed (memory)" : "")}{(v.Discovered ? ", taste learned" : "")}.");

            return v == IntimateActVerdict.Nothing ? ActionOutcome.Refused("") : new ActionOutcome("", v.RegardDelta >= 0);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[INTIMACY] intimate_act failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Self-test seam: forgets what was charged today.</summary>
      internal static void ForgetCharges() => ChargedOn.Clear();
   }
}
