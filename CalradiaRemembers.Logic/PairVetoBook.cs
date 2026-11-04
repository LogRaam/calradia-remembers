// Code written by Gabriel Mailhot, 28/10/2026.
// Another mod may forbid a conception Calradia Remembers would create. tashmetu (Nexus, 28/10/2026) ships Traits of
// Calradia, whose Sterile trait tells a character "you have no child of your blood and know you will have none", while
// CR's own conception (an intimate act carried to completion, a tale's reward) never read it: a Sterile woman could fall
// pregnant through CR while her own words said she never would. Only the mod that owns the trait knows it, so it
// registers a veto here and CR asks every veto before it conceives (Gabriel approved, 28/10/2026).
// tashmetu (Nexus, 03/11/2026), for Romance & Seduction: CR's hidden affairs between lords should not give a lord a
// second affair in another mod. Gabriel (03/11/2026) wanted CR to depend on no mod, so the same book now answers for
// any pair CR would join, a conception's mother and father or an affair's married lord and lover.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What the vetoes answered: who refused and why, and which vetoes failed while asked.</summary>
   public sealed class PairVetoVerdict
   {
      /// <summary>The mod that refused, or null when none did.</summary>
      public string Source { get; init; }

      /// <summary>Why it refused, in its own words, for the log.</summary>
      public string Reason { get; init; }

      /// <summary>Each veto that threw, as "source: error"; a fault never refuses.</summary>
      public IReadOnlyList<string> Faults { get; init; } = Array.Empty<string>();

      /// <summary>Whether a veto refused.</summary>
      public bool Refused => Source != null;
   }

   /// <summary>The vetoes other mods registered against a pair CR would join, asked in the order they were registered.</summary>
   public sealed class PairVetoBook<THero>
   {
      private readonly List<KeyValuePair<string, Func<THero, THero, string>>> _vetoes = new();

      /// <summary>How many vetoes are registered.</summary>
      public int Count => _vetoes.Count;

      /// <summary>
      ///   Registers <paramref name="veto" /> under <paramref name="source" />, replacing one the same source registered
      ///   before. A blank source or a null veto is ignored. Returns whether it was registered.
      /// </summary>
      public bool Register(string source, Func<THero, THero, string> veto)
      {
         if (string.IsNullOrWhiteSpace(source) || veto == null) return false;

         Unregister(source);
         _vetoes.Add(new KeyValuePair<string, Func<THero, THero, string>>(source.Trim(), veto));

         return true;
      }

      /// <summary>Removes the veto of <paramref name="source" />. Returns whether one was there.</summary>
      public bool Unregister(string source)
      {
         if (string.IsNullOrWhiteSpace(source)) return false;

         return _vetoes.RemoveAll(v => v.Key == source.Trim()) > 0;
      }

      /// <summary>
      ///   Asks every veto about the pair, <paramref name="first" /> then <paramref name="second" /> (a conception's
      ///   mother and father, default when unknown; an affair's married lord and lover). The first that answers with
      ///   words refuses; one that throws is noted and passed over.
      /// </summary>
      public PairVetoVerdict Ask(THero first, THero second)
      {
         var faults = new List<string>();

         foreach (KeyValuePair<string, Func<THero, THero, string>> v in _vetoes.ToArray())
         {
            string reason;
            try
            {
               reason = v.Value(first, second);
            }
            catch (Exception ex)
            {
               faults.Add($"{v.Key}: {ex.GetType().Name}: {ex.Message}");

               continue;
            }

            if (!string.IsNullOrWhiteSpace(reason)) return new PairVetoVerdict {Source = v.Key, Reason = reason.Trim(), Faults = faults};
         }

         return new PairVetoVerdict {Faults = faults};
      }
   }
}
