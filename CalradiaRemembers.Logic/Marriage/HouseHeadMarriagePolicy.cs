// Code written by Gabriel Mailhot, 26/10/2026.
// The game weds no two heads of houses (DefaultMarriageModel.IsCoupleSuitableForMarriage refuses a couple who each
// lead their clan), and MarriageAction.Apply then does nothing, silently. The player always heads their own clan, so
// a lady who heads hers can never be wed to them. RaveN017Mod (Nexus, 26/10/2026): "I want my hero to marry Rhagaea,
// and our relationship has progressed quite a bit, but it feels like it's just not triggering." The marry action was
// taught, Rhagaea agreed, the mod announced the wedding, and nothing was sealed. Gabriel the same day: "Jamais elle
// laissera son trone." So the rule is named before the model speaks, and she says what she can offer instead.

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>Who heads a house, on each side of a match the player would make.</summary>
   public sealed class HouseHeadFacts
   {
      /// <summary>The intended heads their own clan.</summary>
      public bool CandidateHeadsTheirHouse { get; init; }

      /// <summary>The player heads their own clan (always so in the game today, asked rather than assumed).</summary>
      public bool PlayerHeadsTheirHouse { get; init; }
   }

   /// <summary>The game's own bar on wedding two heads of houses, asked before the model is told a marriage is on offer.</summary>
   public static class HouseHeadMarriagePolicy
   {
      /// <summary>True when both head their house: one would have to give it up, and the game seals no such match.</summary>
      public static bool BarsTheMatch(HouseHeadFacts f)
         => f != null && f.CandidateHeadsTheirHouse && f.PlayerHeadsTheirHouse;
   }
}
