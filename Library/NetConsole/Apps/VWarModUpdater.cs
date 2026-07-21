
using System.Collections.Immutable;
using System.Text;
using System.Xml.Serialization;
using XQuinn.IO;
using System.Diagnostics;

using System.ComponentModel;

namespace XQuinn.NetConsole.Apps;


internal class VWarModUpdater : IApp
{

    public static void Run()
    {
        Dictionary<string, string> roots = ProjectFinder.GetProjectRoots();
        List<Mod> mods = ModFetcher.FetchMods(roots);
        HashSet<Mod> needsUpdate = new ModSerializer().SaveAndGetUpdatedMods(mods);
        DLLUpdater.UpdatePlugins(roots, needsUpdate);

    }

}



//if i made some of these fields initializable/nonstatic then i could make this modupdater more modular and use it for all kinds of stuff
//only problem is dllupdater which performs builds, everything else just needs to be made more modular
//i can update qudmodupter to be more modular and boom, you can now use my types to build or copy any mod

public class BuildFailedException : Exception
{
    public BuildFailedException(string msg) : base(msg)
    {
        
    }
}

file static class DLLUpdater
{


    static readonly string TransferDirectory = Path.Combine(VietnamWarSource.Path, @"BepInEx\plugins");
    public static void UpdatePlugins(Dictionary<string, string> roots, HashSet<Mod> needsUpdate)
    {
        string[] names = [.. roots.Keys];
        foreach (var mod in needsUpdate)
        {
            if (names.Contains(mod.Name))
            {
                Console.WriteLine($"Updating {mod.Name}!");
                if (!BuildDLL(mod.Name, roots[mod.Name]))
                    throw new BuildFailedException($"Warning: DLL building process failed, aborting. Target project: {mod.Name}");
                string dllpath = @$"{roots[mod.Name]}\bin\Debug\net6.0\{mod.Name}.dll";
                string pluginpath = @$"{TransferDirectory}\{mod.Name}.dll";
                File.Copy(dllpath, pluginpath, true);
                Console.WriteLine($"{mod.Name}.dll succesfully copied to {pluginpath}!");
            }
        }
    }
    static bool BuildDLL(string name, string folder) //proccess.waitforexit?
    {
        // string path = @$"{folder}\{name}.csproj"; i was never actually using this i was just using the folder path
        //apparently both work but they work slightly differently, its fine here tho
        ProcessStartInfo build = new()
        {
            FileName = "dotnet",
            Arguments = $"build \"{folder}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(build);
        if (process != null)
        {
            Console.WriteLine($"Building {name}");
            string output = process.StandardOutput.ReadToEnd();
            string err = process.StandardError.ReadToEnd();
            process.WaitForExit();
            int code = process.ExitCode;
            if (code != 0)
            {
                Console.WriteLine(output);
                Console.WriteLine(err);
                Console.ReadLine();
                return false;
            }
        }
        else
        {
            Console.WriteLine($"Process is null when trying to build {name}!");
            Console.ReadLine();
            return false;
        }
        return true;
    }

}

file class Mod
{
    public List<CSFile> Files;
    public string Name;
    public string FolderPath;
    public Mod(List<CSFile> files, string name, string path)
    {
        Files = files;
        Name = name;
        FolderPath = path;
    }
}

[XmlRoot("CSFile")]
file class CSFile
{
    [XmlElement]
    public DateTime LastWrite = default!;

    [XmlElement]
    public string Path = null!;

    [XmlElement]
    public string Name = null!;

    CSFile()
    {

    }
    public CSFile(DateTime lastWrite, string path)
    {
        LastWrite = lastWrite;
        Path = path;
        Name = MakeName(path);
    }

    static string MakeName(string path)
    {
        string[] split = path.Split('\\');
        string name = split[^1];
        return name[..^".cs".Length];
    }
}

file static class ModFetcher
{
    public static List<Mod> FetchMods(Dictionary<string, string> roots)
    {
        List<Mod> mods = [];
        foreach (var proj in roots)
        {
            IEnumerable<string> filePaths = Directory.EnumerateFiles(proj.Value, "*.cs", SearchOption.AllDirectories).Where(path => !path.Contains(@"\obj\"));
            List<CSFile> csFiles = GetCSFiles(filePaths);
            mods.Add(new Mod(csFiles, proj.Key, proj.Value));
        }
        return mods;
    }

    static List<CSFile> GetCSFiles(IEnumerable<string> filePaths)
    {
        List<CSFile> modFiles = [];
        foreach (var path in filePaths)
        {
            FileInfo info = new(path);
            DateTime lastWrite = info.LastWriteTime;
            modFiles.Add(new CSFile(lastWrite, path));
        }
        return modFiles;
    }
}

file class ModSerializer
{
    static readonly string SaveLocation = Path.Combine(VietnamWarModLab.Path, @"UpdatePlugins2\ModData\VietnamWar");
    static readonly string[] SaveFolders = Directory.GetDirectories(SaveLocation);
    readonly HashSet<Mod> NeedUpdate = [];
    static readonly XmlSerializer Serializer = new(typeof(CSFile));

    //runs the serializer/deserializer, dumps mods that need to be updated
    public HashSet<Mod> SaveAndGetUpdatedMods(List<Mod> mods)
    {
        NeedUpdate.Clear();
        foreach (var mod in mods)
        {
            string path = GetSaveFolderPath(mod);
            Dictionary<string, string> saveData = GetSavedData(mod, path, out List<string> newFiles);
            CheckForUpdate(saveData, mod, newFiles);
        }
        return NeedUpdate;
    }

    //compares serialized data to cs file data, re-serializes if cs file data and serialized data arent equal
    //adds mods that have updated cs files to the dump
    void CheckForUpdate(Dictionary<string, string> saveData, Mod mod, List<string> newFiles)
    {
        bool update = false;
        foreach (var save in saveData)
        {
            XmlSerializer data = new(typeof(CSFile));
            CSFile modFile = mod.Files.First(file => file.Name == save.Key);
            CSFile? savedFile = null;
            if (!newFiles.Contains(save.Key)) //when new files are generated, they dont have xml roots, and it will create errors if it tries to deserialize it
            {
                using (var stream = new FileStream(save.Value, FileMode.Open))
                {
                    savedFile = data.Deserialize(stream) as CSFile;
                    if (savedFile != null)
                        Console.WriteLine($"Successfully deserialized {modFile.Name}!");
                }
            }
            if (savedFile == null || modFile.LastWrite != savedFile.LastWrite)
            {
                SerializeCSFile(save.Value, modFile);
                update = true;
            }
        }

        if (update)
        {
            NeedUpdate.Add(mod);
        }
    }

    static void SerializeCSFile(string path, CSFile file)
    {
        StreamWriter writer = new(path);
        Serializer.Serialize(writer, file);
        writer.Close();
        Console.WriteLine($"Serialized {file.Name}!");
    }

    //locates the mod's save folder and grabs the XML files that contain the serialized data for the CSFile objects of a mod
    //if they dont exist, it creates them
    Dictionary<string, string> GetSavedData(Mod mod, string saveFolder, out List<string> newFiles)
    {
        Dictionary<string, string> saveFiles = GetNamesWithPaths([.. Directory.GetFiles(saveFolder)]);// key is Name, Value is path
        CheckForDeletion(saveFiles, mod);
        newFiles = AddNewFiles(saveFiles, mod.Files, saveFolder);
        return saveFiles;
    }

    //detects if there is not a sibling xml data file for a cs file in a project, creates it
    static List<string> AddNewFiles(Dictionary<string, string> saveFiles, List<CSFile> csFiles, string path)
    {
        List<string> newfiles = [];
        HashSet<string> names = [.. saveFiles.Keys];
        foreach (var file in csFiles)
        {
            if (!names.Contains(file.Name))
            {
                string newFile = @$"{path}\{file.Name}.xml";
                using (var stream = File.Create(newFile))
                {
                    saveFiles[file.Name!] = newFile;
                }
                newfiles.Add(file.Name!);
                Console.WriteLine($"Adding {file.Name} to XML!!");
            }
        }
        return newfiles;
    }

    //if any xmls are created or deleted, the mod is added to the dump

    //detects if there is a leftover xml file for a cs file that was deleted from a project, deletes it
    void CheckForDeletion(Dictionary<string, string> saveFiles, Mod mod)
    {
        foreach (var save in saveFiles.ToArray())
        {
            if (!mod.Files.Any(cs => cs.Name == save.Key))
            {
                File.Delete(save.Value);
                saveFiles.Remove(save.Key);
                Console.WriteLine($"Deleting {save.Value} from XML!!");
                NeedUpdate.Add(mod);
            }
        }
    }

    //gets the full list of xml data files in the save folder
    static Dictionary<string, string> GetNamesWithPaths(IEnumerable<string> files)
    {
        Dictionary<string, string> namesAndPaths = [];
        foreach (var file in files)
        {
            string[] path = file.Split('\\');
            string sliced = path[^1][..^".xml".Length];
            namesAndPaths[sliced] = file;
        }
        return namesAndPaths;
    }


    //finds or creates the save folder for a mod if there isnt one
    static string GetSaveFolderPath(Mod mod)
    {
        string? path = CompareFolderNames(mod);
        if (path == null)
        {
            Console.WriteLine($"Creating save directroy for {mod.Name}");
            path = @$"{SaveLocation}\{mod.Name}";
            Directory.CreateDirectory(path);
        }
        else
            Console.WriteLine($"Found save dirctory for {mod.Name!}");
        return path;
    }

    //checks to see if a save folder for a mod exists
    static string? CompareFolderNames(Mod mod)
    {
        foreach (var folder in SaveFolders)
        {
            string[] path = folder.Split('\\');
            string name = path[^1];
            if (name == mod.Name)
                return folder;
        }
        return null;
    }

}


file static class ProjectFinder //this requires folder names, assembly names, and csproj names to be the same!
{
    static readonly string Root = VietnamWarModLab.Path;
    static readonly string[] _projectFiles = Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories);
    static string RecombinePath(string[] array) //can do root + projName, string concat, or substring, or path combine
    {
        StringBuilder path = new();
        for (int i = 0; i < array.Length - 1; i++)
        {
            path.Append(i > 0 ? $@"\{array[i]}" : array[i]);
        }
        return path.ToString();
    }
    public static Dictionary<string, string> GetProjectRoots()
    {
        Dictionary<string, string> projAndFolders = [];
        foreach (var proj in _projectFiles)
        {
            IEnumerable<string> projFileText = File.ReadLines(proj); //this is better to readalltext because its a stream, you can also use StreamReader for super large files
            if (projFileText.Any(text => text.Contains("<TargetFramework>net6.0"))) //Any and FirstOrDefault short circuit early so it doesnt read the entire steam
            {
                string[] path = proj.Split('\\');
                string projName = path[^1]; //this is path.length - 1
                string slicedName = projName.Substring(0, projName.Length - ".csproj".Length); // projName[..^".csproj".Length]; alternative
                projAndFolders[slicedName] = RecombinePath(path); //can also use path.combine(root, slicedname)
            }
        }
        return projAndFolders;
    }

}




// bool DLLDelegate(string dll)
// {
//     bool isNet6 = dll.IndexOf("net6.0") >= 0; //we check for net6 earlier by reading the csproj file
//     bool isNotRef = dll.IndexOf("ref") < 0;
//     return isNet6 && isNotRef;
// }

// public readonly struct ProjectData
// {
//     public readonly string DLLPath;

//     public readonly string Root;

//     public readonly string Name;
// }

// Dictionary<string, string> GetProjectDLLs()
// {
//     Dictionary<string, string> projDLLs = [];
//     foreach (var proj in _projectRoots)
//     {
//         string folder = proj.Value;
//         string name = proj.Key;
//         IEnumerable<string> dlls = Directory.EnumerateFiles(folder, $"{name}*.dll", SearchOption.AllDirectories);
//         string? dll = dlls.FirstOrDefault(dll => !dll.Contains("ref"));
//         if (dll == null)
//         {
//             Console.WriteLine($"Building {name}.dll for the first time.");
//             TryBuildDLL(proj.Key, proj.Value, out dll);
//         }
//         projDLLs[name] = dll!;
//     }
//     return projDLLs;
//