[CmdletBinding()]
param(
    [string]$BrowserPath
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$work = Join-Path ([IO.Path]::GetTempPath()) ('XQuinn-docs-' + [Guid]::NewGuid().ToString('N'))
$screenshots = Join-Path $root 'Docs\Screenshots'
$utf8 = New-Object Text.UTF8Encoding($false)
New-Item -ItemType Directory -Path $work -Force | Out-Null
New-Item -ItemType Directory -Path $screenshots -Force | Out-Null

function Write-Utf8([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n"), $utf8)
}

function Invoke-Dotnet([string[]]$Arguments, [string]$LogPath) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet @Arguments 2>&1 | Out-File -LiteralPath $LogPath -Encoding utf8
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference }
    if ($exitCode -ne 0) {
        Get-Content -LiteralPath $LogPath -Tail 70 | Write-Host
        throw "dotnet failed; see $LogPath"
    }
}

if (-not $BrowserPath) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe')
    )
    $BrowserPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $BrowserPath -or -not (Test-Path -LiteralPath $BrowserPath)) {
    throw 'Microsoft Edge or Google Chrome is required. Pass -BrowserPath with its executable path.'
}

$declarationPath = Join-Path $root 'Library\Reflection\TypeRegister.cs'
$declaration = [IO.File]::ReadAllText($declarationPath)
if ($declaration -notmatch 'public static class TypeRegister' -or
    $declaration -match 'partial\s+class\s+TypeRegister') {
    throw 'TypeRegister must have the documented non-partial declaration.'
}

