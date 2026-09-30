// Code written by Gabriel Mailhot, 30/09/2026.
// The one door every character's ACTION goes through (first built for initiatives toward the player; renamed from
// NpcInitiativeGate on 30/09/2026 when Gabriel ruled that answering a summons is an action too, see below). Nexusrrr (Nexus, 28/09/2026): "my son is a
// 0-year-old baby, but he wants to discuss warfare with me." Gabriel the same day: "Serait-ce mieux de faire une
// composition ou toute action d'un NPC doit passer des conditions comme par exemple etre majeur? Si les conditions ne
// sont pas respectees, l'action ne se concretise pas."
//
// An inventory (30/09/2026) found no shared check: each path built its own filter, two of about a dozen checked age,
// and several leaned on Hero.IsLord, which the engine defines by occupation (Occupation == Lord), not by age, so an
// underage heir of a lord's house passes it. The widest, StanceConsequenceBehavior, could arm an assassination, force a
// scheme or write a threat from anyone alive, free and outside the player's clan.
//
// BASE conditions, for every initiative: the character is alive, of age, free, and not the player. Each path keeps its
// own conditions on top (a field party for a road interception, a lord's rank for an offer of service). What this gate
// does NOT cover: a character ANSWERING the player (a child may reply to a letter the player sent), being the subject
// of news, or standing in a conversation; and the verbs a model emits mid-conversation, which have their own gates.
//
// Maifailun (Nexus, 30/09/2026): a dead father sat at a council the player convened. Gabriel the same day: "s'asseoir
// a la table devrait etre une action". So the gate covers ACTIONS, not only initiatives: taking a council seat asks it
// too (CouncilSeat), with the council's own age (CouncilRosterPolicy.MinSeatAge) in place of the game's majority. A
// hero the game has taken out of the world (disabled) acts no more than the dead.

#region

using CalradiaRemembers.Logic.Assemblies;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The ways a character acts: toward the player on their own, or when summoned. Each path names its own.</summary>
   public enum ActionKind
   {
      /// <summary>An unprompted letter (any trigger), or one written straight into the post by a system.</summary>
      Letter,

      /// <summary>A lord riding out on the road to meet the player.</summary>
      RoadInterception,

      /// <summary>Someone standing in a town asking the player for a word.</summary>
      FiefAudience,

      /// <summary>A companion in the player's party asking for a private word.</summary>
      CompanionAudience,

      /// <summary>A consequence of a character's stance: a letter, a scheme, or an armed attempt on the player.</summary>
      StanceConsequence,

      /// <summary>Hired killers springing an attempt in a town on someone's behalf.</summary>
      Ambush,

      /// <summary>A lord relaying news of his realm by letter.</summary>
      RealmTidings,

      /// <summary>A seat on the council that tries the player.</summary>
      TrialSeat,

      /// <summary>
      ///   A seat at a council the player convenes: the player summons, the character comes and sits, and sitting is an
      ///   action (Gabriel, 30/09/2026). Of age here means the council's own age, 13 (CouncilRosterPolicy.MinSeatAge).
      /// </summary>
      CouncilSeat
   }

   /// <summary>Why an action did not happen.</summary>
   public enum ActionRefusal
   {
      /// <summary>It may happen.</summary>
      None,

      /// <summary>No character at all.</summary>
      NoOne,

      /// <summary>The character is dead.</summary>
      Dead,

      /// <summary>The character is the player.</summary>
      IsThePlayer,

      /// <summary>The character is a child: children answer, they do not take the initiative.</summary>
      Child,

      /// <summary>The character is a prisoner: a captive sends no courier and rides out to no one.</summary>
      Prisoner,

      /// <summary>The game has taken the character out of the world (disabled): no whereabouts, no action.</summary>
      OutOfTheWorld
   }

   /// <summary>The facts the gate reads about the character who would act.</summary>
   public sealed class ActorFacts
   {
      /// <summary>There is a character (false for a missing or unresolved one).</summary>
      public bool Exists { get; set; } = true;

      public bool IsAlive { get; set; } = true;

      /// <summary>Hero.IsChild: younger than the game's age of majority. Never inferred from rank.</summary>
      public bool IsChild { get; set; }

      public bool IsPrisoner { get; set; }

      /// <summary>Taken out of the world by the game (HeroState Disabled): not yet in play, or set aside by a mod.</summary>
      public bool IsDisabled { get; set; }

      /// <summary>Age in years, for a kind with its own age (a council seat). Null: only <see cref="IsChild" /> is known.</summary>
      public int? Age { get; set; }

      public bool IsPlayer { get; set; }
   }

   /// <summary>The base conditions of every action a character takes. Pure.</summary>
   public static class NpcActionGate
   {
      /// <summary>Why this character may not take the initiative, or None when they may. Checked in this order.</summary>
      public static ActionRefusal Check(ActorFacts f) => Check(f, ActionKind.Letter);

      /// <summary>
      ///   Why this character may not take this action, or None. Of age is the game's majority for every kind but a
      ///   council seat, which takes the council's own age when the age is known.
      /// </summary>
      public static ActionRefusal Check(ActorFacts f, ActionKind kind)
      {
         if (f == null || !f.Exists) return ActionRefusal.NoOne;
         if (!f.IsAlive) return ActionRefusal.Dead;
         if (f.IsDisabled) return ActionRefusal.OutOfTheWorld;
         if (f.IsPlayer) return ActionRefusal.IsThePlayer;
         if (!IsOfAgeFor(f, kind)) return ActionRefusal.Child;

         return f.IsPrisoner ? ActionRefusal.Prisoner : ActionRefusal.None;
      }

      /// <summary>True when this character may take the initiative toward the player.</summary>
      public static bool Allows(ActorFacts f) => Check(f) == ActionRefusal.None;

      /// <summary>True when this character may take this action.</summary>
      public static bool Allows(ActorFacts f, ActionKind kind) => Check(f, kind) == ActionRefusal.None;

      private static bool IsOfAgeFor(ActorFacts f, ActionKind kind)
         => kind == ActionKind.CouncilSeat && f.Age.HasValue
            ? CouncilRosterPolicy.IsOldEnoughToSit(f.Age.Value)
            : !f.IsChild;
   }
}
