using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace MWL_Ports.tutorials;

public class PortTutorial
{
    private static readonly string TutorialFolderPath = Paths.PluginPath + Path.DirectorySeparatorChar + "Tutorials";
    
    private static readonly StringBuilder sb = new StringBuilder();

    internal static readonly Dictionary<string, PortTutorial> tutorials = new();
    public string text;
    public string label;
    
    private PortTutorial(string label, string resource) : this(label, LoadMarkdownFromAssembly(resource))
    {
    }

    private PortTutorial(string label, List<string> lines)
    {
        if (!Directory.Exists(TutorialFolderPath)) Directory.CreateDirectory(TutorialFolderPath);
        this.label = label;
        string filePath = Path.Combine(TutorialFolderPath, label + ".md");
        if (File.Exists(filePath))
        {
            string[] newLines = File.ReadAllLines(filePath);
            text = CreateText(newLines.ToList());
        }
        else
        {
            text = CreateText(lines);
            File.WriteAllLines(filePath, lines);
        }
        tutorials[filePath] = this;
    }

    private PortTutorial(string label, string[] lines)
    {
        if (!Directory.Exists(TutorialFolderPath)) Directory.CreateDirectory(TutorialFolderPath);
        this.label = label;
        string filePath = Path.Combine(TutorialFolderPath, label + ".md");
        text = CreateText(lines.ToList());
        tutorials[filePath] = this;
    }

    private PortTutorial(string label)
    {
        this.label = label;
        text = string.Empty;
    }

    private static string CreateText(List<string> lines)
    {
        sb.Clear();

        foreach (string? line in lines)
        {
            sb.Append(MarkdownToRichText(line));
            sb.Append('\n');
        }
        return sb.ToString();
    }
    
    private static string MarkdownToRichText(string line)
    {
        // Bold: **text**
        line = Regex.Replace(line, @"\*\*(.+?)\*\*", "<b>$1</b>");

        // Italic: *text* or _text_
        line = Regex.Replace(line, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");
        line = Regex.Replace(line, "_(.+?)_", "<i>$1</i>");

        // Inline code: `text`
        line = Regex.Replace(line, "`(.+?)`", "<color=#d19a66><i>$1</i></color>");

        // Headers: #, ##, ###
        line = Regex.Replace(line, "^### (.+)$", "<size=18%><b>$1</b></size>");
        line = Regex.Replace(line, "^## (.+)$", "<size=14%><b>$1</b></size>");
        line = Regex.Replace(line, "^# (.+)$", "<size=10%><b>$1</b></size>");

        // Links: [text](url) → just display text in blue
        line = Regex.Replace(line, @"\[(.+?)\]\((.+?)\)", "<color=#61afef><u>$1</u></color>");
        
        // Unordered list: "- " → "• "
        line = Regex.Replace(line, @"^\-\s+", "• ");
        
        return line;
    }

    
    public static void Setup()
    {
        if (!Directory.Exists(TutorialFolderPath)) Directory.CreateDirectory(TutorialFolderPath);
        _ = new PortTutorial("Port", "port.md");
        _ = new PortTutorial("Manifest", "manifest.md");
        _ = new PortTutorial("Shipment",  "shipment.md");
        _ = new PortTutorial("Delivery",  "delivery.md");
        _ = new PortTutorial("Teleport", "teleport.md");
        foreach (string path in Directory.GetFiles(TutorialFolderPath, "*.md"))
        {
            if (tutorials.ContainsKey(path)) continue;
            _ = new PortTutorial(Path.GetFileNameWithoutExtension(path), File.ReadAllLines(path));
        }
        FileSystemWatcher watcher = new FileSystemWatcher(TutorialFolderPath, "*.md");
        watcher.EnableRaisingEvents = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.Changed += OnFileChange;
        watcher.Created += OnFileCreated;
        watcher.Deleted += OnFileDeleted;
        watcher.Renamed += OnFileRenamed;
    }

    private static void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        string? old = e.OldFullPath;
        string? path = e.FullPath;
        if (!tutorials.TryGetValue(old, out PortTutorial? tutorial)) return;
        tutorials.Remove(old);
        tutorial.label = Path.GetFileNameWithoutExtension(path);
        tutorials.Add(path, tutorial);
    }

    private static void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        string? path = e.FullPath;
        tutorials.Remove(path);
    }

    private static void OnFileChange(object sender, FileSystemEventArgs e)
    {
        string? path = e.FullPath;
        if (!tutorials.TryGetValue(path, out PortTutorial tutorial)) return;
        string[] lines = File.ReadAllLines(path);
        tutorial.text = CreateText(lines.ToList());
    }

    private static void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        string? path = e.FullPath;
        string[] lines = File.ReadAllLines(path);
        _ = new PortTutorial(Path.GetFileNameWithoutExtension(path), lines);
    }

    private static List<string> LoadMarkdownFromAssembly(string resourceName, string folder = "src.tutorials")
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string path = $"{MWL_PortsPlugin.ModName}.{folder}.{resourceName}";
        using Stream? stream = assembly.GetManifestResourceStream(path);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");

        using StreamReader reader = new StreamReader(stream);
        List<string> lines = new List<string>();
        while (!reader.EndOfStream)
        {
            lines.Add(reader.ReadLine() ?? string.Empty);
        }
        return lines;
    }
    
    
}