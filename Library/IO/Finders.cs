using Microsoft.Win32;

namespace XQuinn.IO;


/// <summary>
/// Finds the EXE of a Steam Game that is stored in the Common folder. Only for Windows 64 bit.
/// </summary>
public static class SteamGameFinder //beleive there is a better way to do this incase they do not store the game in common folder but for now idk
{
    public static readonly string SteamCommonPath = Path.Combine(GetSteamPath(), @"steamapps\common");
    static readonly EnumerationOptions DefaultOption = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// exeName should be the file name without the .exe extension.
    /// </summary>
    public static string? Find(string exeName, EnumerationOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName, nameof(exeName));
        return Directory.EnumerateFiles(SteamCommonPath, $"{exeName}*.exe", options ?? DefaultOption).FirstOrDefault();
    }
    public static string FindOrThrow(string exeName, EnumerationOptions? options = null) => Find(exeName, options) ?? throw new FileNotFoundException(null, $"{exeName}.exe");

    static string GetSteamPath()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam")) //if 32 bit, must change this string
        {
            if (key?.GetValue("InstallPath") is string path) return path;
        }
        throw new DirectoryNotFoundException("Steam directory not found!");
    }
}


/// <summary>
/// Folder directory for the Vietnam War Mod Lab.
/// </summary>
internal static class VietnamWarModLab
{
    public static readonly string Path = Directory.EnumerateDirectories(CodeLabFinder.Path, "VietnamWarModLab", SearchOption.TopDirectoryOnly).FirstOrDefault() 
    ?? throw new DirectoryNotFoundException("VietnamWarModLab not found in C#Lab!");

    //could cause problems if a program exe is not located in vietnamwarmodlab, but all the related programs to vietnamwar are located there
    // static string BackupFinder()
    // {
    //     string dir = Directory.GetCurrentDirectory();
    //     string name = System.IO.Path.GetFileName(dir);
    //     while(name != "VietnamWarModLab")
    //     {
    //         dir = System.IO.Path.GetDirectoryName(dir)!;
    //         name = System.IO.Path.GetFileName(dir);
    //     }
    //     return dir;
    // }
}
/// <summary>
/// Folder directory for the Vietnam War game.
/// </summary>
internal static class VietnamWarSource
{
    public static readonly string Path = System.IO.Path.GetDirectoryName(SteamGameFinder.FindOrThrow("Vietnam War"))!;


}

internal static class CodeLabFinder
{
    public static readonly string Path = Directory.EnumerateDirectories(KnownFolders.Desktop, "C#Lab", SearchOption.TopDirectoryOnly).FirstOrDefault() 
    ?? throw new DirectoryNotFoundException("C#Lab not found on Desktop!");
}