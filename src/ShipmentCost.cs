using System.Collections.Generic;

namespace MWL_Ports;

public class PortItem
{
    public string ChestName = "";
    public List<PortCost> Cost = new();

    
    
    
    public class PortCost
    {
        public string ItemName;
        public int Stack;

        public PortCost(string itemName, int stack)
        {
            ItemName = itemName;
            Stack = stack;
        }
    }
}