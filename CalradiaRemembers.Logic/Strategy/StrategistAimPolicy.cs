// Code written by Gabriel Mailhot, 30/10/2026.
// The strategist asks the player for help (Gabriel, 30/10/2026, "go" on the second increment). Increment 1 had lords
// play their realm's strategy offscreen (a counterweight against a realm grown too strong, plots against a house grown
// too strong); here a few of them turn to the player with it, as an aim of their own (the CrAims door): break the
// dominant realm's armies, or bring down the head of a rival house grown too strong. Bound by gating before declaration
// (Gabriel, 09/09/2026): the lord asks outright only for a deed the game will really verify, so the aim's deed is put at
// the head of his grounded quest menu, the same road every war task he offers already takes (attack_faction,
// attack_lord). Bound too by NPCs never lie: every line states what is true of the world, and the outcome is the
// player's to make.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Strategy
{
   /// <summary>The deed a strategist asks of the player.</summary>
   public enum StrategistDeed
   {
      /// <summary>Defeat the head of a rival house grown too strong, in the field (attack_lord).</summary>
      StrikeTheRival,

      /// <summary>Defeat the dominant realm's forces in the field (attack_faction).</summary>
      BreakTheDominant
   }

   /// <summary>What is known of a lord who might hold a strategist's aim toward the player.</summary>
   public sealed class StrategistGiverFacts
   {
      /// <summary>The lord's StringId.</summary>
      public string Id { get; init; }

      /// <summary>He heads his house: the house's strategy is his to pursue.</summary>
      public bool IsHouseHead { get; init; }

      /// <summary>The player has met him (Calradia Remembers keeps a profile): a lord asks no task of a stranger.</summary>
      public bool KnowsThePlayer { get; init; }

      /// <summary>His relation with the player (the game's own).</summary>
      public int RelationWithPlayer { get; init; }

      /// <summary>His realm is at war with the player's: an enemy asks no favour.</summary>
      public bool AtWarWithPlayer { get; init; }

      /// <summary>He already holds an aim (any source): a lord holds one at a time.</summary>
      public bool HoldsAnAim { get; init; }

      /// <summary>A task he gave the player is still outstanding: the game allows one at a time.</summary>
      public bool HasOutstandingTask { get; init; }

      /// <summary>He is of the player's own house.</summary>
      public bool IsPlayerSide { get; init; }
   }

   /// <summary>Whether the deed an aim asks for can still be done, read from the world.</summary>
   public sealed class StrategistTargetFacts
   {
      /// <summary>The target (a lord, or the dominant realm) still stands: alive and free, or not eliminated.</summary>
      public bool Stands { get; init; }

      /// <summary>The target's realm is at war with the giver's: otherwise the giver has no war to ask help in.</summary>
      public bool AtWarWithGiversRealm { get; init; }

      /// <summary>The target is of the player's own realm or house: the player is never asked to strike his own.</summary>
      public bool IsPlayersOwn { get; init; }
   }

   /// <summary>The names an aim's words are told with.</summary>
   public sealed class StrategistAimWording
   {
      /// <summary>The deed.</summary>
      public StrategistDeed Deed { get; init; }

      /// <summary>The target as the quest block must name it: the rival house's head, or the dominant realm.</summary>
      public string TargetName { get; init; }

      /// <summary>The rival's house (StrikeTheRival only).</summary>
      public string RivalHouse { get; init; }

      /// <summary>The rival's realm (StrikeTheRival only).</summary>
      public string RivalRealm { get; init; }
   }

   /// <summary>Who among the strategists turns to the player, with which deed, in which words, and until when. Pure.</summary>
   public static class StrategistAimPolicy
   {
      /// <summary>TUNE: strategist aims standing at once in all the world ("most get nothing", the CrAims door's own spirit).</summary>
      public const int MaxStandingAims = 3;

      /// <summary>TUNE: days an aim stands before it lapses, if the deed was neither asked nor done.</summary>
      public const int AimDays = 40;

      /// <summary>The least relation with the player a lord must have to turn to them: one who dislikes the player asks nothing.</summary>
      public const int MinRelationWithPlayer = NpcMemoryService.Core.Standing.VanillaRelation.NeutralTier;

      /// <summary>True when this lord may hold a strategist's aim toward the player now.</summary>
      public static bool MayTurnToThePlayer(StrategistGiverFacts f)
         => f != null && f.IsHouseHead && f.KnowsThePlayer && !f.AtWarWithPlayer && !f.HoldsAnAim && !f.HasOutstandingTask && !f.IsPlayerSide
            && f.RelationWithPlayer >= MinRelationWithPlayer;

      /// <summary>
      ///   The lords who turn to the player, warmest first, as many as the open places allow: the player hears first from
      ///   those who already think well of them.
      /// </summary>
      public static IReadOnlyList<string> Choose(IEnumerable<StrategistGiverFacts> candidates, int openPlaces)
         => openPlaces <= 0 || candidates == null
            ? new List<string>()
            : candidates.Where(MayTurnToThePlayer).OrderByDescending(c => c.RelationWithPlayer).Take(openPlaces).Select(c => c.Id).ToList();

      /// <summary>The places left under <see cref="MaxStandingAims" />.</summary>
      public static int OpenPlaces(int standing) => System.Math.Max(0, MaxStandingAims - standing);

      /// <summary>
      ///   The deed a lord asks, or null: his own rival first (a house grown too strong whose head rides for a realm at war
      ///   with his), the dominant realm otherwise (when his realm is at war with it). A rival in his own realm is plotted
      ///   against offscreen, never brought to the player: there is no war in which to strike him.
      /// </summary>
      public static StrategistDeed? ChooseDeed(StrategistTargetFacts rival, StrategistTargetFacts dominant)
      {
         if (DeedStands(rival)) return StrategistDeed.StrikeTheRival;
         if (DeedStands(dominant)) return StrategistDeed.BreakTheDominant;

         return null;
      }

      /// <summary>True while the deed can still be done and asked: the target stands, the war holds, and it is not the player's own.</summary>
      public static bool DeedStands(StrategistTargetFacts f) => f != null && f.Stands && f.AtWarWithGiversRealm && !f.IsPlayersOwn;

      /// <summary>The lord's want, in his own terms.</summary>
      public static string Want(StrategistAimWording w)
         => w.Deed == StrategistDeed.StrikeTheRival
            ? $"The house of {w.RivalHouse} has grown far stronger than your own, and its head, {w.TargetName}, rides for {w.RivalRealm}, at war with your realm. You want him brought low in the field before his house grows stronger still."
            : $"{w.TargetName} has grown too strong for the balance of Calradia, and your realm is at war with it. You want its armies broken in the field.";

      /// <summary>
      ///   What the player has to do with it. It names no task: the Lean prompt teaches no task-giving, so a "task you may
      ///   give" would be a promise the world does not keep there. The task itself heads his quest menu (Full prompts).
      /// </summary>
      public static string PlayerPart(StrategistAimWording w)
         => w.Deed == StrategistDeed.StrikeTheRival
            ? $"The player could defeat {w.TargetName} in battle for you."
            : $"The player could defeat {w.TargetName}'s forces in battle for you.";

      /// <summary>The quest block's type token for the deed, as the grounded menu and the quest factory know it.</summary>
      public static string TypeToken(StrategistDeed deed) => deed == StrategistDeed.StrikeTheRival ? "attack_lord" : "attack_faction";

      /// <summary>The quest block's target field for the deed.</summary>
      public static string TargetField(StrategistDeed deed) => deed == StrategistDeed.StrikeTheRival ? "target_hero" : "target_faction";

      /// <summary>The cue the grounded menu gives the deed.</summary>
      public static string MenuCue(StrategistAimWording w)
         => w.Deed == StrategistDeed.StrikeTheRival
            ? $"your own aim: {w.TargetName} heads a house grown too strong; see him beaten in the field"
            : $"your own aim: {w.TargetName} has grown too strong; see its forces beaten in the field";
   }
}
