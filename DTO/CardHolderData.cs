using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal record CardHolderData(
       [property: JsonPropertyName("cardNumber")] string CardNumber,
       [property: JsonPropertyName("FirstName")] string FirstName,
       [property: JsonPropertyName("LastName")] string LastName,
       [property: JsonPropertyName("ExpiryDate")] string ExpiryDate,
       [property: JsonPropertyName("PinCode")] string PinCode, // Code PIN 4 chiffres obligatoire
       [property: JsonPropertyName("EncodingMode")] EncodingMode EncodingMode,          // Sélection des modes d'encodage requis
       [property: JsonPropertyName("MagneticTracks")] MagneticTracksData? MagneticTracks, // Requis si Mode Magnetic activé
       [property: JsonPropertyName("ChipDataPayload")] string? ChipDataPayload             // Requis si Mode Contact ou Contactless activé
    );


    public record MagneticTracksData(
        [property: JsonPropertyName("track1")] string? Track1, // ISO1 Alphanumérique (Max 79)
        [property: JsonPropertyName("track2")] string? Track2, // ISO2 Numérique (Max 40)
        [property: JsonPropertyName("track3")] string? Track3  // ISO3 Numérique (Max 107)
    );

    /// <summary>
    /// Résultat détaillé délivré après le cycle matériel complet sur l'Evolis.
    /// </summary>
    public record CardIssuanceResult(
        bool Success,
        string? ChipUid,
        string ErrorCode,
        string Message
    );

    public enum ChipType
    {
        DesfireEV,      // MIFARE DESFire (EV1/EV2/EV3)
        Iso7816Native   // Carte à puce Native / JavaCard / EMV
    }
}
