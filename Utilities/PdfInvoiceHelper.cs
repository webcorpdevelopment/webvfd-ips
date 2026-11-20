using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using InvoiceScanner.Models;
using SkiaSharp;
using SkiaSharp.QrCode.Image;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Text;
using System.Net;

namespace InvoiceScanner.Cloud.Utilities
{
    public static class PdfInvoiceHelper
    {
        // Draws a test text at the bottom right of every page
        public static byte[] DrawTextOnEveryPage(byte[] pdfBytes, Invoice invoice, string text = "TEST TEXT")
        {
            using (var inputStream = new MemoryStream(pdfBytes))
            using (var outputStream = new MemoryStream())
            {
                var document = PdfSharp.Pdf.IO.PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

                try
                {
                    foreach (PdfSharp.Pdf.PdfPage page in document.Pages)
                    {
                        using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                        {
                            double margin = 20;
                            XFont font = new XFont("Helvetica", 10);
                            string drawText = text;
                            // Calculate position: bottom right
                            double x = page.Width.Point - margin;
                            double y = page.Height.Point - margin;
                            XSize size = gfx.MeasureString(drawText, font);
                            x -= size.Width;
                            // Draw text
                            gfx.DrawString(drawText, font, XBrushes.Red, x, y);
                        }
                    }
                    
                }
                catch (Exception)
                {
                    return null;
                    
                }
                document.Save(outputStream);
                return outputStream.ToArray();
            }
        }

        // Splits a PDF into individual page byte arrays
        public static List<byte[]> SplitPDFPages(byte[] pdfBytes)
        {
            var pages = new List<PdfSharp.Pdf.PdfPage>();
            List<byte[]> pdfPagesBytes = new List<byte[]>();

            using (var document = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(pdfBytes), PdfDocumentOpenMode.Import))
            {
                for (int i = 0; i < document.PageCount; i++)
                {
                    pages.Add(document.Pages[i]);
                }
            }
            foreach (PdfSharp.Pdf.PdfPage page in pages)
            {
                if (page != null)
                {
                    pdfPagesBytes.Add(GetPageBytes(page));
                }
            }
            return pdfPagesBytes;
        }

