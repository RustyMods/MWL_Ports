using UnityEngine;

namespace MWL_Ports;

public static class Commands
{
    public static void Setup()
    {
        Terminal.ConsoleCommand command = new("shipments", "list of all shipments", args =>
        {
            foreach (Shipment shipment in ShipmentManager.Shipments.Values)
            {
                foreach (var log in shipment.LogPrint())
                {
                    Debug.Log(log);
                }
            }
        });
    }
}