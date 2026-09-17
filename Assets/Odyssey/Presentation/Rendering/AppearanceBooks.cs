#nullable enable
using Odyssey.Hud;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Builds a <see cref="ColonistAppearanceBook"/> for the catalogue the game actually loaded.
    ///
    /// <para><b>All that is left behind by the move.</b> The book itself went down into
    /// <c>Odyssey.Hud</c> on 2026-09-18 so that the HUD could ask what a colonist looks like
    /// (<c>docs/design/20-avatars.md</c>) and so that its tests could run in the fast tier. The one
    /// thing it could not take with it was counting the colonist family, because
    /// <see cref="ModuleCatalogue"/> is Unity and <c>Odyssey.Hud</c> is not. That count is this
    /// file, and it is the whole of the difference.</para>
    ///
    /// <para><b>Every row, holes included</b>, exactly as before: if the lottery ran over rows that
    /// happen to have usable art, installing a pack would silently re-deal the entire colony. One
    /// when there is no catalogue at all, so a clone without the licensed packs still answers
    /// rather than dividing by zero.</para>
    /// </summary>
    public static class AppearanceBooks
    {
        public static ColonistAppearanceBook For(uint seed, ModuleCatalogue? catalogue) =>
            new ColonistAppearanceBook(
                seed, catalogue == null ? 1 : catalogue.FindFamily(ModuleIds.ColonistBase).Count);
    }
}
