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
//
// 03/11/2026, that guard is lifted. Lordfadooboo (Nexus, 01/11/2026): "i can never seem to get it to trigger";
// Maifailun (03/11/2026): "They always mention they have duties". Some realm is nearly always at war, so the verb
// was almost never taught. Gabriel's ruling ("oui vas-y", 03/11/2026), the one follow_me got on 10/09/2026: he may
// ride while his realm fights, says as he agrees that he may be called away, and leaves when his clan's leader or
// his ruler leads an army (RetainerLeavePolicy.CalledToMuster).

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
      ///   Everything the retainer question rests on, read off the live campaign by the caller. Named rather
      ///   than positional: this used to be nine parameters in a row, eight of them booleans, and the compiler
      ///   cannot tell "is at war with the player" from "is already riding" when both are just true.
      /// </summary>
      public sealed class RideWithMeFacts
      {
         /// <summary>A lord or lady. A wanderer or a bandit is not who "ride with me for a while" is meant for.</summary>
         public bool IsLord { get; set; }

         /// <summary>Alive.</summary>
         public bool IsAlive { get; set; }

         /// <summary>Somebody's prisoner, and so in no position to ride anywhere.</summary>
         public bool IsPrisoner { get; set; }

         /// <summary>
         ///   Leads their own field party. THE INVERTED FACT that makes this the exact complement of follow_me:
         ///   a party-leader is follow_me's business, and taking them here would orphan their command.
         /// </summary>
         public bool LeadsOwnParty { get; set; }

         /// <summary>Already of the player's own clan, who ride along by default; this is for another clan's lord.</summary>
         public bool IsPlayerClan { get; set; }

         /// <summary>Their realm and the player's are at war with EACH OTHER.</summary>
         public bool AtWarWithPlayer { get; set; }

         /// <summary>Already riding as a retainer. One at a time.</summary>
         public bool AlreadyRiding { get; set; }

         /// <summary>The mod's own personal regard ledger, never the vanilla clan relation.</summary>
         public int Regard { get; set; }

         /// <summary>
         ///   Their own realm is at war with somebody, anybody. NO LONGER A REFUSAL (03/11/2026): it is the fact
         ///   behind <see cref="ShouldWarnOfMuster" />, and the muster that may follow ends the arrangement
         ///   (<see cref="RetainerLeavePolicy" />). The history below is why it once refused.
         ///   <para>
         ///     EscortEligibilityPolicy dropped the same blanket gate on 2026-09-10, because it made follow_me
         ///     nearly unreachable, and it was safe to drop there because a summoned lord's own party joins an
         ///     army and the escort ENDS, with the lord saying why (EscortContinuationPolicy.SummonedByLiege).
         ///     A retainer has no party of his own, by the very precondition above, so there is nothing for a
         ///     muster to reclaim and no recall to end the arrangement. Dropping it here would let a lord ride
         ///     inside the player's party for the whole term while his realm fights, with no way out, which is
         ///     precisely what the 29/08/2026 gate was for. Same words, different situation, so it awaits its
         ///     own ruling rather than inheriting the other one.
         ///   </para>
         /// </summary>
         public bool RealmAtWar { get; set; }
      }

      /// <summary>
      ///   True only when this hero is a lord or lady, alive, not a prisoner, does NOT lead their own field
      ///   party, is not already of the player's clan, is not at war with the player, is not already riding,
      ///   and holds the player in at least <see cref="RegardFloor" /> personal regard. A realm at war no longer
      ///   refuses (03/11/2026). Missing facts refuse: the safe answer to "I do not know" is no.
      /// </summary>
      public static bool CanRideWithPlayer(RideWithMeFacts facts)
      {
         if (facts == null) return false;

         if (!facts.IsLord) return false;
         if (!facts.IsAlive) return false;
         if (facts.IsPrisoner) return false;
         if (facts.LeadsOwnParty) return false;
         if (facts.IsPlayerClan) return false;
         if (facts.AtWarWithPlayer) return false;
         if (facts.AlreadyRiding) return false;
         if (facts.Regard < RegardFloor) return false;

         return true;
      }

      /// <summary>
      ///   True when he should say, as he agrees, that his liege may call him away: his realm is at war, so the
      ///   muster may reach him and he will leave the player's party for it. Said in advance it is honesty; said only
      ///   when he rides off it is a broken promise (Gabriel, 10/09/2026, for follow_me; the same for this verb).
      /// </summary>
      public static bool ShouldWarnOfMuster(RideWithMeFacts facts) => facts?.RealmAtWar == true;
   }
}
