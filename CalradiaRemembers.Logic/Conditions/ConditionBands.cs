// Code written by Gabriel Mailhot, 14/11/2026.
// The bands a steward and a quartermaster speak in: a seat's loyalty, security and food, a company's morale,
// provisions and wounded. They were decided inside EncounterContextBuilder (the engine glue), where no test held them,
// and the 50 that splits a restless town from a content one was written a second time in CompanionMissionBehavior.
// Gabriel, 14/11/2026, on the glue-decisions map: "on pourrait aussi traiter les candidats pour ameliorer le code".
// Moved here unchanged, every mark and every comparison as it was (the prompts read these bands through the SDK's
// SeatStandingFacts and PartyStandingFacts); each mark's reason is the one its reader carried.

#region

using NpcMemoryService.Core.Knowledge;

#endregion

namespace CalradiaRemembers.Logic.Conditions
{
   /// <summary>How a seat and a company are faring, on the engine's own scales. Pure.</summary>
   public static class ConditionBands
   {
      /// <summary>Below this loyalty a town is rebellious: where vanilla itself starts worrying.</summary>
      public const float RebelliousBelow = 25f;

      /// <summary>Below this loyalty a town is restless; at or above it, content (the settled half of the scale).</summary>
      public const float RestlessBelow = 50f;

      /// <summary>Below this loyalty a town is content; at or above it, devoted.</summary>
      public const float ContentBelow = 75f;

      /// <summary>Below this security a seat is lawless: low security IS banditry on the roads.</summary>
      public const float LawlessBelow = 25f;

      /// <summary>Below this security a seat is uneasy; at or above it, settled.</summary>
      public const float UneasyBelow = 60f;

      /// <summary>A daily food change beyond this, either way, is a trend; within it, steady.</summary>
      public const float FoodTrendMargin = 0.5f;

      /// <summary>Morale below this is mutinous (the engine's 0-100 morale, where 50 is the settled middle it drifts back to).</summary>
      public const float MutinousBelow = 25f;

      /// <summary>Morale below this is sullen.</summary>
      public const float SullenBelow = 45f;

      /// <summary>Morale above this is high-hearted.</summary>
      public const float HighHeartedAbove = 70f;

      /// <summary>A town's loyalty, on the engine's 0-100 scale.</summary>
      public static LoyaltyBand Loyalty(float loyalty)
      {
         if (loyalty < RebelliousBelow) return LoyaltyBand.Rebellious;
         if (loyalty < RestlessBelow) return LoyaltyBand.Restless;
         if (loyalty < ContentBelow) return LoyaltyBand.Content;

         return LoyaltyBand.Devoted;
      }

      /// <summary>Whether a town is settled rather than restless: content or devoted.</summary>
      public static bool IsSettled(LoyaltyBand band) => band == LoyaltyBand.Content || band == LoyaltyBand.Devoted;

      /// <summary>A seat's security, on the engine's 0-100 scale.</summary>
      public static SecurityBand Security(float security)
      {
         if (security < LawlessBelow) return SecurityBand.Lawless;
         if (security < UneasyBelow) return SecurityBand.Uneasy;

         return SecurityBand.Settled;
      }

      /// <summary>Which way a seat's food is going: the half of a steward's report that decides what to do.</summary>
      public static FoodTrend FoodDirection(float dailyChange)
      {
         if (dailyChange < -FoodTrendMargin) return FoodTrend.Falling;
         if (dailyChange > FoodTrendMargin) return FoodTrend.Rising;

         return FoodTrend.Steady;
      }

      /// <summary>The granary as a share of the engine's own ceiling (0 to 1).</summary>
      public static FoodBand FoodStores(float shareOfCeiling)
      {
         if (shareOfCeiling < 0.10f) return FoodBand.Empty;
         if (shareOfCeiling < 0.35f) return FoodBand.Thin;
         if (shareOfCeiling < 0.75f) return FoodBand.Adequate;

         return FoodBand.Full;
      }

      /// <summary>A company's morale, on the engine's 0-100 scale.</summary>
      public static CampMood Mood(float morale)
      {
         if (morale < MutinousBelow) return CampMood.Mutinous;
         if (morale < SullenBelow) return CampMood.Sullen;
         if (morale > HighHeartedAbove) return CampMood.HighHearted;

         return CampMood.Steady;
      }

      /// <summary>The engine's own count of days of food, banded: a quartermaster says "a week or so", not "eleven".</summary>
      public static ProvisionBand Provisions(int daysOfFood)
      {
         if (daysOfFood <= 0) return ProvisionBand.Starving;
         if (daysOfFood < 7) return ProvisionBand.Short;
         if (daysOfFood < 21) return ProvisionBand.Sufficient;

         return ProvisionBand.Plentiful;
      }

      /// <summary>The share of the company that is wounded (0 to 1).</summary>
      public static WoundedBand Wounded(float hurtShare)
      {
         if (hurtShare > 0.50f) return WoundedBand.Most;
         if (hurtShare > 0.20f) return WoundedBand.Many;

         return WoundedBand.Few;
      }
   }
}
