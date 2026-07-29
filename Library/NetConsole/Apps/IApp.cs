using System.Collections.Immutable;
using System.Reflection;
using XQuinn.IO;
using System.Diagnostics;
using XQuinn.Extensions;
using XQuinn.Reflection;
using System.Collections.Concurrent;


namespace XQuinn.NetConsole.Apps;

/// <summary>
/// This interface represents a type that serves as a complete and self-contained application for use in Console.
/// </summary>
internal interface IApp
{
    internal static readonly TypeBook Apps = GetApps();
    internal static bool IApps(string[] args)
    {
        if (args.Length == 1)
        {
            if (!IApp.Apps.Any(x => x.Key.EqualsCaseless(args[0])))
                return false;
            IApp.Select(args[0]); //this will take a string from string[] args to autoselect
            return true;

        }
        return false;
    }

    static TypeBook GetApps()
    {
        var types = typeof(IApp).Module.GetTypes().Where(x => x != typeof(IApp) && x.IsAssignableTo(typeof(IApp)));
        return TypeBook.New(types, false, Filter);

    }

    static string Filter(Type type)
    {
        string name;
        if (type.Name.IndexOf("__") is int num && num != -1)
            name = type.Name[(num + "__".Length)..];
        else
            name = type.Name;
        return name;
    }
    public static void Select(string choice)
    {
        // string? name = ConsoleTools.Choices(Names(), "Select an IApp to run.");
        // if (name == null) //we use string args now so there are no more choices lol
        //     return;
        Type iapp = Apps[choice];
        MethodInfo run = iapp.GetMethod("Run", BindingFlags.Static | BindingFlags.Public)!;
        run.Invoke(null, null);
        Console.ReadLine();
    }

    // static string[] Names()
    // {
    //     string[] arr = new string[Apps.Length];
    //     int i = 0;
    //     const string gap = "__"; //file types have messed up strings with double underscores preceding just before the actual type name
    //     foreach (string name in Apps.Select(x => x.Name))
    //     {
    //         if (name.IndexOf(gap) is int num && num != -1) //fancy contains
    //             arr[i] = name[(num + gap.Length)..]; //substring
    //         else
    //             arr[i] = name;
    //         i++;
    //         if (i == Apps.Length)
    //             break;
    //     }
    //     return arr;
    // }
    // Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
    // return assemblies.SelectMany(x => x.GetTypes()).Where(x => x.IsAssignableFrom(typeof(IApp))).ToImmutableDictionary(k => k.Name, v => v);
    //only BCL has IApps first of all, plus if BCL isnt executing, then its probably not a program that needs IApps since they only work in console

    public static abstract void Run(); //Every IApp has a static Run() method, similar to a Main() method, a Run() takes no parameters and should require 0 prior setup.


}                                       //IApps are ready to go and Run ecapsulates the entirety of their program's lifecycle



