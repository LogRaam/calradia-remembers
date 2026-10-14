// Code written by Gabriel Mailhot, 12/10/2026.
// The Troy-inspired tale (docs/design/tale-scenario-bridge; Gabriel, 12/10/2026): the consequences "elope" (a married lady
// leaves her husband and rides with the player as a guest who may later be married: "Invitee et epousable") and
// "return_spouse" (she goes back to him: "Le retour de la Reine a son mari est la condition de paix"). The marriage is
// ended and restored through the game's own marriage, so these say when that can really happen, before anything moves:
// a tale must never tell the realm she left while the game kept her married.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What decides whether a married lady can leave her husband with the player.</summary>
   public sealed class ElopementFacts
   {
      /// <summary>She is married to the husband the tale names (and he to her).</summary>
      public bool SheIsMarriedToHim { get; init; }

      /// <summary>Both alive and in the world.</summary>
      public bool BothInTheWorld { get; init; }

      /// <summary>Either is held captive: nobody rides away from a cell.</summary>
      public bool EitherIsPrisoner { get; init; }

      /// <summary>She is with child (the game's own divorce waits on it).</summary>
      public bool SheIsPregnant { get; init; }

      /// <summary>She leads her own house: it cannot be left without its head.</summary>
      public bool SheLeadsHerHouse { get; init; }

      /// <summary>She leads a party of her own (a post released before she goes).</summary>
      public bool SheLeadsAParty { get; init; }

      /// <summary>She governs a town or castle (a post released before she goes).</summary>
      public bool SheGoverns { get; init; }
   }

   /// <summary>What decides whether she can be returned to her husband.</summary>
   public sealed class ReturnFacts
   {
      /// <summary>Both alive and in the world.</summary>
      public bool BothInTheWorld { get; init; }

      /// <summary>She is unwed (marrying the player closes the way back).</summary>
      public bool SheIsSingle { get; init; }

      /// <summary>He is unwed (wed again, he has no place for her).</summary>
      public bool HeIsSingle { get; init; }

      /// <summary>Either is held captive.</summary>
      public bool EitherIsPrisoner { get; init; }

      /// <summary>His party is in an army or a battle: the game weds nobody then (a wait, not an end).</summary>
      public bool HeIsOnCampaign { get; init; }

      /// <summary>The game's own marriage rule would wed them (MarriageModel.IsCoupleSuitableForMarriage).</summary>
      public bool TheGameWouldWedThem { get; init; } = true;
   }

   /// <summary>Whether she goes back now, waits, or not at all.</summary>
   public sealed class ReturnDecision
   {
      /// <summary>Why not now, or null when she goes back now.</summary>
      public string Why { get; init; }

      /// <summary>The reason passes (a campaign, a captivity): the return is tried again later.</summary>
      public bool WaitAndRetry { get; init; }
   }

   /// <summary>When a tale's elopement and return can really happen. Pure.</summary>
   public static class TaleElopementPolicy
   {
      /// <summary>Null when she can leave with the player; otherwise why not, for the log and cr.story_check.</summary>
      public static string WhyNotElope(ElopementFacts f)
      {
         f ??= new ElopementFacts();
         if (!f.SheIsMarriedToHim) return "she is not married to him";
         if (!f.BothInTheWorld) return "one of them is no longer in the world";
         if (f.EitherIsPrisoner) return "one of them is held captive";
         if (f.SheIsPregnant) return "she is with child, and the game will not end a marriage then";
         if (f.SheLeadsHerHouse) return "she leads her house and cannot leave it";

         return null;
      }

      /// <summary>A party she leads or a town she governs is released before she goes, as for a Stolen Love divorce.</summary>
      public static bool MustReleasePost(ElopementFacts f) => f != null && (f.SheLeadsAParty || f.SheGoverns);

      /// <summary>Null when she can be returned to her husband; otherwise why not.</summary>
      public static string WhyNotReturn(ReturnFacts f) => DecideReturn(f).Why;

      /// <summary>
      ///   Now, wait, or never. What closes the way back for good ends it; a captivity or a husband away on campaign is a
      ///   wait, tried again later (Gabriel's cr.testall, 13/10/2026: the game weds nobody in an army or a battle, and had
      ///   refused in silence); the game's own marriage rule refusing them otherwise ends it, said plainly.
      /// </summary>
      public static ReturnDecision DecideReturn(ReturnFacts f)
      {
         f ??= new ReturnFacts();
         if (!f.BothInTheWorld) return new ReturnDecision {Why = "one of them is no longer in the world"};
         if (!f.SheIsSingle) return new ReturnDecision {Why = "she is married now, so the way back is closed"};
         if (!f.HeIsSingle) return new ReturnDecision {Why = "he has wed again"};
         if (f.EitherIsPrisoner) return new ReturnDecision {Why = "one of them is held captive", WaitAndRetry = true};
         if (f.HeIsOnCampaign) return new ReturnDecision {Why = "he is away on campaign (an army or a battle)", WaitAndRetry = true};
         if (!f.TheGameWouldWedThem) return new ReturnDecision {Why = "the game will not wed them again"};

         return new ReturnDecision();
      }
   }
}
