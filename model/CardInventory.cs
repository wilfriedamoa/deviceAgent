using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class CardInventory: BaseM
    {
        public int FeederCassetteNo { get; set; }
        public string CardType { get; set; } = string.Empty;
        public int InitialQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public int LowThreshold { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
