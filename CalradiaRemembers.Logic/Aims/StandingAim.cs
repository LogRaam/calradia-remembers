// Code written by Gabriel Mailhot, 14/10/2026.
// Lords with aims of their own (tashmetu, BC-CR bridge author, Nexus 14/10/2026, from two of his testers: "in Calradia,
// nobody takes the first step with the player"). Another mod says WHAT a lord wants; Calradia Remembers decides WHEN it
// surfaces and remembers that it did (Gabriel, 14/10/2026). This is the record one aim is kept as, plain values only so
// the mod can persist it as JSON under an additive save key and an old save simply reads no aims.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>The ways an aim may reach the player. A registering mod may narrow them; CR still paces each one.</summary>
   [Flags]
   public enum AimChannels
   {
      /// <summary>The aim may not surface at all (kept as knowledge only: the lord still wants it if asked).</summary>
      None = 0,

      /// <summary>The lord may bring it up himself in a conversation the player opens.</summary>
      Conversation = 1,

      /// <summary>The lord may write to the player about it, through the paced mailbox.</summary>
      Letter = 2,

      /// <summary>The aim is a reason for the lord to seek the player out (an audience at his fief, a word on the road).</summary>
      Audience = 4,

      /// <summary>Every channel: the default when a mod says nothing.</summary>
      All = Conversation | Letter | Audience
   }

   /// <summary>
   ///   One aim a lord holds: his own want, told in his voice, and what the player has to do with it. Raised at most
   ///   twice (see <see cref="AimRaisePolicy" />); the ledger fields record when it was.
   /// </summary>
   public sealed class StandingAim
   {
      /// <summary>The registering mod's stable id for this aim: dedup, update and retraction all go by it.</summary>
      public string Id { get; set; }

      /// <summary>The lord's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>The registering mod's tag, so a misbehaving source can be singled out without touching others.</summary>
      public string SourceMod { get; set; }

      /// <summary>The want itself, in the lord's own terms ("Lakonon. By right it is the Osticos', but ...").</summary>
      public string Want { get; set; }

      /// <summary>One line on what the player has to do with it ("his men can take what a claim alone cannot"). May be empty.</summary>
      public string PlayerPart { get; set; }

      /// <summary>
      ///   Optional MissionBoard mission id: when set, the service the lord asks for is a real, verified deed, so he may
      ///   ask for it outright (gating before declaration). Empty means he may only sound the player out. Calradia
      ///   Remembers' own strategist aims (30/10/2026) set it to their grounded quest menu task instead
      ///   ("quest:attack_lord:&lt;hero&gt;", "quest:attack_faction:&lt;realm&gt;"), equally real and verified.
      /// </summary>
      public string MissionId { get; set; }

      /// <summary>Where the aim may surface.</summary>
      public AimChannels Channels { get; set; } = AimChannels.All;

      /// <summary>The game day it was registered.</summary>
      public int RegisteredOnDay { get; set; }

      /// <summary>The last game day it stands; null when it stands until retracted.</summary>
      public int? ExpiresOnDay { get; set; }

      /// <summary>How many times the lord has raised it with the player, by any channel.</summary>
      public int TimesRaised { get; set; }

      /// <summary>The game day it was last raised; null when never.</summary>
      public int? LastRaisedDay { get; set; }

      /// <summary>The channel it was last raised through; None when never.</summary>
      public AimChannels LastRaisedChannel { get; set; }
   }
}
