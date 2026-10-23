// Code written by Gabriel Mailhot, 22/10/2026.
// Gabriel (22/10/2026): "une liste ou chaque ligne serait avec le portrait, le statut et la raison. Les lignes amies
// pourraient avoir un marqueur vert pale et ennemis un marqueur rouge. Pour les maisons, [...] l'image du clan plutot que
// le portrait." The rows of EncyclopediaHeroPageRegardMixin. The portrait and the banner are the game's own encyclopedia
// elements (EncyclopediaSubPageElement over a HeroVM, EncyclopediaClanSubPageElement over an EncyclopediaFactionVM), so
// they link to the page and carry the game's tooltip as the Friends grid does.

#region

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>One friend or enemy: the portrait, the status beside it, the reason beneath, a pale green or red marker.</summary>
   public sealed class RegardPersonRowVM : ViewModel
   {
      public RegardPersonRowVM(Hero hero, string status, string reason, bool isFriend)
      {
         Person = new HeroVM(hero);
         Status = status ?? "";
         Reason = reason ?? "";
         IsFriend = isFriend;
      }

      /// <summary>The portrait, the game's own (it opens the character's page).</summary>
      [DataSourceProperty]
      public HeroVM Person { get; }

      /// <summary>"Manteos, enemy", or on the player's page how they hold the player.</summary>
      [DataSourceProperty]
      public string Status { get; }

      /// <summary>Why, in a line the player may read.</summary>
      [DataSourceProperty]
      public string Reason { get; }

      /// <summary>Pale green marker when true, red when false.</summary>
      [DataSourceProperty]
      public bool IsFriend { get; }

      public override void OnFinalize()
      {
         base.OnFinalize();
         Person?.OnFinalize();
      }
   }

   /// <summary>
   ///   One reason the houses share: their banners side by side, the reason above, a pale green or red marker; or one
   ///   house alone on the vanilla number, its banner then its line (Gabriel, 23/10/2026).
   /// </summary>
   public sealed class RegardHouseRowVM : ViewModel
   {
      public RegardHouseRowVM(string reason, bool isFriend, bool isOneHouse = false)
      {
         Reason = reason ?? "";
         IsFriend = isFriend;
         IsOneHouse = isOneHouse;
         Houses = new MBBindingList<RegardHouseVM>();
      }

      /// <summary>One house, its banner first and its line beside it, rather than a reason over banners.</summary>
      [DataSourceProperty]
      public bool IsOneHouse { get; }

      /// <summary>The reason the houses share.</summary>
      [DataSourceProperty]
      public string Reason { get; }

      /// <summary>Of affinity (pale green) rather than at odds (red).</summary>
      [DataSourceProperty]
      public bool IsFriend { get; }

      /// <summary>The houses of the row, strongest first.</summary>
      [DataSourceProperty]
      public MBBindingList<RegardHouseVM> Houses { get; }

      public override void OnFinalize()
      {
         base.OnFinalize();
         foreach (RegardHouseVM h in Houses) h.OnFinalize();
      }
   }

   /// <summary>One house of a row: the game's banner element, and a note beneath ("at war", "through Manteos").</summary>
   public sealed class RegardHouseVM : ViewModel
   {
      public RegardHouseVM(Clan clan, string note)
      {
         Faction = new EncyclopediaFactionVM(clan);
         Note = note ?? "";
      }

      /// <summary>The banner, the game's own (it opens the house's page).</summary>
      [DataSourceProperty]
      public EncyclopediaFactionVM Faction { get; }

      /// <summary>Under the house's name: at war, or the lord through whom it is explained above. Empty hides it.</summary>
      [DataSourceProperty]
      public string Note { get; }

      public override void OnFinalize()
      {
         base.OnFinalize();
         Faction?.OnFinalize();
      }
   }
}
