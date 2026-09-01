using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Runtime.Versioning;

namespace deviceAgent.services
{

    public record ReceiptData(
        string CustomerName,
        string CardNumber,
        string ExpirationDate,
        string TransactionId
    );
    internal class ReceiptPrinterService(ILogger<ReceiptPrinterService> logger)
    {
         private readonly ILogger<ReceiptPrinterService> _logger = logger;

        
        public async Task<bool> PrintReceiptAsync(ReceiptData receiptData, string printerName)
        {
            try
            {
                _logger.LogInformation("Printing receipt for {CustomerName} to printer {PrinterName}", receiptData.CustomerName, printerName);
                // Load the receipt template
                string receiptTemplate = await GetReceiptContent();

                if (string.IsNullOrEmpty(receiptTemplate))
                {
                    _logger.LogWarning("Receipt template is empty or not found.");
                    return false;
                }
                // mask the card number for security
                string maskedCardNumber = MaskCardNumber(receiptData.CardNumber);
                // Replace placeholders in the template with actual data
                string receiptContent = receiptTemplate
                    .Replace("CUSTOMER_NAME", receiptData.CustomerName)
                    .Replace("MASKED_CARD_NUMBER", maskedCardNumber)
                    .Replace("EXPIRY_DATE", receiptData.ExpirationDate)
                    .Replace("TRANSACTION_ID", receiptData.TransactionId)
                    .Replace("DATE_TIME",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .Replace("KIOSK_ID","BORNE_DISP_CARD");

                // Print the receipt
                return PrintRawRtf(receiptContent, printerName);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt");
                return false;
            }
        }

        private static async Task<string> GetReceiptContent()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pattern", "receipt.rtf");
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path, Encoding.ASCII);
            }

            return string.Empty;
        }

        private static string MaskCardNumber(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 10)
                return cardNumber;

            return $"{cardNumber[..4]} **** **** {cardNumber[^4..]}";
        }

        [SupportedOSPlatform("windows6.1")]
        private bool PrintRawRtf(string rtfContent, string printerName)
        {
            // Si l'imprimante thermique accepte le RTF ou l'impression via RichTextBox / System.Drawing.Printing
            try
            {
                using PrintDocument pd = new();
                pd.PrinterSettings.PrinterName = printerName;

                // Logique de rendu de la chaîne RTF vers la page imprimée
                pd.PrintPage += (sender, ev) =>
                {
                    // Dessin simple du reçu (pour imprimante thermique 80mm)
                    using Font font = new("Courier New", 9, FontStyle.Regular);
                    // Si vous convertissez le RTF en texte brut pour les tickets de caisse :
                    //string plainText = StripRtf(rtfContent);
                    ev.Graphics?.DrawString(rtfContent, font, Brushes.Black, new PointF(10, 10));
                };

                pd.Print();
                _logger.LogInformation("Reçu transmis à l'imprimante thermique '{Printer}'.", printerName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'envoi vers l'imprimante thermique.");
                return false;
            }
        }
    }
}
