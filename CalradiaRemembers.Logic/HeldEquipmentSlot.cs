// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: a pure, engine-agnostic mirror of TaleWorlds.Core.EquipmentIndex's 12
// real slots (the mod maps one to the other at the engine boundary). Kept as its own enum so the
// best-per-slot policy below never needs a TaleWorlds reference.

namespace CalradiaRemembers.Logic
{
    /// <summary>One equipped slot a holder can accumulate an item for. Mirrors EquipmentIndex's 12 real
    /// slots (Weapon0..ExtraWeaponSlot, Head/Body/Leg/Gloves/Cape, Horse/HorseHarness); the engine maps
    /// between the two, this project never references the game's own enum.</summary>
    public enum HeldEquipmentSlot
    {
        Weapon0,
        Weapon1,
        Weapon2,
        Weapon3,
        ExtraWeapon,
        Head,
        Body,
        Leg,
        Gloves,
        Cape,
        Horse,
        HorseHarness
    }
}
