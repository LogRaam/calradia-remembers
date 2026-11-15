// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (26/10/2026): "le LLM a tendance a aimer tous les gestes sexuels que le joueur peut decrire. Ce serait plus
// interessant si chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois des gestes qu'il n'aime
// pas. Par exemple un NPC pourrait particulierement aimer le sexe oral mais detester le sexe anal." Then (27/10/2026):
// "je dirais au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle aime moins". Then, the same day,
// the list widened ("Je trouve interessant d'en mettre beaucoup. Ca peut amener des scenes plus subtiles et complexes")
// and weighed by the character's nature ("les kinks pourraient fortement qualifier ou disqualifier certains gestes";
// "les traits influencent les kinks qui eux influencent les gestes"). So each character is given, once, two or three
// gestures they love, one or two they are less fond of, and about one in two a gesture they dislike outright (rarely two).
// A kink or a romantic preference makes its gestures likelier to be loved; a kink's own gesture is never disliked nor
// less loved (a masochist who dislikes rough handling told the model two opposite things); a nature that runs against a
// gesture (the dominant being led) makes it rarer to love and likelier to dislike, never impossible. Every other gesture
// is met with ordinary pleasure and is not written down.
// The roll is seeded by the hero's id with a stable hash (string.GetHashCode changes from one run to the next), so the
// same character rolls the same tastes; it is then kept in the save, so retuning this table never changes a character
// the player already knows.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>A character's tastes in bed, as rolled.</summary>
   public sealed class IntimateTastes
   {
      /// <summary>The gestures they love: two or three.</summary>
      public List<IntimateAct> Loved { get; init; } = new();

      /// <summary>The gestures they are less fond of: one or two.</summary>
      public List<IntimateAct> LessFond { get; init; } = new();

      /// <summary>The gestures they dislike outright, often none.</summary>
      public List<IntimateAct> Disliked { get; init; } = new();
   }

   /// <summary>Who is rolled: the character and the nature that weighs their gestures.</summary>
   public sealed class IntimateTasteFacts
   {
      /// <summary>The hero's id, the seed of the roll.</summary>
      public string HeroId { get; init; }

      /// <summary>The character is a woman.</summary>
      public bool IsFemale { get; init; }

      /// <summary>Their kinks (rolled from their traits): each makes its gestures likelier and never disliked.</summary>
      public IReadOnlyList<Kink> Kinks { get; init; } = new List<Kink>();

      /// <summary>Their romantic preferences (rolled from their traits): each makes its gestures likelier.</summary>
      public IReadOnlyList<RomanticPreference> Preferences { get; init; } = new List<RomanticPreference>();
   }

   /// <summary>The gestures a character loves, is less fond of and dislikes, rolled once from their id. Pure.</summary>
   public static class IntimateActPolicy
   {
      /// <summary>The roll this policy gives today (<see cref="RomanticProfile.TastesRevision" />).</summary>
      public const int Revision = 2;

      /// <summary>At most this many loved, less loved and disliked gestures.</summary>
      public const int MaxLoved = 3, MaxLessFond = 2, MaxDisliked = 2;

      /// <summary>Every gesture a character may love or dislike.</summary>
      public static readonly IReadOnlyList<IntimateAct> All = Enum.GetValues(typeof(IntimateAct)).Cast<IntimateAct>().Where(a => a != IntimateAct.None).ToList();

      /// <summary>The eleven gestures of the first roll (27/10/2026), the ones a profile of revision 0 was rolled among.</summary>
      public static readonly IReadOnlyList<IntimateAct> FirstEleven = All.Where(a => a <= IntimateAct.DirtyTalk).ToList();

      // A kink's own gestures: likelier loved, never disliked nor less loved.
      private static readonly IReadOnlyDictionary<Kink, IntimateAct[]> KinkGestures = new Dictionary<Kink, IntimateAct[]> {
         [Kink.Dominance] = new[] {IntimateAct.TakingTheLead, IntimateAct.OnTop},
         [Kink.Submission] = new[] {IntimateAct.BeingLed, IntimateAct.Restrained, IntimateAct.Beneath},
         [Kink.SwitchTendencies] = new[] {IntimateAct.TakingTheLead, IntimateAct.BeingLed},
         [Kink.Sadism] = new[] {IntimateAct.TakingTheLead},
         [Kink.Masochism] = new[] {IntimateAct.RoughHandling, IntimateAct.Spanking},
         [Kink.BondageGiving] = new[] {IntimateAct.TakingTheLead},
         [Kink.BondageReceiving] = new[] {IntimateAct.Restrained},
         [Kink.PowerImbalance] = new[] {IntimateAct.TakingTheLead, IntimateAct.BeingLed},
         [Kink.Exhibitionism] = new[] {IntimateAct.BeingUndressed, IntimateAct.Bathing},
         [Kink.Voyeurism] = new[] {IntimateAct.FaceToFace, IntimateAct.Teasing},
         [Kink.Possessiveness] = new[] {IntimateAct.BodyKisses},
         [Kink.PublicAffection] = new[] {IntimateAct.Kissing},
         [Kink.OrgasmControl] = new[] {IntimateAct.Teasing},
         [Kink.Chastity] = new[] {IntimateAct.Teasing},
         [Kink.FreeUse] = new[] {IntimateAct.Urgency},
         [Kink.Degradation] = new[] {IntimateAct.DirtyTalk},
         [Kink.Objectification] = new[] {IntimateAct.DirtyTalk},
         [Kink.PetPlay] = new[] {IntimateAct.BeingLed},
         [Kink.Praise] = new[] {IntimateAct.SweetWords},
         [Kink.ImpactPlay] = new[] {IntimateAct.Spanking},
         [Kink.SensoryDeprivation] = new[] {IntimateAct.Restrained, IntimateAct.Teasing},
         [Kink.FearPlay] = new[] {IntimateAct.RoughHandling},
         [Kink.MasterSlave] = new[] {IntimateAct.TakingTheLead, IntimateAct.BeingLed},
         [Kink.Breeding] = new[] {IntimateAct.Beneath, IntimateAct.FaceToFace},
         [Kink.Training] = new[] {IntimateAct.TakingTheLead, IntimateAct.Teasing},
         [Kink.CorruptionKink] = new[] {IntimateAct.Teasing, IntimateAct.DirtyTalk},
         [Kink.Prize] = new[] {IntimateAct.BeingUndressed}
      };

      // What runs against a kink: rarer to love, likelier to dislike.
      private static readonly IReadOnlyDictionary<Kink, IntimateAct[]> KinkAgainst = new Dictionary<Kink, IntimateAct[]> {
         [Kink.Dominance] = new[] {IntimateAct.BeingLed, IntimateAct.Restrained},
         [Kink.Submission] = new[] {IntimateAct.TakingTheLead},
         [Kink.Sadism] = new[] {IntimateAct.BeingLed},
         [Kink.BondageGiving] = new[] {IntimateAct.Restrained}
      };

      // A preference's gestures: likelier loved.
      private static readonly IReadOnlyDictionary<RomanticPreference, IntimateAct[]> PreferenceGestures = new Dictionary<RomanticPreference, IntimateAct[]> {
         [RomanticPreference.Dominant] = new[] {IntimateAct.TakingTheLead, IntimateAct.OnTop},
         [RomanticPreference.Submissive] = new[] {IntimateAct.BeingLed, IntimateAct.Beneath},
         [RomanticPreference.Possessive] = new[] {IntimateAct.BodyKisses, IntimateAct.RoughHandling},
         [RomanticPreference.Devoted] = new[] {IntimateAct.SweetWords, IntimateAct.LingeringAfter, IntimateAct.FaceToFace},
         [RomanticPreference.Reserved] = new[] {IntimateAct.Teasing, IntimateAct.Massage},
         [RomanticPreference.SlowBurn] = new[] {IntimateAct.Teasing, IntimateAct.Massage, IntimateAct.LingeringAfter},
         [RomanticPreference.Intense] = new[] {IntimateAct.Urgency, IntimateAct.RoughHandling},
         [RomanticPreference.Casual] = new[] {IntimateAct.Urgency},
         [RomanticPreference.MonogamousStrict] = new[] {IntimateAct.FaceToFace}
      };

      // What runs against a preference: rarer to love, likelier to dislike.
      private static readonly IReadOnlyDictionary<RomanticPreference, IntimateAct[]> PreferenceAgainst = new Dictionary<RomanticPreference, IntimateAct[]> {
         [RomanticPreference.Dominant] = new[] {IntimateAct.BeingLed},
         [RomanticPreference.Submissive] = new[] {IntimateAct.TakingTheLead},
         [RomanticPreference.Reserved] = new[] {IntimateAct.Urgency, IntimateAct.DirtyTalk},
         [RomanticPreference.SlowBurn] = new[] {IntimateAct.Urgency},
         [RomanticPreference.Casual] = new[] {IntimateAct.LingeringAfter},
         [RomanticPreference.Independent] = new[] {IntimateAct.LingeringAfter}
      };

      /// <summary>The weight of any gesture in the draw of loved gestures.</summary>
      private const int Base = 6;

      /// <summary>Added to a gesture's weight for each kink of theirs that holds it.</summary>
      private const int KinkBoost = 12;

      /// <summary>Added to a gesture's weight for each romantic preference of theirs that holds it.</summary>
      private const int PreferenceBoost = 6;

      /// <summary>The tastes of a character with no kink or preference known (a fresh id, a console roll).</summary>
      public static IntimateTastes Roll(string heroId, bool isFemale) => Roll(new IntimateTasteFacts {HeroId = heroId, IsFemale = isFemale});

      /// <summary>
      ///   The tastes of a character, weighed by their kinks and preferences. A woman is never given taking her partner
      ///   anally (the setting has no place for it); no gesture falls in two lists; a kink's own gesture is never disliked
      ///   nor less loved.
      /// </summary>
      public static IntimateTastes Roll(IntimateTasteFacts f)
      {
         var rng = new Random(StableSeed(f?.HeroId));
         int loved = rng.Next(100) < 70 ? 2 : 3;
         int lessFond = rng.Next(100) < 75 ? 1 : 2;
         int dislikeRoll = rng.Next(100);
         int disliked = dislikeRoll < 50 ? 0 : dislikeRoll < 85 ? 1 : 2;

         List<IntimateAct> open = All.Where(a => !(f?.IsFemale == true && a == IntimateAct.AnalGiving)).ToList();
         HashSet<IntimateAct> core = Core(f);
         HashSet<IntimateAct> against = Against(f);

         List<IntimateAct> lovedActs = Draw(open, loved, rng, a => against.Contains(a) ? 2 : Base + Boost(f, a));
         List<IntimateAct> dislikedActs = Draw(open, disliked, rng, a => core.Contains(a) ? 0 : against.Contains(a) ? Base * 3 : Favoured(f, a) ? 2 : Base);
         List<IntimateAct> lessFondActs = Draw(open, lessFond, rng, a => core.Contains(a) ? 0 : against.Contains(a) ? Base * 2 : Favoured(f, a) ? 2 : Base);

         return new IntimateTastes {Loved = lovedActs, LessFond = lessFondActs, Disliked = dislikedActs};
      }

      /// <summary>
      ///   The tastes of a profile rolled before the widened list (revision 0), brought up to date: a dislike that a kink
      ///   of theirs contradicts goes, unless the player already learned it (a taste the Heart named is never taken back);
      ///   then the gestures of a fresh roll that did not exist before are added, each list up to its maximum. What they
      ///   loved, were less fond of and disliked among the first eleven stays as it was.
      /// </summary>
      public static IntimateTastes Complete(IntimateTastes old, IEnumerable<IntimateAct> known, IntimateTasteFacts f)
      {
         var knownSet = new HashSet<IntimateAct>(known ?? Enumerable.Empty<IntimateAct>());
         HashSet<IntimateAct> core = Core(f);
         var loved = new List<IntimateAct>(old?.Loved ?? new List<IntimateAct>());
         var lessFond = new List<IntimateAct>((old?.LessFond ?? new List<IntimateAct>()).Where(a => !core.Contains(a) || knownSet.Contains(a)));
         var disliked = new List<IntimateAct>((old?.Disliked ?? new List<IntimateAct>()).Where(a => !core.Contains(a) || knownSet.Contains(a)));

         IntimateTastes fresh = Roll(f);
         void AddNew(List<IntimateAct> into, IEnumerable<IntimateAct> from, int max)
         {
            foreach (IntimateAct a in from.Where(a => !FirstEleven.Contains(a)))
               if (into.Count < max && !loved.Contains(a) && !lessFond.Contains(a) && !disliked.Contains(a))
                  into.Add(a);
         }

         AddNew(loved, fresh.Loved, MaxLoved);
         AddNew(disliked, fresh.Disliked, MaxDisliked);
         AddNew(lessFond, fresh.LessFond, MaxLessFond);

         return new IntimateTastes {Loved = loved, LessFond = lessFond, Disliked = disliked};
      }

      /// <summary>
      ///   The gesture a character rolled before the less loved gesture existed (27/10/2026) is less fond of: the first of
      ///   their own shuffled first eleven gestures not already loved or disliked, as the first roll would have given it.
      /// </summary>
      public static List<IntimateAct> LessFondFor(string heroId, bool isFemale, IEnumerable<IntimateAct> taken)
      {
         var takenSet = new HashSet<IntimateAct>(taken ?? Enumerable.Empty<IntimateAct>());

         return FirstPool(isFemale, new Random(StableSeed(heroId))).Where(a => !takenSet.Contains(a)).Take(1).ToList();
      }

      /// <summary>FNV-1a over the id: the same number on every run and every machine, unlike string.GetHashCode.</summary>
      public static int StableSeed(string heroId) => unchecked((int) Hashing.StableHash.Of(heroId));

      /// <summary>The gestures a character's kinks hold: never disliked nor less loved.</summary>
      public static HashSet<IntimateAct> Core(IntimateTasteFacts f)
         => new((f?.Kinks ?? new List<Kink>()).Where(KinkGestures.ContainsKey).SelectMany(k => KinkGestures[k]));

      /// <summary>The gestures a character's nature runs against, unless a kink of theirs holds them.</summary>
      public static HashSet<IntimateAct> Against(IntimateTasteFacts f)
      {
         var against = new HashSet<IntimateAct>((f?.Kinks ?? new List<Kink>()).Where(KinkAgainst.ContainsKey).SelectMany(k => KinkAgainst[k]));
         against.UnionWith((f?.Preferences ?? new List<RomanticPreference>()).Where(PreferenceAgainst.ContainsKey).SelectMany(p => PreferenceAgainst[p]));
         against.ExceptWith(Core(f));

         return against;
      }

      private static int Boost(IntimateTasteFacts f, IntimateAct a)
         => (f?.Kinks ?? new List<Kink>()).Count(k => KinkGestures.TryGetValue(k, out IntimateAct[] g) && g.Contains(a)) * KinkBoost
            + (f?.Preferences ?? new List<RomanticPreference>()).Count(p => PreferenceGestures.TryGetValue(p, out IntimateAct[] g) && g.Contains(a)) * PreferenceBoost;

      private static bool Favoured(IntimateTasteFacts f, IntimateAct a) => Boost(f, a) > 0;

      // Draws n gestures from open by weight, without putting one back; a weight of 0 is never drawn.
      private static List<IntimateAct> Draw(List<IntimateAct> open, int n, Random rng, Func<IntimateAct, int> weight)
      {
         var drawn = new List<IntimateAct>();
         for (int i = 0; i < n; i++)
         {
            List<(IntimateAct act, int w)> weighed = open.Select(a => (a, weight(a))).Where(x => x.Item2 > 0).ToList();
            int total = weighed.Sum(x => x.w);
            if (total == 0) break;

            int pick = rng.Next(total);
            foreach ((IntimateAct act, int w) in weighed)
            {
               if (pick < w)
               {
                  drawn.Add(act);
                  open.Remove(act);
                  break;
               }

               pick -= w;
            }
         }

         return drawn;
      }

      // The first roll's shuffled order over the first eleven gestures (the first draws of the seeded generator), kept for
      // LessFondFor so a profile of that time is given the gesture it would have been given.
      private static List<IntimateAct> FirstPool(bool isFemale, Random rng)
      {
         List<IntimateAct> pool = FirstEleven.Where(a => !(isFemale && a == IntimateAct.AnalGiving)).ToList();
         for (int i = pool.Count - 1; i > 0; i--)
         {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
         }

         return pool;
      }
   }
}
