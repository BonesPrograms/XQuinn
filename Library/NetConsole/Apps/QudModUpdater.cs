
using XQuinn.IO;
using System.Xml.Linq;


namespace XQuinn.NetConsole.Apps;


internal class QudModUpdater : IApp
{

    public static void Run()
    {
        string locallow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        string copyTo = Path.Combine(locallow, @"Freehold Games\CavesOfQud\Mods");

        if (!Directory.Exists(copyTo))
            throw new DirectoryNotFoundException("Cannot find Qud Mods directory!");

        ModCopier copier = new(copyTo);
        foreach (var mod in ModDirectories.GetModDirectories())
        {
            copier.Update(mod);
            copier.Copy();
        }
    }
}

//ModCopier does not create mod folders, it requires a target mod folder to already exist.
//It will copy your manifest.json, preview.png, and workshop.json but will not create them.
//Organization is lost, all CS and XML files are dumped into the mod root.
//Only image files in the Textures folder have their directories recreated at copy destination.
file class ModCopier
{
    readonly string CopyRoot; //root for the overlying directory - mods will be copied to matching subdirectories in this directory
    string? CopyDest;
    string? ModPath; //mod directory
    const string Image = ".png";
    static readonly string[] CodeExtensions =
    [
      ".xml", ".cs", ".json"
    ];
    static readonly string[] ExcludedFolders = //explicitly exclude code by placing it into a "DoNotShip" folder
    [
      "DoNotShip", "obj", "bin", ".git", ".dotnet", ".vs", "Textures"
    ];
    public ModCopier(string copyroot)
    {
        if (!Directory.Exists(copyroot))
            throw new DirectoryNotFoundException($"Directory {copyroot} does not exist!");
        CopyRoot = copyroot;
    }
    public void Update(string mod)
    {
        if (!Directory.Exists(mod))
            throw new DirectoryNotFoundException($"Directory {mod} not found!");
        ModPath = mod;
        CopyDest = Path.Combine(CopyRoot, Path.GetFileName(mod));
    }

    public void Copy()
    {
        Delete();
        IEnumerable<string> files = Directory.EnumerateFiles(ModPath!, "*", SearchOption.AllDirectories);
        IEnumerable<string> codes = GetCode(files);
        Copy(codes, CodeCopyPath);
        IEnumerable<string> textures = GetTextures(files);
        CreateTextureDirectories(textures);
        Copy(textures, TextureCopyPath);
        IEnumerable<string> preview = GetPreview(files);
        Copy(preview, CodeCopyPath);
    }

    static void Copy(IEnumerable<string> paths, Func<string, (string, string)> expr)
    {
        IEnumerable<(string, string)> copyPaths = paths.Select(expr);
        foreach (var copy in copyPaths)
        {
            Console.WriteLine($"{copy.Item1} to {copy.Item2}");
            File.Copy(copy.Item1, copy.Item2, true);
        }
    }

    void CreateTextureDirectories(IEnumerable<string> textures)
    {
        string copyTextureDir = Path.Combine(CopyDest!, "Textures");
        if (!Directory.Exists(copyTextureDir))
            Directory.CreateDirectory(copyTextureDir);
        foreach (var path in textures)
        {
            string dir = Path.GetDirectoryName(path)!;
            string dirname = Path.GetFileName(dir);
            string copydir = Path.Combine(copyTextureDir, dirname);
            if (!Directory.Exists(copydir))
                Directory.CreateDirectory(copydir);
        }
    }

    static IEnumerable<string> GetPreview(IEnumerable<string> files)
    {
        return files.Where(file => Path.GetFileName(file) == "preview.png");
    }
    static IEnumerable<string> GetTextures(IEnumerable<string> files) //textures however will be placed into their specific directory
    {
        return files.Where(file => file.Contains("Textures") && Path.GetExtension(file) == Image);
    }
    static IEnumerable<string> GetCode(IEnumerable<string> files) //cs and xml files will simply just be dumped into the target mod folder
    {
        return files.Where(file => CodeExtensions.Contains(Path.GetExtension(file))).Where(file => !ExcludedFolders.Any(name => file.Contains(name)));
    }
    (string, string) TextureCopyPath(string sourcePath)
    {
        string subdir = sourcePath[sourcePath.IndexOf(@"Textures\")..];
        return (sourcePath, Path.Combine(CopyDest!, subdir));//as you can see it *expects* it to be an immediate subdirectory
    }
    (string, string) CodeCopyPath(string sourcePath)
    {
        return (sourcePath, Path.Combine(CopyDest!, Path.GetFileName(sourcePath)));
    }


    void Delete() //incase you change file names, or remove files, we just do a wholesale deletion of every file in the directory before transferring
    {
        IEnumerable<string> files = Directory.EnumerateFiles(CopyDest!, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            Console.WriteLine($"Deleting {file}");
            File.Delete(file);
        }
    }
    //it could be more in depth (only delete files that no longer exist or have name changes) 
    //but i didnt feel like doing a mod serializer for this one + its not really necessary (the mod serializer was used to check if we need to rebuild an entire DLL, which takes time)
    //building is not required here, so that is a nonissue, however
    //i have so many mods for qud i felt like it would be laggy sifting through 100s/1000s of files (mostly just project files and not actual mod code)
    //this one instead you just put in the name of the mod you want to copy in mods.xml so it only sifts through that mod directory

}

file static class ModDirectories
{
    readonly static string ModLab = Path.Combine(CodeLabFinder.Path, "QudModLab");
    readonly static string XmlPath = Path.Combine(ModLab, "mods.xml");
    static readonly XDocument XML = XDocument.Load(XmlPath); //will throw a filenotfoundexception of the modlab or mods.xml cannot be located
    public static List<string> GetModDirectories()
    {
        IEnumerable<string> subdirectories = Directory.EnumerateDirectories(ModLab);
        string[] xmlValues = [.. XML.Descendants("mod").Select(x => x.Value)];
        List<string> dirs = new(xmlValues.Length);
        foreach (var dir in subdirectories)
        {
            if (xmlValues.Contains(Path.GetFileName(dir)))
                dirs.Add(dir);
            if (dirs.Count >= xmlValues.Length)
                break;
        }
        return dirs;
    }

}
