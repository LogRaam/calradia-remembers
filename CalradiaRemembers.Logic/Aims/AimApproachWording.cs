// Code written by Gabriel Mailhot, 14/10/2026.
// HOW a lord goes after his aim, from his traits (tashmetu, 14/10/2026: "an honest lord asks openly, a calculating or
// dishonest one works around the player or uses them"). Bound by two of Gabriel's standing rulings: NPCs never lie
// (20/09/2026: a designed lie reads as the model failing; the range is vagueness, ignorance, refusal, silence), so a
// devious lord CONCEALS his purpose but never states a falsehood; and gating before declaration (09/09/2026), so a lord
// asks outright for a service only when it is a real deed the game will verify.

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>The traits and stakes the approach sentence reads.</summary>
   public sealed class AimApproachFacts
   {
      /// <summary>The lord's Honor trait (-2..2).</summary>
      public int Honor { get; init; }

      /// <summary>The lord's Calculating trait (-2..2).</summary>
      public int Calculating { get; init; }

      /// <summary>True when the aim carries a MissionBoard deed, so a service asked for is real and verified.</summary>
      public bool HasVerifiedService { get; init; }
   }

   /// <summary>How a lord pursues his aim with the player, in one sentence addressed to him.</summary>
   public enum AimApproach
   {
      /// <summary>He asks openly.</summary>
      Open,

      /// <summary>He works around the player, keeps his purpose to himself, and never lies.</summary>
      Guarded,

      /// <summary>Neither marked honest nor devious: in his own way.</summary>
      OwnWay
   }

   /// <summary>Pure: picks the approach and words it.</summary>
   public static class AimApproachWording
   {
      /// <summary>Picks the approach. Dishonour or a calculating bent outweighs honour: an honourable schemer still schemes.</summary>
      public static AimApproach Choose(AimApproachFacts facts)
      {
         if (facts == null) return AimApproach.OwnWay;
         if (facts.Honor < 0 || facts.Calculating > 0) return AimApproach.Guarded;
         if (facts.Honor > 0) return AimApproach.Open;

         return AimApproach.OwnWay;
      }

      /// <summary>The sentence telling the lord how he goes about it.</summary>
      public static string Describe(AimApproachFacts facts)
      {
         string how;
         switch (Choose(facts))
         {
            case AimApproach.Open:
               how = "You go about it openly: you say what you want and ask plainly.";
               break;
            case AimApproach.Guarded:
               how = "You go about it with care: you may sound them out, keep your purpose to yourself, or steer them toward it without saying why. You never tell them a falsehood; you only choose what to leave unsaid.";
               break;
            default:
               how = "You go about it in your own way.";
               break;
         }

         string ask = facts != null && facts.HasVerifiedService
            ? " You may ask them outright for the service."
            : " You may sound them out or offer them something, but promise nothing the world has not already made true.";

         return how + ask;
      }
   }
}
