#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Odyssey.Sim.Saving;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Where saves live on this machine (U38).
    ///
    /// <para><b>This is the half of U36 that was left undone, and it is deliberately tiny.</b> That
    /// unit put the recipe in the header and gave <c>WorldSave</c> a path to write to, and then
    /// stopped: its own commit message left the folder to "the caller (a menu)", because
    /// <c>Odyssey.Sim</c> is compiled without UnityEngine and cannot read
    /// <see cref="Application.persistentDataPath"/>. So the only thing this class knows is
    /// <i>where</i>. Everything about a save that is not a path — what a file is called, how a
    /// collision is resolved, how a folder is ordered, what happens to a file this build cannot
    /// open — is <see cref="SaveCatalogue"/>, in the assembly the fast tier can read, and none of
    /// it needs a filesystem to test.</para>
    ///
    /// <para>Static because there is one such folder per machine and nothing about it varies. It is
    /// not a service anybody would want two of.</para>
    /// </summary>
    public static class SaveFiles
    {
        /// <summary>The folder's name under the persistent data path.</summary>
        public const string FolderName = "Saves";

        /// <summary>
        /// Where saves go. Under <see cref="Application.persistentDataPath"/>, which is the one
        /// directory a built player is guaranteed to be able to write to on every platform this
        /// game could ship on — and, in the editor, a per-project folder outside the repository,
        /// so a playtest cannot leave save files in a working tree.
        /// </summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>
        /// The folder, made if it is not there yet.
        ///
        /// <para>On demand rather than at startup: a player who never saves should not have an
        /// empty folder appear in their application data, and the load screen reads a missing
        /// folder as "no saves" without needing one to exist.</para>
        /// </summary>
        public static string EnsureFolder()
        {
            string folder = Folder;
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Every save in the folder, newest first, with the unreadable ones kept and their reason
        /// beside them. A missing folder is an empty list, which to a player says the same thing.
        /// </summary>
        public static IReadOnlyList<SaveEntry> List() => SaveCatalogue.Read(Folder);

        /// <summary>
        /// Where a colony described by this recipe should be written, with a name that no file in
        /// the folder already has.
        ///
        /// <para><b>A new file every time, rather than overwriting the colony's last save.</b>
        /// There is no autosave and no rotation, so the only saves that exist are ones the player
        /// asked for, and quietly replacing the previous one would make Save a destructive act
        /// wearing the name of a safe one. The day is in the file name, so a folder of them reads
        /// as a history.</para>
        /// </summary>
        public static string PathFor(SaveRecipe recipe)
        {
            string folder = EnsureFolder();

            var taken = new List<string>();
            foreach (SaveEntry entry in SaveCatalogue.Read(folder)) taken.Add(entry.FileName);

            return Path.Combine(folder, SaveCatalogue.FileNameFor(recipe, taken));
        }

        /// <summary>
        /// When a save was written, as a row in the load list should say it (owner, 2026-09-17).
        ///
        /// <para><b>In the player's own local time and their own short formats</b>, not the UTC the
        /// catalogue records: the file's timestamp is a fact about the disk, and what the player
        /// wants to know is which evening this was. Formatting is done here, in the assembly that is
        /// allowed to know about a machine, and handed to <c>MenuDirector</c> as words — that is why
        /// <c>SaveRow.When</c> is a string.</para>
        ///
        /// <para>Date and time rather than "3 hours ago": a relative time is only readable while it
        /// is small, and the row it shares is already carrying the colony's own calendar in "Day
        /// 12". Two clocks in one line want to be told apart, not blurred together.</para>
        /// </summary>
        public static string WhenOf(SaveEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            DateTime local = entry.ModifiedUtc.ToLocalTime();
            return local.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// A save's own name, as a row in the load list should say it: the colony, or the file's
        /// stem when the header could not be read and there is no colony to name.
        /// </summary>
        public static string TitleOf(SaveEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            string? colony = entry.Header?.Recipe.ColonyName;
            return string.IsNullOrWhiteSpace(colony)
                ? Path.GetFileNameWithoutExtension(entry.FileName)
                : colony!;
        }
    }
}
