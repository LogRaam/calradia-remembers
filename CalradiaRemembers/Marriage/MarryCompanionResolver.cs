// Code written by Gabriel Mailhot, 11/08/2026.
// Marry-your-own-companion feature: the ONE live-facts adapter over the pure MarryCompanionEligibilityPolicy.
// Both the SameClan carve-out (EncounterContextBuilder.ResolveMarriageBlockReason) and the bridge's marry
// re-validation (BannerlordGameStateBridge.ExecuteMarry) ask THIS, so the offer and the commit can never
// disagree over which same-clan hero is an eligible non-kin companion the player may wed. A genuine blood-kin
// clan member is refused here exactly as the SameClan bar always refused them.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Gathers the live facts behind the "marry your own companion" carve-out and evaluates them through the
   ///   pure <see cref="MarryCompanionEligibilityPolicy" />. The single eligibility authority shared by the
   ///   prompt-time offer and the bridge-time commit.
   /// </summary>
   public static class MarryCompanionResolver
   {
      /// <summary>
      ///   True when <paramref name="hero" /> is one of the player's OWN clan companions the mod would let the
      ///   player wed despite the ordinary <c>SameClan</c> bar: a hired wanderer of the player's clan (the one
      ///   same-clan case that is not kin), alive, adult, of a sex the engine can wed to the player, unwed, NOT
      ///   blood kin of the player, while the player can still take a spouse. A blood relative in the player's
      ///   clan is refused, so the incest custom the bar enforces is never relaxed.
      /// </summary>
      public static bool IsEligibleCompanionMatch(Hero hero)
      {
         Hero player = Hero.MainHero;

         if (hero == null || player == null) return false;

         bool isCompanion = IsPlayerCompanion(hero);
         bool isAlive = hero.IsAlive;
         bool isAdult = hero.Age >= MarriageEligibility.MinMarriageAge;
         // The engine marries opposite-sex only (DefaultMarriageModel.IsCoupleSuitableForMarriage bails on
         // same-sex), so orientation compatibility reduces to opposite sex here; opening the offer any wider
         // would only promise a union MarriageAction would silently drop.
         bool orientationCompatible = hero.IsFemale != player.IsFemale;
         bool alreadyWed = hero.Spouse != null;
         bool bloodKin = IsBloodKinOfPlayer(hero);
         bool playerFree = player.Spouse == null;

         return MarryCompanionEligibilityPolicy.CanMarry(new MarryCompanionEligibilityPolicy.MarryCompanionFacts {
            IsCompanion = isCompanion,
            IsAlive = isAlive,
            IsAdult = isAdult,
            OrientationCompatibleWithPlayer = orientationCompatible,
            AlreadyWed = alreadyWed,
            IsBloodKinOfPlayer = bloodKin,
            PlayerCanTakeAnotherSpouse = playerFree
         });
      }

      /// <summary>True when <paramref name="hero" /> is one of the player's own clan companions (a hired wanderer of the player's clan).</summary>
      public static bool IsPlayerCompanion(Hero hero)
         => hero != null && hero != Hero.MainHero && hero.IsAlive
            && (hero.IsPlayerCompanion || (hero.Clan == Clan.PlayerClan && hero.IsWanderer));

      /// <summary>
      ///   Best-effort blood-kin check between <paramref name="hero" /> and the player: the one same-clan case
      ///   the SameClan bar must ALWAYS keep barring (you cannot wed blood family). A hired wanderer companion
      ///   is essentially never kin, so this is a safety floor rather than a live concern; it reads only the
      ///   immediate family links the engine exposes.
      /// </summary>
      public static bool IsBloodKinOfPlayer(Hero hero)
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
