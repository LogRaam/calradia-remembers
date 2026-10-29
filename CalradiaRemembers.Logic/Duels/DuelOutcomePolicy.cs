// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0 (pure): what a duel LEAVES BEHIND. Four ways it can end (the player wins, the NPC wins,
// or either side walks away before it starts), each moving the NPC's posture toward the player, the player's
// renown, the loser's clan influence, a linked grudge, and the wager. The through-line is that steel honestly crossed SETTLES things
// (respect is earned, a grudge that drove the fight is answered whoever wins), while walking away HARDENS them
// (contempt, a grudge that festers because it was never faced). Non-lethal is assumed: this is regard and
// reputation moving, never a death. Get the signs wrong and winning a duel would cost you the NPC's respect,
// or fleeing one would settle a grudge, and the pillar would teach players the opposite of what it means.

using CalradiaRemembers.Logic.Grudges;

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>How a duel ended.</summary>
   public enum DuelResolution
   {
      /// <summary>The player bested the NPC on the field.</summary>
      PlayerWon,

      /// <summary>The NPC bested the player on the field.</summary>
      NpcWon,

      /// <summary>The player refused the challenge or failed to appear. No blades were crossed.</summary>
      PlayerForfeited,

      /// <summary>The NPC refused or backed out. No blades were crossed.</summary>
      NpcForfeited
   }

   /// <summary>What the duel did to a grudge that drove it. None when no grudge was in play.</summary>
   public enum DuelGrudgeEffect
   {
      /// <summary>No linked grudge, so nothing to move.</summary>
      None,

      /// <summary>Honour was answered by steel: the grudge is settled whoever won.</summary>
      Settled,

      /// <summary>The fight was ducked: the grudge festers and deepens.</summary>
      Hardened
   }

   /// <summary>Who collects the wager. The host applies it only when the stake was material (gold / a prisoner's freedom).</summary>
   public enum DuelStakeAward
   {
      /// <summary>No transfer: an honour-only duel, or a contest that never happened.</summary>
      None,

      /// <summary>The player won the wager.</summary>
      ToPlayer,

      /// <summary>The NPC won the wager.</summary>
      ToNpc
   }

   /// <summary>Everything <see cref="DuelOutcomePolicy.Resolve" /> needs to weigh the aftermath.</summary>
   public sealed class DuelResolutionContext
   {
      /// <summary>How the duel ended.</summary>
      public DuelResolution Resolution { get; set; }

      /// <summary>
      ///   WHY it was fought, which decides whether losing is a humiliation or an afternoon. Defaults to
      ///   HonourAffront, the behaviour every caller had before a friendly bout existed.
      /// </summary>
      public DuelReason Reason { get; set; } = DuelReason.HonourAffront;

      /// <summary>
      ///   A duel with steel or a brawl with fists (Gabriel, 28/10/2026: the duel is noble, the brawl a settling of
      ///   scores). A brawl moves only the two who fought: no renown, no house's influence, no wager.
      /// </summary>
      public DuelKind Kind { get; set; } = DuelKind.Honour;

      /// <summary>A grudge against the player drove this duel, so its fate (settled or hardened) is in play.</summary>
      public bool AGrudgeDroveIt { get; set; }

      /// <summary>The stake beyond honour, so the award can be reported (the host transfers it only if material).</summary>
      public DuelStakeKind Stake { get; set; }

      /// <summary>How many witnessed it. A win or a walk-away before a crowd swings renown harder than one in private.</summary>
      public int WitnessCount { get; set; }
   }

   /// <summary>
   ///   The stance, renown, grudge and wager consequences of a duel. All stance deltas are the NPC's posture
   ///   TOWARD THE PLAYER (Affection, Respect, Fear); renown is the player's. The host applies each to the live
   ///   systems.
   /// </summary>
   public sealed class DuelOutcomeResult
   {
      /// <summary>Change to the NPC's fondness toward the player.</summary>
      public int NpcAffectionDelta { get; set; }

      /// <summary>Change to the NPC's esteem for the player's worth and prowess.</summary>
      public int NpcRespectDelta { get; set; }

      /// <summary>Change to how much the NPC fears the player (0..100 axis).</summary>
      public int NpcFearDelta { get; set; }

      /// <summary>Change to the player's clan renown.</summary>
      public int PlayerRenownDelta { get; set; }

      /// <summary>
      ///   Influence the LOSER's clan forfeits (always <= 0). Set only for a FOUGHT duel (a loss in single
      ///   combat is a public embarrassment for the loser's house); 0 for a forfeit or a contest that never
      ///   happened. The host maps the resolution to the losing hero (the NPC when the player won, the player
      ///   when the NPC won) and applies it to their clan.
      /// </summary>
      public int LoserInfluenceDelta { get; set; }

      /// <summary>What became of a grudge that drove the duel.</summary>
      public DuelGrudgeEffect GrudgeEffect { get; set; }

      /// <summary>Who won the wager (applied by the host only for a material stake).</summary>
      public DuelStakeAward StakeAward { get; set; }
   }

   /// <summary>
   ///   Resolves a finished (or refused) duel into its consequences. Every constant is a tuning knob. The
   ///   shape: an honestly fought duel earns the loser's respect for the victor and settles a driving grudge
   ///   either way; a refusal earns contempt and hardens the grudge. Renown swings scale with the size of the
   ///   crowd, since a duel's whole social weight is who saw it.
   /// </summary>
   public static class DuelOutcomePolicy
   {
      // ── Fought, player won ────────────────────────────────────────────────

      /// <summary>Tuning: respect the NPC gains for the player who bested them. Being beaten by a real hand earns esteem.</summary>
      public const int RespectFromLosingToPlayer = 12;

      /// <summary>Tuning: fear the NPC gains on learning the player can beat them.</summary>
      public const int FearFromLosingToPlayer = 8;

      /// <summary>Tuning: the sting to a proud loser's fondness. Small, and outweighed when a grudge is settled by the fight.</summary>
      public const int LoserPrideAffrontToAffection = -3;

      /// <summary>Tuning: renown the player gains for winning a duel, before the witness bonus.</summary>
      public const int RenownForWinning = 6;

      /// <summary>Tuning: extra renown per witness to a win. A victory before a crowd travels.</summary>
      public const int RenownPerWitnessOnWin = 2;

      // ── Fought, NPC won ───────────────────────────────────────────────────

      /// <summary>Tuning: respect the NPC gains for a player who DARED the field and lost. Showing up is not contemptible.</summary>
      public const int RespectFromPlayerDaring = 5;

      /// <summary>Tuning: fear the NPC SHEDS on beating the player. One no longer fears a hand one has bested.</summary>
      public const int FearReliefFromBeatingPlayer = -6;

      /// <summary>Tuning: fondness the magnanimous victor extends to a worthy opponent.</summary>
      public const int VictorMagnanimityToAffection = 2;

      /// <summary>Tuning: renown the player keeps for fighting bravely and losing, before the witness bonus. Small: losing is not glory.</summary>
      public const int RenownForLosingBravely = 1;

      // ── Influence: a lost duel costs the loser standing ──────────────────

      /// <summary>Tuning: base influence the loser of a FOUGHT duel forfeits. A defeat in single combat is a public embarrassment for their house.</summary>
      public const int BaseInfluenceLossForLoser = 8;

      /// <summary>Tuning: extra influence the loser forfeits per witness. A defeat before a crowd shames the house further.</summary>
      public const int InfluenceLossPerWitnessForLoser = 2;

      /// <summary>Tuning: ceiling on the loser's influence loss, so a vast crowd cannot annihilate a house's standing over a single duel.</summary>
      public const int MaxInfluenceLossForLoser = 30;

      // ── Settling a grudge by steel ────────────────────────────────────────

      /// <summary>
      ///   Tuning: how much a grudge DEEPENS when the duel it drove is ducked by either side. Enough to be felt and
      ///   to keep a grievance from quietly decaying away after a refusal, well short of turning a quarrel into a
      ///   feud on its own.
      /// </summary>
      public const int GrudgeHardenedByForfeit = 15;

      /// <summary>Tuning: the warmth restored when a driving grudge is answered by an honest fight, whoever won. Steel clears the air.</summary>
      public const int GrudgeSettledWarmthToAffection = 8;

      // ── Walked away (either side forfeited) ───────────────────────────────

      /// <summary>Tuning: respect the NPC loses for a player who would not face them.</summary>
      public const int RespectLostFromPlayerForfeit = -10;

      /// <summary>Tuning: fear the NPC SHEDS when the player will not fight. A man who ducks steel is not feared.</summary>
      public const int FearLostFromPlayerForfeit = -8;

      /// <summary>Tuning: the fondness sting of the player refusing.</summary>
      public const int PlayerForfeitAffrontToAffection = -3;

      /// <summary>Tuning: renown the player gains when the NPC is the one who balks. You called them out and they blinked.</summary>
      public const int RenownForCalledBluff = 3;

      /// <summary>Tuning: the humiliated NPC's resentment of the player who shamed them into backing down.</summary>
      public const int NpcForfeitResentmentToAffection = -5;

      /// <summary>Tuning: fear the NPC gains of a player they dared not fight.</summary>
      public const int FearFromNpcForfeit = 5;

      // ── A friendly bout: agreed, not provoked ─────────────────────────────

      /// <summary>Tuning: the warmth of a good match between people who meant no harm by it. Crossing blades in play is a thing friends do.</summary>
      public const int FriendlyBoutWarmthToAffection = 5;

      /// <summary>Tuning: the respect earned by whoever showed well, winner or loser. A bout is a demonstration, and both sides watched it.</summary>
      public const int RespectFromAFriendlyBout = 4;

      /// <summary>Tuning: renown for a friendly match. Small on purpose: sparring is not a feat of arms, and a crowd changes nothing about that.</summary>
      public const int RenownFromAFriendlyBout = 1;

      /// <summary>The full aftermath of a duel. A null context yields an all-zero, no-effect result.</summary>
      public static DuelOutcomeResult Resolve(DuelResolutionContext ctx)
      {
         var result = new DuelOutcomeResult {GrudgeEffect = DuelGrudgeEffect.None, StakeAward = DuelStakeAward.None};

         if (ctx == null) return result;

         int witnesses = ctx.WitnessCount < 0 ? 0 : ctx.WitnessCount;

         switch (ctx.Resolution)
         {
            // A FRIENDLY BOUT SHORT-CIRCUITS THE LOT, and it has to, because every branch below is written
            // about an affront: wounded pride, a shamed house, fear of the man who beat you. None of that
            // follows from a match two people agreed to for the pleasure of it. Warmth and respect both ways,
            // no fear, no influence lost, and a token of renown; a bout nobody goes through with costs nothing
            // at all, because nothing binding was ever agreed.
            case DuelResolution.PlayerWon when ctx.Reason == DuelReason.Friendly:
            case DuelResolution.NpcWon when ctx.Reason == DuelReason.Friendly:
               result.NpcAffectionDelta = FriendlyBoutWarmthToAffection;
               result.NpcRespectDelta = RespectFromAFriendlyBout;
               result.PlayerRenownDelta = RenownFromAFriendlyBout;
               ApplyFoughtGrudge(ctx, result); // steel still clears the air when one was carried in

               break;

            case DuelResolution.PlayerForfeited when ctx.Reason == DuelReason.Friendly:
            case DuelResolution.NpcForfeited when ctx.Reason == DuelReason.Friendly:

               break;

            case DuelResolution.PlayerWon:
               result.NpcRespectDelta = RespectFromLosingToPlayer;
               result.NpcFearDelta = FearFromLosingToPlayer;
               result.NpcAffectionDelta = LoserPrideAffrontToAffection;
               result.PlayerRenownDelta = RenownForWinning + witnesses * RenownPerWitnessOnWin;
               result.LoserInfluenceDelta = -LoserInfluenceLoss(witnesses); // the bested NPC's house loses standing
               result.StakeAward = MaterialAwardOrNone(ctx.Stake, DuelStakeAward.ToPlayer);
               ApplyFoughtGrudge(ctx, result);

               break;

            case DuelResolution.NpcWon:
               result.NpcRespectDelta = RespectFromPlayerDaring;
               result.NpcFearDelta = FearReliefFromBeatingPlayer;
               result.NpcAffectionDelta = VictorMagnanimityToAffection;
               // Flat, and deliberately not scaled by the crowd: fighting well and losing is respectable, but
               // it is not a victory, so a bigger audience does not turn a loss into renown.
               result.PlayerRenownDelta = RenownForLosingBravely;
               result.LoserInfluenceDelta = -LoserInfluenceLoss(witnesses); // the bested PLAYER's clan loses standing
               result.StakeAward = MaterialAwardOrNone(ctx.Stake, DuelStakeAward.ToNpc);
               ApplyFoughtGrudge(ctx, result);

               break;

            case DuelResolution.PlayerForfeited:
               result.NpcRespectDelta = RespectLostFromPlayerForfeit;
               result.NpcFearDelta = FearLostFromPlayerForfeit;
               result.NpcAffectionDelta = PlayerForfeitAffrontToAffection;
               result.PlayerRenownDelta = -DuelStakesPolicy.ForfeitRenownCost(witnesses);
               if (ctx.AGrudgeDroveIt) result.GrudgeEffect = DuelGrudgeEffect.Hardened;

               break;

            case DuelResolution.NpcForfeited:
               result.NpcAffectionDelta = NpcForfeitResentmentToAffection;
               result.NpcFearDelta = FearFromNpcForfeit;
               result.PlayerRenownDelta = RenownForCalledBluff;
               if (ctx.AGrudgeDroveIt) result.GrudgeEffect = DuelGrudgeEffect.Hardened;

               break;
         }

         // A brawl is between the two who fought: what they think of each other moves as it would, nothing else does.
         if (ctx.Kind == DuelKind.Brawl)
         {
            result.PlayerRenownDelta = 0;
            result.LoserInfluenceDelta = 0;
            result.StakeAward = DuelStakeAward.None;
         }

         return result;
      }

      #region private

      /// <summary>A duel that was actually FOUGHT settles a driving grudge whoever won, and restores some warmth for having cleared the air.</summary>
      private static void ApplyFoughtGrudge(DuelResolutionContext ctx, DuelOutcomeResult result)
      {
         if (!ctx.AGrudgeDroveIt) return;

         result.GrudgeEffect = DuelGrudgeEffect.Settled;
         result.NpcAffectionDelta += GrudgeSettledWarmthToAffection;
      }

      /// <summary>
      ///   Carries a duel's verdict onto the grudge that drove it: SETTLED resolves it, HARDENED deepens it by
      ///   <see cref="GrudgeHardenedByForfeit" />. Until 19/09/2026 both were computed and neither was ever applied,
      ///   because no API could deepen a grudge and applying only the settle half would have taught that a duel can
      ///   only ever help. A vendetta is refused here as it is everywhere else: steel does not answer a dead kinsman,
      ///   and a vendetta cannot be made worse than unforgivable. Returns whether the grudge changed.
      /// </summary>
      public static bool ApplyGrudgeEffect(Grudge grudge, DuelGrudgeEffect effect, int today)
      {
         if (grudge == null || !GrudgePolicy.IsResolvable(grudge)) return false;

         switch (effect)
         {
            case DuelGrudgeEffect.Settled: return GrudgePolicy.Resolve(grudge, today);
            case DuelGrudgeEffect.Hardened: return GrudgePolicy.Harden(grudge, GrudgeHardenedByForfeit);
            default: return false;
         }
      }

      /// <summary>The award only means a transfer when the stake was material; an honour-only duel moves no goods however it ended.</summary>
      private static DuelStakeAward MaterialAwardOrNone(DuelStakeKind stake, DuelStakeAward winnerSide)
         => stake == DuelStakeKind.Honour ? DuelStakeAward.None : winnerSide;

      /// <summary>The (positive) magnitude of the loser's influence loss: a base cost plus a per-witness surcharge, capped so one duel cannot ruin a house.</summary>
      private static int LoserInfluenceLoss(int witnesses)
      {
         int loss = BaseInfluenceLossForLoser + witnesses * InfluenceLossPerWitnessForLoser;

         return loss > MaxInfluenceLossForLoser ? MaxInfluenceLossForLoser : loss;
      }

      #endregion
   }
}
