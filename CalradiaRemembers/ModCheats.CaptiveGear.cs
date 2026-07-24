// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: console diagnostics so the holder ledger, the best-per-slot policy, and
// the recovery routes can all be exercised without a real fled escape or a 40-minute play session. Mirrors
// ModCheats.Duel.cs's "these exercise the REAL pipeline" philosophy: every command below calls the exact
// same CaptiveGearBehavior methods the live escape/recovery hooks call, never a reimplementation.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Duels;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.loot_stock
      ///   Dev-truth dump of the SCATTERED-loot ledger (AUDIT-NEMESIS M1): every piece of the player's gear
      ///   scattered among bandit bands after a fled bandit escape, its culture, and the day it was lost.
      ///   Recovery is per-battle: beat a band of a matching culture and one piece has a chance to return.
      ///   Read-only. (Distinct from cr.gear_stock, which is the LORD holder ledger.)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("loot_stock", "cr")]
      public static string LootStock(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (ScatteredLootBehavior.Instance == null) return "Scattered-loot system not active.";

         IReadOnlyList<ScatteredLootItem> all = ScatteredLootBehavior.Instance.All;
         if (all.Count == 0) return "No player gear is currently scattered among the bandits.";

         var sb = new StringBuilder();
         sb.AppendLine($"{all.Count} scattered piece(s) awaiting recovery (recovery chance {ScatteredLootPolicy.RecoveryChancePercent}% per matching band beaten):");
         foreach (ScatteredLootItem piece in all.OrderBy(p => p.LostDay))
            sb.AppendLine($"  {piece.Item?.ItemStringId ?? "(unknown)"} among {piece.CultureKey} (lost day {piece.LostDay})");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.gear_stock [name]
      ///   With a name: reports that hero's held-gear stock (every item, tier/value, and which are
      ///   currently "worn" per CaptiveGearStockPolicy.SelectWorn). Without a name: dev-truth dump of
      ///   EVERY holder currently on the ledger. Read-only.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("gear_stock", "cr")]
      public static string GearStock(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (CaptiveGearBehavior.Instance == null) return "Captive-gear system not active.";

         if (args == null || args.Count == 0)
         {
            IReadOnlyList<HolderGearStock> all = CaptiveGearBehavior.Instance.All;
            if (all.Count == 0) return "No holder currently keeps any escaped player gear.";

            var sb = new StringBuilder();
            sb.AppendLine($"{all.Count} holder(s) on the gear ledger:");
            foreach (HolderGearStock stock in all)
               sb.AppendLine($"  {DescribeHolder(stock.HolderHeroStringId)}: {stock.Items.Count} item(s)");

            return sb.ToString();
         }

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment.";

         return DescribeStock(npc, CaptiveGearBehavior.Instance.GetStock(npc.StringId));
      }

      /// <summary>
      ///   cr.gear_recover [name]
      ///   Forces CaptiveGearBehavior.RecoverAndGrantToPlayer for the resolved hero right now (the same
      ///   call the real "holder dies/is captured" hooks make): the WHOLE stock (worn + inventory) is
      ///   granted to the player's own roster and the ledger entry is cleared. A no-op, reported, when the
      ///   holder keeps nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("gear_recover", "cr")]
      public static string GearRecover(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (CaptiveGearBehavior.Instance == null) return "Captive-gear system not active.";

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         HolderGearStock before = CaptiveGearBehavior.Instance.GetStock(npc.StringId);
         if (before == null || before.Items.Count == 0) return $"{npc.Name} keeps no escaped player gear to recover.";

         int count = before.Items.Count;
         CaptiveGearBehavior.Instance.RecoverAndGrantToPlayer(npc);

         return $"Recovered {count} item(s) held by {npc.Name} into the player's inventory (see the log for any skipped, unresolvable items).";
      }

      /// <summary>
      ///   cr.gear_test [name]
      ///   Force-runs the REAL Fled transfer (CaptiveGearBehavior.TransferPlayerGearTo) against the
      ///   resolved hero, using the player's CURRENT battle equipment, without staging the escape mission.
      ///   Equip whatever you want to test with first. Follow with cr.gear_stock to see it accumulate, or
      ///   cr.gear_recover to test getting it back.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("gear_test", "cr")]
      public static string GearTest(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (CaptiveGearBehavior.Instance == null) return "Captive-gear system not active.";

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (npc == Hero.MainHero) return "Pick a holder, not the player.";

         bool ok = CaptiveGearBehavior.Instance.TransferPlayerGearTo(npc);
         if (!ok) return "Transfer failed: no player battle equipment resolved (no active campaign?).";

         HolderGearStock stock = CaptiveGearBehavior.Instance.GetStock(npc.StringId);

         return stock == null
            ? $"Nothing was equipped on the player to take. {npc.Name}'s stock is unchanged."
            : $"Transferred the player's current battle equipment to {npc.Name}. He now holds {stock.Items.Count} item(s) total.";
      }

      #region private

      private static string DescribeHolder(string holderHeroStringId)
      {
         Hero holder = Hero.FindFirst(h => h != null && h.StringId == holderHeroStringId);

         return holder?.Name?.ToString() ?? $"(unresolved hero '{holderHeroStringId}')";
      }

      private static string DescribeStock(Hero npc, HolderGearStock stock)
      {
         if (stock == null || stock.Items.Count == 0) return $"{npc.Name} keeps no escaped player gear on record.";

         IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(stock.Items);

         var sb = new StringBuilder();
         sb.AppendLine($"{npc.Name}: {stock.Items.Count} item(s) on record.");
         foreach (HeldItem item in stock.Items.OrderBy(i => i.Slot))
         {
            bool isWorn = worn.TryGetValue(item.Slot, out HeldItem w) && ReferenceEquals(w, item);
            string modifier = string.IsNullOrEmpty(item.ModifierStringId) ? "" : $" ({item.ModifierStringId})";
            sb.AppendLine($"  [{item.Slot}] {item.ItemStringId}{modifier} tier={item.Tier} value={item.Value}{(isWorn ? " <worn>" : " (inventory)")}");
         }

         return sb.ToString();
      }

      #endregion
   }
}
