// Code written by Gabriel Mailhot, 15/10/2026.
// The reckoning that follows an unserved war duty, as Gabriel laid it out (15/10/2026): a SUMMONS to come and answer
// for it in person; then, if the player does not fight, a levy in coin to stand for his absence from the front; or,
// failing coin, troops, horses or arms; and if in the end he neither fights nor provides anything, a fief is taken.
// Serving the realm again settles it at any step. One reckoning per war (WarDutyPolicy). Pure: the host sends the
// letters, opens the journal entry, moves the gold and the goods, and carries out the confiscation this returns.

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>Where a reckoning stands.</summary>
   public enum WarDutyStage
   {
      /// <summary>The ruler has summoned the player to come and answer for his absence.</summary>
      Summoned,

      /// <summary>
      ///   The player owes the levy (coin, or troops, horses or arms to its value) by the deadline, unless he serves
      ///   first. Reached when he answered the summons in person, or by letter when he never came.
      /// </summary>
      Owing,

      /// <summary>Settled: he served, or the levy was paid in full.</summary>
      Settled,

      /// <summary>He neither served nor paid, and a fief was taken.</summary>
      Confiscated,

      /// <summary>He neither served nor paid and held no fief to take: the ruler remembers it, and nothing more.</summary>
      Lapsed,

      /// <summary>The war ended, or the player left the realm, before it was settled.</summary>
      Dropped
   }

   /// <summary>What happened to the reckoning.</summary>
   public enum WarDutyEvent
   {
      /// <summary>A day passed (the deadline is checked).</summary>
      DayPassed,

      /// <summary>The player came before the ruler: a conversation with him opened, or he joined the ruler's army.</summary>
      Appeared,

      /// <summary>The player served the realm: a battle against its enemy, a siege, a day in its army.</summary>
      Served,

      /// <summary>The player handed over part of the levy, in coin or in kind (<see cref="WarDutyInput.Amount" />, its value).</summary>
      LevyPaid,

      /// <summary>The realm made peace with every enemy, or the player is no longer its sworn vassal.</summary>
      DutyEnded
   }

   /// <summary>What the host must do after a step.</summary>
   public enum WarDutyEffect
   {
      /// <summary>Nothing to do.</summary>
      None,

      /// <summary>He never came: the ruler writes to demand the levy (the stage is now Owing).</summary>
      SendLevyLetter,

      /// <summary>He answered in person: the levy is demanded face to face (the stage is now Owing).</summary>
      DemandInPerson,

      /// <summary>Settled; the ruler remembers it was answered.</summary>
      Settled,

      /// <summary>Take one of the player's fiefs for the ruler's clan.</summary>
      Confiscate,

      /// <summary>Nothing could be taken; the ruler remembers the defaulted duty.</summary>
      RememberDefault,

      /// <summary>Close quietly: the war or the vassalage ended.</summary>
      Drop
   }

   /// <summary>One reckoning, persisted by the host (plain values, additive JSON).</summary>
   public sealed class WarDutyCase
   {
      /// <summary>The realm's id.</summary>
      public string KingdomId { get; set; }

      /// <summary>The ruler's hero StringId when the reckoning opened.</summary>
      public string RulerId { get; set; }

      /// <summary>The game day the summons went out.</summary>
      public int OpenedOnDay { get; set; }

      /// <summary>Where it stands.</summary>
      public WarDutyStage Stage { get; set; } = WarDutyStage.Summoned;

      /// <summary>The last day of the current stage.</summary>
      public int DeadlineDay { get; set; }

      /// <summary>The levy, in coin, fixed when the reckoning opened (WarLevyPolicy).</summary>
      public int LevyAmount { get; set; }

      /// <summary>What has been handed over so far, coin plus the value of goods and troops.</summary>
      public int LevyPaid { get; set; }

      /// <summary>True once he came before the ruler.</summary>
      public bool Appeared { get; set; }

      /// <summary>True while the reckoning is still open.</summary>
      public bool IsOpen => Stage == WarDutyStage.Summoned || Stage == WarDutyStage.Owing;

      /// <summary>What is still owed.</summary>
      public int LevyOutstanding => LevyAmount > LevyPaid ? LevyAmount - LevyPaid : 0;
   }

   /// <summary>One step's input.</summary>
   public sealed class WarDutyInput
   {
      /// <summary>What happened.</summary>
      public WarDutyEvent Event { get; init; }

      /// <summary>The game day.</summary>
      public int Today { get; init; }

      /// <summary>For <see cref="WarDutyEvent.LevyPaid" />: the coin, or the value of what was handed over.</summary>
      public int Amount { get; init; }

      /// <summary>The player's clan holds at least one fief the ruler could take (not under siege).</summary>
      public bool HoldsATakeableFief { get; init; }
   }

   /// <summary>Pure: opens a reckoning and moves it a step.</summary>
   public static class WarDutyLadder
   {
      /// <summary>
      ///   Days the player has at each step: to come before the ruler, then to serve or pay. Generous because a king on
      ///   campaign is hard to reach. Claude's default, accepted by Gabriel 15/10/2026.
      /// </summary>
      public const int StageDays = 20;

      /// <summary>Opens a reckoning: the summons goes out today.</summary>
      public static WarDutyCase Open(string kingdomId, string rulerId, int levyAmount, int today)
         => new WarDutyCase {
            KingdomId = kingdomId,
            RulerId = rulerId,
            OpenedOnDay = today,
            Stage = WarDutyStage.Summoned,
            DeadlineDay = today + StageDays,
            LevyAmount = levyAmount < 0 ? 0 : levyAmount
         };

      /// <summary>
      ///   Whether the ruler calls the player in himself, at his hall or on the road (Gabriel's ladder, 15/10/2026: the
      ///   vassal is summoned to come and answer for it). While summoned, and while the levy is owed by letter because
      ///   the player never came; once the player has stood before him, the levy is between them and he does not
      ///   send for them again.
      /// </summary>
      public static bool CallsThePlayerIn(WarDutyCase c)
         => c != null && (c.Stage == WarDutyStage.Summoned || (c.Stage == WarDutyStage.Owing && !c.Appeared));

      /// <summary>Moves the reckoning one step and says what the host must do. A closed reckoning never moves.</summary>
      public static WarDutyEffect Advance(WarDutyCase c, WarDutyInput input)
      {
         if (c == null || input == null || !c.IsOpen) return WarDutyEffect.None;

         switch (input.Event)
         {
            case WarDutyEvent.DutyEnded:
               c.Stage = WarDutyStage.Dropped;
               return WarDutyEffect.Drop;

            case WarDutyEvent.Served:
               c.Stage = WarDutyStage.Settled;
               return WarDutyEffect.Settled;

            case WarDutyEvent.Appeared:
               c.Appeared = true;
               if (c.Stage != WarDutyStage.Summoned) return WarDutyEffect.None;
               c.Stage = WarDutyStage.Owing;
               c.DeadlineDay = input.Today + StageDays;
               return WarDutyEffect.DemandInPerson;

            case WarDutyEvent.LevyPaid:
               if (input.Amount <= 0) return WarDutyEffect.None;
               c.LevyPaid += input.Amount;
               if (c.LevyOutstanding > 0) return WarDutyEffect.None;
               c.Stage = WarDutyStage.Settled;
               return WarDutyEffect.Settled;

            case WarDutyEvent.DayPassed:
               if (input.Today <= c.DeadlineDay) return WarDutyEffect.None;
               if (c.Stage == WarDutyStage.Summoned)
               {
                  c.Stage = WarDutyStage.Owing;
                  c.DeadlineDay = input.Today + StageDays;
                  return WarDutyEffect.SendLevyLetter;
               }

               if (input.HoldsATakeableFief)
               {
                  c.Stage = WarDutyStage.Confiscated;
                  return WarDutyEffect.Confiscate;
               }

               c.Stage = WarDutyStage.Lapsed;
               return WarDutyEffect.RememberDefault;

            default:
               return WarDutyEffect.None;
         }
      }
   }
}
