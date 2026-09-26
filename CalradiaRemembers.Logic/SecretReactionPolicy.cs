// Code written by Gabriel Mailhot, 26/09/2026.
// Courier Inc 4b: what a lord does about an intercepted letter that came into their hands, "in proportion to the secret"
// (Gabriel, 26/09/2026). The model reads the letter from the reader's side and names how it touches them in one word;
// this turns that word into a severity, and the severity into a reaction. Arrests "in the King's name" are a later
// pillar that will hang off the gravest band.
//
//   Nothing / Embarrassing  : the reader remembers it (4a), and word gets about. No more.
//   AgainstInterests        : a grudge against whoever wrote it; the player, if they did, loses a little renown.
//   Treason                 : a sharp grudge, more renown lost, and, when the player wrote it, the reader writes to
//                             confront them.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>How an intercepted letter touches the lord who read it.</summary>
   public enum SecretSeverity
   {
      /// <summary>It does not concern them.</summary>
      Nothing,

      /// <summary>Gossip worth having, no harm to them.</summary>
      Embarrassing,

      /// <summary>It works against their interests or their house.</summary>
      AgainstInterests,

      /// <summary>It plots against them or their liege.</summary>
      Treason
   }

   /// <summary>What the reader does about it.</summary>
   public sealed class SecretReaction
   {
      /// <summary>The grudge they form against the letter's author, 0 for none.</summary>
      public int GrudgeMagnitude { get; set; }

      /// <summary>The renown the player loses, when the player wrote it; 0 for none.</summary>
      public int PlayerRenownPenalty { get; set; }

      /// <summary>True when the reader writes to the player to confront them (the player wrote it).</summary>
      public bool ConfrontsPlayerByLetter { get; set; }
   }

   /// <summary>From the model's one-word reading to a severity, and from a severity to a reaction. Pure.</summary>
   public static class SecretReactionPolicy
   {
      /// <summary>TUNE: the grudge a letter plotting against the reader leaves.</summary>
      public const int TreasonGrudgeMagnitude = 35;

      /// <summary>TUNE: the renown the player loses when their plot is read by its target.</summary>
      public const int TreasonRenownPenalty = 5;

      /// <summary>
      ///   The severity the model's answer names, read forgivingly (first matching word wins, gravest first); an
      ///   answer that names none is Nothing, so a garbled reply never punishes the player.
      /// </summary>
      public static SecretSeverity Parse(string answer)
      {
         string a = answer?.ToUpperInvariant() ?? "";
         if (a.Contains("TREASON")) return SecretSeverity.Treason;
         if (a.Contains("AGAINST")) return SecretSeverity.AgainstInterests;
         if (a.Contains("EMBARRASS")) return SecretSeverity.Embarrassing;

         return SecretSeverity.Nothing;
      }

      /// <summary>
      ///   What the reader does. Against their interests: the leak's existing grudge and renown (sharper if they already
      ///   held a grudge against the author). Treason: a sharp grudge, more renown, and a confronting letter when the
      ///   player wrote it. Renown and the letter apply only to a letter the player wrote.
      /// </summary>
      public static SecretReaction For(SecretSeverity severity, bool authorIsPlayer, bool readerAlreadyHeldGrudge)
      {
         switch (severity)
         {
            case SecretSeverity.AgainstInterests:
               return new SecretReaction {
                  GrudgeMagnitude = LetterLeakConsequencePolicy.GrudgeMagnitude(readerAlreadyHeldGrudge),
                  PlayerRenownPenalty = authorIsPlayer ? LetterLeakConsequencePolicy.RenownPenalty(readerAlreadyHeldGrudge) : 0
               };
            case SecretSeverity.Treason:
               return new SecretReaction {
                  GrudgeMagnitude = TreasonGrudgeMagnitude,
                  PlayerRenownPenalty = authorIsPlayer ? TreasonRenownPenalty : 0,
                  ConfrontsPlayerByLetter = authorIsPlayer
               };
            default:
               return new SecretReaction();
         }
      }
   }
}
