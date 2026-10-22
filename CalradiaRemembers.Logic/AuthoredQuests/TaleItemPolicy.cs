// Code written by Gabriel Mailhot, 21/10/2026.
// Items in tales, the second phase tashmetu asked for (20/10/2026): "A tale that asks for things ('bring 30 grain to
// {giver}'), and a thing given in an ending." Gabriel approved it the same day, for a second phase "car ca peut etre plus
// complique". A `bring` objective is met when the player hands the goods over through the conversation's bag, the same
// picker a lord's supply task uses: nothing leaves the party until the player confirms, and a hand-over short of the
// count takes nothing (gating before declaration: the tale never says he brought what he did not). An `item`
// consequence gives the player one named item, never "the first of a kind", which would change with the load order.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>How a tale's goods are named, matched and handed over. Pure; the game's items are read through the seams given.</summary>
   public static class TaleItemPolicy
   {
      /// <summary>TUNE: the most a tale may ask for or give in one go (a lord's supply task stops there too).</summary>
      public const int MaxCount = ProvideGoodsPolicy.MaxCount;

      /// <summary>
      ///   The key the party's goods are matched against for a `bring`: a kind first (horses, livestock, grain and their
      ///   words), then an item category of the game ("wine", "iron"), then one exact item; null when it names none of them.
      ///   The game's trade goods carry one item each, named as their category, so "wine" reads as the category and any wine
      ///   counts.
      /// </summary>
      public static string BringKey(string raw, Func<string, bool> isCategory, Func<string, bool> isItem)
      {
         if (string.IsNullOrWhiteSpace(raw)) return null;
         string name = raw.Trim();
         string kind = ProvideGoodsPolicy.Normalize(name);
         if (kind != null) return kind;
         if (isCategory?.Invoke(name) == true) return ProvideGoodsPolicy.MarketKey(name);

         return isItem?.Invoke(name) == true ? ProvideGoodsPolicy.ItemKey(name) : null;
      }

      /// <summary>The item an `item` consequence gives: the one the file names, when the game has it; null otherwise.</summary>
      public static string GiftItemId(string raw, Func<string, bool> isItem)
         => string.IsNullOrWhiteSpace(raw) || isItem?.Invoke(raw.Trim()) != true ? null : raw.Trim();

      /// <summary>How many a hand-over takes: exactly the count asked when enough was offered; none when it falls short.</summary>
      public static int Taken(int offered, int asked) => asked > 0 && offered >= asked ? asked : 0;

      /// <summary>The one who receives a `bring`: the role the file names, the giver when it names none.</summary>
      public static string Receiver(AuthoredObjective o) => string.IsNullOrWhiteSpace(o?.To) ? "{giver}" : o.To.Trim();

      /// <summary>
      ///   What the loader can say of a tale's goods without the game: every `bring` and `item` names something, in a count
      ///   from 1 to <see cref="MaxCount" />. Whether the game knows the name is told by <see cref="UnknownItems" /> at load.
      /// </summary>
      public static IEnumerable<string> ShapeProblems(string where, string type, string item, int count)
      {
         if (string.IsNullOrWhiteSpace(item)) yield return $"{where}: {type} needs \"item\": what is asked or given, as the game names it (\"grain\", \"wine\", \"horses\").";
         if (count < 1 || count > MaxCount) yield return $"{where}: {type} needs a \"count\" from 1 to {MaxCount}.";
      }

      /// <summary>
      ///   The goods a tale names that this game does not know (a misspelt id, an item of a mod that is not loaded): a tale
      ///   naming one is never offered, rather than asking for goods nobody could bring or promising one nobody could give.
      /// </summary>
      public static IReadOnlyList<string> UnknownItems(AuthoredQuest q, Func<string, bool> isCategory, Func<string, bool> isItem)
      {
         var problems = new List<string>();
         if (q?.Stages == null) return problems;

         IEnumerable<AuthoredObjective> objectives = q.Stages.Where(s => s != null)
                                                      .SelectMany(s => new[] {s.Objective}.Concat((s.Choices ?? new List<AuthoredChoice>()).Where(c => c != null).Select(c => c.Objective)));
         foreach (AuthoredObjective o in objectives.Where(o => o?.Type == "bring" && !string.IsNullOrWhiteSpace(o.Item)))
            if (BringKey(o.Item, isCategory, isItem) == null)
               problems.Add($"quest '{q.Id}': bring asks for '{o.Item}', which is no kind (horses, livestock, grain), item category or item this game knows; the tale is not offered.");

         IEnumerable<AuthoredOutcome> outcomes = new[] {q.Reward, q.Failure}.Concat(q.Endings?.Values ?? Enumerable.Empty<AuthoredOutcome>())
                                                                            .Concat(objectives.Where(o => o?.Spawn?.OnDefeat != null).Select(o => o.Spawn.OnDefeat.AsOutcome()));
         IEnumerable<AuthoredConsequence> consequences = outcomes.Where(o => o != null).SelectMany(o => o.Consequences ?? new List<AuthoredConsequence>())
                                                                 .Concat(q.Stages.Where(s => s != null).SelectMany(s => s.OnEnter ?? new List<AuthoredConsequence>()));
         foreach (AuthoredConsequence c in consequences.Where(c => c?.Type == "item" && !string.IsNullOrWhiteSpace(c.Item)))
            if (GiftItemId(c.Item, isItem) == null)
               problems.Add($"quest '{q.Id}': the item consequence gives '{c.Item}', which is no item this game knows (an item's id, as \"wine\" or \"sumpter_horse\"); the tale is not offered.");

         return problems;
      }
   }
}
