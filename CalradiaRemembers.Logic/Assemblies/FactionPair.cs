// Code written by Gabriel Mailhot, 10/09/2026.
// Extracted during the facts-object realignment. NonAggressionPactPolicy.IsMatch took FOUR faction ids in
// a row (the pact's two, then the query's two) and its own doc had to promise the caller "need not worry
// which side it happens to pass first". A promise in prose is exactly what a type should be making, so
// the pair becomes a type and the order-independence becomes its behaviour.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
   /// <summary>
   ///   Two factions bound by one agreement, in no particular order. A pact between A and B is the same
   ///   pact as one between B and A, which is why this type compares itself both ways round and why two
   ///   of these may safely sit side by side in a signature.
   /// </summary>
   public readonly struct FactionPair
   {
      public FactionPair(string firstId, string secondId)
      {
         FirstId = firstId;
         SecondId = secondId;
      }

      /// <summary>One of the two factions. Which one is "first" carries no meaning.</summary>
      public string FirstId { get; }

      /// <summary>The other faction.</summary>
      public string SecondId { get; }

      /// <summary>Both ids resolved. An unresolved pair matches nothing, so a failed lookup is never a pact.</summary>
      public bool IsResolved
         => !string.IsNullOrEmpty(FirstId) && !string.IsNullOrEmpty(SecondId);

      /// <summary>True when this pair names the same two factions as <paramref name="other" />, in either order.</summary>
      public bool Matches(FactionPair other)
      {
         if (!IsResolved || !other.IsResolved) return false;

         return FirstId == other.FirstId && SecondId == other.SecondId
                || FirstId == other.SecondId && SecondId == other.FirstId;
      }
   }
}
