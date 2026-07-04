// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Appreciations
{
   /// <summary>
   ///   The whole persisted appreciation store: every felt <see cref="Appreciation" /> plus the stamped
   ///   <see cref="MigrationVersion" /> that makes the one-shot backfill idempotent across session
   ///   launches.
   /// </summary>
   public sealed class AppreciationLedger
   {
      /// <summary>
      ///   Every appreciation ever formed, live and fully-faded alike (kept for history/decay bookkeeping; a fully-faded
      ///   one never re-surfaces once its effective magnitude reads zero).
      /// </summary>
      public List<Appreciation> Appreciations { get; set; } = new();

      /// <summary>The migration version already applied to this ledger (0 = never migrated).</summary>
      public int MigrationVersion { get; set; }
   }
}