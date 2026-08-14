// Code written by Gabriel Mailhot, 14/08/2026.
// Duels pillar, bugfix (player report, group conversation): a witness challenge got armed as a PLAYER-vs-NPC
// duel even though the fiction described a quarrel between two OTHER people in the room. challenge_duel
// carries no target parameter (a deliberate SDK gap: true NPC-vs-NPC duels are a separate future increment),
// so the mod's ResolveDuelChallenge always treated whoever spoke this turn as "opponent" no matter who the
// story actually named. This policy is the guard: a challenge may only be armed against the player when it
// comes from the NPC the player is genuinely, mechanically talking to THIS turn, never a bystander witness
// merely present in the room.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>
   ///   Decides whether the speaker who just emitted challenge_duel is the player's real conversational
   ///   partner (and so a fair target for a player-vs-NPC duel), or a bystander witness who happened to hold
   ///   the floor for other reasons. Pure so it is reachable without a live Hero/session.
   /// </summary>
   public static class DuelInterlocutorPolicy
   {
      /// <summary>
      ///   True when <paramref name="speakerIsMain" /> (the conversation's own NPC, always a legitimate
      ///   target) or when <paramref name="speakerHeroId" /> matches <paramref name="addressedHeroId" /> (the
      ///   witness the player is currently, explicitly speaking with). Any other witness present in the room
      ///   is speaking on their own turn, not the player's: not an interlocutor, so never a duel target.
      /// </summary>
      public static bool IsDirectInterlocutor(bool speakerIsMain, string speakerHeroId, string addressedHeroId)
         => speakerIsMain
            || (!string.IsNullOrEmpty(speakerHeroId) && speakerHeroId == addressedHeroId);
   }
}
