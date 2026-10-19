// Code written by Gabriel Mailhot, 14/10/2026.
// What a captive of the player may offer for her freedom, and how readily she offers it.
//
// Maifailun (Nexus, 14/10/2026): Adrichea (Generosity, Honor and Mercy all -1), a clan leader held by the player, was
// asked to swear fealty to Rhagaea or spend a night with him, and answered "kill me if you must"; nothing in CR could
// hold a counter-offer anyway. Gabriel (14/10/2026): the captive proposes terms herself; in Mature and Hardcore intimacy
// is a bargaining term ("dans ces modes, les joueurs sont des adultes qui veulent probablement explorer du RP
// sexualise"), and "un NPC manipulateur et sans honneur n'a pas de scrupule a aller dans la sexualite... surtout si ca
// lui donne un avantage". The terms are only those the game can carry out for HER, decided before she speaks
// (gating before declaration; NPCs never lie). Her readiness uses the nemesis turn's own trait and fear thresholds.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Appreciations;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>A term a captive may put on the table for her freedom.</summary>
   public enum CaptiveTerm
   {
      /// <summary>Coin, paid from her own or her house's means.</summary>
      Ransom,

      /// <summary>She brings her house into the realm the player serves.</summary>
      Fealty,

      /// <summary>She takes service in the player's clan (the recruit_prisoner gate).</summary>
      Service,

      /// <summary>Her body, in Mature content and above, between adults.</summary>
      Intimacy,

      /// <summary>
      ///   Her bare word alone, costing her nothing: always open, never gated, never added to
      ///   <see cref="CaptiveOffer.Terms" /> by <see cref="CaptiveOfferPolicy.Decide" /> (it is the unconditional
      ///   line every captive is told she may offer regardless of means or disposition). Named here only so an
      ///   emitted counter_offer naming it parses to something, rather than an unrecognised word.
      /// </summary>
      Goodwill
   }

   /// <summary>How readily she bargains.</summary>
   public enum CaptiveDisposition
   {
      /// <summary>Honor-bound and unafraid: she may refuse to bargain and wait for ransom, and never sells herself.</summary>
      StandsOnPride,

      /// <summary>She bargains when it comes to it.</summary>
      Bargains,

      /// <summary>No honor to die for: she makes the first offer, and uses whatever card is strongest.</summary>
      OffersFirst
   }

   /// <summary>What decides a captive's offer.</summary>
   public sealed class CaptiveOfferFacts
   {
      /// <summary>Her traits (vanilla -2..+2): Honor and Calculating decide her readiness.</summary>
      public TraitFacts Traits { get; init; } = new();

      /// <summary>Her fear of the player (CR stance, 0..100).</summary>
      public int Fear { get; init; }

      /// <summary>The coin she or her house can actually pay, in denars.</summary>
      public int RansomMeans { get; init; }

      /// <summary>She leads her clan (only a clan's head can swear it to a realm).</summary>
      public bool LeadsHerClan { get; init; }

      /// <summary>The player serves a realm (there is a realm to swear to).</summary>
      public bool PlayerServesARealm { get; init; }

      /// <summary>Her clan already belongs to that realm (nothing to swear).</summary>
      public bool HerClanIsInThatRealm { get; init; }

      /// <summary>
      ///   Her clan RULES the kingdom it belongs to (she is its sovereign). Found reading the decompiled engine
      ///   (ChangeKingdomAction.ApplyInternal / Clan.ClanLeaveKingdom, 14/10/2026) while building the executor that
      ///   would actually carry fealty out: leaving a kingdom only clears Kingdom = null on the clan and never
      ///   checks whether it was that kingdom's RulingClan, so swearing a captured QUEEN's house to another realm
      ///   would leave her former kingdom's RulingClan pointing at a clan no longer in it, an orphaned kingdom the
      ///   game has no path to repair. Fealty is a term only the game can truthfully carry out for her (the whole
      ///   point of this policy), so a ruling clan's head never has fealty as a term at all, gated BEFORE she ever
      ///   speaks rather than refused after (gating before declaration).
      /// </summary>
      public bool RulesHerRealm { get; init; }

      /// <summary>The recruit_prisoner gate is open for her (EncounterContext.CanRecruitPrisoner).</summary>
      public bool ServiceIsOpen { get; init; }

      /// <summary>The player's content level (MCM).</summary>
      public AdultContentLevel ContentLevel { get; init; }

      /// <summary>She and the player are both adults.</summary>
      public bool BothOfAge { get; init; }

      /// <summary>Her orientation lets her want the player at all.</summary>
      public bool OrientationAllowsThePlayer { get; init; }
   }

   /// <summary>What she may offer, and how readily.</summary>
   public sealed class CaptiveOffer
   {
      /// <summary>The terms open to her.</summary>
      public IReadOnlyList<CaptiveTerm> Terms { get; init; } = new List<CaptiveTerm>();

      /// <summary>The most she can pay as ransom; 0 when ransom is no term.</summary>
      public int RansomCeiling { get; init; }

      /// <summary>How readily she bargains.</summary>
      public CaptiveDisposition Disposition { get; init; }
   }

   /// <summary>Decides a captive's offer. Pure.</summary>
   public static class CaptiveOfferPolicy
   {
      /// <summary>TUNE: below this, her coin is not a ransom worth naming.</summary>
      public const int RansomFloor = 100;

      /// <summary>
      ///   Principled (Honor at the nemesis turn's defiance threshold, and no calculator's override) and unafraid: she
      ///   stands on pride. Honorless or calculating: she offers first. Otherwise, or principled but afraid: she bargains.
      ///   Ransom up to her means; fealty only from a clan's head to a realm she is not in; service only through the
      ///   recruit gate; intimacy only in Mature content and above, between adults, if she could want the player, and
      ///   never from an honorable captive.
      /// </summary>
      public static CaptiveOffer Decide(CaptiveOfferFacts f)
      {
         f ??= new CaptiveOfferFacts();
         TraitFacts traits = f.Traits ?? new TraitFacts();
         bool principled = traits.Honor >= NemesisTurnPolicy.DefiantHonorThreshold && traits.Calculating < NemesisTurnPolicy.PragmatistCalculatingThreshold;
         bool honorless = traits.Honor < 0 || traits.Calculating >= NemesisTurnPolicy.PragmatistCalculatingThreshold;
         bool afraid = f.Fear >= NemesisTurnPolicy.FearFloor;

         CaptiveDisposition disposition = principled
            ? afraid ? CaptiveDisposition.Bargains : CaptiveDisposition.StandsOnPride
            : honorless || afraid ? CaptiveDisposition.OffersFirst : CaptiveDisposition.Bargains;

         var terms = new List<CaptiveTerm>();
         bool ransom = f.RansomMeans >= RansomFloor;
         if (ransom) terms.Add(CaptiveTerm.Ransom);
         if (f.LeadsHerClan && f.PlayerServesARealm && !f.HerClanIsInThatRealm && !f.RulesHerRealm) terms.Add(CaptiveTerm.Fealty);
         if (f.ServiceIsOpen) terms.Add(CaptiveTerm.Service);
         if (f.ContentLevel != AdultContentLevel.Off && f.BothOfAge && f.OrientationAllowsThePlayer && traits.Honor <= 0)
            terms.Add(CaptiveTerm.Intimacy);

         return new CaptiveOffer {Terms = terms, RansomCeiling = ransom ? f.RansomMeans : 0, Disposition = disposition};
      }

      /// <summary>
      ///   Reads a counter_offer action's "terms" parameter (a comma list the model writes, e.g. "ransom, fealty")
      ///   into the <see cref="CaptiveTerm" /> values it names, matching by keyword so "a ransom" or "her fealty"
      ///   still parse. An unrecognised word is skipped rather than failing the whole parse; an empty or
      ///   unparseable string yields an empty list, which the bridge reads as nothing to settle.
      /// </summary>
      public static IReadOnlyList<CaptiveTerm> ParseTerms(string raw)
      {
         var terms = new List<CaptiveTerm>();
         if (string.IsNullOrWhiteSpace(raw)) return terms;

         foreach (string part in raw.Split(','))
         {
            string w = part.Trim();
            if (w.Length == 0) continue;
            if (w.IndexOf("ransom", StringComparison.OrdinalIgnoreCase) >= 0) terms.Add(CaptiveTerm.Ransom);
            else if (w.IndexOf("fealty", StringComparison.OrdinalIgnoreCase) >= 0) terms.Add(CaptiveTerm.Fealty);
            else if (w.IndexOf("service", StringComparison.OrdinalIgnoreCase) >= 0) terms.Add(CaptiveTerm.Service);
            else if (w.IndexOf("intimacy", StringComparison.OrdinalIgnoreCase) >= 0) terms.Add(CaptiveTerm.Intimacy);
            else if (w.IndexOf("goodwill", StringComparison.OrdinalIgnoreCase) >= 0) terms.Add(CaptiveTerm.Goodwill);
         }

         return terms;
      }
   }
}
