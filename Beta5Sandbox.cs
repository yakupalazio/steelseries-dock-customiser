using System;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Beta 5 engineering builds never use Beta 4's user settings directory.
/// Physical writes to the dock require an explicit hardware-test switch.
/// These guards must be reviewed before preparing any release package.
/// </summary>
internal static class Beta5Sandbox
{
    internal static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser-Beta5-Development");

    internal static bool AllowPhysicalDockWrites =>
        Environment.GetCommandLineArgs().Contains("--allow-beta5-hardware-test",
            StringComparer.OrdinalIgnoreCase);
}
