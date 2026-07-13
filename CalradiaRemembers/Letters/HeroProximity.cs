// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (SuperGraham339, 2026-07-10): a lady in the player's own ARMY kept sending "I miss you" letters.
// This is the letter system's name for "is this NPC effectively beside the player right now?" It now delegates
// to the single presence authority (PlayerPresenceResolver), so letters, chat and jealousy all read one truth.

#region

using CalradiaRemembers.Social;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>One authoritative answer to "is this NPC effectively beside the player right now?" (delegates to <see cref="PlayerPresenceResolver" />).</summary>
   public static class HeroProximity
   {
      /// <summary>
      ///   True when the NPC is travelling with the player (same party, a party the player leads, or a party
      ///   marching in the player's army) or is in the same town or castle as the player. Near people talk,
      ///   they do not post letters.
      /// </summary>
      public static bool IsNearPlayer(Hero hero) => PlayerPresenceResolver.IsBeside(hero);
   }
}
