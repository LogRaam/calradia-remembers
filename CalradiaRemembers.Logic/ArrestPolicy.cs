// Code written by Gabriel Mailhot, 26/09/2026.
// Arrests "in the King's name" (Gabriel, 26/09/2026, born of the courier secrets: a lord who reads that the player
// plotted against him should be able to act). The rulings this file holds:
//
//   - Only a VASSAL or a MERCENARY can be arrested. Once the player leads a faction, it is politics and war.
//   - A GRAVE act brings a warrant: a treason letter read by a lord of the realm, a crime in conversation against a
//     member of the realm, or a crime rating the game itself calls severe in the player's own realm.
//   - The sergeants come. The player surrenders and is TRIED, or flees and becomes an OUTLAW with a BOUNTY on their
//     head: a vassal leaves the kingdom, a mercenary loses the contract.
//   - Captured while an outlaw, the bounty is removed (as if the captor collected it; the reward is only told), and the
//     player is tried all the same, the flight counted against them.
//   - The council is the liege and the vassals who hate the player most.
//   - Losing the trial means PRISON and nothing else. An ACQUITTAL wipes the charges entirely: only a new grave act can
//     bring another arrest.
//   - Only the player is ever taken, never their companions.
//
// Pure: who may be arrested, what is grave, who sits, how long a sentence runs, and the case itself (ArrestCase), a
// small state machine every engine step drives.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Violence;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A grave act the player can be charged with.</summary>
   public enum ArrestCharge
   {
      /// <summary>A letter of theirs, plotting against a lord of the realm or its ruler, was read by that lord.</summary>
      TreasonLetter,

      /// <summary>They killed a member of their own realm.</summary>
      KilledRealmMember,

      /// <summary>They assaulted a member of their own realm.</summary>
      AssaultedRealmMember,

      /// <summary>They robbed a member of their own realm.</summary>
      RobbedRealmMember,

      /// <summary>The game rates their crimes in their own realm as severe.</summary>
      SevereCrimeRating,

      /// <summary>They fled or fought the sergeants sent to arrest them.</summary>
      FledArrest
   }

   /// <summary>Where the player stands in their realm.</summary>
   public sealed class ArrestStandingFacts
   {
      public bool IsFactionLeader { get; set; }
      public bool IsVassal { get; set; }
      public bool IsMercenary { get; set; }
   }

   /// <summary>A vassal of the realm, and how they regard the player on the mod's own ledger.</summary>
   public sealed class CouncilCandidate
   {
      public string HeroId { get; set; } = "";
      public int RegardForPlayer { get; set; }
   }

   /// <summary>The rules of an arrest. Pure.</summary>
   public static class ArrestPolicy
   {
      /// <summary>TUNE: vassals who sit beside the liege at the trial.</summary>
      public const int CouncilVassalSeats = 3;

      /// <summary>TUNE: the days of prison every conviction carries, before the charges' own weight.</summary>
      public const int BasePrisonDays = 10;

      /// <summary>TUNE: the most days a single sentence can run.</summary>
      public const int MaxPrisonDays = 60;

      /// <summary>Only a vassal or a mercenary answers to a liege; a faction leader answers to no one.</summary>
      public static bool CanBeArrested(ArrestStandingFacts facts)
         => facts != null && !facts.IsFactionLeader && (facts.IsVassal || facts.IsMercenary);

      /// <summary>
      ///   The charge an act of violence against a member of the player's own realm brings, or null when nothing was
      ///   attempted. A killing that failed is still an assault.
      /// </summary>
      public static ArrestCharge? ChargeFor(ViolentAct act, ViolenceVerdict verdict)
      {
         if (verdict != ViolenceVerdict.Succeeded && verdict != ViolenceVerdict.Failed) return null;

         return act switch
         {
            ViolentAct.Kill => verdict == ViolenceVerdict.Succeeded ? ArrestCharge.KilledRealmMember : ArrestCharge.AssaultedRealmMember,
            ViolentAct.Assault => ArrestCharge.AssaultedRealmMember,
            ViolentAct.Rob => ArrestCharge.RobbedRealmMember,
            _ => null
         };
      }

      /// <summary>What the warrant reads, in plain words: the distinct charges, joined ("treason and flight from arrest").</summary>
      public static string Describe(IEnumerable<ArrestCharge> charges)
      {
         List<string> words = (charges ?? Enumerable.Empty<ArrestCharge>()).Distinct().Select(Words).ToList();
         if (words.Count == 0) return "crimes against the realm";
         if (words.Count == 1) return words[0];

         return string.Join(", ", words.Take(words.Count - 1)) + " and " + words[words.Count - 1];
      }

      private static string Words(ArrestCharge charge) => charge switch
      {
         ArrestCharge.TreasonLetter => "treason",
         ArrestCharge.KilledRealmMember => "the killing of a member of the realm",
         ArrestCharge.AssaultedRealmMember => "violence against a member of the realm",
         ArrestCharge.RobbedRealmMember => "robbery of a member of the realm",
         ArrestCharge.SevereCrimeRating => "crimes against the realm",
         ArrestCharge.FledArrest => "flight from arrest",
         _ => "crimes against the realm"
      };

      /// <summary>TUNE: how heavily each charge weighs on the sentence, in days.</summary>
      public static int Weight(ArrestCharge charge) => charge switch
      {
         ArrestCharge.KilledRealmMember => 30,
         ArrestCharge.TreasonLetter => 20,
         ArrestCharge.AssaultedRealmMember => 10,
         ArrestCharge.RobbedRealmMember => 10,
         ArrestCharge.SevereCrimeRating => 10,
         ArrestCharge.FledArrest => 15,
         _ => 0
      };

      /// <summary>
      ///   The days a conviction on <paramref name="charges" /> puts the player in prison: the base, plus each distinct
      ///   charge's weight, capped at <see cref="MaxPrisonDays" />.
      /// </summary>
      public static int PrisonDays(IEnumerable<ArrestCharge> charges)
      {
         int days = BasePrisonDays + (charges ?? Enumerable.Empty<ArrestCharge>()).Distinct().Sum(Weight);

         return Math.Min(days, MaxPrisonDays);
      }

      /// <summary>
      ///   Who sits in judgement: the liege, then the <see cref="CouncilVassalSeats" /> vassals who regard the player
      ///   worst (the mod's own regard), never the liege twice nor the player.
      /// </summary>
      public static IReadOnlyList<string> CouncilSeats(string liegeId, IEnumerable<CouncilCandidate> vassals, string playerId)
      {
         var seats = new List<string>();
         if (!string.IsNullOrWhiteSpace(liegeId)) seats.Add(liegeId);

         seats.AddRange((vassals ?? Enumerable.Empty<CouncilCandidate>())
                        .Where(v => !string.IsNullOrWhiteSpace(v?.HeroId) && v.HeroId != liegeId && v.HeroId != playerId)
                        .OrderBy(v => v.RegardForPlayer)
                        .ThenBy(v => v.HeroId, StringComparer.Ordinal)
                        .Take(CouncilVassalSeats)
                        .Select(v => v.HeroId));

         return seats;
      }
   }

   /// <summary>Where an arrest stands.</summary>
   public enum ArrestStage
   {
      /// <summary>No charge against the player.</summary>
      None,

      /// <summary>A warrant is out; the sergeants are coming.</summary>
      WarrantOut,

      /// <summary>The player fled the sergeants: outlawed, with a bounty on their head.</summary>
      Outlaw,

      /// <summary>Taken, awaiting or facing the council.</summary>
      OnTrial,

      /// <summary>Convicted, serving the sentence.</summary>
      Imprisoned
   }

   /// <summary>
   ///   The player's case, one at a time: the charges against them and where it stands. Every transition the engine
   ///   makes goes through here, so the rulings (acquittal wipes everything, flight is a charge of its own, capture ends
   ///   the bounty) hold whatever the engine does.
   /// </summary>
   public sealed class ArrestCase
   {
      public ArrestStage Stage { get; set; } = ArrestStage.None;

      /// <summary>What the player is charged with, in the order the charges came.</summary>
      public List<ArrestCharge> Charges { get; set; } = new();

      /// <summary>The liege in whose name the warrant runs.</summary>
      public string LiegeId { get; set; }

      /// <summary>True while a bounty is on the player's head.</summary>
      public bool BountyOnHead { get; set; }

      /// <summary>The day the sentence ends, while imprisoned.</summary>
      public int ReleaseDay { get; set; }

      /// <summary>The town or castle whose cells hold the player, from the taking to the release.</summary>
      public string PrisonSettlementId { get; set; }

      /// <summary>True once the trial's council has been convened; a council that rose without a verdict is then judged by default.</summary>
      public bool TrialOpened { get; set; }

      /// <summary>
      ///   A grave act. With no case open it brings a warrant in <paramref name="liegeId" />'s name; with one open, it is
      ///   one more charge on it. A player who cannot be arrested is not charged.
      /// </summary>
      public void Charge(ArrestCharge charge, string liegeId, bool canBeArrested)
      {
         if (!canBeArrested) return;

         if (Stage == ArrestStage.None)
         {
            Stage = ArrestStage.WarrantOut;
            LiegeId = liegeId;
         }

         if (!Charges.Contains(charge)) Charges.Add(charge);
      }

      /// <summary>The player fled or fought the sergeants: an outlaw, a bounty on their head, and one more charge.</summary>
      public void Flee()
      {
         if (Stage != ArrestStage.WarrantOut) return;

         Stage = ArrestStage.Outlaw;
         BountyOnHead = true;
         if (!Charges.Contains(ArrestCharge.FledArrest)) Charges.Add(ArrestCharge.FledArrest);
      }

      /// <summary>
      ///   The player is taken, surrendering or caught: the bounty is removed (its captor has "collected" it) and they
      ///   go before the council.
      /// </summary>
      public void Taken()
      {
         if (Stage != ArrestStage.WarrantOut && Stage != ArrestStage.Outlaw) return;

         Stage = ArrestStage.OnTrial;
         BountyOnHead = false;
         TrialOpened = false;
      }

      /// <summary>
      ///   The player broke out while awaiting trial or serving the sentence: a flight like any other, so an outlaw with
      ///   a bounty and the flight charged.
      /// </summary>
      public void Escape()
      {
         if (Stage != ArrestStage.OnTrial && Stage != ArrestStage.Imprisoned) return;

         Stage = ArrestStage.Outlaw;
         BountyOnHead = true;
         TrialOpened = false;
         ReleaseDay = 0;
         PrisonSettlementId = null;
         if (!Charges.Contains(ArrestCharge.FledArrest)) Charges.Add(ArrestCharge.FledArrest);
      }

      /// <summary>
      ///   The council's verdict. Acquitted: every charge wiped, as if none had been brought. Convicted: prison until
      ///   <see cref="ArrestPolicy.PrisonDays" /> have passed.
      /// </summary>
      public void Verdict(bool convicted, int today)
      {
         if (Stage != ArrestStage.OnTrial) return;

         if (!convicted)
         {
            Clear();

            return;
         }

         Stage = ArrestStage.Imprisoned;
         ReleaseDay = today + ArrestPolicy.PrisonDays(Charges);
      }

      /// <summary>True once the sentence has run its course.</summary>
      public bool SentenceServed(int today) => Stage == ArrestStage.Imprisoned && today >= ReleaseDay;

      /// <summary>The sentence served: the case is closed, the charges paid for.</summary>
      public void Release()
      {
         if (Stage == ArrestStage.Imprisoned) Clear();
      }

      private void Clear()
      {
         Stage = ArrestStage.None;
         Charges.Clear();
         LiegeId = null;
         BountyOnHead = false;
         ReleaseDay = 0;
         PrisonSettlementId = null;
         TrialOpened = false;
      }
   }
}
