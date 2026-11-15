// Code written by Gabriel Mailhot, 14/11/2026.
// Extension Surface: chat portraits from another mod. Bhelogan (Detective's Notebook, Muh Prompts), 14/11/2026: "a portrait
// source for chat portraits, so a mod can supply a picture per hero without naming files by runtime id". A hero created in
// play has a per-save id (CharacterObject_N), so a picture dropped in ChatPortraits under that name means nothing in the next
// campaign. A source answers in code, for the hero in front of CR, with the path of its own image. Since 2.8.10.

#region

using System;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The door through which another mod gives heroes their chat portrait. Since 2.8.10.</summary>
   public static class CrPortraits
   {
      /// <summary>
      ///   Registers (or replaces) a portrait source named <paramref name="sourceMod" />. CR calls <paramref name="source" />
      ///   with a hero's StringId each time it shows that hero's portrait (a chat line, a council seat, a letter) and shows the
      ///   image whose full path it returns: a PNG or JPG that exists, square, about 512x512 or larger. Return null to leave
      ///   the hero to CR. The player's own ChatPortraits file still wins; a source wins over the realm portrait CR gives a
      ///   queen. Sources are asked in the order of their names. Answer fast and without side effects (it runs while a window
      ///   is drawn); a source that throws is passed over and logged once. CR keeps a loaded image by its path for the
      ///   session, so write a changed picture under a new file name. Game thread.
      /// </summary>
      public static void RegisterSource(string sourceMod, Func<string, string> source) => Portraits.ChatPortraitSources.Register(sourceMod, source);

      /// <summary>Removes the source named <paramref name="sourceMod" />. Whether one was there.</summary>
      public static bool UnregisterSource(string sourceMod) => Portraits.ChatPortraitSources.Unregister(sourceMod);

      /// <summary>The image CR would show for <paramref name="heroId" /> now (the player's file, a source, or a realm portrait), or null for the 3D render.</summary>
      public static string Resolve(string heroId, string displayName = null) => Portraits.ChatPortraitResolver.ResolveForHero(heroId, displayName);
   }
}
