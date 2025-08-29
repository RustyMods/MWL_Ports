using MWL_Ports.Managers;

namespace MWL_Ports;

public static class LocalKeys
{
    // organization to keep the localize keys within their own class
    public static readonly string ShipmentLabel = new Key("$label_shipment", "Shipment").GetKey();
    public static readonly string PortLabel = new Key("$label_port", "Port").GetKey();
    public static readonly string DeliveryLabel = new Key("$label_delivery", "Delivery").GetKey();
    public static readonly string ManifestLabel = new Key("$label_manifest", "Manifest").GetKey();
    public static readonly string OpenMapLabel = new Key("$label_open_map", "Open Map").GetKey();
    public static readonly string TeleportLabel = new Key("$label_teleport", "Teleport").GetKey();
    public static readonly string InTransitLabel = new Key("$label_in_transit", "In Transit").GetKey();
    public static readonly string DeliveredLabel = new Key("$label_delivered", "Delivered").GetKey();
    public static readonly string ExpiredLabel =  new Key("$label_expired", "Expired").GetKey();
    public static string ToKey(this ShipmentState state) => state switch
    {
        ShipmentState.InTransit => InTransitLabel,
        ShipmentState.Delivered => DeliveredLabel,
        ShipmentState.Expired => ExpiredLabel,
        _ => DeliveredLabel,
    };
    public class Key
    {
        private readonly LocalizeKey key;

        public string GetKey() => "$" + key.Key;

        public Key(string key, string english)
        {
            this.key = new LocalizeKey(key).English(english);
        }
    }
}