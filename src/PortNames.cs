using System.Collections.Generic;
using System.Linq;
using MWL_Ports.Managers;

namespace MWL_Ports;

public static class PortNames
{
    private static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>
    {
        { "$MWL_PortName_Skjoldhavn", "Port of Skjoldhavn" },
        { "$MWL_PortName_Ravensvik", "Ravensvik Port" },
        { "$MWL_PortName_Drakkarsund", "Drakkarsund" },
        { "$MWL_PortName_Jotunfjord", "Port of Jotunfjord" },
        { "$MWL_PortName_Miststrand", "Miststrand Port" },
        { "$MWL_PortName_Ironvik", "Ironvik" },
        { "$MWL_PortName_Stormhavn", "Port of Stormhavn" },
        { "$MWL_PortName_Eirsholm", "Eirsholm Port" },
        { "$MWL_PortName_Njordhavn", "Njordhavn" },
        { "$MWL_PortName_Hrafnnes", "Port of Hrafnnes" },
        { "$MWL_PortName_Vargrvik", "Vargrvik Port" },
        { "$MWL_PortName_Skeldholm", "Skeldholm" },
        { "$MWL_PortName_Frostsund", "Port of Frostsund" },
        { "$MWL_PortName_Ulfsfjord", "Ulfsfjord Port" },
        { "$MWL_PortName_Runavik", "Runavik" },
        { "$MWL_PortName_Ormsvik", "Port of Ormsvik" },
        { "$MWL_PortName_Dyrhavn", "Dyrhavn Port" },
        { "$MWL_PortName_Eldersund", "Eldersund" },
        { "$MWL_PortName_Seidrholm", "Port of Seidrholm" },
        { "$MWL_PortName_Skaldhavn", "Skaldhavn Port" }
    };

    private static readonly List<string> UsedTokens = new();
    
    public static void Setup()
    {
        foreach (KeyValuePair<string, string> kvp in Tokens)
        {
            LocalizeKey key = new LocalizeKey(kvp.Key);
            key.English(kvp.Value);
        }
    }

    public static string GetRandomName()
    {
        string token = "";
        while (string.IsNullOrEmpty(token))
        {
            if (UsedTokens.Count >= Tokens.Count)
            {
                UsedTokens.Clear();
            }
            string? randomToken = Tokens.Keys.ToList()[UnityEngine.Random.Range(0, Tokens.Count)];
            if (UsedTokens.Contains(randomToken)) continue;
            token = randomToken;
            UsedTokens.Add(token);
        }
        return token;
    }
}