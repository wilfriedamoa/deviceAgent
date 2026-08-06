using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    [Flags]
    internal enum EncodingMode
    {
        None = 0,
        Magnetic = 1,          // Mode 1 : Encodage bande magnétique (ISO1, ISO2, ISO3)
        SmartContact = 2,      // Mode 2 : Encodage puce à contact (ISO 7816)
        SmartContactless = 4   // Mode 3 : Encodage puce sans contact (RFID / NFC / DESFire)
    }
}
