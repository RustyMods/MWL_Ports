using System.Collections.Generic;
using UnityEngine;
using static Terminal;

namespace MWL_Ports;

public static class Commands
{

    private static readonly List<Minimap.PinData> tempPins = new();
    public static void Setup()
    {
        ConsoleCommand shipments = new("mwl_shipments", "list of all shipments", args =>
        {
            foreach (Shipment shipment in ShipmentManager.Shipments.Values)
            {
                foreach (var log in shipment.LogPrint())
                {
                    Debug.Log(log);
                }
            }
        });

        ConsoleCommand ports = new ConsoleCommand("mwl_ports", "pins port locations on map", args =>
        {
            if (!Minimap.instance) return;
            foreach (var pin in tempPins)
            {
                Minimap.instance.RemovePin(pin);
            }
            tempPins.Clear();
            foreach (var port in ShipmentManager.GetPorts())
            {
                var pin = Minimap.instance.AddPin(port.GetPosition(), Minimap.PinType.Icon3, "port", false, false);
                tempPins.Add(pin);
            }
        });

        ConsoleCommand clearPins = new ConsoleCommand("mwl_clear_ports", "removes port pins from map", args =>
        {
            if (!Minimap.instance) return;
            foreach (var pin in tempPins)
            {
                Minimap.instance.RemovePin(pin);
            }
            tempPins.Clear();
        });
    }
}