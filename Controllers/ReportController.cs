using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.IO;
using ClosedXML.Excel;
using InvoiceScanner.Models;
using InvoiceScanner.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace InvoiceScanner.Controllers
{
    public class ReportController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly PdfService _pdfService;

        public ReportController(IConfiguration configuration, PdfService pdfService)
        {
            _configuration = configuration;
            _pdfService = pdfService;
        }
        
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetInvoices(string startDate, string endDate)
        {
            try
            {
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    return BadRequest(new { success = false, error = "Start date and end date are required" });
                }

                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest(new { success = false, error = "Invalid date format" });
                }

                var invoices = await GetInvoicesForExport(parsedStartDate, parsedEndDate);

                // Calculate summary
                var summary = new
                {
                    totalCount = invoices.Count,
                    totalSubtotal = invoices.Sum(i => i.TotalAmountTaxEx),
                    totalTax = invoices.Sum(i => i.TotalTaxAmount),
                    totalAmount = invoices.Sum(i => i.TotalAmountTaxInc)
                };

                return Json(new { 
                    success = true, 
                    data = invoices, 
                    summary = summary,
                    dateRange = new {
                        from = parsedStartDate.ToString("dd MMM yyyy"),
                        to = parsedEndDate.ToString("dd MMM yyyy")
                    }
                });
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in GetInvoices: {ex.Message}");
                
                // Return error response
                return BadRequest(new { 
                    success = false, 
                    error = "An error occurred while processing your request.",
                    details = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string startDate, string endDate)
        {
            try
            {
                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest("Invalid date format");
                }

                var invoices = await GetInvoicesForExport(parsedStartDate, parsedEndDate);
                
                if (!invoices.Any())
                {
                    return NotFound("No invoices found for the selected date range");
                }

                // Calculate summary values once
                var totalCount = invoices.Count;
                var totalTax = invoices.Sum(i => i.TotalTaxAmount);
                var totalAmount = invoices.Sum(i => i.TotalAmountTaxInc);
                var totalSubtotal = invoices.Sum(i => i.TotalAmountTaxEx);
                
                // Create a new model and generate PDF
                var model = new SalesReportModel
                {
                    StartDate = parsedStartDate,
                    EndDate = parsedEndDate,
                    Invoices = invoices,
                    TotalCount = totalCount,
                    TotalTax = totalTax,
                    TotalAmount = totalAmount,
                    TotalSubtotal = totalSubtotal
                };
                
                // Generate the PDF using our injected service
                var pdfData = _pdfService.GenerateSalesReport(model);

                string fileName = $"SalesReport_{parsedStartDate:yyyy-MM-dd}_to_{parsedEndDate:yyyy-MM-dd}.pdf";
                return File(pdfData, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error generating PDF: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string startDate, string endDate)
        {
            try
            {
                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest("Invalid date format");
                }

                var invoices = await GetInvoicesForExport(parsedStartDate, parsedEndDate);
                
                if (!invoices.Any())
                {
                    return NotFound("No invoices found for the selected date range");
                }

                // Create the Excel file in memory
                var memoryStream = new MemoryStream();
                
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Sales Report");

                    // Set column widths for better readability
                    worksheet.Column(1).Width = 15;  // Reference No
                    worksheet.Column(2).Width = 12;  // Z Number
                    worksheet.Column(3).Width = 25;  // Customer
                    worksheet.Column(4).Width = 15;  // TIN
                    worksheet.Column(5).Width = 15;  // Amount (Tax Ex)
                    worksheet.Column(6).Width = 15;  // Tax
                    worksheet.Column(7).Width = 15;  // Amount (Tax Inc)
                    worksheet.Column(8).Width = 15;  // Processed Date
                    worksheet.Column(9).Width = 15;  // Invoice Date
                    worksheet.Column(10).Width = 20; // Fiscal Code

                    // Add title and date range
                    worksheet.Cell("A1").Value = "SALES REPORT";
                    worksheet.Cell("A2").Value = $"Period: {parsedStartDate:dd MMM yyyy} to {parsedEndDate:dd MMM yyyy}";
                    worksheet.Cell("A3").Value = $"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}";
                    
                    // Style the header
                    var titleRange = worksheet.Range("A1:J1");
                    titleRange.Merge();
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.FontSize = 14;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    
                    var dateRange = worksheet.Range("A2:J2");
                    dateRange.Merge();
                    dateRange.Style.Font.Italic = true;
                    dateRange.Style.Font.FontSize = 12;
                    dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    
                    var generatedRange = worksheet.Range("A3:J3");
                    generatedRange.Merge();
                    generatedRange.Style.Font.Italic = true;
                    generatedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Add headers at row 5
                    worksheet.Cell("A5").Value = "Reference No";
                    worksheet.Cell("B5").Value = "Z Number";
                    worksheet.Cell("C5").Value = "Customer";
                    worksheet.Cell("D5").Value = "TIN";
                    worksheet.Cell("E5").Value = "Amount (Tax Ex)";
                    worksheet.Cell("F5").Value = "Tax";
                    worksheet.Cell("G5").Value = "Amount (Tax Inc)";
                    worksheet.Cell("H5").Value = "Processed Date";
                    worksheet.Cell("I5").Value = "Invoice Date";
                    worksheet.Cell("J5").Value = "Fiscal Code";

                    // Style the headers
                    var headerRange = worksheet.Range("A5:J5");
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    
                    // Add data rows
                    int rowIndex = 6;
                    foreach (var invoice in invoices)
                    {
                        worksheet.Cell(rowIndex, 1).Value = invoice.ReferenceNumber;
                        worksheet.Cell(rowIndex, 2).Value = invoice.ZNumber;
                        worksheet.Cell(rowIndex, 3).Value = invoice.CustomerName;
                        worksheet.Cell(rowIndex, 4).Value = invoice.CustomerID;
                        
                        // Format currency values
                        worksheet.Cell(rowIndex, 5).Value = invoice.TotalAmountTaxEx;
                        worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(rowIndex, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        
                        worksheet.Cell(rowIndex, 6).Value = invoice.TotalTaxAmount;
                        worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(rowIndex, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        
                        worksheet.Cell(rowIndex, 7).Value = invoice.TotalAmountTaxInc;
                        worksheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(rowIndex, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        
                        worksheet.Cell(rowIndex, 8).Value = invoice.ProcessedDate;
                        worksheet.Cell(rowIndex, 9).Value = invoice.invoiceDate;
                        worksheet.Cell(rowIndex, 10).Value = invoice.FiscalCode;

                        // Apply borders to all cells
                        worksheet.Range(rowIndex, 1, rowIndex, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        
                        rowIndex++;
                    }

                    // Apply zebra striping for better readability
                    for (int i = 6; i < rowIndex; i += 2)
                    {
                        worksheet.Range(i, 1, i, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
                    }

                    // Calculate totals
                    decimal totalAmountTaxEx = invoices.Sum(x => x.TotalAmountTaxEx);
                    decimal totalTaxAmount = invoices.Sum(x => x.TotalTaxAmount);
                    decimal totalAmountTaxInc = invoices.Sum(x => x.TotalAmountTaxInc);

                    // Add totals row
                    worksheet.Cell(rowIndex, 1).Value = "TOTALS";
                    worksheet.Range(rowIndex, 1, rowIndex, 4).Merge();
                    worksheet.Range(rowIndex, 1, rowIndex, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    worksheet.Range(rowIndex, 1, rowIndex, 4).Style.Font.Bold = true;
                    worksheet.Range(rowIndex, 1, rowIndex, 10).Style.Fill.BackgroundColor = XLColor.LightGray;
                    
                    worksheet.Cell(rowIndex, 5).Value = totalAmountTaxEx;
                    worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(rowIndex, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    worksheet.Cell(rowIndex, 5).Style.Font.Bold = true;
                    
                    worksheet.Cell(rowIndex, 6).Value = totalTaxAmount;
                    worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(rowIndex, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    worksheet.Cell(rowIndex, 6).Style.Font.Bold = true;
                    
                    worksheet.Cell(rowIndex, 7).Value = totalAmountTaxInc;
                    worksheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(rowIndex, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    worksheet.Cell(rowIndex, 7).Style.Font.Bold = true;
                    
                    // Add border to totals row
                    worksheet.Range(rowIndex, 1, rowIndex, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    
                    // Add summary section
                    rowIndex += 3;
                    worksheet.Cell(rowIndex, 1).Value = "REPORT SUMMARY";
                    worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, 1).Style.Font.FontSize = 12;
                    
                    rowIndex++;
                    worksheet.Cell(rowIndex, 1).Value = "Total Invoices:";
                    worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, 2).Value = invoices.Count;
                    
                    rowIndex++;
                    worksheet.Cell(rowIndex, 1).Value = "Total Amount (Tax Ex):";
                    worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, 2).Value = totalAmountTaxEx;
                    worksheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "#,##0.00";
                    
                    rowIndex++;
                    worksheet.Cell(rowIndex, 1).Value = "Total Tax:";
                    worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, 2).Value = totalTaxAmount;
                    worksheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "#,##0.00";
                    
                    rowIndex++;
                    worksheet.Cell(rowIndex, 1).Value = "Total Amount (Tax Inc):";
                    worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, 2).Value = totalAmountTaxInc;
                    worksheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(rowIndex, 2).Style.Font.FontColor = XLColor.Blue;
                    
                    // Apply print settings
                    worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
                    worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                    worksheet.PageSetup.FitToPages(1, 0);
                    worksheet.PageSetup.CenterHorizontally = true;
                    worksheet.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber, XLHFOccurrence.AllPages);
                    worksheet.PageSetup.Header.Center.AddText("Sales Report", XLHFOccurrence.AllPages);
                    worksheet.PageSetup.SetRowsToRepeatAtTop(5, 5); // Repeat header row when printing

                    // Save the workbook to the MemoryStream and reset the position
                    workbook.SaveAs(memoryStream);
                    memoryStream.Position = 0;
                }

                string fileName = $"SalesReport_{parsedStartDate:yyyy-MM-dd}_to_{parsedEndDate:yyyy-MM-dd}.xlsx";
                return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error generating Excel: {ex.Message}");
            }
        }
        
        private async Task<List<InvoiceViewModel>> GetInvoicesForExport(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Retrieve the connection string from appsettings.json
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                List<InvoiceViewModel> invoices = new List<InvoiceViewModel>();

                // Query that filters for validated invoices that are not for save
                string query = @"SELECT DISTINCT
                    ""Number"" AS ""reference_no"",
                    REPLACE(TO_CHAR(""Date"", 'YYYYMMDD'), ':', '') AS ""znumber"",
                    ""Customer"" AS ""CustomerName"",
                    ""TIN"" AS ""customerID"",
                    ""SubTotal"" AS ""totalAmountTaxEx"",
                    ""Tax"" AS ""totalTaxAmount"",
                    ""Total"" AS ""totalAmountTaxInc"",
                    ""ProcessedDate"" AS ""date_time"",
                    ""invoiceDate"" AS ""invoice_date"",
                    ""FiscalCode"" AS ""fiscal_code""
                FROM public.""Invoices""
                WHERE ""Date"" >= @StartDate 
                    AND ""Date"" < @EndDate
                    AND ""FiscalCode"" IS NOT NULL
                    AND LENGTH(""FiscalCode"") > 0
                    AND ""isForSave"" = false
                    AND ""Status"" = 'VALIDATED';";

                // Create a NpgsqlConnection object
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        // Add parameters
                        command.Parameters.AddWithValue("@StartDate", startDate.Date);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).Date);

                        // Open the connection
                        await connection.OpenAsync();

                        // Execute the query and read the data
                        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                invoices.Add(new InvoiceViewModel
                                {
                                    ReferenceNumber = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                    ZNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    CustomerName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    CustomerID = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    TotalAmountTaxEx = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                                    TotalTaxAmount = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                                    TotalAmountTaxInc = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                                    ProcessedDate = reader.IsDBNull(7) ? string.Empty : reader.GetDateTime(7).ToString("dd/MM/yyyy"),
                                    invoiceDate = reader.IsDBNull(8) ? string.Empty : Convert.ToDateTime(reader[8]).ToString("dd/MM/yyyy"),
                                    FiscalCode = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                                });
                            }
                        }
                    }
                }
                
                return invoices;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error in GetInvoicesForExport: {ex.Message}");
                // Return empty list on error
                return new List<InvoiceViewModel>();
            }
        }
    }
}

