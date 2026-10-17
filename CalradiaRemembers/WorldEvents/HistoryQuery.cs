// Code written by Gabriel Mailhot, 17/10/2026.
// The read door of the world's long-term memory (Gabriel, 17/10/2026: "La memoire long terme servirait entre autre a
// definir les relations entre NPC.. et pourrait parfois etre utilise pour des conversations entre joueur et NPC sur le
// passe"). The regard policy asks what two lords share; a conversation asks what a lord knows of a person, a place or
// a realm. One query object, so neither caller can pass two heroes the wrong way round.

#region

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>What to read from the long-term tier (<see cref="WorldEventStore.GetHistory" />). Every criterion left null is not applied.</summary>
   public sealed class HistoryQuery
   {
      /// <summary>Only events naming this hero (an actor, or one it harmed).</summary>
      public Hero Involving { get; set; }

      /// <summary>With <see cref="Involving" />: only events naming both, the past a pair shares.</summary>
      public Hero AlsoInvolving { get; set; }

      /// <summary>Only events that happened at this settlement.</summary>
      public Settlement At { get; set; }

      /// <summary>Only events tagged with this realm (or clan, for a clan with no realm).</summary>
      public IFaction Realm { get; set; }

      /// <summary>Only events this hero would know (LongTermHistoryPolicy.WouldKnow). Null: every event, for tools.</summary>
      public Hero KnownTo { get; set; }

      /// <summary>At most this many, oldest first; 0 or less means no limit.</summary>
      public int Max { get; set; }
   }
}
