// Code written by Gabriel Mailhot, 21/07/2026.
// Perception layer (no stripping/action here, that is a later increment): reads Hero.MainHero's TWO
// Bannerlord equipment sets into the flat facts PlayerDressPolicy (CalradiaRemembers.Logic) needs to
// classify the player's state of dress.
//
// READING RULE: a Bannerlord hero can be dressed from EITHER BattleEquipment or CivilianEquipment depending
// on context, and the whole point of this increment is that Naked must be reachable and must NOT be fooled by
// an empty BATTLE set while the CIVILIAN set still carries clothes (the coming stripping feature will empty
// BattleEquipment first; a captor who has not also emptied CivilianEquipment has not made the player naked).
// So torso/leg coverage is the UNION of both sets: a slot is "covered" the moment EITHER set shows something
// there, and only when BOTH sets are empty on BOTH slots does the player read as genuinely bare. Battle armour
// and garment tier are read the same way: real armour (ItemObject.IsCivilian == false) on either set reads
// Armoured outright; otherwise the highest CIVILIAN tier found on either set drives the Rags/Civilian split.
// Only the Body and Leg slots decide coverage: head/gloves/cape are accessories, not what makes someone
// dressed or bare (a hat alone does not stop a person from being naked).

#region

using System;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Resolves the player's live <see cref="PlayerDressBand" /> from Hero.MainHero's equipment. Never throws.</summary>
   public static class PlayerDressResolver
   {
      /// <summary>The torso/leg slots that decide coverage (see the file header for why only these two).</summary>
      private static readonly EquipmentIndex[] CoverageSlots = {EquipmentIndex.Body, EquipmentIndex.Leg};

      public static PlayerDressBand Resolve()
      {
         try
         {
            Hero player = Hero.MainHero;
            if (player == null) return PlayerDressBand.Civilian; // no live hero (main menu), unremarkable default

            SlotReading battle = ReadSlots(player.BattleEquipment);
            SlotReading civilian = ReadSlots(player.CivilianEquipment);

            bool covered = battle.Covered || civilian.Covered;
            bool armored = battle.Armored || civilian.Armored;
            int tier = Math.Max(battle.CivilianTier, civilian.CivilianTier);

            return PlayerDressPolicy.Classify(covered, armored, tier);
         }
         catch { return PlayerDressBand.Civilian; }
      }

      #region private

      /// <summary>
      ///   Reads the Body and Leg slots of ONE equipment set: whether either is occupied (<see cref="Covered" />),
      ///   whether either occupant is real battle armour rather than clothing (<see cref="Armored" />, from
      ///   <c>ItemObject.IsCivilian == false</c>), and the highest tier among the occupants that ARE civilian
      ///   clothing (<see cref="CivilianTier" />, meaningless once <see cref="Armored" /> is true).
      /// </summary>
      private static SlotReading ReadSlots(Equipment eq)
      {
         var reading = new SlotReading();
         if (eq == null) return reading;

         foreach (EquipmentIndex index in CoverageSlots)
         {
            ItemObject item = eq[index].Item;
            if (item == null) continue;

            reading.Covered = true;
            if (!item.IsCivilian)
               reading.Armored = true;
            else if ((int) item.Tier > reading.CivilianTier)
               reading.CivilianTier = (int) item.Tier;
         }

         return reading;
      }

      private struct SlotReading
      {
         public bool Covered;
         public bool Armored;
         public int CivilianTier;
      }

      #endregion
   }
}
