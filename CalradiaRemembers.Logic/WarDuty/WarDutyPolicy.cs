// Code written by Gabriel Mailhot, 15/10/2026.
// A vassal owes his liege service in war. Maifailun (Nexus, 15/10/2026): a vassal who does not fight the realm's enemy
// should hear from the ruler. Gabriel (15/10/2026): "La lettre devrait etre une convocation ou le joueur doit aller
// rencontrer le Lord pour rendre des comptes. Si le joueur ne participe pas, le Lord pourrait alors demander
// compensation en taxes. Si le joueur ne verse pas le montant ... alors il pourrait fournir des troupes, des chevaux,
// des armes. Si au final le joueur ne participe pas et ne fournit rien, il pourrait etre confisque d'un fief."
// This decides only WHETHER the player owes the duty today; the ladder that follows is WarDutyLadder.

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>Why the player owes, or does not owe, war service today.</summary>
   public enum WarDutyStanding
   {
      /// <summary>He is a sworn vassal of a realm at war and has done nothing for it: the ruler calls him to account.</summary>
      Owes,

      /// <summary>Not a sworn vassal: a mercenary is paid by the day and owes no fealty; a free lord owes nothing.</summary>
      NotAVassal,

      /// <summary>He rules the realm himself.</summary>
      RulesTheRealm,

      /// <summary>He is someone's captive and cannot ride.</summary>
      Captive,

      /// <summary>His realm is at peace.</summary>
      AtPeace,

      /// <summary>The war has not yet run a whole window: an absence is measured within THIS war, not before it.</summary>
      WarTooYoung,

      /// <summary>He swore too recently to be held to account.</summary>
      NewVassal,

      /// <summary>He has fought for the realm within the window.</summary>
      Served,

      /// <summary>The ruler already called him to account in this war: one reckoning per war.</summary>
      AlreadyCalledThisWar
   }

   /// <summary>The facts the duty reads. Days are counted by the host from its own service ledger.</summary>
   public sealed class WarDutyFacts
   {
      /// <summary>The player's clan is sworn to a realm and NOT under mercenary service.</summary>
      public bool IsSwornVassal { get; init; }

      /// <summary>The player leads the realm.</summary>
      public bool RulesTheRealm { get; init; }

      /// <summary>The player is a captive.</summary>
      public bool IsCaptive { get; init; }

      /// <summary>Days the realm has been at war, as the ledger saw it (the longest running war); null at peace.</summary>
      public int? DaysAtWar { get; init; }

      /// <summary>Days since the player swore to this realm (or since the ledger first saw him sworn).</summary>
      public int DaysAsVassal { get; init; }

      /// <summary>Days since the player last served (a battle against the realm's enemy, a siege, a day in its army); null when never.</summary>
      public int? DaysSinceServed { get; init; }

      /// <summary>The window, in days, a vassal may go without serving (MCM, default <see cref="WarDutyPolicy.DefaultWindowDays" />).</summary>
      public int WindowDays { get; init; } = WarDutyPolicy.DefaultWindowDays;

      /// <summary>A reckoning was already opened for the war now running.</summary>
      public bool AlreadyCalledThisWar { get; init; }
   }

   /// <summary>Pure rule: does the player owe his liege war service today.</summary>
   public static class WarDutyPolicy
   {
      /// <summary>How long a vassal may go without serving a realm at war. MCM default; tuning.</summary>
      public const int DefaultWindowDays = 30;

      /// <summary>Clamp for the MCM window so a hand-edited value cannot make the duty instant or absurd.</summary>
      public const int MinWindowDays = 10;

      /// <inheritdoc cref="MinWindowDays" />
      public const int MaxWindowDays = 120;

      /// <summary>Decides whether the player owes war service today, and if not, why.</summary>
      public static WarDutyStanding Decide(WarDutyFacts facts)
      {
         if (facts == null || !facts.IsSwornVassal) return WarDutyStanding.NotAVassal;
         if (facts.RulesTheRealm) return WarDutyStanding.RulesTheRealm;
         if (facts.IsCaptive) return WarDutyStanding.Captive;
         if (!facts.DaysAtWar.HasValue) return WarDutyStanding.AtPeace;
         int window = Window(facts.WindowDays);
         if (facts.DaysAtWar.Value < window) return WarDutyStanding.WarTooYoung;
         if (facts.DaysAsVassal < window) return WarDutyStanding.NewVassal;
         if (facts.DaysSinceServed.HasValue && facts.DaysSinceServed.Value < window) return WarDutyStanding.Served;
         if (facts.AlreadyCalledThisWar) return WarDutyStanding.AlreadyCalledThisWar;

         return WarDutyStanding.Owes;
      }

      /// <summary>The MCM window, clamped.</summary>
      public static int Window(int windowDays)
         => windowDays < MinWindowDays ? MinWindowDays : windowDays > MaxWindowDays ? MaxWindowDays : windowDays;
   }
}
