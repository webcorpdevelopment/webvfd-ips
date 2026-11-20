using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace InvoiceScanner.Utilities
{
    public static class TaxInvoiceHelper
    {
        
        public static Invoice PopulateTaxInvoiceData(string extractedText, string fileName, bool isZeroRated = false, bool isUSD = false, bool isEuro = false)
        {
            try
            {
                Invoice invoice = new Invoice();
                invoice.Items = new List<Item>();
                invoice.Filename = fileName;
                invoice.DocumentName = fileName;
                invoice.OcrText = extractedText;
                invoice.Status = "PENDING";
                invoice.ProcessedDate = DateTime.UtcNow;
                
                // Variables to store values before creating items
                decimal totalPayableAmount = 0;
                decimal totalVAT = 0;
                decimal taxableAmount = 0;
                decimal nonTaxableAmount = 0;
                decimal exchangeRate = 0; // Initially zero
                
                // Extract lines for further processing
                string[] lines = extractedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                // SCENARIO 1 & 2 & 3 & 4: First line starting with currency code
                if (lines.Length > 0)
                {
                    // Extract total amount in TZS from first line (usually at the top of the invoice)
                    var totalTZSMatch = Regex.Match(lines[0], @"(USD|TZS|EUR) Total TZS:\s*([0-9,.]+)");
                    if (totalTZSMatch.Success)
                    {
                        string currencyIndicator = totalTZSMatch.Groups[1].Value;
                        string totalValue = totalTZSMatch.Groups[2].Value.Replace(",", "");
                        
                        // Set the currency based on the indicator
                        if (currencyIndicator == "USD")
                        {
                            invoice.Currency = "USD";
                            isUSD = true;

                            // For USD, try to get exchange rate from the lines after TAX INVOICE
                            var taxInvoiceIndex = extractedText.IndexOf("TAX INVOICE");
                            if (taxInvoiceIndex >= 0)
                            {
                                // Get the lines after TAX INVOICE
                                string afterTaxInvoice = extractedText.Substring(taxInvoiceIndex);
                                var invoiceLines = afterTaxInvoice.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                
                                // Look for the exchange rate line (usually the second line after TAX INVOICE)
                                for (int i = 0; i < Math.Min(3, invoiceLines.Length); i++)
                                {
                                    var rateMatch = Regex.Match(invoiceLines[i].Trim(), @"^\s*([0-9,.]+)(?:\s+Exempt)?$");
                                    if (rateMatch.Success)
                                    {
                                        string rateStr = rateMatch.Groups[1].Value.Replace(",", "");
                                        if (decimal.TryParse(rateStr, out decimal rate))
                                        {
                                            exchangeRate = Math.Round(rate, 2);
                                            invoice.ExchangeRate = exchangeRate;
                                            
                                            // If it contains "Exempt", mark as zero-rated
                                            if (invoiceLines[i].Contains("Exempt"))
                                            {
                                                isZeroRated = true;
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else if (currencyIndicator == "EUR")
                        {
                            invoice.Currency = "EURO";
                            isEuro = true;
                        }
                        else
                        {
                            invoice.Currency = "TZS";
                        }
                        
                        if (decimal.TryParse(totalValue, out decimal amount))
                        {
                            totalPayableAmount = Math.Round(amount, 2);
                            invoice.Total = totalPayableAmount;
                        }
                        
                        // SCENARIO 3: For TZS, check if invoice number is in the second line
                        if (currencyIndicator == "TZS" && lines.Length > 1)
                        {
                            string potentialInvoiceNumber = lines[1].Trim();
                            if (Regex.IsMatch(potentialInvoiceNumber, @"^\s*\d+\s*$"))
                            {
                                invoice.Number = potentialInvoiceNumber;
                            }
                        }
                        
                        // SCENARIO 4: For EUR, check for Exempt and extract exchange rate from second line
                        if (currencyIndicator == "EUR" && lines.Length > 1)
                        {
                            // Extract exchange rate from second line with format like "2,596.82 Exempt"
                            string secondLine = lines[1].Trim();
                            // Extract the number at the beginning of the line
                            var euroExchangeMatch = Regex.Match(secondLine, @"^\s*([0-9,.]+)");
                            if (euroExchangeMatch.Success)
                            {
                                string exchangeRateStr = euroExchangeMatch.Groups[1].Value.Replace(",", ".");
                                if (decimal.TryParse(exchangeRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
                                {
                                    exchangeRate = Math.Round(rate, 2);
                                    invoice.ExchangeRate = exchangeRate;
                                    
                                    // Check if this is an exempt invoice
                                    bool isExempt = secondLine.Contains("Exempt");
                                    if (isExempt)
                                    {
                                        isZeroRated = true;
                                        
                                        // For EUR Exempt scenario, calculate taxable amount by dividing total by exchange rate
                                        if (totalPayableAmount > 0 && exchangeRate > 0)
                                        {
                                            taxableAmount = Math.Round(totalPayableAmount / exchangeRate, 2);
                                            invoice.SubTotal = taxableAmount;
                                            // Since it's exempt, VAT is 0
                                            totalVAT = 0;
                                            invoice.Tax = 0;
                                        }
                                    }
                                    else
                                    {
                                        // For non-exempt EUR invoices, specifically look for VAT
                                        // between "Total VAT" and "Address" sections
                                        int totalVatIndex = extractedText.IndexOf("Total VAT");
                                        int addressIndex = extractedText.IndexOf("Address");
                                        
                                        if (totalVatIndex > 0 && addressIndex > totalVatIndex)
                                        {
                                            // Extract text between these two markers
                                            string vatSection = extractedText.Substring(
                                                totalVatIndex + "Total VAT".Length, 
                                                addressIndex - (totalVatIndex + "Total VAT".Length)
                                            ).Trim();
                                            
                                            // Find the VAT amount in this section
                                            var vatAmountMatch = Regex.Match(vatSection, @"(\d+[.,]?\d*)");
                                            if (vatAmountMatch.Success)
                                            {
                                                string vatAmountStr = vatAmountMatch.Groups[1].Value.Replace(",", ".");
                                                if (decimal.TryParse(vatAmountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vatAmount))
                                                {
                                                    // For EUR, multiply by exchange rate to get the VAT in TZS
                                                    totalVAT = Math.Round(vatAmount * exchangeRate, 2);
                                                    invoice.Tax = totalVAT;
                                                    
                                                    // Calculate taxable amount
                                                    decimal foreignAmount = Math.Round(totalPayableAmount / exchangeRate, 2);
                                                    taxableAmount = Math.Round(foreignAmount - vatAmount, 2);
                                                    invoice.SubTotal = taxableAmount;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
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
                }
                
                // Extract exchange rate if not already extracted (for non-EUR scenarios)
                if (exchangeRate == 0)
                {
                    var exchangeRateMatch = Regex.Match(extractedText, @"([0-9,.]+)\s+Taxable");
                    if (exchangeRateMatch.Success)
                    {
                        string exchangeRateStr = exchangeRateMatch.Groups[1].Value.Replace(",", "");
                        if (decimal.TryParse(exchangeRateStr, out decimal rate))
                        {
                            exchangeRate = Math.Round(rate, 2);
                            invoice.ExchangeRate = exchangeRate;
                        }
                    }
                }
                
                // Extract taxable amount if not already set
                if (taxableAmount == 0)
                {
                    var taxableMatch = Regex.Match(extractedText, @"Subtotal before VAT\s*([0-9,.]+)");
                    if (taxableMatch.Success)
                    {
                        string taxableStr = taxableMatch.Groups[1].Value.Replace(",", "");
                        if (decimal.TryParse(taxableStr, out decimal amount))
                        {
                            taxableAmount = Math.Round(amount, 2);
                            invoice.SubTotal = taxableAmount;
                        }
                    }
                }
                
                // Extract VAT/tax amount if not already set
                if (totalVAT == 0 && !isZeroRated)
                {
                    var vatMatch = Regex.Match(extractedText, @"Total VAT\s*([0-9,.]+)");
                    if (vatMatch.Success)
                    {
                        string vatStr = vatMatch.Groups[1].Value.Replace(",", "");
                        if (decimal.TryParse(vatStr, out decimal vatAmount))
                        {
                            totalVAT = Math.Round(vatAmount, 2);
                            
                            // Convert TAX amount using exchange rate for USD or EURO currency
                            if ((isUSD || isEuro) && exchangeRate > 0)
                            {
                                // Use exchange rate to convert the tax amount to the appropriate currency
                                totalVAT = Math.Round(totalVAT * exchangeRate, 2);
                            }
                            
                            invoice.Tax = totalVAT;
                        }
                    }
                }
                
                // Try to extract non-taxable amount from text
                var nonTaxableMatch = Regex.Match(extractedText, @"Non-taxable amount:?\s*([0-9,.]+)");
                if (nonTaxableMatch.Success)
                {
                    string nonTaxStr = nonTaxableMatch.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(nonTaxStr, out decimal nonTaxAmount))
                    {
                        nonTaxableAmount = Math.Round(nonTaxAmount, 2);
                    }
                }
                
                // Extract invoice number if not already extracted
                if (string.IsNullOrEmpty(invoice.Number))
                {
                    // First try to get the invoice number from the second line
                    if (lines.Length > 1)
                    {
                        string secondLine = lines[1].Trim();
                        if (Regex.IsMatch(secondLine, @"^\s*\d+\s*$"))
                        {
                            invoice.Number = secondLine;
                        }
                    }

                    // If not found in second line, try other methods
                    if (string.IsNullOrEmpty(invoice.Number))
                    {
                        var invoiceHeaderIndex = extractedText.IndexOf("TAX INVOICE");
                        if (invoiceHeaderIndex >= 0)
                        {
                            // Get the lines after TAX INVOICE
                            string afterHeader = extractedText.Substring(invoiceHeaderIndex + "TAX INVOICE".Length);
                            var headerLines = afterHeader.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            if (headerLines.Length > 0)
                            {
                                // The number should be on the next non-empty line
                                string potentialNumberLine = headerLines[0].Trim();
                                if (Regex.IsMatch(potentialNumberLine, @"^\s*\d+\s*$"))
                                {
                                    // This looks like just a number
                                    invoice.Number = potentialNumberLine.Trim();
                                }
                                else
                                {
                                    // Try to find it before "Number:"
                                    var numberMatch = Regex.Match(extractedText, @"TAX INVOICE.*?(\d+)\s*Number:", RegexOptions.Singleline);
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
                    }
                }
                
                // Extract company name - look for name before "Sold"
                var soldIndex = extractedText.IndexOf("Sold");
                if (soldIndex > 0)
                {
                    // Get a portion of text before "Sold"
                    int lookBackLength = Math.Min(200, soldIndex);
                    string textBeforeSold = extractedText.Substring(soldIndex - lookBackLength, lookBackLength);
                    
                    // Split into lines
                    string[] soldPrecedingLines = textBeforeSold.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Look at the last few lines before "Sold" 
                    if (soldPrecedingLines.Length >= 3)
                    {
                        // Format often looks like:
                        // "Tel : +255 27 2553941/2554104/5 /"
                        // "COMPANY NAME"
                        // "+255 744 000045"
                        // "Sold"
                        
                        // Check if the line right before "Sold" contains a phone number
                        string lastLine = soldPrecedingLines[soldPrecedingLines.Length - 1].Trim();
                        string secondLastLine = soldPrecedingLines[soldPrecedingLines.Length - 2].Trim();
                        
                        // If the last line is a phone number and second-last line doesn't look like contact info
                        if (Regex.IsMatch(lastLine, @"^\s*\+?\d") && 
                            !Regex.IsMatch(secondLastLine, @"^\s*\+?\d") && 
                            !secondLastLine.Contains("Tel") && 
                            !secondLastLine.Contains("Fax") && 
                            !secondLastLine.Contains("Email") && 
                            !secondLastLine.ToLower().Contains("swissport"))
                        {
                            invoice.Customer = secondLastLine;
                        }
                    }
                }
                
                // Extract date with improved pattern matching
                var dateIndex = extractedText.IndexOf("Date:");
                if (dateIndex > 0)
                {
                    // Look for the line before "Date:"
                    int startOfLine = extractedText.LastIndexOf('\n', dateIndex);
                    if (startOfLine >= 0)
                    {
                        // Get the text before "Date:" and look for a date pattern
                        string beforeDateText = extractedText.Substring(Math.Max(0, startOfLine - 50), Math.Min(50, startOfLine));
                        var dateMatch = Regex.Match(beforeDateText, @"(\d{2}-\d{2}-\d{4})");
                        if (dateMatch.Success)
                        {
                            string dateStr = dateMatch.Groups[1].Value;
                            if (DateTime.TryParseExact(dateStr, "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                            {
                                invoice.Date = parsedDate;
                                invoice.invoiceDate = parsedDate;
                                invoice.ProcessedDate = parsedDate;
                            }
                        }
                    }
                }
                
                // Extract PT amount
                var ptMatch = Regex.Match(extractedText, @"SUB TOTAL \(PT\)\s*([0-9,.]+)");
                if (ptMatch.Success)
                {
                    string ptAmountStr = ptMatch.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(ptAmountStr, out decimal ptAmount))
                    {
                        // Convert PT amount if needed based on currency
                        if ((isUSD || isEuro) && exchangeRate > 0)
                        {
                            ptAmount = Math.Round(ptAmount * exchangeRate, 2);
                        }
                        
                        // Deduct PT amount from total
                        if (totalPayableAmount > 0)
                        {
                            totalPayableAmount -= ptAmount;
                            invoice.Total = totalPayableAmount;
                        }
                    }
                }
                
                // Extract customer name - located above "Sold"
                var _soldIndex = extractedText.IndexOf("Sold");
                if (_soldIndex > 0)
                {
                    // SCENARIO 3: For TZS - try to find customer name right before "Sold"
                    if ((string.IsNullOrEmpty(invoice.Customer) || invoice.Currency == "TZS") && invoice.Currency == "TZS")
                    {
                        // Get a portion of text before "Sold"
                        int lookBackLength = Math.Min(200, soldIndex);
                        string textBeforeSold = extractedText.Substring(soldIndex - lookBackLength, lookBackLength);
                        
                        // Split into lines
                        string[] soldPrecedingLines = textBeforeSold.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        // Look at the last few lines before "Sold" 
                        if (soldPrecedingLines.Length >= 3)
                        {
                            // Format often looks like:
                            // "Tel : +255 27 2553941/2554104/5 /"
                            // "EGYPT AIR"
                            // "+255 744 000045"
                            // "Sold"
                            
                            // Check if the line right before "Sold" contains a phone number
                            string lastLine = soldPrecedingLines[soldPrecedingLines.Length - 1].Trim();
                            string secondLastLine = soldPrecedingLines[soldPrecedingLines.Length - 2].Trim();
                            
                            // If the last line is a phone number and second-last line doesn't look like a phone number
                            if (Regex.IsMatch(lastLine, @"^\s*\+?\d") && 
                                !Regex.IsMatch(secondLastLine, @"^\s*\+?\d") && 
                                !secondLastLine.Contains("Tel") && 
                                !secondLastLine.Contains("Fax") && 
                                !secondLastLine.ToLower().Contains("swissport"))
                            {
                                invoice.Customer = secondLastLine;
                            }
                            // Try earlier lines if needed
                            else if (soldPrecedingLines.Length >= 4)
                            {
                                string thirdLastLine = soldPrecedingLines[soldPrecedingLines.Length - 3].Trim();
                                if (!Regex.IsMatch(thirdLastLine, @"^\s*\+?\d") && 
                                    !thirdLastLine.Contains("Tel") && 
                                    !thirdLastLine.Contains("Fax") && 
                                    !thirdLastLine.ToLower().Contains("swissport"))
                                {
                                    invoice.Customer = thirdLastLine;
                                }
                            }
                        }
                    }
                    
                    // Try the original method as well if customer name still not found
                    if (string.IsNullOrEmpty(invoice.Customer))
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
                }
                
                // Extract TIN - appears between "Sold" and "CUST TIN:"
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
                
                // Ensure we have all necessary values
                // If we don't have taxable amount but have total and VAT, calculate it
                if (taxableAmount == 0 && totalPayableAmount > 0 && totalVAT >= 0)
                {
                    taxableAmount = Math.Round(totalPayableAmount - totalVAT, 2);
                    invoice.SubTotal = taxableAmount;
                }
                
                // If we don't have VAT but have taxable amount and it's not zero-rated, calculate VAT
                if (totalVAT == 0 && !isZeroRated && taxableAmount > 0)
                {
                    totalVAT = Math.Round(taxableAmount * 0.18m, 2);
                    
                    // Convert TAX using exchange rate for USD or EURO
                    if ((isUSD || isEuro) && exchangeRate > 0)
                    {
                        totalVAT = Math.Round(totalVAT * exchangeRate, 2);
                    }
                    
                    invoice.Tax = totalVAT;
                }
                
                // If total is not set but we have other values, calculate it
                if (totalPayableAmount == 0 && taxableAmount > 0)
                {
                    totalPayableAmount = Math.Round(taxableAmount + (isZeroRated ? 0 : totalVAT), 2);
                    invoice.Total = totalPayableAmount;
                }
                
                // Create items following the exact pattern from PdfReaderHelper
                // Clear items list to ensure we don't have duplicates
                invoice.Items.Clear();
                 if (nonTaxableAmount == decimal.Parse("0.01"))
                {
                    nonTaxableAmount = 0;
                    invoice.Total -= decimal.Parse("0.01");
                }
                // If there's a non-taxable amount, create an item for it (Code 0017)
                if (nonTaxableAmount > 0)
                {
                    Item item = new Item();
                    item.Code = "0017";
                    item.Amount = nonTaxableAmount;
                    item.UnitPrice = nonTaxableAmount;
                    item.Quantity = 1;
                    item.Tax = 0;
                    item.GrossUnitPrice = nonTaxableAmount;
                    item.Name = "Summary of Service-E";
                    invoice.Items.Add(item);
                }

                 if (taxableAmount > 0) // If there's a taxable amount, create an item for it (Code A or C)
                {
                    Item item = new Item();
                    item.Tax = totalVAT;
                    if (!isZeroRated)
                    {
                        item.Code = "A";
                        item.Name = "Summary of Service-A";
                    }
                    else
                    {
                        item.Code = "0017";
                        item.Name = "Summary of Service-E";
                        item.Tax = 0;
                    }
                    item.Amount = taxableAmount;
                    item.UnitPrice = taxableAmount;
                    item.Quantity = 1;
                   
                    item.GrossUnitPrice = Math.Round(taxableAmount + totalVAT, 2);


                    invoice.Items.Add(item);
                }
                
                // Recalculate the totals based on items (exactly as in PdfReaderHelper)
                if (invoice.Items.Count > 0)
                {
                    invoice.Total = Math.Round(invoice.Items.Sum(s => s.GrossUnitPrice), 2);
                    invoice.SubTotal = Math.Round(invoice.Items.Sum(s => s.Amount), 2);
                }
                
                // Extract exchange rate for USD from the line after TAX INVOICE
                if (isUSD && exchangeRate == 0)
                {
                    var taxInvoiceIndex = extractedText.IndexOf("TAX INVOICE");
                    if (taxInvoiceIndex >= 0)
                    {
                        // Get the next line after TAX INVOICE
                        string afterTaxInvoice = extractedText.Substring(taxInvoiceIndex + "TAX INVOICE".Length);
                        var _lines = afterTaxInvoice.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        if (_lines.Length > 0)
                        {
                            // Try to extract exchange rate from format like "2,630.00 Exempt"
                            var rateMatch = Regex.Match(_lines[0].Trim(), @"^\s*([0-9,.]+)");
                            if (rateMatch.Success)
                            {
                                string rateStr = rateMatch.Groups[1].Value.Replace(",", "");
                                if (decimal.TryParse(rateStr, out decimal rate))
                                {
                                    exchangeRate = Math.Round(rate, 2);
                                    invoice.ExchangeRate = exchangeRate;
                                }
                            }
                        }
                    }
                }

                // Extract VAT after SR- Standard Revenue marker
                if (totalVAT == 0 && !isZeroRated)
                {
                    var srIndex = extractedText.IndexOf("SR- Standard Revenue");
                    if (srIndex >= 0)
                    {
                        // Get the text after SR- Standard Revenue
                        string afterSR = extractedText.Substring(srIndex + "SR- Standard Revenue".Length);
                        var _lines = afterSR.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        // Look for VAT in the next few lines
                        for (int i = 0; i < Math.Min(5, _lines.Length); i++)
                        {
                            if (_lines[i].Trim().StartsWith("VAT"))
                            {
                                // Try to extract the amount after "VAT"
                                var vatMatch = Regex.Match(_lines[i], @"VAT\s*([0-9,.]+)");
                                if (vatMatch.Success)
                                {
                                    string vatStr = vatMatch.Groups[1].Value.Replace(",", "");
                                    if (decimal.TryParse(vatStr, out decimal vatAmount))
                                    {
                                        totalVAT = Math.Round(vatAmount, 2);
                                        
                                        // Convert VAT if it's in USD or EURO
                                        if ((isUSD || isEuro) && exchangeRate > 0)
                                        {
                                            totalVAT = Math.Round(totalVAT * exchangeRate, 2);
                                        }
                                        
                                        invoice.Tax = totalVAT;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return invoice;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error processing tax invoice: {ex.Message}");
                return null;
            }
        }
    }
} 