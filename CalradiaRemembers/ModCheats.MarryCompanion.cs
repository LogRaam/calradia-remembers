// Code written by Gabriel Mailhot, 11/08/2026.
// SPIKE (marry a companion): a console-gated, NOT player-facing probe. Players want to wed one of their own
// wanderer companions, which the mod bars today as MarriageBlockReason.SameClan (a companion is CompanionOf ==
// PlayerClan, so hero.Clan == PlayerClan). This command answers the engine half of the feasibility question
// HONESTLY against the LIVE campaign: it checks the pure carve-out rule on real facts, asks the engine's own
// MarriageModel.IsCoupleSuitableForMarriage, then actually calls MarriageAction.Apply and reports exactly what the
// engine did to the companion (whether a marriage was recorded, and the companion's occupation / clan / companion
// role afterwards). If the engine unexpectedly DID wed them, the union is reversed at once via CR's own divorce
// service so a diagnostic never leaves the player married. Nothing here ships as a feature; it exists to produce
// the verdict.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.marry_companion [name]
      ///   FEASIBILITY PROBE (spike, not a shipped verb). Resolves one of the player's own companions (a name
      ///   fragment, else the first companion found), evaluates the pure
      ///   <see cref="MarryCompanionEligibilityPolicy" /> carve-out on live facts, asks the engine's
      ///   <c>MarriageModel.IsCoupleSuitableForMarriage</c>, then attempts a real
      ///   <see cref="MarriageAction.Apply" /> and reports the outcome and the companion's resulting state.
      ///   Should the engine actually seal the union (it will not for a raw wanderer, but the guard is honest),
      ///   the marriage is reversed immediately through <see cref="MarriageDissolutionService" /> so no
      ///   diagnostic ever leaves the player wed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry_companion", "cr")]
      public static string MarryCompanionProbe(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (Hero.MainHero == null) return "No player hero. Start a campaign first.";

         Hero companion = ResolveHero(args);
         if (companion == null || !IsPlayerCompanionHero(companion))
            companion = Hero.AllAliveHeroes.FirstOrDefault(IsPlayerCompanionHero);

         if (companion == null)
            return "No player companion found. Recruit a wanderer companion first, or pass a companion's name.";

         Hero player = Hero.MainHero;
         var sb = new StringBuilder();
         sb.AppendLine($"cr.marry_companion probing a same-clan companion: {companion.Name}");

         // ── Live facts for the pure carve-out rule ────────────────────────────────────────────────
         bool isCompanion = IsPlayerCompanionHero(companion);
         bool isAlive = companion.IsAlive;
         bool isAdult = companion.Age >= MarriageEligibility.MinMarriageAge;
         bool orientationCompatible = companion.IsFemale != player.IsFemale; // engine marries opposite-sex only
         bool alreadyWed = companion.Spouse != null;
         bool bloodKin = IsBloodKinOfPlayer(companion);
         bool playerFree = player.Spouse == null;

         bool policySaysYes = MarryCompanionEligibilityPolicy.CanMarry(new MarryCompanionEligibilityPolicy.MarryCompanionFacts {
            IsCompanion = isCompanion,
            IsAlive = isAlive,
            IsAdult = isAdult,
            OrientationCompatibleWithPlayer = orientationCompatible,
            AlreadyWed = alreadyWed,
            IsBloodKinOfPlayer = bloodKin,
            PlayerCanTakeAnotherSpouse = playerFree
         });

         sb.AppendLine($"  occupation={companion.Occupation}, isPlayerCompanion={companion.IsPlayerCompanion}, "
                     + $"clan={companion.Clan?.Name?.ToString() ?? "(none)"} (== player clan: {companion.Clan == Clan.PlayerClan})");
         sb.AppendLine($"  carve-out facts: companion={isCompanion}, alive={isAlive}, adult={isAdult} (age {(int) companion.Age}), "
                     + $"orientationOk={orientationCompatible}, alreadyWed={alreadyWed}, bloodKin={bloodKin}, playerFree={playerFree}");
         sb.AppendLine($"  MarryCompanionEligibilityPolicy.CanMarry -> {(policySaysYes ? "YES (the mod would admit this match)" : "NO")}");

         // ── The engine's own suitability verdict (does NOT check same-clan; it checks IsLord via CanMarry) ──
         bool engineSuitable;
         try
         {
            engineSuitable = Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(player, companion) ?? false;
         }
         catch (Exception ex)
         {
            engineSuitable = false;
            sb.AppendLine($"  MarriageModel.IsCoupleSuitableForMarriage threw: {ex.GetType().Name}: {ex.Message}");
         }

         sb.AppendLine($"  engine IsCoupleSuitableForMarriage(player, companion) -> {engineSuitable}"
                     + (engineSuitable ? "" : "  (a wanderer is not Occupation.Lord, so the engine's own CanMarry() refuses it)"));

         // ── Attempt the real marriage and report what the engine actually did ─────────────────────
         if (player.Spouse != null)
         {
            sb.AppendLine($"  SKIP the live Apply: you are already married to {player.Spouse.Name} (a second marriage is impossible).");
            return sb.ToString();
         }

         Occupation occBefore = companion.Occupation;
         try
         {
            MarriageAction.Apply(player, companion);
         }
         catch (Exception ex)
         {
            sb.AppendLine($"  MarriageAction.Apply threw: {ex.GetType().Name}: {ex.Message}");
            return sb.ToString();
         }

         bool wed = player.Spouse == companion;
         sb.AppendLine($"  after MarriageAction.Apply: player.Spouse == companion -> {wed}");
         sb.AppendLine($"  companion now: occupation={companion.Occupation} (was {occBefore}), isPlayerCompanion={companion.IsPlayerCompanion}, "
                     + $"clan={companion.Clan?.Name?.ToString() ?? "(none)"}, spouse={companion.Spouse?.Name?.ToString() ?? "(none)"}");

         if (!wed)
         {
            sb.AppendLine("  VERDICT: the raw engine call NO-OPPED (nothing sealed). To wed a companion the mod must first "
                        + "promote them out of Occupation.Wanderer into Occupation.Lord; the SameClan bar itself is a mod/UI rule the engine does not enforce.");
            return sb.ToString();
         }

         // Should not happen for a raw wanderer, but if the engine DID wed them, undo it so the probe is clean.
         bool reversed = MarriageDissolutionService.TryDissolvePlayerMarriage(companion, out string why);
         sb.AppendLine(reversed
            ? "  VERDICT: the engine SEALED the marriage; the probe reversed it via CR's divorce service so nothing was left changed."
            : $"  WARNING: the engine SEALED the marriage and it could NOT be cleanly reversed ({why}). Reload without saving to restore your campaign.");

         return sb.ToString();
      }

      /// <summary>True when <paramref name="hero" /> is one of the player's own clan companions (a hired wanderer).</summary>
      private static bool IsPlayerCompanionHero(Hero hero)
         => hero != null && hero != Hero.MainHero && hero.IsAlive
            && (hero.IsPlayerCompanion || (hero.Clan == Clan.PlayerClan && hero.IsWanderer));

      /// <summary>
      ///   Best-effort blood-kin check between <paramref name="hero" /> and the player: the one same-clan case the
      ///   SameClan bar must always keep barring. A hired wanderer companion is essentially never kin, so this is
      ///   a safety floor rather than a live concern; it reads only the immediate family links the engine exposes.
      /// </summary>
      private static bool IsBloodKinOfPlayer(Hero hero)
      {
         Hero player = Hero.MainHero;
         if (hero == null || player == null) return false;

         return hero.Father == player || hero.Mother == player
             || player.Father == hero || player.Mother == hero
             || (player.Siblings?.Contains(hero) ?? false)
             || (player.Children?.Contains(hero) ?? false);
      }
   }
}
