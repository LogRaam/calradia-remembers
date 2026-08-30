// Code written by Gabriel Mailhot, 11/08/2026.
// ride_with_me (1:1 bridge action): the idle-lord half of "a lord accompanies you", the exact complement of
// follow_me. follow_me is for a lord who LEADS their own field party (their party escorts the player's, keeping
// their command). ride_with_me is for a lord who does NOT lead a party (idle, at court, in a settlement): they
// ride IN the player's OWN party personally, staying in their OWN clan (no marriage, no clan-join), for any
// reason the scene gives (friendship, alliance, a shared cause, a courtship trial). The two verbs are made
// MUTUALLY EXCLUSIVE by inverting the party fact: EscortEligibilityPolicy.CanEscort requires leadsParty == true,
// this requires leadsParty == false. A hero can satisfy exactly one, so the LLM is never offered both and a
// party-leader is never taken into the roster (that would orphan their command; follow_me handles them instead).
//
// Like EscortEligibilityPolicy, this pure gate is consulted at BOTH surfaces around the LLM ("the prompt is
// advice, the bridge is law"): whether ride_with_me is even taught this turn (EncounterContextBuilder ->
// EncounterContext.NpcCanRideWithPlayer -> SDK PromptBuilder.AppendRideWithMe) and again whether it really
// executes (BannerlordGameStateBridge.ExecuteRideWithMe). Pure and unit-tested so the two call sites share one
// decision and can never drift. The factionAtWar guard (added 29/08/2026) mirrors EscortEligibilityPolicy's own:
// a lord whose realm is at war with anyone stays home and available for his liege's muster call, rather than
// riding off with the player and quietly weakening his faction.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Pure eligibility gate for ride_with_me: the idle-lord retainer, the exact complement of EscortEligibilityPolicy.CanEscort.</summary>
   public static class RideWithMeEligibilityPolicy
   {
      /// <summary>
      ///   TUNE: the personal regard (<c>NpcProfile.ReputationWithPlayer</c>, clamped [-100,100]) a lord must
      ///   hold for the player before they will ride at their side. Set at the CLOSE-FRIEND / deep-trust mark
      ///   (30, the same bar <c>ContextualNameResolver</c> calls "a very close friend"), deliberately BELOW the
      ///   deep-bond 60 that a permanent clan-join (<see cref="NotableRecruitmentPolicy.RegardFloor" />,
      ///   <see cref="PrisonerRecruitmentPolicy.RegardFloor" />) demands: riding along is a real ask but a
      ///   TEMPORARY, clan-preserving one, so it costs a friendly bond, not the life-changing trust of forsaking
      ///   one's own house. Asking someone to travel at your side should still never be a cold-stranger exploit,
      ///   which is why it is gated at all rather than left open like the always-on verbs.
      /// </summary>
      public const int RegardFloor = 30;

      /// <summary>
      ///   True only when this hero is a lord/lady (a wanderer or bandit is not who "ride with me for a while"
      ///   is meant for), alive, not a prisoner, does NOT lead their own field party (the inverted fact that
      ///   makes this the exact complement of follow_me: a party-leader is follow_me's job, taking them here
      ///   would orphan their command), is not already of the player's own clan (a clan member rides with you by
      ///   default; this is for an OTHER clan's lord), is not at war with the player (an enemy lord does not ride
      ///   inside the very army they may soon fight), their OWN FACTION is not at war with anyone (author's
      ///   concern: a lord riding along while his realm fights a war stops answering his liege's muster call,
      ///   quietly weakening the faction, so he stays home and available regardless of his personal regard for
      ///   the player - distinct from <paramref name="atWar" />, which is only the player's own war), is not
      ///   already riding as a retainer (one at a time), and holds the player in at least
      ///   <see cref="RegardFloor" /> personal regard.
      /// </summary>
      public static bool CanRideWithPlayer(
         bool isLord, bool isAlive, bool isPrisoner, bool leadsParty,
         bool isPlayerClan, bool atWar, bool alreadyRiding, int regard, bool factionAtWar)
      {
         if (!isLord) return false;
         if (!isAlive) return false;
         if (isPrisoner) return false;
         if (leadsParty) return false;
         if (isPlayerClan) return false;
         if (atWar) return false;
         if (alreadyRiding) return false;
         if (regard < RegardFloor) return false;
         if (factionAtWar) return false;

         return true;
      }
   }
}
