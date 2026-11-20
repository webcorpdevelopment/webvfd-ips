using Amazon.Runtime.Internal;
using InvoiceScanner.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PdfSharp.Diagnostics;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace InvoiceScanner
{
    public static class ServiceHelper
    {
        const string baseUrl= "https://app.webvfd.co.tz";
       // const string baseUrl = "https://development.weberp.co.tz";

        const string webOcrBaseUrl = "http://158.69.112.220:900";
        public static DateTime AUTH_KEY_EXPIRE_TIME = DateTime.Now;
        public static string accessToken = ""; 
        public static string authKey = "";
        public static async Task<string> Authenticate(Business business)
        {
            RestClient client = new RestClient(baseUrl);

            RestRequest request = new RestRequest("/v1/gateway/get-token", Method.Post);
            request.AddHeader("Authorization", "Bearer 657657657");
            request.AlwaysMultipartFormData = true;
            request.AddParameter("username", business.GatewayUsername);
            request.AddParameter("password", business.GatewayPassword);
            request.AddParameter("companyID", business.GatewayCompanyId);
            request.AddParameter("serial_number", business.GatewaySerialNumber);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);
                string _accessToken = (string)jObject["access_token"];
                return _accessToken;
            }

            return null;
        }

        public static async Task<string> GetAuthKey(string token, Business business)
        {
            RestClient client = new RestClient(baseUrl);

            RestRequest request = new RestRequest("/v1/gateway/comp-registration", Method.Post);
            request.AddHeader("Authorization", $"Bearer {token}");
            request.AlwaysMultipartFormData = true;
            request.AddParameter("TIN", business.TIN);
            request.AddParameter("companyID", business.GatewayCompanyId);
            request.AddParameter("serial_number", business.GatewaySerialNumber);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);
                string accessToken = (string)jObject["response"]["AUTH_KEY"]; 
                string EXPIRE_TIME = (string)jObject["response"]["AUTH_KEY_EXPIRE_TIME"];
                var _EXPIRE_TIME = DateTime.Parse(EXPIRE_TIME);
                if (AUTH_KEY_EXPIRE_TIME <= _EXPIRE_TIME)
                {
                    AUTH_KEY_EXPIRE_TIME = _EXPIRE_TIME;
                }

                return accessToken;
            }

            return null;
        }

        public static async Task<Invoice> ValidateRequest(string token, string authKey, Invoice invoice, Business business)
        {
           // invoice.Date = DateTime.Now;
            var data = new
            {
                PROTOCOL = "WEBVFD",
                COMPANYID = business.GatewayCompanyId,
                AUTH_KEY = authKey,
                business.TIN,
                SERIALNUMBER = business.GatewaySerialNumber,
                SALES = new List<dynamic>()

            };

            dynamic customer;
            if (!string.IsNullOrWhiteSpace(invoice.TIN))
            {
                customer = new
                {
                    CUSTNAME = invoice.Customer,
                    MOBILENUM = string.Empty,
                    CUSTIDTYPE = 1,
                    CUSTID = invoice.TIN
                };
            }
            else
            {
                customer = new
                {
                    CUSTNAME = invoice.Customer,
                    MOBILENUM = string.Empty,
                    CUSTIDTYPE = 6,
                    CUSTID = string.Empty
                };
            }

            var invoiceData = new
            {
                REFERENCE_NO = invoice.Number,
                CUSTOMER = customer,
                DATETIME = invoice.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                ITEMS = new List<dynamic>(),
                receiptPayments = new List<dynamic>
                {
                    new  {
                        moneyType = "INVOICE",
                        paymentAmount = invoice.Total
                    }
                }
            };

            data.SALES.Add(invoiceData);

            foreach (var item in invoice.Items)
            {
                string _TAXCODE = "";

                //if (item.Name.Contains("0012"))
                //{
                //    _TAXCODE = "C";
                //}
                //else if (item.Name.Contains("0017"))
                //{
                //    _TAXCODE = "E";
                //}
                //else
                //{
                //    _TAXCODE = item.Tax > 0 ? "A" : "C";

                //}

                if(item.Code != "C") {

                    if (item.Tax == 0)
                    {
                        _TAXCODE = "E";
                    }
                    else
                    {
                        _TAXCODE = "A";
                    }
                }
                else
                {
                    _TAXCODE = "C";
                }

              
                var itemData = new
                {
                    ID = 0, //item.Code,
                    DESC = item.Name,
                    TAXCODE = _TAXCODE,
                    QTY = item.Quantity,
                    AMT = item.GrossUnitPrice,
                    line_total = Math.Round(item.GrossUnitPrice * item.Quantity, 2)
                };

                invoiceData.ITEMS.Add(itemData);
            }

            RestClient client = new RestClient(baseUrl);
            string json = JsonConvert.SerializeObject(data);

            RestRequest request = new RestRequest("/v1/gateway/submit", Method.Post);
            request.AddHeader("Authorization", $"Bearer {token}");
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", json, ParameterType.RequestBody);


            var response = await client.ExecuteAsync(request);
            invoice.GatewayResponse = response.Content;
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    JObject jObject = JObject.Parse(response.Content);
                    int status = (int)jObject["status"];
                    if (status == 200)
                    {
                        invoice.FiscalCode = (string)jObject["RECEIPT"]["FISCAL_CODE"];
                        invoice.ReceiptNumber = (string)jObject["RECEIPT"]["RECEIPT_NO"];
                        invoice.QrCode = (string)jObject["COUNTER"]["QR_CODE"];
                        invoice.Status = "VALIDATED";
                    }
                }
                catch { }

            }

            return invoice;
        }

        public static async Task<ZReport> GetZReport(string token, string authKey, Business business, DateTime date)
        {
            RestClient client = new RestClient(baseUrl);

            RestRequest request = new RestRequest("/v1/gateway/z-report", Method.Get);
            request.AddHeader("Authorization", $"Bearer {token}");
            request.AddQueryParameter("AUTH_KEY", authKey);
            request.AddQueryParameter("DATE", date.ToString("yyyy-MM-dd")); // DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd"));
            request.AddQueryParameter("TIN", business.TIN);
            request.AddQueryParameter("COMPANYID", business.GatewayCompanyId);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);                

                if (response.Content.Contains("There is no z-report of this"))
                    return null;

                var zReport = new ZReport();
                string[] taxNames = { "VAT A (18.00%)", "VAT B (0.00%)", "VAT C (0.00%)", "VAT D (0.00%)", "VAT E (0.00%)", "TOTAL" };

                
                zReport.CompanyName = (string)jObject["COMPANY INFORMATION"]["name"];
                zReport.CompanyAddress = (string)jObject["COMPANY INFORMATION"]["address"];
                zReport.CompanyTIN = (string)jObject["COMPANY INFORMATION"]["tin"];
                zReport.CompanyVRN = (string)jObject["COMPANY INFORMATION"]["vrn"];
                zReport.SerialNo = (string)jObject["COMPANY INFORMATION"]["serial_number"];
                zReport.UIN = (string)jObject["COMPANY INFORMATION"]["uin"];
                zReport.TaxOffice = (string)jObject["COMPANY INFORMATION"]["tax_office"];
                zReport.ContactPerson = (string)jObject["COMPANY INFORMATION"]["contact_person"];

                zReport.CurrentZNumber = (string)jObject["z report"][0]["CURRENTZNUMBER"];
                zReport.Discount = (decimal)jObject["z report"][0]["CURRENTZNUMBER"];

                zReport.FirstReceipt = DateTime.Parse($"{(string)jObject["z report"][0]["RECEIPT"]["FIRST RECEIPT"]["DATE"]} {(string)jObject["z report"][0]["RECEIPT"]["FIRST RECEIPT"]["TIME"]}");
                zReport.LastReceipt = DateTime.Parse($"{(string)jObject["z report"][0]["RECEIPT"]["LAST RECEIPT"]["DATE"]} {(string)jObject["z report"][0]["RECEIPT"]["LAST RECEIPT"]["TIME"]}");
                zReport.FiscalReceiptNo = (string)jObject["z report"][0]["RECEIPT"]["FISCAL RECEIPT NO"];
                zReport.Discount = (decimal)jObject["z report"][0]["DISCOUNT"];
                zReport.NetSale = (decimal)jObject["z report"][0]["NET SALE"];
                zReport.CashPayments = (decimal)jObject["z report"][0]["PAYMENTS"]["CASH"];
                zReport.ChequePayments = (decimal)jObject["z report"][0]["PAYMENTS"]["CHEQUE"];
                zReport.CCardPayments = (decimal)jObject["z report"][0]["PAYMENTS"]["CCARD"];
                zReport.InvoicePayments = (decimal)jObject["z report"][0]["PAYMENTS"]["INVOICE"];

                var reportItem = jObject["z report"][0];
                zReport.TaxData = new List<ZReportTaxInfo>();
                foreach (var taxName in taxNames)
                {
                    var taxItem = jObject["z report"][0][taxName];
                    if (taxItem != null )
                    {
                        var data = new ZReportTaxInfo();
                        data.Name = taxName;
                        data.TaxAmount = (decimal)taxItem["TAX AMOUNT"];
                        data.NetAmount = (decimal)taxItem["NET AMOUNT"];
                        data.Turnover = (decimal)taxItem["TURNOVER"];
                        zReport.TaxData.Add(data);
                    }
                }

                return zReport;
            }

            return null;
        }

        public static async Task<ZReportSummary> GetZReportSummary(string token, string authKey, Business business, DateTime startDate, DateTime endDate)
        {
            RestClient client = new RestClient(baseUrl);

            RestRequest request = new RestRequest("/v1/gateway/z-report-summary", Method.Get);
            request.AddHeader("Authorization", $"Bearer {token}");
            request.AddQueryParameter("AUTH_KEY", authKey);
            request.AddQueryParameter("STARTDATE", startDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("ENDDATE", endDate.ToString("yyyy-MM-dd"));
            request.AddQueryParameter("TIN", business.TIN);
            request.AddQueryParameter("COMPANYID", business.GatewayCompanyId);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);

                if (response.Content.Contains("There is no z-report summary for the date"))
                    return null;

                var summary = new ZReportSummary();
                string[] turnoverNames = { "TURNOVER TOTAL * A", "TAX * A", "TURNOVER TOTAL * B", "TAX * B", "TURNOVER TOTAL * C", "TAX * C",
                    "TURNOVER TOTAL * D", "TAX * D", "TURNOVER TOTAL * E", "TAX * E", "NET (A+B+C)", "TURNOVER (EX)",
                    "TURNOVER (S.R)", "LEGAL RECEIPTS" };


                summary.CompanyName = (string)jObject["COMPANY INFORMATION"]["name"];
                summary.CompanyAddress = (string)jObject["COMPANY INFORMATION"]["address"];
                summary.CompanyTIN = (string)jObject["COMPANY INFORMATION"]["tin"];
                summary.CompanyVRN = (string)jObject["COMPANY INFORMATION"]["vrn"];
                summary.SerialNo = (string)jObject["COMPANY INFORMATION"]["serial_number"];
                summary.UIN = (string)jObject["COMPANY INFORMATION"]["uin"];
                summary.TaxOffice = (string)jObject["COMPANY INFORMATION"]["tax_office"];
                summary.ContactPerson = (string)jObject["COMPANY INFORMATION"]["contact_person"];

                summary.StartDate = DateTime.Parse((string)jObject["z report summary"][0]["DATE RANGE"]["DATE FROM"]);
                summary.EndDate = DateTime.Parse((string)jObject["z report summary"][0]["DATE RANGE"]["DATE TO"]);
                
                summary.TurnoverData = new List<ZReportTurnoverInfo>();
                foreach (var turnoverName in turnoverNames)
                {
                    var turnoverItem = jObject["z report summary"][0]["TURNOVER"][turnoverName];
                    if (turnoverItem != null)
                    {
                        var data = new ZReportTurnoverInfo();
                        data.Name = turnoverName;
                        data.Amount = (decimal)turnoverItem;;
                        summary.TurnoverData.Add(data);
                    }
                }

                return summary;
            }

            return null;
        }

        public static async Task<byte[]> DownloadFile(string url)
        {
            int maxRetries = 3;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    var client = new RestClient();
                    var request = new RestRequest(url, Method.Get);
                    
                    // Add a timeout to prevent hanging requests
                    request.Timeout = 30000; // 30 seconds
                    
                    var response = await client.ExecuteAsync(request);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (response.RawBytes != null && response.RawBytes.Length > 0)
                        {
                            return response.RawBytes;
                        }
                        else
                        {
                            // Log empty response
                            Console.WriteLine($"Empty response received from URL: {url}");
                            retryCount++;
                            await Task.Delay(1000 * retryCount); // Exponential backoff
                            continue;
                        }
                    }
                    else
                    {
                        // Log error status code
                        Console.WriteLine($"Error downloading file from {url}: {response.StatusCode} - {response.ErrorMessage}");
                        retryCount++;
                        await Task.Delay(1000 * retryCount);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Exception downloading file from {url}: {ex.Message}");
                    retryCount++;
                    await Task.Delay(1000 * retryCount);
                }
            }
            
            // Return a default empty array after all retries fail
            return new byte[0];
        }

        public static async Task<JObject> GetUserProfile(Business business)
        {
            RestClient client = new RestClient(baseUrl);

            RestRequest request = new RestRequest("/v1/gateway/get-token", Method.Post);
            request.AddHeader("Authorization", "Bearer 657657657");
            request.AlwaysMultipartFormData = true;
            request.AddParameter("username", business.GatewayUsername);
            request.AddParameter("password", business.GatewayPassword);
            request.AddParameter("companyID", business.GatewayCompanyId);
            request.AddParameter("serial_number", business.GatewaySerialNumber);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);
               // string accessToken = (string)jObject["access_token"];
                return jObject;
            }

            return null;
        }
        public static async Task<string> ReadOcrText(string fileBase64string)
        {
            RestClient client = new RestClient(webOcrBaseUrl);

            RestRequest request = new RestRequest("/api/reader", Method.Post);
            // request.AddHeader("Authorization", "Bearer 657657657");
            request.AddHeader("Content-Type", "text/plain");
            request.AddParameter("text/plain", fileBase64string, ParameterType.RequestBody);
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // string accessToken = (string)jObject["access_token"];
                return response.Content;
            }

            return null;
        }

        public static async Task<string> ReadIronOcrText(string fileBase64string)
        {
            RestClient client = new RestClient(webOcrBaseUrl);

            RestRequest request = new RestRequest("/api/reader/ReadIronOcr", Method.Post);
            // request.AddHeader("Authorization", "Bearer 657657657");
            request.AddHeader("Content-Type", "text/plain");
            request.AddParameter("text/plain", fileBase64string, ParameterType.RequestBody);
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // string accessToken = (string)jObject["access_token"];
                return response.Content;
            }

            return response.Content;
        }
    }
}