foreach ($configuration in @('Debug', 'Release')) {
    $log = Join-Path $work ("build-$configuration.log")
    Invoke-Dotnet -Arguments @(
        'build', (Join-Path $root 'XQuinn.csproj'),
        '-c', $configuration, '-f', 'net10.0-windows',
        '--artifacts-path', (Join-Path $work 'artifacts'),
        ('-p:OutputPath=' + (Join-Path $work ('artifacts\bin\XQuinn\' + $configuration.ToLowerInvariant()))),
        '-p:CopyLocalLockFileAssemblies=true',
        '--nologo', '--verbosity', 'minimal'
    ) -LogPath $log
    Write-Host "$configuration build passed. Log: $log"
}

$assembly = Get-ChildItem -LiteralPath (Join-Path $work 'artifacts\bin\XQuinn') -Recurse -Filter 'XQuinn.dll' |
    Where-Object { $_.FullName -match '[\\/]release[^\\/]*[\\/]' } | Select-Object -First 1
if (-not $assembly) { throw 'Release assembly was not found in the isolated artifacts directory.' }
$assemblyDirectory = $assembly.DirectoryName
$referenceItems = foreach ($name in @('XQuinn', '0Harmony', 'Mono.Reflection')) {
    $hint = [Security.SecurityElement]::Escape((Join-Path $assemblyDirectory "$name.dll"))
    "    <Reference Include=`"$name`"><HintPath>$hint</HintPath></Reference>"
}
$runner = Join-Path $work 'Runner'
New-Item -ItemType Directory -Path $runner | Out-Null
Write-Utf8 (Join-Path $runner 'Runner.csproj') @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
$($referenceItems -join "`n")
  </ItemGroup>
</Project>
"@

Write-Utf8 (Join-Path $runner 'Program.cs') @'
using System.Globalization;
using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Runtime;

static class Program
{
    static readonly CaptureLogger Logger = new();
    static int Checks;

    static void Main(string[] args)
    {
        string output = args[0];
        Expect<InvalidOperationException>(() => _ = Navigator.Logger);
        Navigator.Logger = Logger;
        Check(Logger.PrintToChat && !Logger.PrintEnumerableToChat, "Logger defaults");
        Logger.PrintToChat = false;

        string references = Capture(
            "+ greeting = \"Hello; world\"",
            "#greeting",
            "+ numbers = List<int>.new()",
            "#numbers",
            "Add(22)",
            "numbers",
            "?refs");
        Contains(references, "Reference: greeting");
        Contains(references, "Reference: numbers");
        Contains(references, "Object Type:");
        Contains(references, "Key: greeting");
        Contains(references, "Key: numbers");
        Contains(references, "Hello; world");
        Contains(references, "22");
        Save(output, "navigator-references", references);

        string queries = Capture(
            "$List<int>",
            "?methods(\"new\")",
            "?methods(\"Add\", public | instance)",
            "?props(\"Count\", public | instance)");
        Contains(queries, "Key: new ::");
        Contains(queries, "Key: new:1 ::");
        Contains(queries, "Key: Add ::");
        Contains(queries, "Key: Count ::");
        Save(output, "navigator-queries", queries);

        List<string> register = new() { "Declaration: public static class TypeRegister", "" };
        AddRegister(register, "Contains(\"LIST<T>\")", TypeRegister.Contains("LIST<T>"), true);
        AddRegister(register, "GetTypeOrThrow(\"list<T>\") == typeof(List<>)",
            TypeRegister.GetTypeOrThrow("list<T>") == typeof(List<>), true);
        AddRegister(register, "CacheType(typeof(DateTime), \"Clock\")",
            TypeRegister.CacheType(typeof(DateTime), "Clock"), true);
        AddRegister(register, "CacheType(typeof(DateTime), \"clock\")",
            TypeRegister.CacheType(typeof(DateTime), "clock"), false);
        AddRegister(register, "TryGetType(\"CLOCK\", out type) && type == typeof(DateTime)",
            TypeRegister.TryGetType("CLOCK", out Type? clock) && clock == typeof(DateTime), true);
        AddRegister(register, "CacheType(typeof(List<>), \"Items\")",
            TypeRegister.CacheType(typeof(List<>), "Items"), true);
        AddRegister(register, "GetTypeOrThrow(\"Items<T>\") == typeof(List<>)",
            TypeRegister.GetTypeOrThrow("Items<T>") == typeof(List<>), true);
        AddRegister(register, "GetTypeCached(\"MissingDocumentationType\") == null",
            TypeRegister.GetTypeCached("MissingDocumentationType") == null, true);
        Save(output, "type-register", string.Join('\n', register));

        Check(TypeRegister.CacheType<TimeSpan>(false), "Generic registration overload");
        Check(TypeRegister.CacheType<Guid>("DocumentationGuid"), "Explicit generic registration overload");
        Check(TypeRegister.CacheType(typeof(DateTimeOffset), false), "Derived-name registration");
        TypeRegister.CacheTypes(new[] { typeof(DateTime) }, false, "SystemTools.");
        Check(TypeRegister.GetTypeOrThrow("SystemTools.DateTime") == typeof(DateTime), "Prefix registration");
        TypeRegister.CacheTypes(new[] { typeof(TimeSpan), typeof(Guid) },
            type => type == typeof(TimeSpan) ? "DocumentationSpan" : null);
        Check(TypeRegister.Contains("DocumentationSpan"), "Delegate registration");
        Check(!TypeRegister.Contains("Guid"), "Null delegate key skips entry");
        Check(TypeRegister.Keys().Contains("Items<T>"), "Generic display key");
        Check(TypeRegister.Values.Contains(typeof(DateTime)), "Values");
        Check(TypeRegister.Enumerate().Any(pair => pair.Key == "Clock"), "Enumeration");
        Check(TypeRegister.GetCompatibleName(typeof(List<>), false) == "List", "Compatible generic name");
        Check(TypeRegister.CacheTypesSkipDuplicates(new[] { typeof(DateTime) }, false) == null,
            "No-conflict bulk result");
        TypeRegister.CacheType(typeof(DateTime), "Version");
        var conflicts = TypeRegister.CacheTypesSkipDuplicates(new[] { typeof(Version) }, false);
        Check(conflicts is { Count: 1 } && conflicts[0].key == "Version", "Duplicate reporting");
        Expect<NotSupportedException>(() => TypeRegister.CacheType(typeof(List<int>), "ClosedItems"));
        Check(!TypeRegister.CacheType(typeof(Fixture.Nested), "NestedFixture"), "Nested types skipped");
        foreach (string key in new[] { "", "1Bad", ".Bad", "Bad..Key", "Bad.1Key", "Items<T>", "this", "TRUE" })
            Expect<ArgumentException>(() => TypeRegister.CacheType(typeof(Guid), key));

        TypeRegister.CacheType(typeof(Fixture), "DocumentationFixture");
        Run("$DocumentationFixture");
        string inherited = Run("?methods(\"ToString\")");
        Contains(inherited, "ToString");
        string declared = Run("?methods(\"ToString\", public | instance | declaredonly)");
        Contains(declared, "No MethodBase found");
        string hidden = Run("?methods(\"Hidden\")");
        Contains(hidden, "No MethodBase found");
        Run("DocumentationFixture.Value = 15");
        Check(Fixture.Value == 15, "Static field assignment");
        Run("DocumentationFixture.Text = \"Updated\"");
        Check(Fixture.Text == "Updated", "Static property assignment");
        Run("Types.Num<int>(Types.Num<int>(22))");
        Run("Types.Num<int>(22 )");
        Run("Types.Num<char>('x' )");
        Run("DocumentationFixture.Echo(\"value\" )");
        Contains(Run("DocumentationFixture.Sum(1, 2, 3)"), "6");
        Run("DocumentationFixture.Sum()");
        Run("Types.Num<int>(default)");
        Run("Types.Num<int>()");
        Run("Types.Num<byte>(22)");
        Run("Types.Enum<BindingFlags>(public | instance)");
        Run("Types.Methods<Types>(\"Array\")");
        Run("Types.Of<List<int>>()");
        Run("Types.Of(\"List<T>\")");
        Run("Types.Methods<List<int>>(\"new\")");
        Run("Types.Fields<int>(null, public | instance)");
        Run("Types.Props<List<int>>(\"Count\", public | instance)");
        Run("Types.Props(Types.Of<List<int>>(), \"Count\", public | instance)");
        Run("""DocumentationFixture.Text = @"C:\Temp" """);
        Check(Fixture.Text == @"C:\Temp", "No-escape string");
        Run("""DocumentationFixture.Text = "quote: \"" """);
        Check(Fixture.Text == "quote: \"", "Quote escape");
        Run("""DocumentationFixture.Text = "slash: \\" """);
        Check(Fixture.Text == "slash: \\", "Backslash escape");
        Run("DocumentationFixture.Character = ';'");
        Check(Fixture.Character == ';', "Semicolon char");
        Run("DocumentationFixture.Character = '''");
        Check(Fixture.Character == '\'', "Apostrophe char");

        Navigator.Clear();
        Run("+ greeting = \"Hello; world\"");
        Run("#greeting");
        Run("+ greetingCopy");
        string rejected = Run("+ greetingCopy = \"blocked\"", allowException: true);
        Contains(rejected, "Cannot reassign a reference if it is the currently loaded instance.");
        Run("$object");
        Run("+ greeting = \"Updated\"");
        Contains(Run("greeting"), "Updated");
        Run("-greetingCopy");
        Run("+ greeting = null");
        Contains(Run("?refs"), "Enumerable is empty.");
        Run("+ valueReference = 22");
        Contains(Run("+ valueReference = null", allowException: true), "Cannot assign null");
        Run("-valueReference");
        Run("+ numbers = List<int>.new()");
        Run("*numbers");
        Run("Add(7)");
        Run("^IList<int>");
        Run("get_Item(0)");
        Run("^base");
        Run("-numbers");
        string noLabel = Run("this");
        Check(!noLabel.Contains("Reference: numbers"), "Removing loaded name clears label");
        Run("$object");
        Run("+ cleared = \"value\"");
        Navigator.Clear();
        Contains(Run("?vars"), "Enumerable is empty.");
        Check(TypeRegister.Contains("Clock"), "Navigator.Clear preserves TypeRegister");
        Check(ReferenceEquals(Navigator.Logger, Logger), "Navigator.Clear preserves logger");
        TypeRegister.CacheTypes(typeof(TypeRegister).Assembly.GetTypes().Where(type => type.IsPublic), false);
        TypeRegister.CacheTypes(typeof(TypeRegister).Assembly.GetTypes(),
            type => type.IsPublic ? "XQ." + TypeRegister.GetCompatibleName(type, false) : null);
        Check(TypeRegister.Contains("XQ.Navigator"), "Documented public assembly registration");

        Run("$Clock", "?props(\"Now\", public | static)");
        Run("+ message = \"Hello; world\"; #message");
        Navigator.Clear();
        Console.WriteLine($"Documentation checks passed: {Checks} assertions.");
    }

    static string Capture(params string[] commands)
    {
        Navigator.Clear();
        List<string> parts = new();
        foreach (string command in commands) parts.Add(Run(command));
        return string.Join('\n', parts);
    }

    static string Run(string command, bool allowException = false) => Run(new[] { command }, allowException);

    static string Run(string first, string second) => Run(new[] { first, second }, false);

    static string Run(string[] commands, bool allowException)
    {
        Logger.Lines.Clear();
        Navigator.Instruct(commands);
        string text = string.Join('\n', Logger.Lines);
        if (!allowException) Check(!text.Contains("!!! EXCEPTION"), string.Join("; ", commands) + "\n" + text);
        return text;
    }

    static void AddRegister(List<string> lines, string expression, bool actual, bool expected)
    {
        Check(actual == expected, expression);
        lines.Add("> " + expression);
        lines.Add(actual.ToString());
        lines.Add("");
    }

    static void Save(string directory, string name, string text) =>
        File.WriteAllText(Path.Combine(directory, name + ".txt"), text.Replace("\r\n", "\n").TrimEnd() + "\n");

    static void Contains(string text, string expected) => Check(text.Contains(expected), "Missing: " + expected + "\n" + text);

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Documentation assertion failed: " + message);
        Checks++;
    }

    static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { Checks++; return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}

sealed class CaptureLogger : NavigatorLogger
{
    public List<string> Lines { get; } = new();
    public override void ToLog(string text)
    {
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out _)) return;
        Lines.Add(text.Replace("\r\n", "\n"));
    }
    public override void ToChat(string text) { }
}

public class Fixture
{
    public static int Value;
    public static string Text { get; set; } = "";
    public static char Character;
    public static string Echo(string value) => value;
    public static int Sum(params int[] values) => values.Sum();
    [HideFromNavigator] public static void Hidden() { }
    public class Nested { }
}
'@

$runnerLog = Join-Path $work 'examples.log'
Invoke-Dotnet -Arguments @(
    'run', '--project', (Join-Path $runner 'Runner.csproj'), '-c', 'Release', '--', $screenshots
) -LogPath $runnerLog
Get-Content -LiteralPath $runnerLog | Write-Host

$revision = (& git -C $root rev-parse --short HEAD).Trim()
$captures = @(
    @{ Name = 'navigator-references'; Title = 'Navigator | saved references'; Sources = 'Library/Runtime/Navigator.cs | NavigatorCore.cs | NavigatorEngine/Reflector.cs' },
    @{ Name = 'navigator-queries'; Title = 'Navigator | member queries'; Sources = 'Library/Runtime/NavigatorCore.cs | NavigatorEngine/MethodKey.cs | NavigatorEngine/Types.cs' },
    @{ Name = 'type-register'; Title = 'TypeRegister | registration and lookup'; Sources = 'Library/Reflection/TypeRegister.cs | Library/Runtime/NavigatorEngine/GenericKey.cs' }
)
Add-Type -AssemblyName System.Drawing
foreach ($capture in $captures) {
    $text = [IO.File]::ReadAllText((Join-Path $screenshots ($capture.Name + '.txt')))
    $visualLines = 0
    foreach ($line in $text.TrimEnd().Split("`n")) {
        $visualLines += [Math]::Max(1, [Math]::Ceiling($line.Length / 130.0))
    }
    $height = 300 + $visualLines * 23
    $encoded = [Net.WebUtility]::HtmlEncode($text.TrimEnd())
    $title = [Net.WebUtility]::HtmlEncode($capture.Title)
    $sources = [Net.WebUtility]::HtmlEncode($capture.Sources)
    $html = @"
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>$title</title>
<style>
* { box-sizing: border-box; }
body { margin: 0; padding: 32px; background: #0b1220; color: #e5edf7; font: 16px 'Segoe UI', sans-serif; }
main { border: 1px solid #34445e; border-radius: 12px; overflow: hidden; }
header { padding: 20px 24px; background: #17243b; }
h1 { margin: 0 0 8px; font-size: 26px; }
p { margin: 0; color: #b9cbe2; }
pre { margin: 0; padding: 22px 24px; font: 16px/23px Consolas, monospace; white-space: pre-wrap; overflow-wrap: anywhere; }
footer { padding: 16px 24px; background: #17243b; color: #b9cbe2; font-size: 14px; line-height: 22px; }
</style></head><body><main><header><h1>$title</h1>
<p>Live checked output | .NET 10 / Windows x64 | source $revision + declaration/lexer fixes</p>
</header><pre>$encoded</pre><footer>Source references: $sources<br>
Reproduce: Docs/Tools/Update-Documentation.ps1 | Only per-command timestamps omitted</footer></main></body></html>
"@
    $page = Join-Path $work ($capture.Name + '.html')
    $png = Join-Path $screenshots ($capture.Name + '.png')
    Write-Utf8 $page $html
    $uri = ([Uri]$page).AbsoluteUri
    $profile = Join-Path $work ('browser-' + $capture.Name)
    $arguments = @(
        '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        '--no-default-browser-check', '--disable-extensions', '--disable-background-networking',
        '--force-device-scale-factor=1', '--run-all-compositor-stages-before-draw',
        '--virtual-time-budget=1500', "--window-size=1440,$height",
        "--user-data-dir=`"$profile`"", "--screenshot=`"$png`"", "`"$uri`""
    )
    $process = Start-Process -FilePath $BrowserPath -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput (Join-Path $work ($capture.Name + '-browser.out')) `
        -RedirectStandardError (Join-Path $work ($capture.Name + '-browser.err'))
    $null = $process.Handle
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Screenshot browser timed out.' }
    if (($null -ne $process.ExitCode -and $process.ExitCode -ne 0) -or -not (Test-Path -LiteralPath $png)) {
        throw "Screenshot capture failed for $($capture.Name); see $work"
    }
    $image = [Drawing.Image]::FromFile($png)
    try {
        if ($image.Width -ne 1440 -or $image.Height -ne $height) { throw "Wrong image dimensions: $png" }
        Write-Host "Captured $($capture.Name): $($image.Width)x$($image.Height)"
    }
    finally { $image.Dispose() }
}

$markdown = @((Join-Path $root 'README.md')) + @(
    Get-ChildItem -LiteralPath (Join-Path $root 'Docs') -File -Filter '*.md' | Select-Object -ExpandProperty FullName
)
$links = 0
foreach ($document in $markdown) {
    $text = [IO.File]::ReadAllText($document)
    $outsideCode = [regex]::Replace($text, '(?ms)^```.*?^```\s*$', '')
    $fences = [regex]::Matches($text, '(?m)^```').Count
    if ($fences % 2) { throw "Unclosed code block in $document" }
    foreach ($match in [regex]::Matches($outsideCode, '!?\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^[a-z]+://') { throw "Unexpected external link: $target" }
        $parts = $target.Split('#', 2)
        $relative = [Uri]::UnescapeDataString($parts[0])
        $destination = if ($relative) { Join-Path (Split-Path -Parent $document) $relative } else { $document }
        if (-not (Test-Path -LiteralPath $destination)) { throw "Broken link in ${document}: $target" }
        if ($parts.Length -eq 2) {
            $headings = [regex]::Matches([IO.File]::ReadAllText($destination), '(?m)^#{1,6} (.+)$')
            $anchors = foreach ($heading in $headings) {
                $anchor = $heading.Groups[1].Value.Trim().ToLowerInvariant()
                $anchor = [regex]::Replace($anchor, '[^\p{L}\p{N}_\-\s]', '')
                $anchor -replace '\s', '-'
            }
            if ($anchors -notcontains $parts[1]) { throw "Missing heading anchor in ${document}: $target" }
        }
        $links++
    }
    if ([regex]::IsMatch($text, '(?m)[ \t]+$')) { throw "Trailing whitespace in $document" }
}
Write-Host "Validated $links relative links/images/heading anchors across $($markdown.Count) Markdown files."
Write-Host "Documentation refresh passed. Temporary build logs and rendered pages: $work"