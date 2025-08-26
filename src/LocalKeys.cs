using MWL_Ports.Managers;

namespace MWL_Ports;

public static class LocalKeys
{
    // organization to keep the localize keys within their own class
    public static readonly string ShipmentLabel = new Key("$label_shipment", "Shipment").GetKey();
    public static readonly string PortLabel = new Key("$label_port", "Port").GetKey();
    public static readonly string DeliveryLabel = new Key("$label_delivery", "Delivery").GetKey();

    private class Key
    {
        private readonly LocalizeKey key;

        public string GetKey() => "$" + key.Key;

        public Key(string key, string english)
        {
            this.key = new LocalizeKey(key).English(english);
        }
    }
}