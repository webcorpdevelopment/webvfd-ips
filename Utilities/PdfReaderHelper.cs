using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static QuestPDF.Helpers.Colors;

namespace InvoiceScanner.Utilities
{
    public static class PdfReaderHelper
    {
        public static string ExtractTextFromPdf(byte[] pdfBytes)
        {
            StringBuilder text = new StringBuilder();

            using (MemoryStream stream = new MemoryStream(pdfBytes))
            using (PdfReader reader = new PdfReader(stream))
            {
                ITextExtractionStrategy strategy = new LocationTextExtractionStrategy();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    string page = "";

                    page = PdfTextExtractor.GetTextFromPage(reader, i, strategy);
                    string[] lines = page.Split('\n');

                    foreach (string line in lines)
                    {
                        // Add each line to the StringBuilder or process as needed
                        text.AppendLine(line);
                    }
                }
            }

            return text.ToString();
        }

       

        public static Invoice populateData(string extractedText, string fileName)
        {
            try
            {
                Invoice invoice = new Invoice();
                string[] possibleFormats = {
                        "yyyy-MM-dd", "dd/MMM/yy", "dd/MMM/yyyy",
                        "dd/MM/yyyy",
                        "MM/dd/yyyy",
                        "MMM dd, yyyy",
                        "MMMM dd, yyyy",
                        "dd MMM yyyy",
                        "dd MMMM yyyy",
                        "yy-MM-dd",
                        "dd-MM-yyyy",
                        "yyyy/MM/dd",
                        "E, MMM dd yyyy",
                        "dd/MMM/yyyy HH:mm:ss",
                        "yyyy-MM-ddTHH:mm:ss",
                        "MM-dd-yyyy"
                    };


                Item item = new Item();
                decimal TotalPayableAmount = 0;
                decimal TotalVAT = 0;
                decimal TaxableAmount = 0;
                decimal NonTaxableAmount = 0;
                using (StringReader reader = new StringReader(extractedText))
                {
                    string line;
                    bool readingItems = false;
                    string previousLine = string.Empty;
                    bool isInvoiceAvailable = true;
                    bool isInvoiceNumberNext = false;
                    bool isNextLineName = false;



                    //.Sanitize(sanitizerConfigs).Trim() 
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            if (line.StartsWith("Currency") && string.IsNullOrEmpty(invoice.Currency))
                            {
                                var items = line.Split(" ");
                                if (items.Length > 0)
                                {
                                    invoice.Currency = items[1];
                                }
                            }

                            if (invoice.Currency == "USD")
                            {
                                if (line.Contains("Non-taxable amount:"))
                                {
                                    var items = line.Split("Non-taxable amount:");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");
                                        NonTaxableAmount = decimal.Parse(total);

                                    }
                                }
                                if (line.Contains("Total payable amount:"))
                                {
                                    var items = line.Split("Total payable amount:");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");
                                        TotalPayableAmount = decimal.Parse(total);
                                        invoice.Total = TotalPayableAmount;
                                    }
                                }
                                if (line.Contains("Total VAT:"))
                                {
                                    var items = line.Split("Total VAT:");

                                    if (items.Length > 0)
                                    {
                                        if (items.Length > 1)
                                        {
                                            if (string.IsNullOrEmpty(items[1]))
                                            {
                                                isNextLineName = true;
                                                continue;
                                            }
                                            var total = items[1].Trim().Replace(",", "");
                                            TotalVAT = decimal.Parse(total);
                                            invoice.Tax = TotalVAT;
                                        }


                                    }

                                }

                                if (isNextLineName)
                                {
                                    var total = line.Trim().Replace(",", "");
                                    TotalVAT = decimal.Parse(total);
                                    invoice.Tax = TotalVAT;
                                    isNextLineName = false;
                                }

                                if (line.Contains("Taxable:"))
                                {
                                    var items = line.Split("Taxable:");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");
                                        TaxableAmount = decimal.Parse(total);
                                        invoice.SubTotal = TaxableAmount;
                                    }
                                }
                            }
                            else
                            {
                                if (line.StartsWith("Non-taxable amount"))
                                {
                                    if (line.Contains("TZS Non-taxable amount:"))
                                    {
                                        line = line.Replace("TZS Non-taxable amount:", "");
                                    }
                                    var items = line.Split("amount");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");

                                        NonTaxableAmount = decimal.Parse(total);



                                    }
                                }
                                if (line.StartsWith("Total payable amount"))
                                {
                                    var items = line.Split("amount");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");
                                        TotalPayableAmount = decimal.Parse(total);
                                        invoice.Total = TotalPayableAmount;
                                    }
                                }
                                if (line.StartsWith("Total VAT"))
                                {
                                    var items = line.Split("VAT");

                                    if (items.Length > 0)
                                    {
                                        if (items.Length > 1)
                                        {
                                            if (string.IsNullOrEmpty(items[1]))
                                            {
                                                isNextLineName = true;
                                                continue;
                                            }
                                            var total = items[1].Trim().Replace(",", "");
                                            TotalVAT = decimal.Parse(total);
                                            invoice.Tax = TotalVAT;
                                        }


                                    }

                                }

                                if (isNextLineName)
                                {
                                    var total = line.Trim().Replace(",", "");
                                    TotalVAT = decimal.Parse(total);
                                    invoice.Tax = TotalVAT;
                                    isNextLineName = false;
                                }

                                if (line.StartsWith("Taxable Amount(18%)"))
                                {
                                    var items = line.Split("Amount(18%)");
                                    if (items.Length > 0)
                                    {
                                        var total = items[1].Trim().Replace(",", "");
                                        TaxableAmount = decimal.Parse(total);
                                        invoice.SubTotal = TaxableAmount;
                                    }
                                }
                            }

                            if (line.StartsWith("Sold to:") && string.IsNullOrEmpty(invoice.Customer))
                            {
                                var items = line.Split(": ");
                                if (items.Length > 0)
                                {
                                    invoice.Customer = items[1];

                                    if (invoice.Customer.Contains("/"))
                                    {
                                        var names = invoice.Customer.Split("/");
                                        if (names.Length > 1)
                                        {
                                            invoice.Customer = names[0];
                                        }

                                    }
                                }

                            }

                            if (line.Contains("Invoice Number") && string.IsNullOrEmpty(invoice.Number))
                            {
                                var items = line.Split("Number");
                                if (items.Length > 0)
                                {
                                    invoice.Number = items[1].Trim();
                                }

                            }


                            if (line.Contains("Date"))
                            {
                                var items = line.Split(" ");
                                if (items.Length > 1)
                                {


                                    DateTime parsedDate;

                                    foreach (string format in possibleFormats)
                                    {
                                        if (DateTime.TryParseExact(items[1], format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                                        {
                                            invoice.invoiceDate = parsedDate;
                                            break;
                                        }
                                    }

                                }
                                else
                                {
                                    // Date might be on the next line
                                    string nextLine = reader.ReadLine();
                                    if (!string.IsNullOrEmpty(nextLine))
                                    {
                                        DateTime parsedDate;
                                        string dateValue = nextLine.Trim();
                                        
                                        foreach (string format in possibleFormats)
                                        {
                                            if (DateTime.TryParseExact(dateValue, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                                            {
                                                invoice.invoiceDate = parsedDate;
                                                break;
                                            }
                                        }
                                    }
                                }


                               // invoice.Date = DateTime.Now;
                            }

                            if (line.ToUpper().Contains("TIN:") && string.IsNullOrEmpty(invoice.TIN))
                            {
                                var items = line.Split(" ");
                                if (items.Length > 1)
                                {
                                    invoice.TIN = items[1].Replace("-", "").Replace(":", "").Trim();
                                    if (invoice.TIN.Length < 9)
                                    {
                                        invoice.TIN = "";
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            return null;
                        }
                        
                    }
                    reader.Close();
                }

                invoice.Filename = fileName;
                invoice.OcrText = extractedText;
                invoice.Status = "PENDING";
                //  invoice.BusinessId = user.BusinessId.Value;
                invoice.ProcessedDate = DateTime.UtcNow;
                invoice.Currency = "TZS";

                //   TotalPayableAmount   = TotalPayableAmount - NonTaxableAmount;
                if (NonTaxableAmount == decimal.Parse("0.01"))
                {
                    NonTaxableAmount = 0;
                    invoice.Total -= decimal.Parse("0.01");
                }

                if (NonTaxableAmount > 0)
                {
                    item = new Item();
                    item.Code = "0017";
                    item.Amount = NonTaxableAmount;
                    item.UnitPrice = NonTaxableAmount;
                    item.Quantity = 1;
                    item.Tax = 0;
                    item.GrossUnitPrice = NonTaxableAmount;
                    item.Name = "Summary of Service-E";
                    invoice.Items.Add(item);
                }

                if (TaxableAmount > 0)
                {
                    item = new Item();
                    item.Code = "A";
                    item.Amount = TaxableAmount;
                    item.UnitPrice = TaxableAmount;
                    item.Quantity = 1;
                    item.Tax = TotalVAT;
                    item.GrossUnitPrice = TaxableAmount + TotalVAT;//TotalPayableAmount - NonTaxableAmount;
                    item.Name = "Summary of Service-A";
                    invoice.Items.Add(item);
                  
                }

                var tot = invoice.Total;
                var subtt = invoice.SubTotal;
                invoice.Total = Math.Round(invoice.Items.Sum(s => s.GrossUnitPrice), 2);
                invoice.SubTotal = Math.Round(invoice.Items.Sum(s => s.Amount), 2);

                return invoice;

            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public static Invoice populateVerticalInvoiceData(string extractedText, string fileName, bool isZeroRated, bool isUSD, bool isEuro)
        {
            try
            {
                Invoice invoice = new Invoice();
                string[] possibleFormats = {
                        "yyyy-MM-dd", "dd/MMM/yy", "dd/MMM/yyyy",
                        "dd/MM/yyyy",
                        "MM/dd/yyyy",
                        "MMM dd, yyyy",
                        "MMMM dd, yyyy",
                        "dd MMM yyyy",
                        "dd MMMM yyyy",
                        "yy-MM-dd",
                        "dd-MM-yyyy",
                        "yyyy/MM/dd",
                        "E, MMM dd yyyy",
                        "dd/MMM/yyyy HH:mm:ss",
                        "yyyy-MM-ddTHH:mm:ss",
                        "MM-dd-yyyy"
                    };


                Item item = new Item();
                decimal TotalPayableAmount = 0;
                decimal TotalVAT = 0;
                decimal TaxableAmount = 0;
                decimal NonTaxableAmount = 0;
                using (StringReader reader = new StringReader(extractedText))
                {
                    string line;
                    bool readingItems = false;
                    string previousLine = string.Empty;
                    bool isInvoiceAvailable = true;
                    bool isInvoiceNumberNext = false;
                    bool isNextLineName = false;

                    string totalAmount = ExtractValue(extractedText, "TZS Total TZS:", "\n").Replace(",","").Trim();
                    if(string.IsNullOrEmpty(totalAmount))
                    {
                         totalAmount = ExtractValue(extractedText, "USD Total TZS:", "\n").Replace(",", "").Trim();
                        TotalPayableAmount = decimal.Parse(totalAmount);
                    }
                    else
                    {
                        TotalPayableAmount = decimal.Parse(totalAmount);
                    }

                    invoice.Total = TotalPayableAmount;

                    if(isEuro) { invoice.Currency = "EURO"; }
                   else if (isUSD) { invoice.Currency = "USD"; } 
                    else { invoice.Currency = "TZS"; }

                    if(!isZeroRated)
                    {
                        var untaxable_amount =Convert.ToDouble(TotalPayableAmount) / 1.18;
                        invoice.SubTotal = Math.Round( Convert.ToDecimal(untaxable_amount),2);

                        invoice.Tax =Math.Round( Convert.ToDecimal(Convert.ToDouble( invoice.SubTotal) * 0.18),2);
                    }
                    else
                    {
                        invoice.SubTotal = invoice.Total;
                    }

                    invoice.Number = ExtractValue(extractedText, "TAX INVOICE", "Number:").Trim();

                    // Extract date
                    string date = ExtractValue(extractedText, "SWISSPORT TANZANIA PLC", "Date:").Trim();
                    if (!string.IsNullOrEmpty(date))
                    {


                        DateTime parsedDate;

                        foreach (string format in possibleFormats)
                        {
                            if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                            {
                                invoice.invoiceDate = parsedDate;
                                break;
                            }
                        }

                        invoice.Date = DateTime.Now;

                    }
                    else
                    {
                        invoice.Date = DateTime.Now;
                    }

                    // Extract customer name
                   invoice.Customer = ExtractValue(extractedText, "Fax: 255 27 2554553", "Sold").Trim();

                    // Extract customer TIN
                    invoice.TIN = ExtractValue(extractedText, "Sold", "CUST TIN:").Replace("-", "").Replace(":", "").Trim();
                    if (invoice.TIN.Length < 9)
                    {
                        invoice.TIN = "";
                    }
                  
                }

                invoice.Filename = fileName;
                invoice.OcrText = extractedText;
                invoice.Status = "PENDING";
                //  invoice.BusinessId = user.BusinessId.Value;
                invoice.ProcessedDate = DateTime.UtcNow;
                invoice.Status = "FAILED";
                invoice.Currency = "TZS";

                ////   TotalPayableAmount   = TotalPayableAmount - NonTaxableAmount;
                //if (NonTaxableAmount == decimal.Parse("0.01"))
                //{
                //    NonTaxableAmount = 0;
                //}

                //if (NonTaxableAmount > 0)
                //{
                //    item = new Item();
                //    item.Code = "0017";
                //    item.Amount = NonTaxableAmount;
                //    item.UnitPrice = NonTaxableAmount;
                //    item.Quantity = 1;
                //    item.Tax = 0;
                //    item.GrossUnitPrice = NonTaxableAmount;
                //    item.Name = "Summary of Service-E";
                //    invoice.Items.Add(item);
                //}

                if (invoice.SubTotal > 0 && !isZeroRated)
                {
                    item = new Item();
                    item.Code = "A";
                    item.Amount = invoice.SubTotal;
                    item.UnitPrice = invoice.SubTotal;
                    item.Quantity = 1;
                    item.Tax = invoice.Tax;
                    item.GrossUnitPrice = invoice.SubTotal + invoice.Tax;//TotalPayableAmount - NonTaxableAmount;
                    item.Name = "Summary of Service-A";
                    invoice.Items.Add(item);

                }
                else if (invoice.SubTotal > 0 && isZeroRated)
                {
                    item = new Item();
                    item.Code = "C";
                    item.Amount = invoice.SubTotal;
                    item.UnitPrice = invoice.SubTotal;
                    item.Quantity = 1;
                    item.Tax = 0;
                    item.GrossUnitPrice = invoice.SubTotal ;//TotalPayableAmount - NonTaxableAmount;
                    item.Name = "Summary of Service-C";
                    invoice.Items.Add(item);
                }


                return invoice;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        static string ExtractValue(string text, string startMarker, string endMarker)
        {
            int startIndex = text.IndexOf(startMarker);
            if (startIndex == -1) return string.Empty;
            startIndex += startMarker.Length;

            int endIndex = text.IndexOf(endMarker, startIndex);
            if (endIndex == -1) return string.Empty;

            return text.Substring(startIndex, endIndex - startIndex).Trim();
        }

    }
}
