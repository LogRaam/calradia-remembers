// Code written by Gabriel Mailhot, 01/11/2026.
// expose_secret: what makes a threat of exposure more than a bluff (Gabriel's "go", 01/11/2026, on Claude's proposal, the
// player's own affair included and no divorce on the spot). Only an affair the game really holds can be told, so a false
// accusation stays words. The scandal names both lovers and the player as the teller, every house in the affair pays for it,
// the player's included, and coin taken for silence turns the telling into a broken bargain. The figures are mine, to tune
// in play.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Extortion
{
   /// <summary>The names a scandal is told with.</summary>
   public sealed class ExposureNames
   {
      /// <summary>The one whose affair it is.</summary>
      public string Subject { get; init; }

      /// <summary>Their lover.</summary>
      public string Lover { get; init; }

      /// <summary>The player, who told it.</summary>
      public string Teller { get; init; }

      /// <summary>The player is the lover: they told their own affair.</summary>
      public bool TellerIsLover { get; init; }
   }

   /// <summary>What telling a secret costs the player with one of the two it names.</summary>
   public sealed class ExposureCost
   {
      /// <summary>How far their regard for the player falls.</summary>
      public int RelationHit { get; init; }

      /// <summary>The grudge they keep.</summary>
      public int GrudgeMagnitude { get; init; }
   }

   /// <summary>Decides who may hear a secret, what telling it costs, and how the scandal reads.</summary>
   public static class SecretExposurePolicy
   {
      /// <summary>How far the scandal travels, before the culture's outrage: a whisper of the same affair is 60.</summary>
      public const int ScandalMagnitude = 60;

      /// <summary>Renown each house in the affair loses, before the culture's outrage.</summary>
      public const int RenownLoss = 5;

      /// <summary>Influence each house in the affair loses, before the culture's outrage.</summary>
      public const int InfluenceLoss = 10;

      /// <summary>The grudge of someone whose affair the player told.</summary>
      public const int ExposedGrudge = 50;

      /// <summary>The grudge of someone who had paid for silence.</summary>
      public const int BrokenBargainGrudge = 60;

      /// <summary>True when this person can be told: the two lovers already know it.</summary>
      public static bool MayHear(string listenerId, string subjectId, string loverId)
         => !string.IsNullOrEmpty(listenerId) && listenerId != subjectId && listenerId != loverId;

      /// <summary>What it costs with someone named by the secret; dearer with one who paid for silence.</summary>
      public static ExposureCost Cost(bool paidForSilence)
         => paidForSilence
            ? new ExposureCost {RelationHit = DeedRegardPolicy.SecretBargainBrokenVictim, GrudgeMagnitude = BrokenBargainGrudge}
            : new ExposureCost {RelationHit = DeedRegardPolicy.SecretExposedVictim, GrudgeMagnitude = ExposedGrudge};

      /// <summary>The rumour as characters hear it.</summary>
      public static string Rumour(ExposureNames names)
      {
         if (names == null) throw new ArgumentNullException(nameof(names));

         return names.TellerIsLover
            ? $"Word spreads that {names.Subject} has been lying with {names.Teller} in secret, and that {names.Teller} said so aloud."
            : $"Word spreads that {names.Subject} and {names.Lover} are lovers in secret, and that {names.Teller} was the one to tell it.";
      }

      /// <summary>The player's own chronicle of it.</summary>
      public static string Chronicle(ExposureNames names)
      {
         if (names == null) throw new ArgumentNullException(nameof(names));

         return names.TellerIsLover
            ? $"I told what no one knew: {names.Subject} and I were lovers"
            : $"I told what I knew: {names.Subject} and {names.Lover} were lovers";
      }
   }
}
