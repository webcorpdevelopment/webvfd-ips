using Arc.StringSanitizer;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using PdfSharp.Pdf.Content.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InvoiceScanner
{
    public static class InvoiceReader
    {
       static decimal ExchangeRate = 0;
        static decimal ExchangeRateEUR = 0;
        static string[] possibleFormats = {
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
        #region Swissport
        public static Invoice ReadSwissportTZS(string text, Byte[] bytes,decimal HorizontalExchangeRate)
        {
            Invoice invoice = new Invoice();
            bool isUSD = false;
            
            var sanitizerConfigs = new List<SanitizerConfig>()
            {
                new SanitizerConfig("~", string.Empty),
                new SanitizerConfig("oof", string.Empty),
                new SanitizerConfig("&nbsp;", " ")
            };

            var numericSanitizerConfigs = new List<SanitizerConfig>()
            {
                new SanitizerConfig("~", string.Empty),
                new SanitizerConfig("(", string.Empty),
                new SanitizerConfig(")", string.Empty),
                new SanitizerConfig("f", string.Empty),
                
                new SanitizerConfig("&nbsp;", " ")
            };

            var CustomerArea = new System.Drawing.Rectangle() { X = 0, Y = 0, Width = 700, Height = 500 };
            var CustomerTINArea =  new System.Drawing.Rectangle() { X = 1000, Y = 0, Width = 900, Height = 400 };

            string[] lines = text.Split(new string[] { "\\r\\n" }, StringSplitOptions.None);
            text = string.Join("\r\n", lines);

            // string CustomerAreaText = getExtractedText(CustomerArea, bytes, "");
            // string CustomerTINAreaText = getExtractedText(CustomerTINArea, bytes, "");

            // string finalExtractedText = CustomerAreaText + "\n" + CustomerTINAreaText;

            if (text.Contains("USD") || text.Contains("UsD"))
            {
                isUSD = true;
                invoice.Currency = "USD";
            }
            else
            {
                invoice.Currency = "TZS";
            }
            // Split the concatenated string into lines
            // string[] lines = finalExtractedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Filter out empty lines
            // lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

            // Join the non-empty lines back into a string
            //  string resultText = string.Join(Environment.NewLine, lines);
            decimal TotalPayableAmount = 0;
            using (StringReader reader = new StringReader(text))
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
                    if (line.ToLower().StartsWith("Total payable amount"))
                    {
                        var items = line.Split("amount");
                        if (items.Length > 0)
                        {
                            var total = items[1].Trim().Replace(",", "");
                            TotalPayableAmount = decimal.Parse(total.Replace("f", ""));
                        }
                    }
                    if (line.StartsWith("Sold to:") || line.ToLower().StartsWith("Sold to:"))
                    {
                        var items = line.Split(":");

                        if(items.Length > 0)
                        {
                            if (items.Length > 1)
                            {
                                if (string.IsNullOrEmpty(items[1]))
                                {
                                    isNextLineName = true;
                                    continue;
                                }
                                invoice.Customer = line.Replace(":", "").Replace(items[0], "").Sanitize(sanitizerConfigs).Trim();
                            }
                            else
                            {
                                if (previousLine != "")
                                {
                                    invoice.Customer = previousLine.Sanitize(sanitizerConfigs).Trim();
                                    Console.WriteLine($"Parsed customer: {invoice.Customer}");
                                }
                                else
                                {
                                   // invoice.Customer = CustomerAreaText.Split("\n")[0].Sanitize(sanitizerConfigs).Trim();
                                }



                            }

                        }

                    }

                    if (isNextLineName)
                    {
                        invoice.Customer = line.Sanitize(sanitizerConfigs).Trim();
                        isNextLineName = false;
                    }

                    previousLine = line;

                    if (!isInvoiceAvailable && isInvoiceNumberNext)
                    {
                        invoice.Number = line;
                        if (invoice.Number != "")
                        {
                            isInvoiceAvailable = true;
                        }
                    }
                    if (line.Contains("Invoice Number"))
                    {
                        var items = line.Split("Number");
                        if (items.Length > 0)
                        {
                            invoice.Number = items[1].Trim();
                        }
                            if(invoice.Number == "")
                        {
                            isInvoiceAvailable = false;
                            
                        }
                        isInvoiceNumberNext = true;
                    }
                   
                    
                    if (line.Contains("Date"))
                    {
                        var items = line.Split(" ");
                        if(items.Length > 0)
                        {
                            string pattern = @"\b\d{1,2}/[A-Za-z]{3}/\d{2}\b";
                            line = line.Replace("0ct", "Oct");
                            // Use Regex.Match to find and extract the date
                            Match match = Regex.Match(line, pattern);

                            if (match.Success)
                            {
                                string date = match.Value;
                               // string date = items[1].Trim();
                                if (date.Contains("0ct"))
                                {
                                    date = date.Replace("0ct", "Oct");
                                    //if (DateTime.TryParseExact(date, "dd/MMM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                                    //{
                                    //    invoice.Date = parsedDate;
                                    //}
                                    DateTime parsedDate;

                                    foreach (string format in possibleFormats)
                                    {
                                        if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                                        {
                                            invoice.Date = parsedDate;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    DateTime parsedDate;

                                    foreach (string format in possibleFormats)
                                    {
                                        if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                                        {
                                            invoice.Date = parsedDate;
                                            break;
                                        }
                                    }
                                    //if (DateTime.TryParseExact(date, "dd/MMM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                                    //{
                                    //    invoice.Date = parsedDate;
                                    //}
                                }
                            }



                        }

                        invoice.Date = DateTime.Now;
                    }

                    //if (line.Contains("TIN") && line.Contains("VRN"))
                    //{
                    //    var items = line.Split("TIN");
                    //    invoice.TIN = items[1].Replace("-", "").Replace(":", "").Trim();
                    //}
                    if (line.ToUpper().Contains("TIN:"))
                    {
                        var items = line.Split(" ");
                        if (items.Length > 0) { 
                        invoice.TIN = items[1].Replace("-", "").Replace(":", "").Trim();
                        if (invoice.TIN.Length < 9)
                        {
                            invoice.TIN = "";
                        }
                    }
                    }
                }
                reader.Close();
            }
            List<string> itemNames = new List<string>();
           

            //string pattern12 = @"taxable VAT(.*?)Summar";
            string pattern12 = @"taxable VAT(.*?)(Summar|Net amount)";

            Match match12 = Regex.Match(text, pattern12, RegexOptions.Singleline);

            if (match12.Success)
            {
                string extractedText = match12.Groups[1].Value.Trim();
                var items = extractedText.Split("\n").ToList();
                int i = 0;
                Item _item = new Item() { Name = string.Empty };

                foreach (var item in items)
                {
                    _item = new Item();

                    string datePattern = @"\b\d{1,2}\.\s[A-Za-z]{3}\s\d{4}\b";
                    string final_text = item.Replace("]", "").Replace("\r", "").Replace("}", "").Replace("J", "").Replace("|", "").Replace("p","").Replace("P","").Trim();
                    // Use Regex.Match to find the date
                    Match dateMatch = Regex.Match(final_text, datePattern);
                    string[] decimal_parts = final_text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // Initialize a counter for decimal numbers
                    int decimalCount = 0;

                    // Iterate through the parts and count decimal numbers
                    foreach (string part in decimal_parts)
                    {
                        var final = part.Replace("]", "").Replace("\r", "").Replace("}", "").Replace("J", "").Replace("|", "").Trim();

                        if (decimal.TryParse(final, out decimal result))
                        {
                            var cc = part.Split(".").ToArray();
                            if (cc.Length> 0 && part.Contains("."))
                            {
                                if(cc[1] != "")
                                    decimalCount++;
                            }
                                
                        }
                    }
                    // Count the matched numbers
                    if (decimalCount < 2)
                    {
                        continue;
                    }
                    if (dateMatch.Success)
                    {
                        string date = dateMatch.Value;

                        string[] parts = Regex.Split(final_text, Regex.Escape(date));

                        if (parts.Length > 0)
                        {
                             final_text = parts[1];

                        }
                    }
                    
                    final_text = final_text.Replace("]", "").Replace("\r", "").Replace("}", "").Replace("J", "").Replace("|", "").Replace(",", "").Trim();
                    final_text = Regex.Replace(final_text, datePattern, "");
                  
                    string pattern = @"\b\d{2}/\s\w+/\s\d{2}\s";

                    final_text = Regex.Replace(final_text, pattern, " ");
                     string[] _lines = final_text.Split(" ", StringSplitOptions.None);

                    // Filter out empty lines
                     _lines = _lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

                    var item_parts =string.Join(" ",_lines.ToArray()) .Replace("/", " ").Replace("{ ", "").Trim().Split(' ');

                    var reversed = item_parts.Reverse();
                    var itemCode = item_parts.FirstOrDefault(x => x.Contains("-"));
                    if (itemCode != null)
                    {
                        var codeIndex = item_parts.ToList().IndexOf(itemCode);
                        _item.Code = itemCode.Replace("U","");
                        string quantityText = "";
                        string amount = "";
                        string unitprice = "";

                        if (reversed.ToArray()[0].Contains("Y") || reversed.ToArray()[0].Contains("N"))
                        {
                            quantityText = reversed.ToArray()[3].Replace(",", "");
                            amount = reversed.ToArray()[1].Replace(",", "").Replace("f", "");
                            unitprice = reversed.ToArray()[2].Replace(",", "").Replace("f", "");

                        }
                        else
                        {
                            quantityText = reversed.ToArray()[2].Replace(",", "");
                            amount = reversed.ToArray()[0].Replace(",", "").Replace("f", "");
                            unitprice = reversed.ToArray()[1].Replace(",", "").Replace("f", "");

                        }

                        _item.Quantity =decimal.Parse(quantityText);
                        _item.Quantity = Math.Round(_item.Quantity, 2);

                        _item.Amount = Math.Round(decimal.Parse(amount), 2);
                        _item.UnitPrice = Math.Round(_item.Amount / _item.Quantity, 2);

                       
                        var test_price =  Math.Round(decimal.Parse(unitprice), 2);
                        int maxDifference = 100;
                        var difference = Math.Abs(_item.UnitPrice - test_price);

                        if (difference <= maxDifference)
                        {
                            Console.WriteLine("The difference is within the allowed range.");
                        }
                        else
                        {
                            _item.UnitPrice = test_price;
                            _item.Amount = _item.UnitPrice * _item.Quantity;
                        }

                        if (isUSD)
                        {
                            _item.Amount = _item.Amount * HorizontalExchangeRate;
                            _item.UnitPrice = _item.UnitPrice * HorizontalExchangeRate;
                        }

                        List<string> parts = final_text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                        if (parts.Count >= 5)
                        {
                            var name_parts = new List<string>();
                            foreach(var part in parts.SkipLast(1))
                            {
                                if(!decimal.TryParse(part, out decimal _result))
                                {
                                    name_parts.Add(part);
                                }
                            }

                            var sanitizationConfigs = new List<SanitizerConfig>()
                                {
                                    // Remove all digits
                                    new SanitizerConfig(@"\d", string.Empty),
                                     new SanitizerConfig(@"\r", string.Empty),
                                    // Remove dates in common formats (e.g., dd/mm/yy, dd-mm-yy, dd.mm.yy)
                                    new SanitizerConfig(@"\b\d{1,2}[-./]\d{1,2}[-./]\d{2}\b", string.Empty),
                                    new SanitizerConfig(@"\b\d{1,2}[-./]\d{1,2}[-./]\d{4}\b", string.Empty),
    
                                    // Remove dates with month names (e.g., dd Month yyyy)
                                    new SanitizerConfig(@"\b\d{1,2} (?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4}\b", string.Empty),

                                    new SanitizerConfig("~", string.Empty),
                                    new SanitizerConfig("(", string.Empty),
                                    new SanitizerConfig(")", string.Empty),
                                    new SanitizerConfig("f", string.Empty),
                                   //  new SanitizerConfig("Se", string.Empty),
                                      new SanitizerConfig("lu", string.Empty), 
                                    new SanitizerConfig(@"\d{1,2}/ [A-Z][a-z]{2}/ \d{2} \d+\.\d{2}[a-zA-Z]?", string.Empty), // Remove date and amount pattern

                                    new SanitizerConfig("&nbsp;", " "),
                                    new SanitizerConfig(@"\b\d{1,2} (?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{2}\b", string.Empty)
                                };
                            // Join the remaining parts back into a single string
                            _item.Name = string.Join(" ", name_parts);
                            _item.Name = _item.Name.Sanitize(sanitizationConfigs).Trim();

                            if(_item.Name.StartsWith("Se "))
                            {
                                _item.Name.Replace("Se ", "");
                            }
                            if (_item.Name.StartsWith("ul "))
                            {
                                _item.Name.Replace("ul ", "");
                            }
                            // Split the input string by spaces
                            var nam_parts = _item.Name.Split(' ').ToList();

                            string name_result = "";
                            // Iterate through the parts and add only non-numeric parts to the result
                            foreach (string part in nam_parts)
                            {
                                // Check if the part is numeric (contains digits)
                                if (!ContainsDigits(part))
                                {
                                    // Append the non-numeric part to the result
                                    name_result += part + " ";
                                }
                            }

                            // Remove the trailing space, if any
                            _item.Name = name_result.Trim();
                            if(_item.Name.EndsWith("/"))
                                  _item.Name = _item.Name.Substring(0, _item.Name.Length - 4);

                            if (items.IndexOf(item) + 1 < items.Count)
                            {
                                int name1_matches = 0;
                                int name2_matches = 0;
                                bool checkNextName = false;
                                if (items.IndexOf(item) + 1 < items.Count)
                                {
                                    
                                        foreach (string part in items[items.IndexOf(item) + 1].Split().ToArray())
                                        {
                                            var final = part.Replace("]", "").Replace("\r", "").Replace("}", "").Replace("J", "").Replace("|", "").Trim();
                                            if (decimal.TryParse(final, out decimal result))
                                            {
                                                var cc = part.Split(".").ToArray();
                                                if (cc.Length > 0 && part.Contains("."))
                                                {
                                                    if (cc[1] != "")
                                                        name1_matches++;
                                                }

                                            }
                                        }
                                        
                                    
                                  
                                }
                                var index = items.IndexOf(item) + 2;

                                if (items.IndexOf(item) + 2 < items.Count )
                                {
                                   
                                        foreach (string part in items[items.IndexOf(item) + 2].Split().ToArray())
                                        {
                                            var final = part.Replace("]", "").Replace("\r", "").Replace("}", "").Replace("J", "").Replace("|", "").Trim();

                                            if (decimal.TryParse(final, out decimal result))
                                            {
                                                var cc = part.Split(".").ToArray();
                                                if (cc.Length > 0 && part.Contains("."))
                                                {
                                                    if (cc[1] != "")
                                                        name2_matches++;
                                                }

                                            }
                                        }
                                        
                                    
                                        
                                }
                              

                                if (name1_matches < 2)
                                {
                                    var input = items[items.IndexOf(item) + 1];
                                    string result = Regex.Replace(input, @"\d+", "");

                                    _item.Name += " " + result;
                                    checkNextName = true;
                                }
                                if (name2_matches < 2 && index<items.Count && checkNextName)
                                {
                                    var input = items[items.IndexOf(item) + 2];
                                    string result = Regex.Replace(input, @"\d+", " ");

                                    _item.Name += " " + result;
                                    _item.Name = _item.Name.Replace("\r", "");
                                }
                                _item.Name = _item.Name.Sanitize(sanitizationConfigs).Trim();
                                checkNextName = false;
                            }
                        }

                        if (!reversed.ToArray()[0].Contains("N"))
                        {
                            var totalAmount = _item.Amount * 1.18m;
                            _item.Tax = Math.Round(totalAmount - _item.Amount, 2);
                        }

                        i++;
                        invoice.Items.Add(_item);
                    }

                }
            }

            invoice.SubTotal = invoice.Items.Sum(x => x.Amount);
            invoice.SubTotal = Math.Round(invoice.SubTotal, 2);
            invoice.Total = Math.Round(invoice.Items.Sum(x => Math.Round(x.GrossUnitPrice * x.Quantity, 2)), 2);
            invoice.Tax = Math.Round(invoice.Total - invoice.SubTotal, 2);

            return invoice;
        }
        static bool ContainsDigits(string input)
        {
            foreach (char c in input)
            {
                if (char.IsDigit(c))
                {
                    return true;
                }
            }
            return false;
        }
        static bool IsNumber(string s)
        {
            return double.TryParse(s, out _);
        }

        public static Invoice ReadSwissportUSD( Byte[] bytes, string text, decimal _ExchangeRate,decimal _ExchangeRateEUR)
        {
            
            Invoice invoice = new Invoice();

            ExchangeRate = _ExchangeRate;
            ExchangeRateEUR = _ExchangeRateEUR;

            invoice = getInvoiceData(bytes, text);

           

                return invoice;
            
        }

        private static Invoice getInvoiceData(byte[] bytes, string extractedtext)
        {
            try
            {
                Invoice invoice = new Invoice();
                List<Item> itemList = new List<Item>();
                string text = string.Empty;
                var sanitizerConfigs = new List<SanitizerConfig>()
            {
                new SanitizerConfig("~", string.Empty),
                new SanitizerConfig("oof", string.Empty),
                new SanitizerConfig("&nbsp;", " ")
            };

                var numericSanitizerConfigs = new List<SanitizerConfig>()
            {
                new SanitizerConfig("~", string.Empty),
                new SanitizerConfig("(", string.Empty),
                new SanitizerConfig(")", string.Empty),
                new SanitizerConfig("f", string.Empty),

                new SanitizerConfig("&nbsp;", " ")
            };

                var InvoiceNoDateArea = new System.Drawing.Rectangle() { X = 1241, Y = 0, Width = 600, Height = 500 };
                var TINArea = new System.Drawing.Rectangle() { X = 600, Y = 0, Width = 600, Height = 500 };
                var CustomerArea = new System.Drawing.Rectangle() { X = 0, Y = 600, Width = 800, Height = 200 };
                var ItemsArea = new System.Drawing.Rectangle() { X = 0, Y = 0, Width = 0, Height = 0 };

                string CustomerAreaText = getExtractedText(CustomerArea, bytes, extractedtext);
                string InvoiceNoDateAreaText = getExtractedText(InvoiceNoDateArea, bytes, extractedtext);
                string TINAreaAreaText = getExtractedText(TINArea, bytes, extractedtext);
                string ItemsAreaText = getExtractedText(ItemsArea, bytes, extractedtext);


                string finalExtractedText = InvoiceNoDateAreaText + "\n" + TINAreaAreaText + "\n" + CustomerAreaText;
                using (
                    
                    StringReader reader = new StringReader(finalExtractedText))
                {
                    string line;
                    bool readingItems = false;
                    string previousLine = string.Empty;

                    //.Sanitize(sanitizerConfigs).Trim()
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.ToLower().StartsWith("sold"))
                        {
                            var items = line.Split(" ");

                            if (items.Length > 1)
                            {
                                invoice.Customer = line.Replace(items[0], "").Sanitize(sanitizerConfigs).Trim();
                            }
                            else
                            {
                                if (previousLine != "")
                                {
                                    invoice.Customer = previousLine.Sanitize(sanitizerConfigs).Trim();
                                    Console.WriteLine($"Parsed customer: {invoice.Customer}");
                                }
                                else
                                {
                                    invoice.Customer = CustomerAreaText.Split("\n")[0].Sanitize(sanitizerConfigs).Trim();
                                }



                            }

                        }

                        previousLine = line;

                        if (line.StartsWith("Number:"))
                        {
                            var items = line.Split(':');
                            invoice.Number = items[1].Trim();
                        }

                        if (line.StartsWith("Date:"))
                        {
                            var items = line.Split(':');
                           // invoice.Date = DateTime.ParseExact(items[1].Trim(), "MM-dd-yyyy", CultureInfo.InvariantCulture);
                           string date = items[1].Trim();
                            DateTime parsedDate;

                            foreach (string format in possibleFormats)
                            {
                                if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                                {
                                    invoice.Date = parsedDate;
                                    break;
                                }
                            }
                        }

                        //if (line.Contains("TIN") && line.Contains("VRN"))
                        //{
                        //    var items = line.Split("TIN");
                        //    invoice.TIN = items[1].Replace("-", "").Replace(":", "").Trim();
                        //}
                        if (line.ToUpper().Contains("TIN:"))
                        {
                            var items = line.Split(":");
                            invoice.TIN = items[1].Replace("-", "").Replace(":", "").Trim();
                            if(invoice.TIN.Length < 9)
                            {
                                invoice.TIN = "";
                            }
                        }
                    }
                    reader.Close();
                }

                using (StringReader reader = new StringReader(ItemsAreaText))
                {
                    string line;
                    bool readingItems = false;
                    string previousLine = string.Empty;
                    bool isItemsUsdInvoice = false;
                    bool isItemsEURInvoice = false;
                    bool isItemsTZSInvoice = false;
                    bool isVATInvoice = false;
                    bool isInvoiceWithGoodFomart = false;
                    bool isInvoiceWithTwoOrMorePages = false;
                    string previousVATValueLine = string.Empty;
                    int filePage = 0;
                    invoice.Currency = "TZS";
                    while ((line = reader.ReadLine()) != null)
                    {
                        if ( line.ToUpper().Contains("USD"))
                        {
                            //Wait for response for if the invoice items are eur or usd
                            var items = line.ToLower().Split(" ");
                            if (line.ToLower().Split(" ")[0].ToLower().Contains("usd"))
                            {
                                isItemsUsdInvoice =true;
                                invoice.Currency = "USD";
                            }
                        }
                        if (line.ToLower().Contains("eur"))
                        {
                            //Wait for response for if the invoice items are eur or usd
                            var items = line.ToLower().Split(" ");
                            if (line.ToLower().Split(" ")[0].ToLower().Contains("eur"))
                            {
                                isItemsEURInvoice = true;
                                invoice.Currency = "EUR";
                            }
                        }
                        if (line.StartsWith("Description/Comments") || line.Contains("Quantity") )
                        {
                            filePage++;
                            var items = line.Trim().Split("Comments");
                            if (items[1] != "")
                            {
                                isInvoiceWithGoodFomart = true;
                            }
                        }
                        if (isInvoiceWithGoodFomart)
                        {
                            if (line.ToLower().Contains("total vat"))
                            {
                           
                                var totalVAT = line.Split("VAT")[1];
                                if (decimal.Parse(totalVAT) > 0)
                                {
                                    isVATInvoice = true;
                                }
                             }
                           
                        }
                        else
                        {
                            if(line.StartsWith("Total Amount"))
                            {

                                string pattern0 = @"Total VAT[\s\S]*?(?=\r?\nTotal Amount)";

                                Match match = Regex.Match(ItemsAreaText, pattern0);
                                if (match.Success)
                                {
                                    string extractedText = match.Value.Trim();
                                    if(extractedText.Split('\n').Length > 1)
                                    {
                                        var parts = extractedText.Split('\n').Reverse().ToList();
                                        if (decimal.Parse(parts[0]) > 0)
                                        {
                                            isVATInvoice = true;
                                        }
                                    }
                                    else
                                    {
                                        var totalVAT = extractedText.Split("VAT");
                                        if (decimal.Parse(totalVAT[1]) > 0)
                                        {
                                            isVATInvoice = true;
                                        }
                                    }
                                  
                                }
                               
                              
                                    
                            }
                        }
                        previousVATValueLine = line.Trim();
                    }

                    // for temporally use to check multiple page
                    //if (filePage > 1)
                    //{
                    //    isItemsUsdInvoice = true;
                    //}

                
                        string pattern = @"/Comments Quantity Unit Price Line Total(.*?)(BEING|Being|Due)";
                        Item item = new Item() { Name = string.Empty };

                        if (isInvoiceWithGoodFomart)
                        {
                            Match match = Regex.Match(ItemsAreaText, pattern, RegexOptions.Singleline);
                            if (match.Success)
                            {
                                string extractedText = match.Groups[1].Value.Trim();
                                string[] lines = { };
                                int index = 0;
                                if (filePage > 1)
                                {
                                //    var textLines = extractedText.Split("\n");
                                    string[] textLines = extractedText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                                    List<string> newList = new List<string>();
                                   List<string> incompleteLines = new List<string>();
                                int previousIteration = 1;
                                int currentIteration = 1;
                                for (int i = 1; i < textLines.Length; i++)
                                    {

                                    if (textLines[i].Contains("/Comments") || textLines[i].Contains("USD") || textLines[i].Contains("TZS") || textLines[i].Contains("EUR"))
                                    {
                                        continue;
                                    }
                                    if (textLines[i].Split(" ").Length < 3)
                                        {
                                            continue;
                                        }
                                     if (textLines[i].Split(" ").Length == 3 )
                                        {
                                            var subitem = textLines[i + 1] + " " + textLines[i];
                                            incompleteLines.Add(subitem);
                                        previousIteration = i;
                                        continue;
                                    }

                                        currentIteration = i + 1;
                                        if(i == previousIteration + 1)
                                        {
                                            continue;
                                        }
                                      
                                        newList.Add(textLines[i]);
                                    }

                                    if (newList.Count > 0)
                                    {
                                   
                                        if (incompleteLines.Count > 0)
                                    {
                                        List<string> combinedList = new List<string>(newList);
                                        for (int i = 0; i < incompleteLines.Count; i++)
                                        {
                                            if (incompleteLines[i].Contains("/Comments"))
                                            {
                                                continue;
                                            }
                                            combinedList.Add(incompleteLines[i]);
                                        }
                                       
                                     //   combinedList.AddRange(incompleteLines);
                                        lines = combinedList.ToArray();
                                    }
                                    else
                                    {
                                        lines = newList.ToArray();
                                        index = 0;
                                    }
                                       
                                    }
                                }
                                else
                                {
                                    lines = extractedText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                                    index = 1;
                                }


                                for (int i=index; i < lines.Length; i++)
                                {
                                    item = new Item();
                                    var _line = Regex.Replace(lines[i], @"\s+", " ");
                                    var parts = _line.Split(' ');
                                    List<string> reversed = new List<string>();
                                    var test_reversed = parts.Reverse().ToList();
                                    var qty = test_reversed.FirstOrDefault();
                                    var _unitPrice = test_reversed.Skip(1).FirstOrDefault();
                                    var totalString = test_reversed.Skip(2).FirstOrDefault();

                                // Process qty and unitPrice here
                                decimal rate = 1;
                                if (isItemsUsdInvoice)
                                {
                                    rate = ExchangeRate;
                                }
                                else if (isItemsEURInvoice)
                                {
                                    rate = ExchangeRateEUR;
                                }
                                
                                    decimal total = 0;
                                    if (decimal.TryParse(totalString, out decimal parsedTotal))
                                    {
                                         reversed = parts.Reverse().Take(3).Reverse().ToList();
                                        item.Name = string.Join(" ", parts.Reverse().Skip(3).Reverse());
                                        item.UnitPrice = decimal.Parse(reversed[1]) * rate;
                                        item.Quantity = decimal.Parse(reversed[0]);

                                    }
                                    else
                                    {
                                        reversed = parts.Reverse().Take(2).Reverse().ToList();
                                        item.Name = string.Join(" ", parts.Reverse().Skip(2).Reverse());
                                        item.UnitPrice = decimal.Parse(reversed[1]) * rate;
                                        item.Quantity = decimal.Parse(reversed[0]);

                                    }

                                    item.Amount = item.Quantity * item.UnitPrice;
                                    item.Amount = Math.Round(item.Amount, 2);

                                    if (isVATInvoice)
                                    {
                                        var totalAmount = item.Amount  * 1.18m;
                                        item.Tax = totalAmount - item.Amount;
                                        item.Tax = Math.Round(item.Tax, 2);
                                    }
                                    

                                    invoice.Items.Add(item);


                                }
                            }
                            else
                            {
                                Console.WriteLine("No match found.");
                            }
                        }
                        else
                        {
                            //Working with unformated invoice here
                            string startMarker = "/Comments";
                            string endMarker = "Total Amount";

                            int startIndex = ItemsAreaText.IndexOf(startMarker) + startMarker.Length;
                            int endIndex = ItemsAreaText.IndexOf(endMarker, startIndex);

                            string extractedInvoiceText = ItemsAreaText.Substring(startIndex, endIndex - startIndex).Trim();
                            string ItemNamespattern = @"Unit Price(.*?)(Being|Due)";

                            Match match = Regex.Match(extractedInvoiceText, ItemNamespattern, RegexOptions.Singleline);
                            if (match.Success)
                            {
                                string extractedText2 = match.Groups[1].Value.Trim();
                                string[] textLines = extractedText2.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                                var data = extractedInvoiceText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                                var lastItem = data.TakeLast(1).ToList();
                                decimal vat = 0;
                                if (decimal.TryParse(lastItem[0], out decimal parsedTotal))
                                {
                                   
                                    if (decimal.Parse(lastItem[0]) > 0)
                                    {
                                        isVATInvoice = true;
                                    }
                                }
                                else
                                {
                                    var _vat = lastItem[0].Split("VAT")[1];
                                    if (decimal.Parse(_vat) > 0)
                                    {
                                        isVATInvoice = true;
                                    }
                                }
                               

                                //Here we get Items name
                                var itemsName = textLines.Skip(1).ToList();
                                string[] lines = { };

                                string startMarker2 = "Amount Due in Words:";
                                string endMarker2 = "Our Bank";

                                int startIndexOfText = extractedInvoiceText.IndexOf(startMarker2) + startMarker2.Length;
                                int endIndexOfText = extractedInvoiceText.IndexOf(endMarker2, startIndexOfText);

                                string extractedQTYPriceText = extractedInvoiceText.Substring(startIndexOfText, endIndexOfText - startIndexOfText).Trim();
                                string[] textQTYPriceLines = extractedQTYPriceText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                                //Here we get Items quantity and prices
                            //    var QTYPriceItems = textQTYPriceLines.Skip(1).ToArray();
                                string[] QTYPriceItems = new string[textQTYPriceLines.Length - 1];
                                Array.Copy(textQTYPriceLines, 1, QTYPriceItems, 0, QTYPriceItems.Length);

                         //       string[] values = QTYPriceItems.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                                List<(decimal Quantity, decimal UnitPrice, decimal Total)> items = new List<(decimal Quantity, decimal UnitPrice, decimal Total)>();

                             
                                // Type 2: Quantity, Unit Price, and Total on the same line
                                 if (QTYPriceItems.Length > 1 && QTYPriceItems[1].Split(' ').Length >= 2)
                                {
                                    var joinedData = string.Join('\n', QTYPriceItems.ToString());
                                    List<string> value_data = new List<string>();
                                    foreach (string _line in QTYPriceItems)
                                    {
                                        string[] parts = _line.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                                        if (parts.Length == 1)
                                        {
                                            value_data.Add(parts[0]);
                                            //decimal quantity = decimal.Parse(parts[0].Trim());
                                            //decimal unitPrice = decimal.Parse(parts[1].Trim());
                                            //decimal total = quantity * unitPrice;

                                            //items.Add((quantity, unitPrice, total));
                                        }
                                        else if (parts.Length == 2)
                                        {
                                    //        var splitedPart = parts[0].Split(" ");
                                            foreach(string rec in parts)
                                            {
                                                value_data.Add(rec);
                                            }
                                            //decimal quantity = decimal.Parse(parts[0].Trim());
                                            //decimal unitPrice = decimal.Parse(parts[1].Trim());
                                            //decimal total = decimal.Parse(parts[2].Trim());

                                            //items.Add((quantity, unitPrice, total));
                                        }
                                    }
                                    var groupedNumbers = value_data
                                    .Select((number, index) => new { Number = number, Group = index / 3 })
                                    .GroupBy(x => x.Group, x => x.Number)
                                    .Select(group => group.ToArray())
                                    .ToArray();

                                    foreach (var group in groupedNumbers)
                                    {
                                        if(group.Length == 3)
                                        {
                                            
                                                decimal quantity = decimal.Parse(group[0].Trim());
                                                decimal unitPrice = decimal.Parse(group[1].Trim());
                                                decimal total = decimal.Parse(group[2].Trim());
                                                items.Add((quantity, unitPrice, total));
                                            
                                        }
                                        if (group.Length == 2)
                                        {
                                           
                                                decimal quantity = decimal.Parse(group[0].Trim());
                                                decimal unitPrice = decimal.Parse(group[1].Trim());
                                                decimal total = 0;
                                                items.Add((quantity, unitPrice, total));
                                            
                                        }

                                    }

                                    List<Item> items2 = new List<Item>();
                                    
                                    for(int x = 0; x < items.ToList().Count; x++)
                                    {
                                        var _item = new Item();
                                        item.Name = itemsName[x];
                                       

                                            item.Quantity = items.ToList()[x].Quantity;
                                            decimal rate = 1;
                                            if (isItemsUsdInvoice)
                                            {
                                                rate = ExchangeRate;
                                            }
                                            else if (isItemsEURInvoice)
                                            {
                                                rate = ExchangeRateEUR;
                                            }

                                             item.UnitPrice  = items.ToList()[x].UnitPrice * rate;
                                          //  item.Amount = items.ToList()[j].Total;

                                            item.Amount = item.Quantity * item.UnitPrice;
                                            item.Amount = Math.Round(item.Amount, 2);

                                            if (isVATInvoice)
                                            {
                                                var totalAmount = item.Amount * 1.18m;
                                                item.Tax = totalAmount - item.Amount;
                                                item.Tax = Math.Round(item.Tax, 2);
                                            }
                                            

                                        invoice.Items.Add(item);
                                    }
                                }
                                // Type 1: Quantity, Unit Price, and Total on separate lines
                                else                                
                                {
                                    //          string[] xlines = extractedQTYPriceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    //         var xgroup = extractedQTYPriceText.Split("\n");
                                    ////          string[] xgroup = extractedQTYPriceText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                                    //          var groups = xgroup
                                    //              .Select(x => x.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries))
                                    //              .ToList();

                                    //          foreach (var group in groups)
                                    //          {
                                    //              var ii = string.Join(", ", group);
                                    //              Console.WriteLine(string.Join(", ", group));
                                    //          }

                                    //          for (int i =1; i < xlines.Length - 2; i += 3)
                                    //              {
                                    //                  decimal quantity = decimal.Parse(xlines[i].Trim());
                                    //                  decimal unitPrice = decimal.Parse(xlines[i + 1].Trim());
                                    //                  decimal total = decimal.Parse(xlines[i + 2].Trim());

                                    //                  items.Add((quantity, unitPrice, total));
                                    //              }
                                    
                                }

                                // Print the items
                                
                            }
                            
                        }
                      
                       

                    
                   
                    reader.Close();
                    if (invoice.Items.Count > 0)
                    {
                        invoice.SubTotal = invoice.Items.Sum(x => x.Amount);
                        invoice.SubTotal = Math.Round(invoice.SubTotal, 2);
                        invoice.Total = Math.Round(invoice.Items.Sum(x => Math.Round(x.GrossUnitPrice * x.Quantity, 2)), 2);
                        invoice.Tax = Math.Round(invoice.Total - invoice.SubTotal, 2);
                    }
                    else
                    {
                        invoice = new Invoice();
                    }
                  
                }


                return invoice;
            }
            catch (Exception ex)
            {
                return new Invoice(); ;

            }
       
          
        }

        private static string getExtractedText(System.Drawing.Rectangle contentArea, byte[] bytes, string extractedtext)
        {
            string text = string.Empty;

            string base64String = Convert.ToBase64String(bytes);

            if (contentArea.X == 0 && contentArea.Y == 0 && contentArea.Height == 0)
            {
              
                    text = extractedtext;
                
            }
            else
            {
               text = ServiceHelper.ReadOcrText(base64String).Result.Trim('"');
            }
          
            text = Regex.Replace(text, @"(^\p{Zs}*\r\n){2,}", "\r\n", RegexOptions.Multiline);
            return text;
        }

        #endregion
        public static Invoice ReadDavis(string text)
        {
            Invoice invoice = new Invoice();
            using (StringReader reader = new StringReader(text))
            {
                string line;
                bool readingName = false, readingItems = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("VAT ANALYSIS"))
                    {
                        readingItems = false;
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.SubTotal = figure;
                            }
                        }
                    }

                    if (readingName)
                    {
                        invoice.Customer = line;
                    }

                    if (readingItems)
                    {
                        Item item = new Item() { Name = string.Empty };

                        var parts = line.Split(' ');

                        string quantityString = string.Empty;
                        for (int i = parts.Length - 1; i >= 0; i--)
                        {
                            decimal figure;
                            if (decimal.TryParse(parts[i], out figure) == true)
                            {
                                if (item.Amount == 0)
                                    item.Amount = figure;
                                else if (item.UnitPrice == 0)
                                    item.UnitPrice = figure;
                                else if (item.Tax == 0)
                                    item.Tax = Convert.ToInt32(figure);
                                else if (item.Quantity == 0)
                                {
                                    item.Quantity = Convert.ToInt32(figure);
                                    quantityString = $" {figure} ";
                                }
                            }
                        }

                        item.Name = line.Split(new string[] { quantityString }, StringSplitOptions.None).First();

                        invoice.Items.Add(item);
                    }



                    if (line.StartsWith("SALE TO CLIENT DETAILS DETAILS"))
                    {
                        line = reader.ReadLine();
                        invoice.Customer = line;
                    }

                    if (line.StartsWith("ITEM DETAILS CODE"))
                    {
                        readingItems = true;
                    }


                    if (line.StartsWith("REF"))
                    {
                        var parts = line.Split(' ');
                        invoice.Number = parts.Last();
                    }

                    if (line.StartsWith("TAX INVOICE DATE"))
                    {
                        string dateString = line.Replace("TAX INVOICE DATE", "");
                        DateTime date = DateTime.Now;
                        //bool dateParsed = DateTime.TryParse(dateString, out date);
                        //if (dateParsed)
                        //    invoice.Date = date;
                        DateTime parsedDate;

                        foreach (string format in possibleFormats)
                        {
                            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                            {
                                invoice.Date = parsedDate;
                                break;
                            }
                        }
                    }



                    if (line.StartsWith("ADD VAT TSH"))
                    {
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Tax = figure;
                            }
                        }
                    }

                    if (line.StartsWith("TOTAL TSH"))
                    {
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Total = figure;
                            }
                        }

                    }


                }

                reader.Close();

                return invoice;
            }
        }

        public static Invoice ReadNeptune(string text)
        {
            Invoice invoice = new Invoice() { Tax = 15, Customer = "Tourist" };
            using (StringReader reader = new StringReader(text))
            {
                string line;
                bool readingName = false, readingItems = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("INVOICE TOTAL"))
                    {
                        readingItems = false;
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Total = figure;
                                invoice.SubTotal = figure - (0.15m * figure);
                            }
                        }
                    }

                    if (readingName)
                    {
                        invoice.Customer = line;
                    }

                    if (readingItems)
                    {
                        Item item = new Item() { Name = string.Empty, Tax = 15 };

                        var parts = line.Split(' ');
                        foreach (var part in parts)
                        {
                            decimal figure;
                            if (decimal.TryParse(part, out figure) == false && item.Quantity == 0)
                            {
                                item.Name += part + " ";
                            }

                            if (decimal.TryParse(part, out figure) == true)
                            {
                                if (item.UnitPrice == 0)
                                {
                                    item.UnitPrice = figure - (0.15m * figure);
                                    item.Amount = item.UnitPrice;
                                }
                            }
                        }

                        if (item.UnitPrice > 0)
                            invoice.Items.Add(item);
                    }



                    if (line.StartsWith("SALE TO CLIENT DETAILS DETAILS"))
                    {
                        line = reader.ReadLine();
                        invoice.Customer = line;
                    }

                    if (line.StartsWith("Date Description"))
                    {
                        readingItems = true;
                    }


                    if (line.StartsWith("Invoice Number"))
                    {
                        var parts = line.Split(' ');
                        invoice.Number = parts.Last();
                    }

                    if (line.StartsWith("DIRECT (USD) - EAST AFRICA Date Of Invoice"))
                    {
                        string dateString = line.Replace("DIRECT (USD) - EAST AFRICA Date Of Invoice:", "");
                        DateTime date = DateTime.Now;
                        //bool dateParsed = DateTime.TryParse(dateString, out date);
                        //if (dateParsed)
                        //    invoice.Date = date;
                        DateTime parsedDate;

                        foreach (string format in possibleFormats)
                        {
                            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                            {
                                invoice.Date = parsedDate;
                                break;
                            }
                        }
                    }



                    if (line.StartsWith("ADD VAT TSH"))
                    {
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Tax = figure;
                            }
                        }
                    }

                    if (line.StartsWith("TOTAL TSH"))
                    {
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Total = figure;
                            }
                        }

                    }


                }
                reader.Close();
                return invoice;
            }
        }

        public static Invoice ReadVerde(string text)
        {
            Invoice invoice = new Invoice();
            using (StringReader reader = new StringReader(text))
            {
                string line;
                bool readingName = false, readingItems = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("Total") && invoice.Total == 0)
                    {
                        readingItems = false;

                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Total = figure;
                            }
                        }
                    }
                    if (line.StartsWith("VAT Included 15") && invoice.Tax == 0)
                    {
                        readingItems = false;
                        var parts = line.Split(' ');
                        decimal figure;
                        foreach (var part in parts)
                        {
                            if (decimal.TryParse(part, out figure) == true)
                            {
                                invoice.Tax = figure;
                            }
                        }
                    }

                    if (readingName)
                    {
                        invoice.Customer = line;
                    }

                    if (readingItems)
                    {
                        Item item = new Item() { Name = string.Empty };

                        var parts = line.Split(' ');

                        string quantityString = string.Empty;
                        string unitPriceString = string.Empty;
                        for (int i = parts.Length - 1; i >= 0; i--)
                        {
                            decimal figure;
                            if (decimal.TryParse(parts[i], out figure) == true)
                            {
                                if (item.Amount == 0)
                                    item.Amount = figure;
                                else if (item.UnitPrice == 0)
                                {
                                    item.UnitPrice = figure;
                                    unitPriceString = parts[i];
                                }
                                else if (item.Quantity == 0)
                                {
                                    item.Quantity = Convert.ToInt32(figure);
                                    quantityString = $" {figure} ";
                                }
                            }
                        }

                        item.Name = line.Split(new string[] { quantityString, unitPriceString }, StringSplitOptions.None)[1];

                        if (item.Name.ToLower().Contains("infrastructure"))
                            item.Tax = 0;
                        else
                            item.Tax = 15;

                        if (item.Amount > 0)
                            invoice.Items.Add(item);
                    }



                    if (line.StartsWith("Guest/Group:"))
                    {
                        invoice.Customer = line.Split(':').Last();
                        //line = reader.ReadLine();
                        //invoice.Customer = line;
                    }

                    if (line.StartsWith("Date Qty Description"))
                    {
                        readingItems = true;
                    }


                    if (line.StartsWith("Invoice Number:"))
                    {
                        var parts = line.Split(':');
                        invoice.Number = parts.Last();
                    }

                    if (line.StartsWith("Invoice Date:"))
                    {
                        string dateString = line.Replace("Invoice Date:", "");
                        DateTime date = DateTime.Now;
                        //bool dateParsed = DateTime.TryParse(dateString, out date);
                        //if (dateParsed)
                        //    invoice.Date = date;
                        DateTime parsedDate;

                        foreach (string format in possibleFormats)
                        {
                            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                            {
                                invoice.Date = parsedDate;
                                break;
                            }
                        }
                    }



                    //if (line.StartsWith("ADD VAT TSH"))
                    //{
                    //    var parts = line.Split(' ');
                    //    decimal figure;
                    //    foreach (var part in parts)
                    //    {
                    //        if (decimal.TryParse(part, out figure) == true)
                    //        {
                    //            invoice.Tax = figure;
                    //        }
                    //    }
                    //}




                }
                reader.Close();
                return invoice;
            }
        }
    }
}
