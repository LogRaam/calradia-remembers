// Code written by Gabriel Mailhot, 30/09/2026.
// The one door every character's INITIATIVE toward the player goes through. Nexusrrr (Nexus, 28/09/2026): "my son is a
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

namespace CalradiaRemembers.Logic
{
   /// <summary>The ways a character takes the initiative toward the player. Each path names its own.</summary>
   public enum InitiativeKind
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
      TrialSeat
   }

   /// <summary>Why an initiative did not happen.</summary>
   public enum InitiativeRefusal
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
      Prisoner
   }

   /// <summary>The facts the gate reads about the character taking the initiative.</summary>
   public sealed class InitiatorFacts
   {
      /// <summary>There is a character (false for a missing or unresolved one).</summary>
      public bool Exists { get; set; } = true;

      public bool IsAlive { get; set; } = true;

      /// <summary>Hero.IsChild: younger than the game's age of majority. Never inferred from rank.</summary>
      public bool IsChild { get; set; }

      public bool IsPrisoner { get; set; }

      public bool IsPlayer { get; set; }
   }

   /// <summary>The base conditions of every initiative toward the player. Pure.</summary>
   public static class NpcInitiativeGate
   {
      /// <summary>Why this character may not take the initiative, or None when they may. Checked in this order.</summary>
      public static InitiativeRefusal Check(InitiatorFacts f)
      {
         if (f == null || !f.Exists) return InitiativeRefusal.NoOne;
         if (!f.IsAlive) return InitiativeRefusal.Dead;
         if (f.IsPlayer) return InitiativeRefusal.IsThePlayer;
         if (f.IsChild) return InitiativeRefusal.Child;

         return f.IsPrisoner ? InitiativeRefusal.Prisoner : InitiativeRefusal.None;
      }

      /// <summary>True when this character may take the initiative toward the player.</summary>
      public static bool Allows(InitiatorFacts f) => Check(f) == InitiativeRefusal.None;
   }
}
