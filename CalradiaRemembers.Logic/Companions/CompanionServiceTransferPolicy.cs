// Code written by Gabriel Mailhot, 13/09/2026.
//
// take_into_service: an NPC LORD takes one of the player's own companions into their household.
//
// WHY IT EXISTS. Gabriel briefed his companion Nabb to win over Ira, governor of Onira, and it worked: she
// agreed to take him under her banner and pledged her friendship to his house in exchange. Then nothing
// happened, because the game had no way to do it. The model reached for the nearest verb it could see,
// recruit_notable, which refuses on its first gate (Ira is a lord, not a settlement notable), so the scene's
// whole payoff evaporated and Nabb was still in the player's party. His ruling: "Un seigneur doit pouvoir
// recruter."
//
// THE DIRECTION IS THE POINT, and it is what makes this verb distinct rather than a duplicate:
//   recruit_notable  a notable leaves their POST      -> INTO the player's clan
//   join_clan        a lord forsakes their own house  -> INTO the player's clan
//   join_party       a free wanderer is hired         -> INTO the player's party
//   part_ways        a retainer riding along          -> OUT of the player's party, to their own affairs
//   take_into_service  a companion of the player      -> OUT, into ANOTHER lord's household   <- this one
//
// Every other recruiting verb in this game points inward. This is the only one that gives somebody away, and
// that asymmetry is why the gates below are about the player's household rather than about the lord's regard.
// Whether Ira WANTS him is hers to play in prose; whether the player can be parted from him is law.
//
// Consulted at BOTH surfaces around the LLM, the way every sibling gate is: whether the verb is even taught
// (EncounterContextBuilder) and again whether it really executes (BannerlordGameStateBridge). Pure and
// unit-tested so the two can never drift.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Companions
{
   /// <summary>Why a companion cannot be handed into a lord's service, or <see cref="None" /> when they can.</summary>
   public enum CompanionServiceTransferBlock
   {
      /// <summary>Every gate passes: the companion can take service with this lord.</summary>
      None,

      /// <summary>The lord is dead, a captive, or otherwise in no position to take anyone on.</summary>
      LordUnavailable,

      /// <summary>The lord holds no household of their own to take anyone into.</summary>
      LordHasNoHouse,

      /// <summary>The "lord" is of the player's OWN clan: nobody changes hands, so there is nothing to do.</summary>
      LordIsOfThePlayersOwnClan,

      /// <summary>Nobody was named and the room does not settle it, so there is no one to hand over.</summary>
      NoCompanionNamed,

      /// <summary>The named hero is not of the player's household at all.</summary>
      NotYourCompanion,

      /// <summary>
      ///   The hero is the player's own BLOOD - a child, a parent, a sibling. Placing your own kin in another
      ///   lord's house is fosterage or hostage-taking, a different act with different consequences, not the
      ///   ordinary placing of a retainer.
      /// </summary>
      CompanionIsYourOwnBlood,

      /// <summary>The hero LEADS the player's clan (or is the player), so they are nobody's to give.</summary>
      CompanionLeadsYourHouse,

      /// <summary>The companion is dead or held prisoner.</summary>
      CompanionUnavailable,

      /// <summary>
      ///   The companion is the PLAYER'S OWN SPOUSE. A marriage is not a term of service and is not ended by
      ///   handing somebody to another house; the divorce path exists for that.
      /// </summary>
      CompanionIsYourSpouse,

      /// <summary>The companion is away on an errand and must be recalled before they can be given over.</summary>
      CompanionAwayOnErrand,

      /// <summary>
      ///   The companion governs one of the player's fiefs. Letting them ride off would leave the fief
      ///   ungoverned as a side effect nobody in the conversation agreed to.
      /// </summary>
      CompanionGovernsAFief
   }

   /// <summary>
   ///   Everything the gate needs, as plain facts the caller reads off the live game. A RECORD rather than the
   ///   plain fact class its siblings use, so a test can state one deviation from a passing set (<c>Fine() with
   ///   {CompanionIsPlayerSpouse = true}</c>) instead of restating every field around it - which is what makes
   ///   a refusal test read as being about the one thing it is about.
   /// </summary>
   public sealed record CompanionServiceTransferFacts
   {
      public bool LordIsAlive { get; init; }
      public bool LordIsPrisoner { get; init; }
      public bool LordIsThePlayer { get; init; }

      /// <summary>The lord belongs to a clan of their own (the household the companion would join).</summary>
      public bool LordHasClan { get; init; }

      /// <summary>That clan IS the player's clan, in which case nobody would actually change hands.</summary>
      public bool LordClanIsPlayerClan { get; init; }

      /// <summary>False when no companion could be resolved from what was said.</summary>
      public bool CompanionResolved { get; init; }

      public bool CompanionIsAlive { get; init; }
      public bool CompanionIsPrisoner { get; init; }
      /// <summary>
      ///   In the player's HOUSEHOLD, which is deliberately wider than Bannerlord's IsPlayerCompanion.
      ///   <para>
      ///     Gabriel's play-test of 13/09/2026 is why. He presented Sley the Grim to Ira as "my Master of
      ///     Arms" - a nemesis he had spared and sworn into his clan. But turn_nemesis (and recruit_prisoner)
      ///     join a hero by assigning Hero.Clan directly, which never sets CompanionOf, so IsPlayerCompanion
      ///     stayed false and Clan.Companions never listed him. The verb was therefore never taught, and Ira
      ///     could not have accepted him however the scene had gone.
      ///   </para>
      ///   <para>
      ///     The question this verb asks is "is this person in your service", not "did they arrive through
      ///     the companion door". So the caller reads Clan.Heroes and lets the two gates below exclude the
      ///     ones who are not anybody's to give.
      ///   </para>
      /// </summary>
      public bool CompanionIsInYourHousehold { get; init; }

      /// <summary>The hero leads the player's clan, or IS the player.</summary>
      public bool CompanionLeadsThePlayersClan { get; init; }

      /// <summary>The hero is the player's child, parent, or sibling.</summary>
      public bool CompanionIsPlayerBlood { get; init; }
      public bool CompanionIsPlayerSpouse { get; init; }
      public bool CompanionAwayOnErrand { get; init; }
      public bool CompanionGovernsPlayerFief { get; init; }
   }

   /// <summary>
   ///   The pure gate behind take_into_service. See the file header for the design and Gabriel's ruling.
   /// </summary>
   public static class CompanionServiceTransferPolicy
   {
      // NO STARTING-RELATION CONSTANT. There was one, and cr.testall on a live campaign showed the engine
      // ignoring the change (two heroes who are by then in the same clan). Rather than move the call earlier
      // to make a number stick, it was dropped: this project judges relationships on
      // NpcProfile.ReputationWithPlayer, and a vanilla relation between a lord and their own clan companion
      // is read by nothing here. See the executor for the fuller reasoning.

      /// <summary>
      ///   Ordered so the FIRST thing a reader is told is the most fundamental reason. The lord's own
      ///   fitness comes before the companion's, because a dead or captive lord makes every later question moot.
      /// </summary>
      public static CompanionServiceTransferBlock Evaluate(CompanionServiceTransferFacts f)
      {
         if (f == null) return CompanionServiceTransferBlock.LordUnavailable;

         if (!f.LordIsAlive || f.LordIsPrisoner || f.LordIsThePlayer)
            return CompanionServiceTransferBlock.LordUnavailable;

         if (!f.LordHasClan) return CompanionServiceTransferBlock.LordHasNoHouse;
         if (f.LordClanIsPlayerClan) return CompanionServiceTransferBlock.LordIsOfThePlayersOwnClan;

         if (!f.CompanionResolved) return CompanionServiceTransferBlock.NoCompanionNamed;
         if (!f.CompanionIsAlive || f.CompanionIsPrisoner) return CompanionServiceTransferBlock.CompanionUnavailable;
         if (!f.CompanionIsInYourHousehold) return CompanionServiceTransferBlock.NotYourCompanion;

         // The three that can never be resolved by waiting, checked AFTER "is one of yours" so somebody
         // outside the household reads as the plainer refusal, and BEFORE the errand/fief gates which can.
         if (f.CompanionLeadsThePlayersClan) return CompanionServiceTransferBlock.CompanionLeadsYourHouse;
         if (f.CompanionIsPlayerSpouse) return CompanionServiceTransferBlock.CompanionIsYourSpouse;
         if (f.CompanionIsPlayerBlood) return CompanionServiceTransferBlock.CompanionIsYourOwnBlood;

         if (f.CompanionAwayOnErrand) return CompanionServiceTransferBlock.CompanionAwayOnErrand;
         if (f.CompanionGovernsPlayerFief) return CompanionServiceTransferBlock.CompanionGovernsAFief;

         return CompanionServiceTransferBlock.None;
      }

      /// <summary>
      ///   Which companion the lord meant. <paramref name="spokenTarget" /> is what the model named, which is
      ///   very often nothing at all - a lord taking the man standing in front of her does not announce his
      ///   name to the room.
      ///   <para>
      ///     So a BLANK target resolves to the single candidate when there is exactly one, and to nobody when
      ///     there are several: guessing which of three companions was given away is not a guess this code is
      ///     entitled to make, and the refusal it produces says so.
      ///   </para>
      ///   <para>
      ///     A named target matches on the whole name or on its FIRST word, because a model writes "Nabb" for
      ///     "Nabb the Bloody Handed". An ambiguous first word (two companions called Nabb) resolves to
      ///     nobody, for the same reason a crowd does.
      ///   </para>
      /// </summary>
      public static string PickCompanion(IReadOnlyList<CompanionCandidate> candidates, string spokenTarget)
      {
         if (candidates == null || candidates.Count == 0) return null;

         if (string.IsNullOrWhiteSpace(spokenTarget))
            return candidates.Count == 1 ? candidates[0].HeroId : null;

         string spoken = spokenTarget.Trim();

         string exact = null;
         var exactCount = 0;
         string byFirstWord = null;
         var firstWordCount = 0;

         foreach (CompanionCandidate c in candidates)
         {
            if (c == null || string.IsNullOrWhiteSpace(c.Name)) continue;

            if (string.Equals(c.Name.Trim(), spoken, StringComparison.OrdinalIgnoreCase))
            {
               exact = c.HeroId;
               exactCount++;

               continue;
            }

            if (string.Equals(FirstWord(c.Name), FirstWord(spoken), StringComparison.OrdinalIgnoreCase))
            {
               byFirstWord = c.HeroId;
               firstWordCount++;
            }
         }

         // A full-name match beats a first-word one, and either has to be unique to count.
         if (exactCount == 1) return exact;
         if (exactCount == 0 && firstWordCount == 1) return byFirstWord;

         return null;
      }

      private static string FirstWord(string s)
      {
         string t = s.Trim();
         int space = t.IndexOf(' ');

         return space > 0 ? t.Substring(0, space) : t;
      }
   }

   /// <summary>One companion the player could part with, as the gate sees them.</summary>
   public sealed class CompanionCandidate
   {
      public string HeroId { get; init; }
      public string Name { get; init; }
   }
}
