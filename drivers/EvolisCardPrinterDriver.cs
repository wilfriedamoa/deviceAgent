using deviceAgent.DTO;
using Evolis;
using PCSC;
using PCSC.Iso7816;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;


namespace deviceAgent.drivers
{
    internal class EvolisCardPrinterDriver : IDisposable
    {
        private readonly ILogger<EvolisCardPrinterDriver> _logger;
        private readonly SemaphoreSlim _hardwareLock = new(1, 1);
        private  Connection? _evolisConnection;
        private  string? _printerName; // Nom du périphérique Evolis "Evolis Primacy"
         private readonly string _pcscReaderName = "Evolis Dual Smart Card Reader 0"; // Lecteur PC/SC interne
         public bool IsConnected => VerifyPrinterConnection();

        public EvolisCardPrinterDriver(ILogger<EvolisCardPrinterDriver> logger)
        {
            _logger = logger;
            InitializeDriver();
        }

        /// <summary>
        /// Initialise la connexion USB avec la première imprimante Evolis détectée.
        /// </summary>
        private void InitializeDriver()
        {
            try
            {
                
                if (!String.IsNullOrEmpty(GetPrinterName()))
                {
                    _printerName = GetPrinterName();
                    _evolisConnection = new Connection(_printerName);
                    
                    _evolisConnection.GetOpenMode();
                    _logger.LogInformation("Driver connecté à l'imprimante Evolis : {Printer}", _printerName);
                }
                else
                {
                    _logger.LogWarning("Aucune imprimante Evolis détectée sur les ports USB.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation du SDK Evolis.");
            }
        }



        /// <summary>
        /// Workflow complet : Insertion -> Encodage (Mag / Contact / Contactless) -> PIN -> Impression Visuelle -> Éjection / Rejet.
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        public async Task<CardIssuanceResult> IssuePersonalizedCardAsync(
            CardHolderData cardData,
            ChipType chipType = ChipType.DesfireEV,
            CancellationToken ct = default)
        {
            await _hardwareLock.WaitAsync(ct);

            try
            {
                if (!IsConnected)
                {
                    InitializeDriver();
                    if (!IsConnected)
                    {
                        return new CardIssuanceResult(false, null, "PRINTER_OFFLINE", "L'imprimante Evolis est hors-ligne ou déconnectée.");
                    }
                }

                // Validation préalable du PIN (4 chiffres)
                if (!string.IsNullOrEmpty(cardData.PinCode) && (cardData.PinCode.Length != 4 || !int.TryParse(cardData.PinCode, out _)))
                {
                    return new CardIssuanceResult(false, null, "INVALID_PIN", "Le code PIN doit comporter exactement 4 chiffres.");
                }

                _logger.LogInformation("Démarrage du cycle d'émission [Mode: {Mode}] pour {FirstName} {LastName}",
                    cardData.EncodingMode, cardData.FirstName, cardData.LastName);

                // -----------------------------------------------------------------
                // ÉTAPE 0 : Insertion de la carte depuis le chargeur (Feeder)
                // -----------------------------------------------------------------
                _evolisConnection!.SendCommand("Si"); // Input card command

                string? lastDetectedChipUid = null;

                // -----------------------------------------------------------------
                // MODE 1 : Encodage Bande Magnétique (Pistes ISO1, ISO2, ISO3)
                // -----------------------------------------------------------------
                if (cardData.EncodingMode.HasFlag(EncodingMode.Magnetic))
                {
                    if (cardData.MagneticTracks == null)
                    {
                        RejectCard();
                        return new CardIssuanceResult(false, null, "INVALID_MAG_DATA", "Données magnétiques manquantes.");
                    }

                    _logger.LogInformation("Positionnement sous la tête magnétique (Sim)...");
                    _evolisConnection.SendCommand("Sim");

                    bool magOk = EncodeMagneticTracks(cardData.MagneticTracks);
                    if (!magOk)
                    {
                        _logger.LogError("Échec de l'encodage magnétique. Rejet de la carte en cours...");
                        RejectCard();
                        return new CardIssuanceResult(false, null, "MAG_ENCODING_FAILED", "Échec d'écriture sur la bande magnétique.");
                    }
                }

                // -----------------------------------------------------------------
                // MODE 2 : Puce à Contact (Smart Contact ISO 7816)
                // -----------------------------------------------------------------
                if (cardData.EncodingMode.HasFlag(EncodingMode.SmartContact))
                {
                    _logger.LogInformation("Positionnement sous la station à puce à contact (Sic)...");
                    string posResp = _evolisConnection.SendCommand("Sic");

                    if (posResp.Contains("ERR"))
                    {
                        RejectCard();
                        return new CardIssuanceResult(false, null, "CONTACT_POS_ERROR", "Impossible de positionner la carte sous la station à contact.");
                    }

                    var chipResult = ProcessChipEncoding(cardData, chipType);
                    if (!chipResult.Success)
                    {
                        _logger.LogError("Échec d'encodage de la puce à contact. Rejet de la carte.");
                        RejectCard();
                        return chipResult;
                    }

                    lastDetectedChipUid = chipResult.ChipUid;
                }

                // -----------------------------------------------------------------
                // MODE 3 : Puce Sans Contact (Smart Contactless / RFID / NFC)
                // -----------------------------------------------------------------
                if (cardData.EncodingMode.HasFlag(EncodingMode.SmartContactless))
                {
                    _logger.LogInformation("Positionnement dans le champ d'antenne sans contact (Srf)...");
                    string posResp = _evolisConnection.SendCommand("Srf");

                    if (posResp.Contains("ERR"))
                    {
                        RejectCard();
                        return new CardIssuanceResult(false, null, "CONTACTLESS_POS_ERROR", "Impossible de positionner la carte sous l'antenne RFID.");
                    }

                    var chipResult = ProcessChipEncoding(cardData, chipType);
                    if (!chipResult.Success)
                    {
                        _logger.LogError("Échec d'encodage de la puce sans contact. Rejet de la carte.");
                        RejectCard();
                        return chipResult;
                    }

                    lastDetectedChipUid = chipResult.ChipUid;
                }

                // -----------------------------------------------------------------
                // ÉTAPE IMPRESSION : Rendu Visuel Textuel du Titulaire (CMS)
                // -----------------------------------------------------------------
                _logger.LogInformation("Positionnement sous la tête d'impression (Sip)...");
                _evolisConnection.SendCommand("Sip");

                bool printOk = await PrintCardVisualAsync(cardData, ct);
                if (!printOk)
                {
                    _logger.LogError("Échec du rendu visuel de la carte. Rejet de la carte en cours...");
                    RejectCard();
                    return new CardIssuanceResult(false, lastDetectedChipUid, "PRINT_FAILED", "Échec de l'impression visuelle du titulaire.");
                }

                // -----------------------------------------------------------------
                // ÉTAPE FINALE : Éjection de la carte au client (Output Hopper)
                // -----------------------------------------------------------------
                _evolisConnection.SendCommand("Se");
                _logger.LogInformation("Carte émise, encodée et délivrée au client avec succès.");

                return new CardIssuanceResult(true, lastDetectedChipUid, "00", "Carte délivrée avec succès.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique lors du cycle d'émission matérielle.");
                RejectCard();
                return new CardIssuanceResult(false, null, "DRIVER_EXCEPTION", ex.Message);
            }
            finally
            {
                _hardwareLock.Release();
            }
        }

        #region ENCODAGE MAGNÉTIQUE (ISO1, ISO2, ISO3)

        /// <summary>
        /// Transmet les chaînes ASCII des pistes magnétiques via les commandes ESC/POS Evolis (Cm1, Cm2, Cm3 + Smw).
        /// </summary>
        private bool EncodeMagneticTracks(MagneticTracksData tracks)
        {
            try
            {
                if (!string.IsNullOrEmpty(tracks.Track1))
                {
                    string r1 = _evolisConnection!.SendCommand($"Cm1;{tracks.Track1}");
                    if (r1.Contains("ERR")) return false;
                }

                if (!string.IsNullOrEmpty(tracks.Track2))
                {
                    string r2 = _evolisConnection!.SendCommand($"Cm2;{tracks.Track2}");
                    if (r2.Contains("ERR")) return false;
                }

                if (!string.IsNullOrEmpty(tracks.Track3))
                {
                    string r3 = _evolisConnection!.SendCommand($"Cm3;{tracks.Track3}");
                    if (r3.Contains("ERR")) return false;
                }

                // Lancement de l'écriture physique sur la bande
                string writeResp = _evolisConnection!.SendCommand("Smw");
                return !writeResp.Contains("ERR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi des pistes magnétiques.");
                return false;
            }
        }

        #endregion

        #region ENCODAGE PUCE APDU PC/SC (Contact & Contactless)

        private CardIssuanceResult ProcessChipEncoding(CardHolderData cardData, ChipType chipType)
        {
            try
            {
                using var context = ContextFactory.Instance.Establish(SCardScope.System);
                using var reader = new IsoReader(context, _pcscReaderName, SCardShareMode.Shared, SCardProtocol.Any, false);

                return chipType switch
                {
                    ChipType.DesfireEV => EncodeDesfireChip(reader, cardData),
                    ChipType.Iso7816Native => EncodeIsoNativeChip(reader, cardData),
                    _ => new CardIssuanceResult(false, null, "UNSUPPORTED_CHIP", "Type de puce non pris en charge.")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'ouverture du contexte PC/SC.");
                return new CardIssuanceResult(false, null, "PCSC_EXCEPTION", ex.Message);
            }
        }

        private CardIssuanceResult EncodeDesfireChip(IsoReader reader, CardHolderData cardData)
        {
            // 1. Get UID (APDU Native DESFire)
            var apduGetUid = new CommandApdu(IsoCase.Case2Short, SCardProtocol.Any)
            {
                CLA = 0xFF,
                INS = 0xCA,
                P1 = 0x00,
                P2 = 0x00,
                Le = 0x00
            };

            var respUid = reader.Transmit(apduGetUid);
            if (!respUid.HasData)
                return new CardIssuanceResult(false, null, "DESFIRE_READ_UID_FAILED", "Impossible de lire l'UID DESFire.");

            string chipUid = BitConverter.ToString(respUid.GetData()).Replace("-", "");

            // 2. Écriture du ChipDataPayload dans le Fichier Données DESFire (File ID #01)
            if (!string.IsNullOrEmpty(cardData.ChipDataPayload))
            {
                byte[] payloadBytes = Encoding.UTF8.GetBytes(cardData.ChipDataPayload);
                byte[] writeBuffer = new byte[7 + payloadBytes.Length];
                writeBuffer[0] = 0x01; // File ID #1
                writeBuffer[1] = 0x00; writeBuffer[2] = 0x00; writeBuffer[3] = 0x00; // Offset 0
                writeBuffer[4] = (byte)(payloadBytes.Length & 0xFF);
                writeBuffer[5] = (byte)((payloadBytes.Length >> 8) & 0xFF);
                writeBuffer[6] = (byte)((payloadBytes.Length >> 16) & 0xFF);
                Array.Copy(payloadBytes, 0, writeBuffer, 7, payloadBytes.Length);

                var apduWrite = new CommandApdu(IsoCase.Case3Short, SCardProtocol.Any)
                {
                    CLA = 0x90,
                    INS = 0x3D,
                    P1 = 0x00,
                    P2 = 0x00,
                    Data = writeBuffer
                };

                var respWrite = reader.Transmit(apduWrite);
                if (respWrite.SW1 != 0x90 && respWrite.SW1 != 0x91)
                    return new CardIssuanceResult(false, chipUid, "DESFIRE_WRITE_FAILED", "Erreur d'écriture sur la puce DESFire.");
            }

            // 3. Écriture du PIN (4 chiffres) dans le Fichier Sécurisé DESFire (File ID #02)
            if (!string.IsNullOrEmpty(cardData.PinCode))
            {
                byte[] pinBytes = Encoding.ASCII.GetBytes(cardData.PinCode);
                byte[] pinBuffer = new byte[7 + pinBytes.Length];
                pinBuffer[0] = 0x02; // File ID #2 réservé PIN
                pinBuffer[1] = 0x00; pinBuffer[2] = 0x00; pinBuffer[3] = 0x00;
                pinBuffer[4] = (byte)pinBytes.Length; pinBuffer[5] = 0x00; pinBuffer[6] = 0x00;
                Array.Copy(pinBytes, 0, pinBuffer, 7, pinBytes.Length);

                var apduPin = new CommandApdu(IsoCase.Case3Short, SCardProtocol.Any)
                {
                    CLA = 0x90,
                    INS = 0x3D,
                    P1 = 0x00,
                    P2 = 0x00,
                    Data = pinBuffer
                };

                var respPin = reader.Transmit(apduPin);
                if (respPin.SW1 != 0x90 && respPin.SW1 != 0x91)
                    return new CardIssuanceResult(false, chipUid, "DESFIRE_PIN_FAILED", "Erreur d'inscription du PIN DESFire.");
            }

            return new CardIssuanceResult(true, chipUid, "SUCCESS", "Puce DESFire encodée avec succès.");
        }

        private CardIssuanceResult EncodeIsoNativeChip(IsoReader reader, CardHolderData cardData)
        {
            // 1. Selection de l'Applet ISO (00 A4 04 00)
            byte[] appletAid = new byte[] { 0xA0, 0x00, 0x00, 0x01, 0x51, 0x00, 0x00 };
            var apduSelect = new CommandApdu(IsoCase.Case3Short, SCardProtocol.Any)
            {
                CLA = 0x00,
                INS = 0xA4,
                P1 = 0x04,
                P2 = 0x00,
                Data = appletAid
            };

            var respSelect = reader.Transmit(apduSelect);
            if (respSelect.SW1 != 0x90 || respSelect.SW2 != 0x00)
                return new CardIssuanceResult(false, null, "ISO_SELECT_FAILED", "Applet ISO 7816 introuvable sur la puce.");

            // 2. Read Serial Number / UID (00 CA 9F 7F)
            var apduSerial = new CommandApdu(IsoCase.Case2Short, SCardProtocol.Any)
            {
                CLA = 0x00,
                INS = 0xCA,
                P1 = 0x9F,
                P2 = 0x7F,
                Le = 0x2D
            };
            var respSerial = reader.Transmit(apduSerial);
            string chipUid = respSerial.HasData ? BitConverter.ToString(respSerial.GetData()).Replace("-", "") : "UNKNOWN_UID";

            // 3. Écriture du ChipDataPayload via UPDATE BINARY (00 D6)
            if (!string.IsNullOrEmpty(cardData.ChipDataPayload))
            {
                byte[] payloadBytes = Encoding.UTF8.GetBytes(cardData.ChipDataPayload);
                var apduUpdate = new CommandApdu(IsoCase.Case3Short, SCardProtocol.Any)
                {
                    CLA = 0x00,
                    INS = 0xD6,
                    P1 = 0x00,
                    P2 = 0x00,
                    Data = payloadBytes
                };

                var respUpdate = reader.Transmit(apduUpdate);
                if (respUpdate.SW1 != 0x90 || respUpdate.SW2 != 0x00)
                    return new CardIssuanceResult(false, chipUid, "ISO_UPDATE_FAILED", "Erreur UPDATE BINARY ISO.");
            }

            // 4. Inscription du PIN via CHANGE REFERENCE DATA (00 24 00 01)
            if (!string.IsNullOrEmpty(cardData.PinCode))
            {
                byte[] pinBytes = new byte[8];
                Array.Fill(pinBytes, (byte)0xFF);
                byte[] rawPin = Encoding.ASCII.GetBytes(cardData.PinCode);
                Array.Copy(rawPin, 0, pinBytes, 0, rawPin.Length);

                var apduPin = new CommandApdu(IsoCase.Case3Short, SCardProtocol.Any)
                {
                    CLA = 0x00,
                    INS = 0x24,
                    P1 = 0x00,
                    P2 = 0x01,
                    Data = pinBytes
                };

                var respPin = reader.Transmit(apduPin);
                if (respPin.SW1 != 0x90 || respPin.SW2 != 0x00)
                    return new CardIssuanceResult(false, chipUid, "ISO_PIN_FAILED", "Échec d'inscription du PIN ISO.");
            }

            return new CardIssuanceResult(true, chipUid, "SUCCESS", "Puce ISO 7816 encodée avec succès.");
        }

        #endregion

        #region IMPRESSION TEXTUELLE DE LA CARTE

        /// <summary>
        /// Génère le visuel textuel (Nom, Prénom, N° Carte, Expiration) et l'envoie à l'imprimante Evolis.
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private async Task<bool> PrintCardVisualAsync(CardHolderData data, CancellationToken ct)
        {
            try
            {
                //init print session from evolis SDK
                PrintSession ps = new (ref _evolisConnection);
                ReturnCode rc;
                // Canvas ISO CR-80 (300 DPI -> 1016 x 638 pixels)
                using var bitmap = new Bitmap(1016, 638);
                using var graphics = Graphics.FromImage(bitmap);

                graphics.Clear(Color.White);

                using var fontTitle = new Font("Arial", 32, FontStyle.Bold);
                using var fontBody = new Font("Arial", 24, FontStyle.Regular);
                using var fontCardNum = new Font("Courier New", 28, FontStyle.Bold);
                using var brush = new SolidBrush(Color.Black);

                // Rendu des textes Titulaire du CMS
                graphics.DrawString($"{data.FirstName} {data.LastName}".ToUpper(), fontTitle, brush, new PointF(80, 180));
                graphics.DrawString(data.CardNumber, fontCardNum, brush, new PointF(80, 280));
                graphics.DrawString($"EXPIRE FIN : {data.ExpiryDate}", fontBody, brush, new PointF(80, 360));

                // Fichier temporaire pour le spooler Evolis
                string tempImgPath = Path.Combine(Path.GetTempPath(), $"evolis_print_{Guid.NewGuid()}.bmp");
                bitmap.Save(tempImgPath, ImageFormat.Bmp);

               ps.SetImage(CardFace.FRONT,tempImgPath);
                rc = ps.Print();
                if (File.Exists(tempImgPath)) File.Delete(tempImgPath);

                await Task.Delay(800, ct);
                _logger.LogInformation("Rendu d'impression de la carte terminé avec le code de retour : {ReturnCode}", rc);
                return rc==ReturnCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du rendu d'impression de la carte.");
                return false;
            }
        }

        #endregion

        #region GESTION DES REJETS & DISPOSE

        /// <summary>
        /// Éjecte immédiatement la carte défectueuse vers le bac de rejet sécurisé (Reject Box).
        /// </summary>
        private void RejectCard()
        {
            try
            {
                if (_evolisConnection != null && VerifyPrinterConnection())
                {
                    _evolisConnection.SendCommand("Se;r"); // Commande d'éjection vers le bac de rejet
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de la commande de rejet (Se;r).");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_evolisConnection != null && VerifyPrinterConnection())
                {
                    _evolisConnection.Close();
                    //_evolisConnection.Dispose();
                }
            }
            catch { }
            finally
            {
                _hardwareLock.Dispose();
            }
        }

        #endregion



        private bool VerifyPrinterConnection()
        {
            if (_evolisConnection != null && _evolisConnection.GetState(out State.MajorState mas, out State.MinorState mis))
            {
                if (mas == State.MajorState.READY)
                {
                    _logger.LogInformation("Connexion à l'imprimante Evolis établie.");
                    return true;
                }
                else
                {
                    _logger.LogError("Connexion à l'imprimante Evolis non établie. État majeur: {MajorState}, État mineur: {MinorState}", mas, mis);
                    return false;
                }
              
            }
            return false;
        }

        public static string GetPrinterName()
        {
            const string DefaultPrinterName = "Evolis Primacy 2";

            var devices = Evolis.Evolis.GetDevices();
            var selected = -1;

            for (int i = 0; i < devices.Count; ++i)
            {
                if (selected == -1 || (!devices[selected].isOnline && devices[i].isOnline))
                {
                    selected = i;
                }
            }
            if (selected >= 0)
            {
                return devices[selected].name;
            }

            return DefaultPrinterName;
        }

        public  string GetPrinterHardwareStatus()
        {
            var printerName = GetPrinterName();
            _evolisConnection = new Connection(printerName);

            if(_evolisConnection.GetStatus(out Status status))
            {
                if (status.IsOn(Status.Flag.WAR_FEEDER_EMPTY)) { 
                    _logger.LogWarning("Le chargeur de cartes est vide.");
                    return "ERROR_FEEDER_EMPTY";
                }

                else if (status.IsOn(Status.Flag.WAR_COVER_OPEN))
                {
                    _logger.LogWarning("printer cover is open, please close it");
                    return "ERROR_COVER_OPEN";
                } else if (status.IsOn(Status.Flag.WAR_NO_RIBBON))
                {
                    _logger.LogWarning("Le ruban d'impression est vide.");
                    return "ERROR_RIBBON_NO";
                }
                else if (status.IsOn(Status.Flag.WAR_PRINTER_LOCKED))
                {
                    _logger.LogWarning("La carte est bloquée dans l'imprimante.");
                    return "ERROR_OFFLINE";
                }
                else
                {
                    return "ERROR";
                }
                
            }
            else
            {
                _logger.LogError("Impossible d'obtenir le statut matériel de l'imprimante Evolis.");
                return "ERROR";
            }
        }
    }
}
