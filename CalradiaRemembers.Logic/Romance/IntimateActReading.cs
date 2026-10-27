// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (26/10/2026): "un geste impose que le NPC n'aime pas devrait reduire le personal regard"; the player reads the
// rest in the roleplay ("sentir la temperature de la relation"). Whoever reads the turn's actions is told, at Hardcore and
// only in a lovemaking the game permits (never a captive scene), the gestures this character loves or dislikes, and
// reports one the player does or asks for. The game decides the rest: a disliked gesture the player DID costs personal
// regard and leaves a memory, once a day per gesture; one only asked for costs nothing (the character refused or showed
// unease); a loved gesture found the first time warms them a little. Either way the player now knows that taste.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>What the turn's reader is told of a character's tastes in bed.</summary>
   public sealed class IntimateActReaderFacts
   {
      /// <summary>The game is at Hardcore.</summary>
      public bool Hardcore { get; init; }

      /// <summary>The game permits intimacy with this character (IntimacyThresholdPolicy).</summary>
      public bool IntimacyPermitted { get; init; }

      /// <summary>A captive or forced scene: never read for tastes (forced acts belong to Trauma and Misdeeds).</summary>
      public bool ForcedScene { get; init; }

      /// <summary>The character's name.</summary>
      public string NpcName { get; init; }

      /// <summary>The gestures they love.</summary>
      public IReadOnlyList<IntimateAct> Loved { get; init; } = new List<IntimateAct>();

      /// <summary>The gestures they dislike.</summary>
      public IReadOnlyList<IntimateAct> Disliked { get; init; } = new List<IntimateAct>();
   }

   /// <summary>What a reported gesture is, as the game judges it.</summary>
   public sealed class IntimateActJudgeFacts
   {
      /// <summary>The gesture reported.</summary>
      public IntimateAct Act { get; init; }

      /// <summary>The player did it (true) or only asked for it (false).</summary>
      public bool Performed { get; init; }

      /// <summary>The game is at Hardcore.</summary>
      public bool Hardcore { get; init; }

      /// <summary>The game permits intimacy with this character.</summary>
      public bool IntimacyPermitted { get; init; }

      /// <summary>The gestures they love.</summary>
      public IReadOnlyList<IntimateAct> Loved { get; init; } = new List<IntimateAct>();

      /// <summary>The gestures they dislike.</summary>
      public IReadOnlyList<IntimateAct> Disliked { get; init; } = new List<IntimateAct>();

      /// <summary>The player already learned this taste of theirs.</summary>
      public bool AlreadyKnown { get; init; }

      /// <summary>This disliked gesture was already charged today: a night is one offence, not one per reply.</summary>
      public bool AlreadyChargedToday { get; init; }
   }

   /// <summary>What the game does with a reported gesture.</summary>
   public sealed class IntimateActVerdict
   {
      /// <summary>The change to the character's personal regard.</summary>
      public int RegardDelta { get; init; }

      /// <summary>The player now knows this taste of theirs.</summary>
      public bool Discovered { get; init; }

      /// <summary>A disliked gesture was imposed: a memory is left.</summary>
      public bool Imposed { get; init; }

      /// <summary>Nothing to do.</summary>
      public static readonly IntimateActVerdict Nothing = new();
   }

   /// <summary>Teaches the turn's reader the character's tastes, and judges a reported gesture. Pure.</summary>
   public static class IntimateActReading
   {
      /// <summary>The action a reader writes for a gesture.</summary>
      public const string Verb = "intimate_act";

      /// <summary>The gesture's id as the reader writes it ("oral_giving").</summary>
      public static string IdOf(IntimateAct act) => IntimateActNames.IdOf(act);

      /// <summary>The gesture an id names ("oral_giving", "OralGiving"), or None.</summary>
      public static IntimateAct Parse(string id) => IntimateActNames.Parse(id);

      /// <summary>The block that teaches the reader, or null when nothing is to be watched.</summary>
      public static string ForReader(IntimateActReaderFacts f)
      {
         if (f == null || !f.Hardcore || !f.IntimacyPermitted || f.ForcedScene) return null;
         List<IntimateAct> watched = f.Loved.Concat(f.Disliked).Where(a => a != IntimateAct.None).Distinct().ToList();
         if (watched.Count == 0) return null;

         string npc = string.IsNullOrWhiteSpace(f.NpcName) ? "the character" : f.NpcName;
         var sb = new StringBuilder();
         sb.AppendLine($"GESTURES THE GAME WATCHES FOR IN THIS EXCHANGE (lovemaking with {npc}; each named from {npc}'s side):");
         foreach (IntimateAct a in watched) sb.AppendLine($"- {IdOf(a)}: {Meaning(a, npc)}");
         sb.AppendLine("Report one ONLY when the player's own message in this exchange plainly does it with them or makes it happen "
                       + "(done: performed), or plainly asks them for it (done: proposed); never a hint, a fantasy or a memory, never "
                       + "what the character alone does unasked. One block per gesture:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: " + Verb);
         sb.AppendLine("act: <the gesture id>");
         sb.AppendLine("done: performed | proposed");
         sb.Append("[/ACTION]");

         return sb.ToString();
      }

      /// <summary>What the game does with a reported gesture.</summary>
      public static IntimateActVerdict Judge(IntimateActJudgeFacts f)
      {
         if (f == null || f.Act == IntimateAct.None || !f.Hardcore || !f.IntimacyPermitted) return IntimateActVerdict.Nothing;

         if (f.Disliked.Contains(f.Act))
         {
            bool imposed = f.Performed && !f.AlreadyChargedToday;

            return new IntimateActVerdict {
               RegardDelta = imposed ? DeedRegardPolicy.For(PlayerDeed.ImposedDislikedGesture).Victim : 0, Imposed = imposed, Discovered = !f.AlreadyKnown
            };
         }

         if (f.Loved.Contains(f.Act))
            return new IntimateActVerdict {
               RegardDelta = f.Performed && !f.AlreadyKnown ? DeedRegardPolicy.For(PlayerDeed.FoundLovedGesture).Victim : 0, Discovered = !f.AlreadyKnown
            };

         return IntimateActVerdict.Nothing;
      }

      /// <summary>The memory a character keeps of a disliked gesture imposed on them.</summary>
      public static string ImposedMemory(IntimateAct act, string playerName)
         => $"In bed, {playerName ?? "the player"} pressed {Plain(act)} on me, though I do not like it and let it show.";

      private static string Meaning(IntimateAct a, string npc) => a switch {
         IntimateAct.Kissing => $"long, deep kisses with {npc}",
         IntimateAct.OralGiving => $"{npc}'s mouth on the player",
         IntimateAct.OralReceiving => $"the player's mouth on {npc}",
         IntimateAct.AnalGiving => $"{npc} takes the player anally",
         IntimateAct.AnalReceiving => $"the player takes {npc} anally",
         IntimateAct.OnTop => $"{npc} on top, riding the player",
         IntimateAct.Beneath => $"{npc} beneath, the player above",
         IntimateAct.FromBehind => $"the player takes {npc} from behind",
         IntimateAct.HandsAndFingers => $"the player's hands and fingers on and in {npc}",
         IntimateAct.RoughHandling => $"the player bites, scratches, pulls the hair of or holds {npc} hard",
         IntimateAct.DirtyTalk => "crude words in the act",
         _ => ""
      };

      private static string Plain(IntimateAct a) => a switch {
         IntimateAct.Kissing => "long kisses",
         IntimateAct.OralGiving => "taking them in my mouth",
         IntimateAct.OralReceiving => "their mouth on me",
         IntimateAct.AnalGiving => "taking them anally",
         IntimateAct.AnalReceiving => "taking me anally",
         IntimateAct.OnTop => "riding them",
         IntimateAct.Beneath => "lying beneath them",
         IntimateAct.FromBehind => "taking me from behind",
         IntimateAct.HandsAndFingers => "their fingers in me",
         IntimateAct.RoughHandling => "rough handling",
         IntimateAct.DirtyTalk => "crude words",
         _ => "something"
      };
   }
}
