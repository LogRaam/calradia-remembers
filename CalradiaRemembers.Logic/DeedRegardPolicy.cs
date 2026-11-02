// Code written by Gabriel Mailhot, 23/10/2026.
// What each deed of the player does to a character's personal regard (NpcProfile.ReputationWithPlayer), the one score the
// player's page reads for "Hold you dear" and "Hold you in contempt". Gabriel (23/10/2026), on Arcor, whose wife the
// player took and whom the player handed to his enemies, still absent from "Hold you in contempt": "il faudrait que pour
// chaque action il y a un impact sur l'estime, qu'il soit positif ou negatif. Car je pourrais complimenter un NPC et
// augmenter son estime, pour ensuite commettre un mefait contre lui qui ne changerait pas sa perception de moi." The audit
// of 23/10 found most misdeeds planted a grudge and never touched regard, and the grudge never feeds regard back. So every
// deed has a regard change here, for its victim and for the victim's close kin (spouse, parents, grown children and
// siblings), in one table: the place to tune. A deed whose path already moves regard (a beating, a robbery, adultery found
// out, a task broken or done, an oath broken, a departure, a rescue, a duel) keeps its own number and is listed here as
// already applied, so nothing is charged twice. The scale is the point: a compliment earns at most +1 every few hours in
// conversation (RelationGate), a misdeed costs tens at once.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A deed of the player toward a character, as regard sees it.</summary>
   public enum PlayerDeed
   {
      /// <summary>Took the character's spouse from them (Stolen Love, an elopement tale).</summary>
      StoleSpouse,

      /// <summary>Handed a prisoner to his own enemies (give, sell, exchange or ransom).</summary>
      HandedToEnemy,

      /// <summary>Sold or traded a prisoner to someone who is not his enemy, like chattel.</summary>
      SoldAsChattel,

      /// <summary>Had the character put to death; only the kin are left to feel it.</summary>
      Executed,

      /// <summary>Killed the character outside battle; only the kin are left to feel it.</summary>
      Murdered,

      /// <summary>Took a prisoner by force in a captive scene.</summary>
      TookByForce,

      /// <summary>Tortured a prisoner.</summary>
      Tortured,

      /// <summary>Interrogated a prisoner under duress.</summary>
      Interrogated,

      /// <summary>Tormented a prisoner in a way no closer word fits.</summary>
      Tormented,

      /// <summary>Beat the character down (the victim's hit is ViolenceConsequencePolicy's).</summary>
      Beat,

      /// <summary>Robbed the character by force (the victim's hit is ViolenceConsequencePolicy's).</summary>
      Robbed,

      /// <summary>Took the character's fief from them.</summary>
      RevokedFief,

      /// <summary>Cast the character out of the player's clan.</summary>
      Expelled,

      /// <summary>Was found to have secretly aided a plot against the character.</summary>
      SchemeAbettedFoundOut,

      /// <summary>Was found to have turned opinion against the character.</summary>
      SwayExposed,

      /// <summary>Broke a pact the character's realm had with the player's.</summary>
      PactBroken,

      /// <summary>Forsook the realm by defection or rebellion; every lord of it feels it.</summary>
      ForsookRealm,

      /// <summary>Gave the character gold or a gift (sized by value, once a day).</summary>
      Gift,

      /// <summary>Set the character free from the player's own captivity, by mercy.</summary>
      FreedPrisoner,

      /// <summary>Shared a night with the character, by consent, when the conversation itself moved nothing.</summary>
      SharedNight,

      /// <summary>Married the character.</summary>
      Married,

      /// <summary>A friendly tournament rivalry.</summary>
      FriendlyRivalry,

      /// <summary>A bitter tournament rivalry.</summary>
      BitterRivalry,

      /// <summary>
      ///   In bed, pressed on the character a gesture they dislike (Gabriel, 26/10/2026: "un geste impose que le NPC n'aime
      ///   pas devrait reduire le personal regard"); once a day per gesture.
      /// </summary>
      ImposedDislikedGesture,

      /// <summary>In bed, found for the first time a gesture the character particularly loves.</summary>
      FoundLovedGesture,

      /// <summary>Bled the character with a threat, or tried (sized by ExtortionPolicy).</summary>
      Extorted,

      /// <summary>Took the character's coin to spare their lands, then raided them.</summary>
      ExtortionBargainBroken,

      /// <summary>Told the character's hidden affair.</summary>
      SecretExposed,

      /// <summary>Took the character's coin for silence, then told their affair.</summary>
      SecretBargainBroken
   }

   /// <summary>The regard a deed costs or earns.</summary>
   public sealed class DeedRegard
   {
      /// <summary>The change to the victim's (or beneficiary's) regard; 0 when they are dead or the path already applies its own.</summary>
      public int Victim { get; init; }

      /// <summary>The change to each close kin's regard.</summary>
      public int Kin { get; init; }

      /// <summary>The victim's change is applied on the deed's own path (named in the table), not by this policy.</summary>
      public bool VictimAlreadyApplied { get; init; }
   }

   /// <summary>The one table of what each deed does to personal regard.</summary>
   public static class DeedRegardPolicy
   {
      /// <summary>A spouse taken: the one left is shamed and bereft at once; their family shares the insult.</summary>
      public const int StoleSpouseVictim = -60;

      /// <summary>The family of the one left.</summary>
      public const int StoleSpouseKin = -20;

      /// <summary>Handed in chains to his own enemies.</summary>
      public const int HandedToEnemyVictim = -50;

      /// <summary>The family of a captive handed to his enemies.</summary>
      public const int HandedToEnemyKin = -15;

      /// <summary>Sold off like chattel, though not to an enemy.</summary>
      public const int SoldAsChattelVictim = -15;

      /// <summary>The family of a lord put to death: the vendetta is unforgivable, and so is the regard.</summary>
      public const int ExecutedKin = -70;

      /// <summary>The family of a man murdered: no lighter than an execution.</summary>
      public const int MurderedKin = -70;

      /// <summary>Taken by force while a prisoner.</summary>
      public const int TookByForceVictim = -60;

      /// <summary>Tortured while a prisoner.</summary>
      public const int TorturedVictim = -60;

      /// <summary>Interrogated under duress.</summary>
      public const int InterrogatedVictim = -30;

      /// <summary>Tormented while a prisoner.</summary>
      public const int TormentedVictim = -40;

      /// <summary>The family of a prisoner abused in a captive scene.</summary>
      public const int CaptiveAbuseKin = -20;

      /// <summary>The family of a man beaten down.</summary>
      public const int BeatKin = -15;

      /// <summary>A fief taken.</summary>
      public const int RevokedFiefVictim = -40;

      /// <summary>Cast out of the clan.</summary>
      public const int ExpelledVictim = -30;

      /// <summary>A plot against them secretly aided, found out.</summary>
      public const int SchemeAbettedVictim = -25;

      /// <summary>Opinion turned against them, found out.</summary>
      public const int SwayExposedVictim = -20;

      /// <summary>Bled with a threat. No kin: a secret paid for is hidden from them, and that is what the coin bought.</summary>
      public const int ExtortedVictim = -25;

      /// <summary>Their coin taken to spare their lands, and the lands raided.</summary>
      public const int ExtortionBargainBrokenVictim = -30;

      /// <summary>Their hidden affair told. No kin: the shame reaches their house through the scandal.</summary>
      public const int SecretExposedVictim = -35;

      /// <summary>Their coin taken for silence, and the secret told.</summary>
      public const int SecretBargainBrokenVictim = -45;

      /// <summary>What a told affair does between the spouses: their vanilla relation, not regard for the player. No divorce, ruled out of this step.</summary>
      public const int BetrayedSpouseRelation = -NpcMemoryService.Core.Standing.VanillaRelation.TrustedTier;

      /// <summary>A pact broken, to the leader of the realm it bound.</summary>
      public const int PactBrokenVictim = -20;

      /// <summary>A realm forsaken by defection or rebellion, to each of its lords.</summary>
      public const int ForsookRealmVictim = -15;

      /// <summary>Freed from the player's captivity by mercy.</summary>
      public const int FreedPrisonerVictim = 15;

      /// <summary>The family of a prisoner freed by mercy.</summary>
      public const int FreedPrisonerKin = 5;

      /// <summary>A night shared by consent, when the conversation itself raised nothing.</summary>
      public const int SharedNightVictim = 3;

      /// <summary>Wed to the player.</summary>
      public const int MarriedVictim = 20;

      /// <summary>A friendly rivalry in the lists.</summary>
      public const int FriendlyRivalryVictim = 3;

      /// <summary>A bitter rivalry in the lists.</summary>
      public const int BitterRivalryVictim = -10;

      /// <summary>A gesture they dislike, imposed in bed: a real cooling, short of a misdeed (it stays between the two).</summary>
      public const int ImposedDislikedGestureVictim = -4;

      /// <summary>A gesture they love, found the first time: a small warmth, once, so it cannot be farmed.</summary>
      public const int FoundLovedGestureVictim = 1;

      /// <summary>
      ///   What <paramref name="deed" /> does to regard. A gift is sized by <see cref="GiftRegard" />; a beating and a
      ///   robbery charge the victim on their own path (ViolenceConsequencePolicy) and only the kin here.
      /// </summary>
      public static DeedRegard For(PlayerDeed deed) => deed switch
      {
         PlayerDeed.StoleSpouse => new DeedRegard {Victim = StoleSpouseVictim, Kin = StoleSpouseKin},
         PlayerDeed.HandedToEnemy => new DeedRegard {Victim = HandedToEnemyVictim, Kin = HandedToEnemyKin},
         PlayerDeed.SoldAsChattel => new DeedRegard {Victim = SoldAsChattelVictim},
         PlayerDeed.Executed => new DeedRegard {Kin = ExecutedKin},
         PlayerDeed.Murdered => new DeedRegard {Kin = MurderedKin},
         PlayerDeed.TookByForce => new DeedRegard {Victim = TookByForceVictim, Kin = CaptiveAbuseKin},
         PlayerDeed.Tortured => new DeedRegard {Victim = TorturedVictim, Kin = CaptiveAbuseKin},
         PlayerDeed.Interrogated => new DeedRegard {Victim = InterrogatedVictim},
         PlayerDeed.Tormented => new DeedRegard {Victim = TormentedVictim, Kin = CaptiveAbuseKin},
         PlayerDeed.Beat => new DeedRegard {Kin = BeatKin, VictimAlreadyApplied = true},
         PlayerDeed.Robbed => new DeedRegard {VictimAlreadyApplied = true},
         PlayerDeed.RevokedFief => new DeedRegard {Victim = RevokedFiefVictim},
         PlayerDeed.Expelled => new DeedRegard {Victim = ExpelledVictim},
         PlayerDeed.SchemeAbettedFoundOut => new DeedRegard {Victim = SchemeAbettedVictim},
         PlayerDeed.SwayExposed => new DeedRegard {Victim = SwayExposedVictim},
         PlayerDeed.PactBroken => new DeedRegard {Victim = PactBrokenVictim},
         PlayerDeed.ForsookRealm => new DeedRegard {Victim = ForsookRealmVictim},
         PlayerDeed.FreedPrisoner => new DeedRegard {Victim = FreedPrisonerVictim, Kin = FreedPrisonerKin},
         PlayerDeed.SharedNight => new DeedRegard {Victim = SharedNightVictim},
         PlayerDeed.Married => new DeedRegard {Victim = MarriedVictim},
         PlayerDeed.FriendlyRivalry => new DeedRegard {Victim = FriendlyRivalryVictim},
         PlayerDeed.BitterRivalry => new DeedRegard {Victim = BitterRivalryVictim},
         PlayerDeed.ImposedDislikedGesture => new DeedRegard {Victim = ImposedDislikedGestureVictim},
         PlayerDeed.FoundLovedGesture => new DeedRegard {Victim = FoundLovedGestureVictim},
         PlayerDeed.Extorted => new DeedRegard {Victim = ExtortedVictim},
         PlayerDeed.ExtortionBargainBroken => new DeedRegard {Victim = ExtortionBargainBrokenVictim},
         PlayerDeed.SecretExposed => new DeedRegard {Victim = SecretExposedVictim},
         PlayerDeed.SecretBargainBroken => new DeedRegard {Victim = SecretBargainBrokenVictim},
         _ => new DeedRegard()
      };

      /// <summary>
      ///   A gift's regard, by its value in denars: +1 under 200, +2 under 1,000, +4 under 5,000, +6 beyond; and nothing for
      ///   a second gift the same day, so affection is not bought by the purse load.
      /// </summary>
      public static int GiftRegard(int value, bool alreadyGiftedToday)
      {
         if (alreadyGiftedToday || value <= 0) return 0;
         if (value < 200) return 1;
         if (value < 1000) return 2;

         return value < 5000 ? 4 : 6;
      }
   }
}
