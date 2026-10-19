// Code written by Gabriel Mailhot, 06/09/2026.
// Tournament memory pillar (player suggestion 2026-09-06), Inc 2 (capture). The campaign's TournamentFinished event
// gives only the winner + roster, not the bracket, so to remember WHO the player faced and who beat whom we read the
// arena mission's own bracket. A Harmony postfix on TournamentBehavior.EndCurrentMatch fires as each match resolves;
// once the final has resolved (Winner is set) and the player took part, we walk Rounds -> Matches -> Participants /
// Winners into a pure TournamentResult (player POV) and hand it to the feeder. Guarded once per tournament instance.
// Only the winner==player case was handled before (a congratulation letter, LetterTriggerEvaluator); this captures
// both winning and losing, at bracket granularity, without touching that path.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Tournaments;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;

#endregion

namespace CalradiaRemembers.Tournaments
{
   /// <summary>Reads the real arena bracket at tournament end into a TournamentResult and feeds the lords' memories.</summary>
   [HarmonyPatch(typeof(TournamentBehavior), "EndCurrentMatch")]
   internal static class TournamentOutcomeCapture
   {
      // One capture per tournament instance (the postfix could fire again on a late tick; Winner stays set).
      private static readonly HashSet<TournamentBehavior> _captured = new HashSet<TournamentBehavior>();

      [HarmonyPostfix]
      private static void Postfix(TournamentBehavior __instance)
      {
         try
         {
            if (__instance?.Winner == null) return;        // only once the final has resolved
            if (!__instance.IsPlayerParticipating) return; // only tournaments the player actually fought in
            if (!_captured.Add(__instance)) return;        // once per tournament

            TournamentResult result = Build(__instance);
            if (result != null) TournamentMemoryFeeder.Apply(result);
         }
         catch (Exception ex) { ModLog.Error($"[TOURNEY] outcome capture failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static TournamentResult Build(TournamentBehavior behavior)
      {
         TournamentRound[] rounds = behavior.Rounds;
         if (rounds == null) return null;

         var bouts = new List<TournamentBout>();
         var eliminatedRound = 0;

         for (var r = 0; r < rounds.Length; r++)
         {
            TournamentRound round = rounds[r];
            if (round?.Matches == null) continue;

            foreach (TournamentMatch match in round.Matches)
            {
               if (match == null || !MatchHasPlayer(match)) continue;

               var opponents = new List<string>();
               foreach (TournamentParticipant p in match.Participants)
               {
                  CharacterObject c = p?.Character;
                  if (c == null || c.IsPlayerCharacter || !c.IsHero || c.HeroObject == null) continue;

                  opponents.Add(c.HeroObject.StringId);
               }

               bool playerAdvanced = match.Winners.Any(w => w?.Character != null && w.Character.IsPlayerCharacter);
               int roundNumber = r + 1;
               bouts.Add(new TournamentBout(roundNumber, opponents, playerAdvanced));

               if (!playerAdvanced) eliminatedRound = roundNumber;
            }
         }

         CharacterObject champ = behavior.Winner?.Character;
         bool playerWon = champ != null && champ.IsPlayerCharacter;
         string championHeroId = champ != null && !champ.IsPlayerCharacter && champ.IsHero && champ.HeroObject != null
            ? champ.HeroObject.StringId
            : null;
         string town = behavior.Settlement?.Name?.ToString() ?? "";

         return new TournamentResult(town, (int) CampaignTime.Now.ToDays, playerWon, eliminatedRound, championHeroId, bouts);
      }

      private static bool MatchHasPlayer(TournamentMatch match)
         => match.Participants.Any(p => p?.Character != null && p.Character.IsPlayerCharacter);
   }
}
