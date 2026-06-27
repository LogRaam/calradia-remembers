// Code written by Gabriel Mailhot, 27/06/2026.
// Extension Surface, Volet B — the PUBLIC mission contract. A third-party mod constructs an ExternalMission and
// registers it with CR via CalradiaRemembers.MissionBoard.Register(...); CR issues it as a verified quest (journal
// mirror, NPC framing, gated reward, deadline/abandon, self-heal on load), and the mod later calls
// MissionBoard.Complete(id) when ITS OWN deed is truly done — the external mod owns the completion signal, CR owns
// everything else. Kept free of Bannerlord types (ids are plain strings) so the contract stays stable while
// CR's internals change behind it.

namespace CalradiaRemembers
{
   /// <summary>The outcome of registering or signalling a mission through <see cref="MissionBoard" />.</summary>
   public enum MissionBoardResult
   {
      /// <summary>The mission was accepted and issued as a quest on the giver.</summary>
      Registered,

      /// <summary>Missing id/source, or no title and no description — nothing to issue.</summary>
      RejectedInvalid,

      /// <summary>A live mission with this id is already registered on the giver.</summary>
      RejectedDuplicate,

      /// <summary>The giver hero id did not resolve to a hero CR has a profile for.</summary>
      RejectedUnknownGiver,

      /// <summary>The memory store is not active (no campaign loaded).</summary>
      RejectedNoStore,

      /// <summary>Completion accepted: the mission is stamped done, awaiting the player's report to the giver.</summary>
      Completed,

      /// <summary>Abandon accepted: the mission was cancelled (no player penalty — the mod withdrew it).</summary>
      Abandoned,

      /// <summary>No outstanding mission with that id was found to complete or abandon.</summary>
      NotFound
   }

   /// <summary>
   ///   One mission a third-party mod wants CR to set the player. CR normalises it (bounds the text, clamps the
   ///   reward to its own caps, normalises the deadline, resolves the giver), then issues it as a verified quest:
   ///   the giver frames it in conversation, the journal mirrors it, and the reward is paid only once the mod
   ///   signals completion AND the player reports back. The mod owns the deed's truth; CR owns the ceremony.
   /// </summary>
   public sealed class ExternalMission
   {
      /// <summary>The mod's STABLE mission id — REQUIRED. CR dedups on it and matches the completion signal to it.</summary>
      public string Id { get; set; }

      /// <summary>The registering mod's id — REQUIRED, for provenance and the evidence line on completion.</summary>
      public string SourceMod { get; set; }

      /// <summary>
      ///   StringId of the hero who gives and frames the mission — REQUIRED in this version. CR must already
      ///   have a profile for them (the player has spoken with them at least once); the reward is collected by
      ///   reporting back to this giver in conversation.
      /// </summary>
      public string GiverHeroId { get; set; }

      /// <summary>A short one-line title for the mission (the giver's ask). Used if <see cref="Description" /> is empty.</summary>
      public string Title { get; set; }

      /// <summary>The player-facing task text, in the giver's voice. CR bounds it to the blob-safe length.</summary>
      public string Description { get; set; }

      /// <summary>Denars paid on completion; CR clamps it to its payable cap. Zero for none.</summary>
      public int RewardGold { get; set; }

      /// <summary>Personal regard granted on completion; CR clamps it to its gated quest-reward cap. Zero for none.</summary>
      public int RewardRelation { get; set; }

      /// <summary>Days from now by which the deed must be done; null (or non-positive) means open-ended.</summary>
      public int? DeadlineDays { get; set; }
   }
}
