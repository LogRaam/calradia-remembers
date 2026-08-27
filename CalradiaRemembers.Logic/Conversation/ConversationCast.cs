// Code written by Gabriel Mailhot, 27/08/2026.
// Spotlight pillar, pure foundation: WHO is a full participant in a conversation and who is only a witness, and
// how that boundary moves. A conversation opens with one ACTIVE anchor (the character the player walked up to)
// and a set of passive WITNESSES who watch, listen, and react from the edge. The boundary is permeable: address
// a witness (the player, or an active speaker, speaks TO them) and they are PROMOTED to a full participant, with
// all the standing that carries, and they STAY one for the rest of the exchange; an active participant told to
// hold their tongue, if they accept it, is DEMOTED back to a witness. When two or more are active it is an
// ensemble scene (each active voice may speak that turn, like a council). The anchor is never demoted: it is the
// conversation's reason to exist. This holds only the roles; whether an address promotes and whether a silence
// order is obeyed are separate policies. Session state, never persisted (a conversation does not span a save).

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>A present character's standing in the conversation.</summary>
   public enum CastRole
   {
      /// <summary>A full participant: speaks in their own voice, on their own initiative, like the anchor.</summary>
      Active,

      /// <summary>Present at the edge: watches, listens, and reacts, but does not hold the floor.</summary>
      Witness
   }

   /// <summary>
   ///   The live cast of a conversation: the anchor and every other present character, each Active or Witness,
   ///   with the moves that shift that boundary (<see cref="Promote" /> / <see cref="Demote" />). The anchor is
   ///   always Active and can never be demoted.
   /// </summary>
   public sealed class ConversationCast
   {
      private readonly Dictionary<string, CastRole> _roles = new Dictionary<string, CastRole>();

      /// <summary>
      ///   Opens the cast with <paramref name="anchorId" /> as the sole active participant and every id in
      ///   <paramref name="witnessIds" /> as a witness. The anchor is never also listed as a witness; blank ids
      ///   and duplicates are ignored.
      /// </summary>
      public ConversationCast(string anchorId, IEnumerable<string> witnessIds = null)
      {
         AnchorId = anchorId;

         if (!string.IsNullOrEmpty(anchorId)) _roles[anchorId] = CastRole.Active;

         if (witnessIds == null) return;

         foreach (string id in witnessIds)
            if (!string.IsNullOrEmpty(id) && id != anchorId && !_roles.ContainsKey(id))
               _roles[id] = CastRole.Witness;
      }

      /// <summary>The character the conversation opened on. Always <see cref="CastRole.Active" />, never demoted.</summary>
      public string AnchorId { get; }

      /// <summary>How many participants are active. Two or more makes this an ensemble scene.</summary>
      public int ActiveCount => _roles.Values.Count(r => r == CastRole.Active);

      /// <summary>True once a witness has been drawn in, so two or more voices are active and every active one may speak the turn.</summary>
      public bool IsEnsemble => ActiveCount >= 2;

      /// <summary>The active participants, the anchor among them.</summary>
      public IReadOnlyList<string> ActiveIds => _roles.Where(kv => kv.Value == CastRole.Active).Select(kv => kv.Key).ToList();

      /// <summary>The passive witnesses.</summary>
      public IReadOnlyList<string> WitnessIds => _roles.Where(kv => kv.Value == CastRole.Witness).Select(kv => kv.Key).ToList();

      /// <summary>This character's role, or null if they are not in the scene at all.</summary>
      public CastRole? RoleOf(string heroId)
         => !string.IsNullOrEmpty(heroId) && _roles.TryGetValue(heroId, out CastRole role) ? role : (CastRole?) null;

      /// <summary>True when this character is a full participant.</summary>
      public bool IsActive(string heroId) => RoleOf(heroId) == CastRole.Active;

      /// <summary>True when this character is a witness at the edge.</summary>
      public bool IsWitness(string heroId) => RoleOf(heroId) == CastRole.Witness;

      /// <summary>
      ///   Adds a character who arrived mid-scene as a witness, so they can later be promoted. No-op for a blank
      ///   id or one already in the cast (their existing role is left untouched). Returns true only if they were newly added.
      /// </summary>
      public bool Introduce(string heroId)
      {
         if (string.IsNullOrEmpty(heroId) || _roles.ContainsKey(heroId)) return false;

         _roles[heroId] = CastRole.Witness;

         return true;
      }

      /// <summary>
      ///   Promotes a present witness to a full active participant (they were spoken to and are now in the
      ///   conversation). Returns true only when it actually changed a witness to active; a no-op (false) for the
      ///   anchor, someone already active, or a character not in the scene.
      /// </summary>
      public bool Promote(string heroId)
      {
         if (RoleOf(heroId) != CastRole.Witness) return false;

         _roles[heroId] = CastRole.Active;

         return true;
      }

      /// <summary>
      ///   Demotes a promoted participant back to a witness (they were told to hold their tongue and accepted).
      ///   Returns true only when it actually changed an active participant to witness. NEVER demotes the anchor
      ///   (the conversation's reason to exist), and a no-op (false) for a witness or an absent character.
      /// </summary>
      public bool Demote(string heroId)
      {
         if (heroId == AnchorId) return false;
         if (RoleOf(heroId) != CastRole.Active) return false;

         _roles[heroId] = CastRole.Witness;

         return true;
      }
   }
}