file class ResourcesReplacer : IApp
{
    static readonly string GameResourcesFolder = Path.Combine(VietnamWarSource.Path, "Vietnam War_Data");
    static readonly string BackupResourcesFolder = Path.Combine(VietnamWarModLab.Path, @"ResourcesGetter\BackupResources");
    static readonly string ModResourcesFolder = Path.Combine(VietnamWarModLab.Path, @"ResourcesGetter\ModResources");
    static readonly Dictionary<string, string> ModResources = Directory.GetFiles(ModResourcesFolder).Select(file => (Name: Path.GetFileName(file), Path: file)).ToDictionary();
    static readonly Dictionary<string, string> GameResources = GetResources(GameResourcesFolder);
    static readonly string[] Options = ["BACKUP", "RESTORE FROM BACKUP", "INSTALL"];
    public static void Run()
    {
        string? option = ConsoleTools.Choices(Options, "This program is for backing up Vietnam War assets and installing mod assets in their place.");
        if (option == null)
            return;
        if (option == Options[0])
        {
            if (Backup() == false)
                Console.WriteLine("Already backed up!");
        }
        else if (option == Options[1])
        {
            Console.WriteLine("Restoring from backup...");
            Restore();
        }
        else if (option == Options[2])
        {
            Backup();
            Console.WriteLine("Installing...");
            Copy(ModResources, key => GameResources[key]);
        }
        Console.ReadLine();
    }

    public static void Restore()
    {
        if (!Directory.EnumerateFiles(BackupResourcesFolder).Any())
            throw new FileNotFoundException("Backup resources folder is empty.");
        var backups = GetResources(BackupResourcesFolder);
        Copy(backups, key => Path.Combine(GameResourcesFolder, key));
    }

    static bool Backup()
    {
        if (!Directory.EnumerateFiles(BackupResourcesFolder).Any())
        {
            Console.WriteLine("Backing up...");
            Copy(GameResources, key => Path.Combine(BackupResourcesFolder, key));
            return true;
        }
        return false;

    }

    //here, dictionary obj is the resources you are copying from
    //expr is a func that creates the copy target
    //typically it is going to be a Path.Combine with your target folder path + the key of the current object selected in the dictionary
    //(which will be a filename key w/o a path)
    static void Copy(Dictionary<string, string> obj, Func<string, string> expr)
    {
        obj.ForEach(x =>
        {
            string copyTo = expr(x.Key);
            Console.WriteLine($"Copying {x.Key} from {x.Value} to {copyTo}!");
            File.Copy(x.Value, copyTo, true);
        });
    }
    static Dictionary<string, string> GetResources(string path)
    {
        string[] names = [.. ModResources.Keys];
        Dictionary<string, string> resources = new(names.Length);
        foreach (var file in Directory.EnumerateFiles(path, "*.assets", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (names.Contains(name))
            {
                resources[name] = file;
            }
        }
        return resources;
    }
}

file class PluginMaker : IApp
{
    static readonly string[] Insertions =
    [
      $"    <Reference Include=\"{InteropAssembly}\"/>",
      $"    <Reference Include=\"{Il2cppmscorlib}\"/>",
      $"    <Reference Include=\"{CoreModule}\"/>",
    ];
    static readonly string ModsRoot = VietnamWarModLab.Path;
    const string CoreModule = @"../interop\UnityEngine.CoreModule.dll";
    const string Il2cppmscorlib = @"../interop\Il2Cppmscorlib.dll";
    const string InteropAssembly = @"../interop\Assembly-CSharp.dll";

    public static void Run()
    {
        Console.WriteLine("Enter the name of your plugin!");

        PluginMaker maker = new();
        maker.Make();
    }
    public void Make()
    {
        string pluginName = EnterName();
        Console.WriteLine($"Are you sure you want the name of your plugin to be {pluginName}?");
        Console.WriteLine("Press ENTER for yes, press any other key to change the name.");
        if (Console.ReadKey().Key != ConsoleKey.Enter)
        {
            Console.WriteLine("Enter a new name:");
            Make();
        }
        else
            CreateDirectory(pluginName);
    }

    void CreateDirectory(string name)
    {
        string pluginPath = Path.Combine(ModsRoot, name!);
        Directory.CreateDirectory(pluginPath);
        CreateProject(name, pluginPath);
        AddInteropReference(name, pluginPath);
        Console.WriteLine($"{name} created in {pluginPath}");
    }

    static void AddInteropReference(string name, string path)
    {
        string csproj = $@"{path}\{name}.csproj"; //could use directory.getfile here
        List<string> text = GetAndModifyText(csproj, name);
        using (StreamWriter writer = new(csproj))
        {
            foreach (var txt in text)
                writer.WriteLine(txt);
        }
    }

    static List<string> GetAndModifyText(string csproj, string name)
    {
        List<string> text = [.. File.ReadAllLines(csproj)];
        int spot = text.IndexOf("  </ItemGroup>");
        foreach (var insert in Insertions)
            text.Insert(spot, insert);
        int nextspot = text.IndexOf("    <Product>My first plugin</Product>");
        string newstring = text[nextspot].Replace("My first plugin", name);
        text[nextspot] = newstring;
        return text;
    }



    void CreateProject(string name, string path)
    {
        ProcessStartInfo create = new()
        {
            FileName = "dotnet",
            Arguments = $"new bep6plugin_unity_il2cpp -n \"{name}\" -o \"{path}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (Process? process = Process.Start(create))
        {
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                int code = process.ExitCode;
                if (code != 0)
                {
                    Console.WriteLine(output);
                    Console.WriteLine(err);
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine("Process is null");
                Console.ReadLine();
            }
        }
    }

    public static string EnterName()
    {
        bool invalid = true;
        string? plugin = string.Empty;
        while (invalid)
        {
            plugin = Console.ReadLine();
            invalid = string.IsNullOrWhiteSpace(plugin);
        }
        return plugin!;
    }
}


file class InteropDLLGetter : IApp //The idea with this is that if i get a new pc, i dont need to update all my project references
{

    static readonly string Source = Path.Combine(VietnamWarSource.Path, @"BepInEx\interop");
    static readonly string CopyTo = Path.Combine(VietnamWarModLab.Path, "interop");
    static readonly string[] Assemblies = ["UnityEngine.CoreModule.dll", "Il2Cppmscorlib.dll", "Assembly-CSharp.dll"];

    public static void Run()
    {
        foreach (var assembly in Assemblies)
        {
            Console.WriteLine($"Copying {assembly}");
            File.Copy(Path.Combine(Source, assembly), Path.Combine(CopyTo, assembly), true);
        }
    }
}