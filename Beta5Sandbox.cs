using System;
using System.IO;
// Only personal settings isolation belongs here.

namespace SteelseriesDockCustomiser;

/// <summary>
/// Beta 5 engineering builds never use Beta 4's user settings directory.
/// Hardware output is initiated from an explicit, user-confirmed OLED UI
/// action; data files are always isolated from the installed Beta 4 build.
/// </summary>
internal static class Beta5Sandbox
{
    internal static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser-Beta5-Development");

}
