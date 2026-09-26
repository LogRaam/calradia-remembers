// Code written by Gabriel Mailhot, 26/09/2026.
// What a lost courier becomes (raphareish, Nexus, 24/09/2026: an intercepted letter just died, and its sender, asked
// about it, said he had "been busy"). Gabriel's rulings, 26/09/2026:
//
//   - A lost letter is SENT AGAIN until it arrives, whatever took the first courier: a lost letter must never break the
//     dialogue it was part of.
//   - An INTERCEPTED letter (bandits, or a troop at war with the courier's own clan) that says something worth hiding
//     ALWAYS becomes a secret. Not an instant leak to a chosen antagonist, as the old roll did, but a thing IN THE
//     WORLD: the band that took it carries it, a band that goes home brings it to its hideout, and whoever defeats the
//     carrier sends it up to the lord who owns that troop (a Pravend patrol, Pravend's owner).
//   - A secret matters for a while, not forever.
//
// This file holds those decisions on flattened facts. Reading the destroyer, carrying the secret, and what the lord
// who learns it then does are engine glue (CourierService and the secret store).

namespace CalradiaRemembers.Logic
{
   /// <summary>What happened to a courier that never arrived.</summary>
   public enum CourierLoss
   {
      /// <summary>Gone (starvation, a battle it wandered into, a despawn), and read by nobody.</summary>
      Lost,

      /// <summary>Taken by bandits, who now hold the letter.</summary>
      InterceptedByBandits,

      /// <summary>Taken by a troop at war with the courier's own clan, whose lord now holds the letter.</summary>
      InterceptedByEnemy
   }

   /// <summary>What is known about whoever destroyed a courier.</summary>
   public sealed class CourierLossFacts
   {
      /// <summary>The destroyer was a bandit band.</summary>
      public bool DestroyerIsBandit { get; set; }

      /// <summary>The destroyer's realm is at war with the clan that sent the courier.</summary>
      public bool DestroyerAtWarWithCourierClan { get; set; }
   }

   /// <summary>Who answers for the troop that defeated a secret's carrier.</summary>
   public sealed class SecretDefeaterFacts
   {
      /// <summary>The player's own party, or a party of the player's clan.</summary>
      public bool IsPlayerSide { get; set; }

      /// <summary>Another bandit band.</summary>
      public bool IsBandit { get; set; }

      /// <summary>The hero at the troop's head, when a hero leads it (a lord's party).</summary>
      public string LeaderHeroId { get; set; }

      /// <summary>The hero who owns the troop, when no hero leads it (a caravan's owner).</summary>
      public string OwnerHeroId { get; set; }

      /// <summary>The owner of the troop's home settlement (a patrol, a militia, a garrison, villagers).</summary>
      public string SettlementOwnerHeroId { get; set; }

      /// <summary>Whom the letter was written to.</summary>
      public string IntendedRecipientId { get; set; }

      /// <summary>Who wrote it.</summary>
      public string SenderId { get; set; }
   }

   /// <summary>How a carried secret changes hands.</summary>
   public enum SecretHandoffKind
   {
      /// <summary>No one takes it up; the secret is lost with its carrier.</summary>
      Nobody,

      /// <summary>A lord now knows what the letter said.</summary>
      Lord,

      /// <summary>Another band took the loot, letter and all, and carries it on.</summary>
      CarriedOn,

      /// <summary>The player's side beat the carrier and took the letter back, unread by anyone.</summary>
      RecoveredByPlayer,

      /// <summary>The one it was written to ended up with it: a late delivery, not a leak.</summary>
      ReachedItsRecipient
   }

   /// <summary>Where a secret went, and to whom.</summary>
   public sealed class SecretHandoff
   {
      public SecretHandoffKind Kind { get; set; }

      /// <summary>The hero who now holds it, for <see cref="SecretHandoffKind.Lord" /> and <see cref="SecretHandoffKind.ReachedItsRecipient" />.</summary>
      public string HeroId { get; set; }
   }

   /// <summary>The decisions a lost courier and its secret turn on. Pure.</summary>
   public static class CourierSecretPolicy
   {
      /// <summary>TUNE: days a captured letter still carries weight; past it, nobody cares what it said.</summary>
      public const int SecretShelfLifeDays = 60;

      /// <summary>What took the courier. Bandits first: a band is never also "at war" with anyone.</summary>
      public static CourierLoss Classify(CourierLossFacts facts)
      {
         if (facts == null) return CourierLoss.Lost;
         if (facts.DestroyerIsBandit) return CourierLoss.InterceptedByBandits;

         return facts.DestroyerAtWarWithCourierClan ? CourierLoss.InterceptedByEnemy : CourierLoss.Lost;
      }

      /// <summary>Gabriel: a lost letter is sent again until it arrives, whatever the loss.</summary>
      public static bool IsResent(CourierLoss loss) => true;

      /// <summary>
      ///   True when the lost letter becomes a secret in the world: it was intercepted (someone now holds it) and it
      ///   says something worth hiding (the same substance bar the old leak used).
      /// </summary>
      public static bool BecomesSecret(CourierLoss loss, string content)
         => loss != CourierLoss.Lost && LetterLeakPolicy.HasSubstance(content);

      /// <summary>
      ///   Who takes up a secret when its carrier is defeated. The player's side recovers it; another band carries it
      ///   on; otherwise it goes up to whoever answers for the troop: the hero leading it, else its owner, else the
      ///   owner of its home settlement. Reaching the one it was written to is a delivery; reaching its own sender is
      ///   nothing at all.
      /// </summary>
      public static SecretHandoff WhoGetsTheSecret(SecretDefeaterFacts facts)
      {
         if (facts == null) return new SecretHandoff {Kind = SecretHandoffKind.Nobody};
         if (facts.IsPlayerSide) return new SecretHandoff {Kind = SecretHandoffKind.RecoveredByPlayer};
         if (facts.IsBandit) return new SecretHandoff {Kind = SecretHandoffKind.CarriedOn};

         string hero = FirstNonBlank(facts.LeaderHeroId, facts.OwnerHeroId, facts.SettlementOwnerHeroId);
         if (hero == null || hero == facts.SenderId) return new SecretHandoff {Kind = SecretHandoffKind.Nobody};
         if (hero == facts.IntendedRecipientId) return new SecretHandoff {Kind = SecretHandoffKind.ReachedItsRecipient, HeroId = hero};

         return new SecretHandoff {Kind = SecretHandoffKind.Lord, HeroId = hero};
      }

      /// <summary>True once a secret captured on <paramref name="capturedDay" /> is past its shelf life.</summary>
      public static bool IsStale(int capturedDay, int today) => today - capturedDay >= SecretShelfLifeDays;

      private static string FirstNonBlank(params string[] ids)
      {
         foreach (string id in ids)
            if (!string.IsNullOrWhiteSpace(id))
               return id;

         return null;
      }
   }
}
