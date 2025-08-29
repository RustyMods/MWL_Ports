using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MWL_Ports.tutorials;

public class PortTutorial
{
    private static readonly StringBuilder sb = new StringBuilder();

    internal static readonly List<PortTutorial> tutorials = new();
    public readonly string text;
    public readonly string label;
    
    private PortTutorial(string label, string resource) : this(label, LoadMarkdownFromAssembly(resource))
    {
    }

    private PortTutorial(string label, List<string> lines)
    {
        this.label = label;
        sb.Clear();

        foreach (string? line in lines)
        {
            sb.Append(MarkdownToRichText(line));
            sb.Append('\n');
        }
        text = sb.ToString();
        tutorials.Add(this);
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
        PortTutorial portTab = new PortTutorial("Port", "port.md");
        PortTutorial manifestTab = new PortTutorial("Manifest", "manifest.md");
        PortTutorial shipmentTab = new PortTutorial("Shipment",  "shipment.md");
        PortTutorial deliveryTab = new PortTutorial("Delivery",  "delivery.md");
        PortTutorial teleportTab = new PortTutorial("Teleport", "teleport.md");
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