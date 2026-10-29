// Code written by Gabriel Mailhot, 28/10/2026.
// The world's initiative: every way the mod comes to the player unasked (a lord's audience, a road interception, a
// companion's word, a letter, a tale on the map, a war or law matter, a lord's aim) files a request, and one director
// decides whose turn it is. Gabriel (28/10/2026): "On doit laisser le joueur en immersion dans le jeu et pas le sortir trop
// souvent vers une gestion de conversation sans que le joueur l'ait initie de lui-meme. Cette regulation n'existe pas
// presentement car chaque sujet se gere lui-meme sans tenir compte de l'ensemble"; "Le regulateur devrait orchestrer a qui
// le tour [...] des regles de priorite [...] de la variete pour ne pas qu'un systeme ecrase les autres"; the player tunes
// it ("combien de systemes peuvent se manifester en meme temps, le delai avant que l'orchestrateur decide de lancer une
// intervention"); and "Il ne faut pas que les priorites empechent les autres [...] chaque categorie qui est dans la file
// d'attente puisse etre jouee une fois par mois". These are the plain values the rule reads.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Initiative
{
   /// <summary>Who is knocking: each family has its own weight, and each is guaranteed a turn a month.</summary>
   public enum InitiativeFamily
   {
      /// <summary>Lords who seek the player out: an audience where they stand, a word on the road.</summary>
      Lords,

      /// <summary>The player's companions asking for a word.</summary>
      Companions,

      /// <summary>The player's spouse and kin (an unhappy wife who has something to say).</summary>
      Household,

      /// <summary>Letters arriving.</summary>
      Letters,

      /// <summary>Written tales offered on the map.</summary>
      Tales,

      /// <summary>War and the law: summons, envoys, arrests, ambushes.</summary>
      WarAndLaw,

      /// <summary>A lord's aim of his own put to the player.</summary>
      Aims
   }

   /// <summary>How the request ranks: the world's imposition, a matter that presses, or the rest.</summary>
   public enum InitiativeRank
   {
      /// <summary>
      ///   The world imposes it (Gabriel, 28/10/2026: the arrest, the war duty, the ambush, an envoy with a short deadline):
      ///   it passes whenever the moment allows, unless the player asked urgent matters to wait their turn. It counts.
      /// </summary>
      Imposed,

      /// <summary>A reason that presses: a grudge, fresh news, an answer owed, a companion close to leaving.</summary>
      Pressing,

      /// <summary>Everything else: affection, a lord's aim, a tale on the map, a letter for its own sake.</summary>
      Ordinary
   }

   /// <summary>How readily the world comes to the player, as one dial over the fine settings.</summary>
   public enum InitiativePace
   {
      /// <summary>Seldom.</summary>
      Rarely,

      /// <summary>The default.</summary>
      Occasionally,

      /// <summary>Often.</summary>
      Often
   }

   /// <summary>How much a family that just had its turn steps back.</summary>
   public enum InitiativeVariety
   {
      /// <summary>A little.</summary>
      Low,

      /// <summary>By half.</summary>
      Normal,

      /// <summary>Far back.</summary>
      Strong
   }

   /// <summary>What a letter arriving costs from the attention budget.</summary>
   public enum LetterCost
   {
      /// <summary>As much as an audience.</summary>
      Full,

      /// <summary>Half: a letter closes with a click and waits in the book.</summary>
      Half,

      /// <summary>Nothing.</summary>
      None
   }

   /// <summary>One system's wish to come to the player.</summary>
   public sealed class InitiativeRequest
   {
      /// <summary>The filing system's stable key for this request (filing the same key again refreshes it).</summary>
      public string Key { get; init; } = "";

      /// <summary>Who is knocking.</summary>
      public InitiativeFamily Family { get; init; }

      /// <summary>How it ranks.</summary>
      public InitiativeRank Rank { get; init; } = InitiativeRank.Ordinary;

      /// <summary>The character it brings, if any: one character does not come twice in a row by two ways.</summary>
      public string CharacterId { get; init; }

      /// <summary>The campaign hour it was filed.</summary>
      public double FiledAtHour { get; init; }

      /// <summary>
      ///   The campaign hour its system last asked (asking again refreshes it and keeps <see cref="FiledAtHour" />). A system
      ///   asks at the door, so a request whose system stopped asking (the player left the town, the reason went) is not
      ///   live and never blocks the systems that are asking. 0 reads as <see cref="FiledAtHour" />.
      /// </summary>
      public double LastAskedAtHour { get; init; }

      /// <summary>A letter arriving, which costs what <see cref="InitiativeSettings.LettersCountAs" /> says.</summary>
      public bool IsLetter { get; init; }
   }

   /// <summary>A turn the director gave.</summary>
   public sealed class InitiativeGrant
   {
      /// <summary>The request's key.</summary>
      public string Key { get; init; } = "";

      /// <summary>Its family.</summary>
      public InitiativeFamily Family { get; init; }

      /// <summary>The character it brought, if any.</summary>
      public string CharacterId { get; init; }

      /// <summary>The campaign hour it was given.</summary>
      public double GrantedAtHour { get; init; }

      /// <summary>What it cost from the budget (1, a letter's share, or 0).</summary>
      public double Cost { get; init; } = 1;
   }

   /// <summary>The player's tuning (MCM "The World's Initiative"); a pace fills them all.</summary>
   public sealed class InitiativeSettings
   {
      /// <summary>How many systems may be at the player at the same time (a pursuit on the road, an audience waiting).</summary>
      public int AtOnce { get; init; } = 1;

      /// <summary>How many turns the budget allows per window.</summary>
      public double PerWindow { get; init; } = 3;

      /// <summary>The budget's window, in days.</summary>
      public int WindowDays { get; init; } = 7;

      /// <summary>The least time between two turns, in hours.</summary>
      public double MinGapHours { get; init; } = 12;

      /// <summary>How long the world waits once the player settles (arrives in a town, ends a battle or a scene), in hours.</summary>
      public double SettlingDelayHours { get; init; } = 2;

      /// <summary>The quiet after a conversation the player opened themselves, in hours.</summary>
      public double QuietAfterOwnTalkHours { get; init; } = 24;

      /// <summary>How much a family that just had its turn steps back.</summary>
      public InitiativeVariety Variety { get; init; } = InitiativeVariety.Normal;

      /// <summary>
      ///   Hours after its system last asked past which a request no longer counts as waiting at the door (systems tick
      ///   daily at the slowest, hence a day and a little more).
      /// </summary>
      public double StaleAfterHours { get; init; } = 26;

      /// <summary>Days after which a request still waiting is dropped (the world's impositions never are).</summary>
      public int ExpireDays { get; init; } = 10;

      /// <summary>What a letter arriving costs.</summary>
      public LetterCost LettersCountAs { get; init; } = LetterCost.Half;

      /// <summary>When true, the world's impositions wait their turn like the rest.</summary>
      public bool UrgentMattersWait { get; init; }

      /// <summary>
      ///   The longest a family waiting in the queue goes without a turn, in days (Gabriel, 28/10/2026: "une fois par mois").
      ///   Past it, the family's turn comes before any rank and past the budget.
      /// </summary>
      public int FairTurnDays { get; init; } = 30;

      /// <summary>Each family's weight in percent (0 turns it off; absent is 100).</summary>
      public IReadOnlyDictionary<InitiativeFamily, int> FamilyWeights { get; init; } = new Dictionary<InitiativeFamily, int>();
   }

   /// <summary>The moment the director is asked at.</summary>
   public sealed class InitiativeMoment
   {
      /// <summary>The campaign hour now.</summary>
      public double NowHour { get; init; }

      /// <summary>A battle, a mission, a chat or a menu holds the player: nothing comes now.</summary>
      public bool Busy { get; init; }

      /// <summary>Hours since the player last settled (arrived, ended a battle or a scene).</summary>
      public double HoursSinceSettled { get; init; } = double.MaxValue;

      /// <summary>Hours since the player last opened a conversation themselves.</summary>
      public double HoursSinceOwnTalk { get; init; } = double.MaxValue;

      /// <summary>How many systems are at the player right now (a lord riding after them, an audience under way).</summary>
      public int ActiveCount { get; init; }

      /// <summary>The campaign hour the director began keeping turns (a family never served counts its wait from here).</summary>
      public double KeptSinceHour { get; init; }
   }

   /// <summary>Why the director gave a turn, or why it gave none.</summary>
   public enum InitiativeVerdict
   {
      /// <summary>A turn was given.</summary>
      Granted,

      /// <summary>Nothing waits.</summary>
      NothingWaiting,

      /// <summary>A battle, a mission, a chat or a menu holds the player.</summary>
      Busy,

      /// <summary>The player has only just settled.</summary>
      Settling,

      /// <summary>As many systems as the player allows are already at them.</summary>
      AtOnceReached,

      /// <summary>The player has just had a conversation of their own.</summary>
      QuietAfterOwnTalk,

      /// <summary>The last turn was too recent.</summary>
      TooSoon,

      /// <summary>The window's budget is spent.</summary>
      BudgetSpent
   }

   /// <summary>Why a turn went to the request it went to.</summary>
   public enum InitiativeGround
   {
      /// <summary>No turn was given.</summary>
      None,

      /// <summary>The world imposed it.</summary>
      Imposed,

      /// <summary>Its family had waited a month for a turn.</summary>
      FairTurn,

      /// <summary>It ranked first and won the draw among its rank.</summary>
      Rank
   }

   /// <summary>The director's answer.</summary>
   public sealed class InitiativeDecision
   {
      /// <summary>Whether a turn was given, and if not, why.</summary>
      public InitiativeVerdict Verdict { get; init; }

      /// <summary>The request given the turn, or null.</summary>
      public InitiativeRequest Granted { get; init; }

      /// <summary>Why it was given the turn.</summary>
      public InitiativeGround Ground { get; init; }

      /// <summary>Requests that waited too long and are dropped.</summary>
      public IReadOnlyList<InitiativeRequest> Expired { get; init; } = new List<InitiativeRequest>();

      /// <summary>What the turn costs from the budget.</summary>
      public double Cost { get; init; }
   }
}