        // Gets the bytes for a single PDF page
        public static byte[] GetPageBytes(PdfSharp.Pdf.PdfPage page)
        {
            using (var document = new PdfSharp.Pdf.PdfDocument())
            {
                document.AddPage(page);
                using (var memoryStream = new MemoryStream())
                {
                    document.Save(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        // Combines multiple single-page PDFs into one multi-page PDF
        public static byte[] CreateMultiPagePdf(List<byte[]> pdfPagesBytes)
        {
            try
            {
                using (var document = new PdfSharp.Pdf.PdfDocument())
                {
                    foreach (var pageBytes in pdfPagesBytes)
                    {
                        using (var stream = new MemoryStream(pageBytes))
                        {
                            var pdfPage = PdfSharp.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                            document.AddPage(pdfPage.Pages[0]);
                        }
                    }
                    using (var output = new MemoryStream())
                    {
                        document.Save(output);
                        return output.ToArray();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Generate QR code as PNG byte array using SkiaSharp
        public static byte[] GenerateQrCode(string text)
        {
            try
            {
                // Create QR code generator with SkiaSharp
                var qrCode = new QrCode(text, new Vector2Slim(256, 256), SKEncodedImageFormat.Png);
                
                // Add logo if needed
                // qrCode.SetLogoImage("path/to/logo.png", new Vector2Slim(64, 64));
                
                // Generate QR code image as byte array
                using (var memoryStream = new MemoryStream())
                {
                    // Generate the QR code and save to memory stream
                    qrCode.GenerateImage(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating QR code: {ex.Message}");
                
                // Try fallback QR code generation method
                try
                {
                    return GenerateQrCodeFallback(text);
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Error with fallback QR code generation: {fallbackEx.Message}");
                    return null;
                }
            }
        }
        
        // Fallback QR code generation using an external API service
        public static byte[] GenerateQrCodeFallback(string text)
        {
            try
            {
                // URL encode the text to put in the URL
                string encodedText = WebUtility.UrlEncode(text);
                
                // Use Google Charts API to generate QR code
                // Size parameter is 256x256
                string url = $"https://chart.googleapis.com/chart?cht=qr&chs=256x256&chl={encodedText}";
                
                using (WebClient client = new WebClient())
                {
                    return client.DownloadData(url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in fallback QR code generation: {ex.Message}");
                throw;
            }
        }

        // Add QR code to PDF using iTextSharp (works on all pages)
        public static byte[] AddQrCodeToPdf(byte[] pdfBytes, Invoice invoice) 
        {
            try
            {
                // Generate QR code for invoice
                byte[] qrCodeBytes = GenerateQrCode(invoice.QrCode);
                if (qrCodeBytes == null)
                {
                    return pdfBytes; // Return original if QR generation fails
                }

                // Create temporary streams
                using (MemoryStream inputStream = new MemoryStream(pdfBytes))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    // Open the existing PDF
                    iTextSharp.text.pdf.PdfReader reader = new iTextSharp.text.pdf.PdfReader(inputStream);
                    
                    // Create a PDF stamper to modify the document
                    PdfStamper stamper = new PdfStamper(reader, outputStream);
                    
                    // Convert QR code bytes to iTextSharp image
                    iTextSharp.text.Image qrImage = iTextSharp.text.Image.GetInstance(qrCodeBytes);
                    
                    // Set the size of the QR code
                    qrImage.ScaleAbsolute(100, 100);
                    
                    // Calculate position for all pages (bottom right)
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        // Get page size from reader
                        iTextSharp.text.Rectangle pageSize = reader.GetPageSize(i);
                        
                        // Position QR code at bottom right with 20pt margins
                        float x = pageSize.Right - 100 - 20; // 20pt margin from right
                        float y = pageSize.Bottom + 20;      // 20pt margin from bottom
                        
                        qrImage.SetAbsolutePosition(x, y);
                        
                        // Get content for the page and add image
                        PdfContentByte content = stamper.GetOverContent(i);
                        content.AddImage(qrImage);
                        
                        // Add QR code text directly using PdfContentByte
                        if (!string.IsNullOrEmpty(invoice.QrCode))
                        {
                            // Allow more characters before truncating
                            string qrCodeText = invoice.QrCode;
                            if (qrCodeText.Length > 40)
                            {
                                qrCodeText = qrCodeText.Substring(0, 37) + "...";
                            }
                            
                            // Use direct text writing at exact position - moved further left to ensure full visibility
                            BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                            content.BeginText();
                            content.SetFontAndSize(bf, 8);
                            // Position text below and to the left of QR code to ensure full visibility
                            content.SetTextMatrix(Math.Max(x - 100, pageSize.Left + 20), y - 10);
                            content.ShowText(qrCodeText);
                            content.EndText();
                        }
                        
                        // Optional: Add text about exchange rate if needed
                        if (invoice.Currency == "USD" || invoice.Currency == "EUR")
                        {
                            // Create font for text
                            iTextSharp.text.Font font = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 9, iTextSharp.text.Font.BOLD);
                            
                            // Create paragraph for exchange rate text
                            string exchangeRateText = $"Exchange Rate: {Math.Round(invoice.ExchangeRate, 2):N2}\n" +
                                                     $"Total Amount(TZS): {Math.Round(invoice.TotalExchangedAmount, 2):N2}";
                            
                            // Position text above QR code
                            ColumnText ct = new ColumnText(content);
                            ct.SetSimpleColumn(
                                x, 
                                y + 110, // Above QR code
                                x + 200,
                                y + 150);
                            ct.AddText(new Phrase(exchangeRateText, font));
                            ct.Go();
                        }
                    }
                    
                    // Close the stamper to apply changes
                    stamper.Close();
                    reader.Close();
                    
                    return outputStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding QR code to PDF: {ex.Message}");
                return pdfBytes; // Return original if process fails
            }
        }
    }
} 