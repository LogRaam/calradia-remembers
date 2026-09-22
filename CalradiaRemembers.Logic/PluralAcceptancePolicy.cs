// Code written by Gabriel Mailhot, 22/09/2026.
// Whether a partner may say, in her own voice, that she accepts the player's other loves.
//
// yozakura12 (Nexus, 22/09/2026) needed a shape the mod did not have: "The player may have multiple
// wives/consorts, but each wife remains exclusive to the player." The only thing close was open_relationship,
// which is reciprocal and suppresses her jealousy outright, and which the interpreter was reaching for whenever
// a conversation turned to accepting other wives. Those are two different arrangements and only one of them is
// his.
//
// THE PRICE, NOT A VETO (Gabriel, 22/09/2026). A woman of an exclusive people may accept, and is somewhat more
// tolerant for having done so, but she is not a Khuzait: her acceptance softens the betrayal price rather than
// moving her to the competition track (JealousyTrackPolicy). What THIS decides is the other half of his ruling,
// the part that stops one sentence from dissolving a culture: whether she can be asked at all.
//
// A CULTURE THAT ALREADY SHARES asks nothing extra, because there is nothing to overcome. A culture that calls
// it betrayal asks for devotion, because that is what it costs: a woman agrees to bear what her own people
// condemn for someone she is devoted to, not for an acquaintance who raised the subject over dinner. The rung
// is Regard.DevotedTier, which already has a name and a meaning in this mod.

#region

using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What is known about a partner when deciding whether she may be asked to accept a plural household.</summary>
   public sealed class PluralAcceptanceFacts
   {
      /// <summary>She is the player's spouse, consort, or committed bond. A flirtation has nothing to accept with.</summary>
      public bool IsCommittedPartner { get; set; }

      /// <summary>She has already said it. Saying it twice is not a thing to teach, and the bridge refuses it.</summary>
      public bool AlreadyAccepted { get; set; }

      /// <summary>She and the player already hold RECIPROCAL open terms, which is a different and wider arrangement.</summary>
      public bool AlreadyOnOpenTerms { get; set; }

      /// <summary>Her own people share partners at the player's standing, so there is nothing for her to overcome.</summary>
      public bool CultureToleratesSharing { get; set; }

      /// <summary>Her personal regard for the player, this mod's own ledger, never the engine's clan relation.</summary>
      public int RegardWithPlayer { get; set; }
   }

   /// <summary>Decides whether the acceptance of a plural household can honestly be offered to this partner.</summary>
   public static class PluralAcceptancePolicy
   {
      /// <summary>
      ///   What a partner of an EXCLUSIVE people must feel before she can be asked to bear a plural household:
      ///   devotion. Below it the verb is never taught and the bridge refuses it, so the model cannot obtain in
      ///   one sentence what her culture spends a lifetime refusing.
      /// </summary>
      public static int DevotionRequiredAgainstCulture => Regard.DevotedTier;

      /// <summary>
      ///   True when she may be asked. The gating is named here so the prompt can say the closed door OUT LOUD
      ///   rather than be refuted after the fact, which is the standing rule for every verb in this mod: a
      ///   character declares knowing the gating, never discovers it when the bridge says no.
      /// </summary>
      public static bool CanBeOffered(PluralAcceptanceFacts facts)
      {
         if (facts == null) return false;
         if (!facts.IsCommittedPartner) return false;
         if (facts.AlreadyAccepted || facts.AlreadyOnOpenTerms) return false;

         return facts.CultureToleratesSharing || facts.RegardWithPlayer >= DevotionRequiredAgainstCulture;
      }

      /// <summary>
      ///   Why she cannot be asked, for the refusal the bridge returns and for the prompt line that names the
      ///   closed door. Empty when she can.
      /// </summary>
      public static string WhyNot(PluralAcceptanceFacts facts)
      {
         if (facts == null) return "there is nobody here to ask";
         if (!facts.IsCommittedPartner) return "she is not a committed partner of yours, so there is no household to accept";
         if (facts.AlreadyOnOpenTerms) return "the two of you already hold open terms, which is wider than this";
         if (facts.AlreadyAccepted) return "she has already said it";

         return CanBeOffered(facts)
            ? ""
            : "her people hold marriage to one alone, and she is not yet devoted enough to bear otherwise";
      }
   }
}
