using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using InvoiceScanner.Models;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace InvoiceScanner.Utilities
{
    public class OpenAIInvoiceExtractor
    {
        public OpenAIInvoiceExtractor(string apiKey)
        {
            // Constructor kept for backward compatibility
        }

        /// <summary>
        /// Extracts invoice data from PDF byte array using default extraction methods
        /// </summary>
        /// <param name="pdfBytes">PDF file as byte array</param>
        /// <param name="fileName">Original file name</param>
        /// <param name="isZeroRated">Whether the invoice is zero-rated</param>
        /// <param name="isUSD">Whether the invoice is in USD</param>
        /// <param name="isEuro">Whether the invoice is in EURO</param>
        /// <returns>Invoice object with populated data</returns>
        public async Task<Invoice> ExtractInvoiceDataAsync(byte[] pdfBytes, string fileName, bool isZeroRated = false, bool isUSD = false, bool isEuro = false)
        {
            try
            {
                Console.WriteLine($"Starting extraction for file: {fileName}");
                
                // Extract text from PDF using PdfReaderHelper
                string extractedText = PdfReaderHelper.ExtractTextFromPdf(pdfBytes);
                
                // Check if the text has any content
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    Console.WriteLine("No text extracted from PDF");
                    return null;
                }
                
                // Use the default PDF data extraction methods
                Invoice invoice = null;
                
                // Check if this is a TAX INVOICE format and use TaxInvoiceHelper
                if (extractedText.Contains("TAX INVOICE"))
                {
                    invoice = TaxInvoiceHelper.PopulateTaxInvoiceData(extractedText, fileName, isZeroRated, isUSD, isEuro);
                }
                
                // If that didn't work, try to process as a vertical invoice
                if (invoice == null || string.IsNullOrEmpty(invoice.Number))
                {
                    invoice = PdfReaderHelper.populateVerticalInvoiceData(extractedText, fileName, isZeroRated, isUSD, isEuro);
                }
                
                // If that didn't work, try the standard invoice processing
                if (invoice == null || string.IsNullOrEmpty(invoice.Number))
                {
                    invoice = PdfReaderHelper.populateData(extractedText, fileName);
                }
                
                return invoice;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting invoice data: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Specialized method to extract data from TAX INVOICE format based on provided examples
        /// </summary>
        private Invoice ExtractTaxInvoice(string extractedText, string fileName, bool isZeroRated = false, bool isUSD = false, bool isEuro = false)
        {
            try
            {
                Console.WriteLine("Detected TAX INVOICE format, using specialized extraction");
                
                Invoice invoice = new Invoice();
                invoice.Items = new List<Item>();
                invoice.Filename = fileName;
                invoice.DocumentName = fileName;
                invoice.OcrText = extractedText;
                invoice.Status = "PENDING";
                invoice.ProcessedDate = DateTime.UtcNow;
                
                // Extract total amount in TZS from first line (as per user's guidance)
                var totalTZSMatch = Regex.Match(extractedText, @"(USD|TZS) Total TZS:\s*([0-9,.]+)");
                if (totalTZSMatch.Success)
                {
                    string currencyIndicator = totalTZSMatch.Groups[1].Value;
                    string totalValue = totalTZSMatch.Groups[2].Value.Replace(",", "");
                    
                    if (currencyIndicator == "USD")
                    {
                        invoice.Currency = "USD";
                        isUSD = true;
                    }
                    else
                    {
                        invoice.Currency = "TZS";
                    }
                    
                    if (decimal.TryParse(totalValue, out decimal totalAmount))
                    {
                        invoice.Total = totalAmount;
                    }
                }
                else if (isEuro)
                {
                    invoice.Currency = "EURO";
                }
                else if (isUSD)
                {
                    invoice.Currency = "USD";
                }
                else
                {
                    invoice.Currency = "TZS"; // Default
                }
                
                // Extract taxable amount from the second line (as per user's guidance)
                var taxableMatch = Regex.Match(extractedText, @"([0-9,.]+)\s+Taxable");
                if (taxableMatch.Success)
                {
                    string taxableStr = taxableMatch.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(taxableStr, out decimal taxableAmount))
                    {
                        // This is likely the subtotal
                        invoice.SubTotal = taxableAmount;
                    }
                }
                
                // Extract invoice number - directly under "TAX INVOICE" or before "Number" (as per user's guidance)
                var invoiceHeaderIndex = extractedText.IndexOf("TAX INVOICE");
                if (invoiceHeaderIndex >= 0)
                {
                    // Get the lines after TAX INVOICE
                    string afterHeader = extractedText.Substring(invoiceHeaderIndex + "TAX INVOICE".Length);
                    var lines = afterHeader.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (lines.Length > 0)
                    {
                        // The number should be on the next non-empty line
                        string potentialNumberLine = lines[0].Trim();
                        if (Regex.IsMatch(potentialNumberLine, @"^\s*\d+\s*$"))
                        {
                            // This looks like just a number
                            invoice.Number = potentialNumberLine.Trim();
                        }
                        else
                        {
                            // Try to find it before "Number:"
                            var numberMatch = Regex.Match(extractedText, @"TAX INVOICE.*?(\d+)\s*Number:");
                            if (numberMatch.Success)
                            {
                                invoice.Number = numberMatch.Groups[1].Value.Trim();
                            }
                            else
                            {
                                // One more try with a different pattern
                                numberMatch = Regex.Match(extractedText, @"Number:\s*(\d+)");
                                if (numberMatch.Success)
                                {
                                    invoice.Number = numberMatch.Groups[1].Value.Trim();
                                }
                            }
                        }
                    }
                }
                
                // Extract date - the value appears in a line BEFORE "Date:" (as per user's guidance)
                var dateIndex = extractedText.IndexOf("Date:");
                if (dateIndex > 0)
                {
                    // Look for the line before "Date:"
                    int startOfLine = extractedText.LastIndexOf('\n', dateIndex);
                    if (startOfLine >= 0)
                    {
                        string dateLine = extractedText.Substring(startOfLine, dateIndex - startOfLine).Trim();
                        // Look for a date pattern MM-DD-YYYY
                        var dateMatch = Regex.Match(dateLine, @"(\d{2}-\d{2}-\d{4})");
                        if (dateMatch.Success)
                        {
                            string dateStr = dateMatch.Groups[1].Value;
                            if (DateTime.TryParseExact(dateStr, "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                            {
                                invoice.Date = parsedDate;
                                invoice.invoiceDate = parsedDate;
                            }
                            else
                            {
                                invoice.Date = DateTime.Now;
                            }
                        }
                        else
                        {
                            // Try with just the line content as a date
                            if (DateTime.TryParse(dateLine, out DateTime parsedDate))
                            {
                                invoice.Date = parsedDate;
                                invoice.invoiceDate = parsedDate;
                            }
                            else
                            {
                                invoice.Date = DateTime.Now;
                            }
                        }
                    }
                    else
                    {
                        invoice.Date = DateTime.Now;
                    }
                }
                else
                {
                    invoice.Date = DateTime.Now;
                }
                
                // Extract customer name - located above "Sold" (as per user's guidance)
                var soldIndex = extractedText.IndexOf("Sold");
                if (soldIndex > 0)
                {
                    // Look for the line before "Sold"
                    int startOfSoldLine = extractedText.LastIndexOf('\n', soldIndex);
                    if (startOfSoldLine >= 0)
                    {
                        // Get the previous line
                        int startOfPrevLine = extractedText.LastIndexOf('\n', startOfSoldLine - 1);
                        if (startOfPrevLine >= 0)
                        {
                            string customerNameLine = extractedText.Substring(startOfPrevLine, startOfSoldLine - startOfPrevLine).Trim();
                            // Skip if this looks like part of the Swissport company information
                            if (!customerNameLine.Contains("SWISSPORT") && 
                                !customerNameLine.Contains("Fax:") && 
                                !customerNameLine.Contains("Email:") && 
                                !customerNameLine.Contains("Tel :"))
                            {
                                invoice.Customer = customerNameLine;
                            }
                        }
                    }
                }
                
                // Extract TIN - appears between "Sold" and "CUST TIN:" (as per user's guidance)
                var tinMatch = Regex.Match(extractedText, @"Sold.*?(\d[\d-]+)\s*CUST TIN:", RegexOptions.Singleline);
                if (tinMatch.Success)
                {
                    invoice.TIN = tinMatch.Groups[1].Value.Replace("-", "").Trim();
                    if (invoice.TIN.Length < 9) // Validate TIN length
                    {
                        invoice.TIN = "";
                    }
                }
                
                // Check for exempt/zero-rated status explicitly in the text
                if (extractedText.Contains("Exempt") || extractedText.Contains("Zero Rated"))
                {
                    isZeroRated = true;
                }
                
                // Extract the amounts if not already set
                if (invoice.SubTotal <= 0 || invoice.Tax <= 0 || invoice.Total <= 0)
                {
                    decimal subtotal = invoice.SubTotal;
                    decimal vat = 0;
                    decimal total = invoice.Total;
                    
                    // Try to find subtotal if not already set
                    if (subtotal <= 0)
                    {
                        var subtotalMatch = Regex.Match(extractedText, @"Subtotal before VAT\s*([0-9,.]+)");
                        if (subtotalMatch.Success)
                        {
                            decimal.TryParse(subtotalMatch.Groups[1].Value.Replace(",", ""), out subtotal);
                            invoice.SubTotal = subtotal;
                        }
                    }
                    
                    // Try to find VAT amount
                    var vatMatch = Regex.Match(extractedText, @"Total VAT\s*([0-9,.]+)");
                    if (vatMatch.Success)
                    {
                        decimal.TryParse(vatMatch.Groups[1].Value.Replace(",", ""), out vat);
                        invoice.Tax = vat;
                    }
                    
                    // Try to find total amount if not already set
                    if (total <= 0)
                    {
                        var totalMatch = Regex.Match(extractedText, @"Total Amount\s*([0-9,.]+)");
                        if (totalMatch.Success)
                        {
                            decimal.TryParse(totalMatch.Groups[1].Value.Replace(",", ""), out total);
                            invoice.Total = total;
                        }
                    }
                    
                    // If no subtotal but we have total and VAT, calculate subtotal
                    if (subtotal <= 0 && total > 0 && vat >= 0)
                    {
                        invoice.SubTotal = total - vat;
                    }
                    
                    // If no VAT amount but isZeroRated is false, calculate VAT (assuming 18%)
                    if (vat <= 0 && !isZeroRated && subtotal > 0)
                    {
                        invoice.Tax = Math.Round(subtotal * 0.18m, 2);
                    }
                    
                    // If no total but we have subtotal and VAT, calculate total
                    if (total <= 0 && subtotal > 0)
                    {
                        invoice.Total = subtotal + (isZeroRated ? 0 : invoice.Tax);
                    }
                }
                
                // Extract line items - they typically appear in a pattern like:
                // Description/Comments Quantity Unit Price Line Total
                // [description] [quantity] [unitPrice] [lineTotal]
                
                // First, try to find the section that contains line items
                int descriptionIndex = extractedText.IndexOf("Description/Comments");
                int lineItemsEnd = extractedText.IndexOf("Due Date Amount Due", descriptionIndex);
                
                if (descriptionIndex > 0 && lineItemsEnd > descriptionIndex)
                {
                    string lineItemsSection = extractedText.Substring(descriptionIndex, lineItemsEnd - descriptionIndex);
                    
                    // Skip header line and process each subsequent line until we hit the footer
                    using (StringReader reader = new StringReader(lineItemsSection))
                    {
                        string line;
                        bool headerPassed = false;
                        
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Skip until we pass the header line
                            if (!headerPassed)
                            {
                                if (line.Contains("Quantity") && line.Contains("Unit Price") && line.Contains("Line Total"))
                                {
                                    headerPassed = true;
                                }
                                continue;
                            }
                            
                            // Skip empty lines
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }
                            
                            // Try to extract line item details
                            var qtyMatch = Regex.Match(line, @"\s(\d+\.\d+|\d+)\s+(\d+,?\.?\d*|\d*,?\d+\.?\d*)\s+(\d+,?\.?\d*|\d*,?\d+\.?\d*)");
                            if (qtyMatch.Success)
                            {
                                string description = line.Substring(0, qtyMatch.Index).Trim();
                                string quantity = qtyMatch.Groups[1].Value;
                                string unitPrice = qtyMatch.Groups[2].Value.Replace(",", "");
                                string amount = qtyMatch.Groups[3].Value.Replace(",", "");
                                
                                var item = new Item();
                                item.Name = description;
                                decimal.TryParse(quantity, out decimal qty);
                                item.Quantity = qty;
                                
                                decimal.TryParse(unitPrice, out decimal price);
                                item.UnitPrice = price;
                                
                                decimal.TryParse(amount, out decimal lineTotal);
                                item.Amount = lineTotal;
                                
                                // Calculate tax for this item if not zero-rated
                                if (!isZeroRated)
                                {
                                    item.Tax = Math.Round(item.Amount * 0.18m, 2);
                                    item.GrossUnitPrice = item.UnitPrice * 1.18m;
                                }
                                else
                                {
                                    item.Tax = 0;
                                    item.GrossUnitPrice = item.UnitPrice;
                                }
                                
                                invoice.Items.Add(item);
                            }
                            else if (line.Contains("Being") || line.Length < 10)
                            {
                                // This is probably a footer or description line
                                continue;
                            }
                        }
                    }
                }
                
                // Alternative format with DESCRIPTION QN NET VAT GROSS format (from the 4th example)
                if (invoice.Items.Count == 0 && extractedText.Contains("DESCRIPTION QN NET VAT GROSS"))
                {
                    // Find the section with the items
                    int descSection = extractedText.IndexOf("DESCRIPTION QN NET VAT GROSS");
                    int endSection = extractedText.IndexOf("SUB TOTAL", descSection);
                    
                    if (descSection > 0 && endSection > descSection)
                    {
                        string itemsSection = extractedText.Substring(descSection, endSection - descSection);
                        using (StringReader reader = new StringReader(itemsSection))
                        {
                            string line;
                            bool headerPassed = false;
                            
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (!headerPassed)
                                {
                                    if (line.Contains("DESCRIPTION") && line.Contains("NET") && line.Contains("VAT") && line.Contains("GROSS"))
                                    {
                                        headerPassed = true;
                                    }
                                    continue;
                                }
                                
                                if (string.IsNullOrWhiteSpace(line))
                                {
                                    continue;
                                }
                                
                                // Looking for patterns like: [description] [quantity] [net] [vat] [gross]
                                var itemMatch = Regex.Match(line, @"([A-Za-z\s-]+)\s+(\d+(?:\.\d+)?)\s+(\d+(?:,?\d+)*(?:\.\d+)?)\s+(\d+(?:,?\d+)*(?:\.\d+)?)\s+(\d+(?:,?\d+)*(?:\.\d+)?)");
                                if (itemMatch.Success)
                                {
                                    var item = new Item();
                                    item.Name = itemMatch.Groups[1].Value.Trim();
                                    decimal.TryParse(itemMatch.Groups[2].Value, out decimal qty);
                                    item.Quantity = qty;
                                    
                                    string netStr = itemMatch.Groups[3].Value.Replace(",", "");
                                    decimal.TryParse(netStr, out decimal net);
                                    item.Amount = net;
                                    item.UnitPrice = item.Quantity > 0 ? (net / item.Quantity) : net;
                                    
                                    string vatStr = itemMatch.Groups[4].Value.Replace(",", "");
                                    decimal.TryParse(vatStr, out decimal itemVat);
                                    item.Tax = itemVat;
                                    
                                    string grossStr = itemMatch.Groups[5].Value.Replace(",", "");
                                    decimal.TryParse(grossStr, out decimal gross);
                                    item.GrossUnitPrice = item.Quantity > 0 ? (gross / item.Quantity) : gross;
                                    
                                    invoice.Items.Add(item);
                                }
                            }
                        }
                    }
                }
                
                // If we couldn't extract line items, but we have total & subtotal, create a summary item
                if (invoice.Items.Count == 0 && invoice.Total > 0)
                {
                    var item = new Item();
                    if (isZeroRated)
                    {
                        item.Code = "C";
                        item.Name = "Summary of Service-C";
                        item.Amount = invoice.Total;
                        item.UnitPrice = invoice.Total;
                        item.Quantity = 1;
                        item.Tax = 0;
                        item.GrossUnitPrice = invoice.Total;
                    }
                    else
                    {
                        item.Code = "A";
                        item.Name = "Summary of Service-A";
                        item.Amount = invoice.SubTotal > 0 ? invoice.SubTotal : (invoice.Total / 1.18m);
                        item.UnitPrice = item.Amount;
                        item.Quantity = 1;
                        item.Tax = invoice.Tax > 0 ? invoice.Tax : (item.Amount * 0.18m);
                        item.GrossUnitPrice = item.UnitPrice + item.Tax;
                    }
                    invoice.Items.Add(item);
                }
                
                return invoice;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TAX INVOICE extraction: {ex.Message}");
                return null;
            }
        }
    }
}