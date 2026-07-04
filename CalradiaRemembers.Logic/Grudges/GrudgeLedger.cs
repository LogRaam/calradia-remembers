// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
   /// <summary>
   ///   The whole persisted grudge store: every live and resolved <see cref="Grudge" /> plus the
   ///   stamped <see cref="MigrationVersion" /> that makes the one-shot backfill idempotent across
   ///   session launches.
   /// </summary>
   public sealed class GrudgeLedger
   {
      /// <summary>Every grudge ever formed (resolved ones kept for history/decay bookkeeping, never re-surfaced once resolved).</summary>
      public List<Grudge> Grudges { get; set; } = new();

      /// <summary>The migration version already applied to this ledger (0 = never migrated).</summary>
      public int MigrationVersion { get; set; }
   }
}