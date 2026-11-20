using InvoiceScanner.Models;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

namespace InvoiceScanner.Services
{
    public static class MailSender
    {
        // const string apiKey = "";

        public static async Task<bool> Send(string receipient, string subject, string html, string text = "", byte[] attachment = null)
        {
            var options = new RestClientOptions
            {
                BaseUrl = new Uri("https://api.eu.mailgun.net/v3"),
                Authenticator = new HttpBasicAuthenticator("api", apiKey)
            };

            RestClient client = new RestClient(options);

            RestRequest request = new RestRequest("/mailing.jsuite.app/messages");
            request.AddParameter("domain", "mailing.jsuite.app", ParameterType.UrlSegment);
            //request.Resource = "agizachap.co.tz/messages";
            request.AddParameter("from", "WebVFD IPS <customers@mailing.jsuite.app>");
            request.AddParameter("h:Reply-To", "joshua@webcorporation.co.tz");
            request.AddParameter("to", receipient);
            request.AddParameter("subject", subject);
            request.AddParameter("html", html);

            //if (attachment != null)
            //    request.AddFile("", );

            if (!string.IsNullOrEmpty(text))
                request.AddParameter("text", text);

            request.Method = Method.Post;
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                return true;

            return false;
        }

        public static async Task<bool> Send(string receipient, string subject, string text, byte[] attachment, string filename)
        {
            var options = new RestClientOptions
            {
                BaseUrl = new Uri("https://api.eu.mailgun.net/v3"),
                Authenticator = new HttpBasicAuthenticator("api", apiKey)
            };

            RestClient client = new RestClient(options);

            RestRequest request = new RestRequest("/mailing.jsuite.app/messages");
            request.AddParameter("domain", "mailing.jsuite.app", ParameterType.UrlSegment);
            //request.Resource = "agizachap.co.tz/messages";
            request.AddParameter("from", "PSPTB <customers@mailing.jsuite.app>");
            request.AddParameter("h:Reply-To", "webvfd.ips@webcorporation.co.tz");
            request.AddParameter("to", receipient);
            request.AddParameter("subject", subject);
            // request.AddParameter("text", text);
            request.AddParameter("html", text);
            request.AddFile("attachment", attachment, filename);

            request.Method = Method.Post;
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                return true;

            return false;
        }

        public static async Task<bool> SendEmailDefault(string receipient, string subject, string text, byte[] attachment, string filename, string ReplyTo)
        {
            var options = new RestClientOptions
            {
                BaseUrl = new Uri("https://api.eu.mailgun.net/v3"),
                Authenticator = new HttpBasicAuthenticator("api", apiKey)
            };

            RestClient client = new RestClient(options);

            RestRequest request = new RestRequest("/mailing.jsuite.app/messages");
            request.AddParameter("domain", "mailing.jsuite.app", ParameterType.UrlSegment);
            request.AddParameter("from", "WebVFD IPS <" + ReplyTo + ">");
            request.AddParameter("h:Reply-To", ReplyTo);
            request.AddParameter("to", receipient);
            request.AddParameter("subject", subject);
           // request.AddParameter("text", text);
            request.AddParameter("html", text);
            if (attachment != null)
            {
                request.AddFile("attachment", attachment, filename);

            }

            request.Method = Method.Post;
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                return true;

            return false;
        }
        public static async Task<bool> MailgunEmailSend(string recipient, string subject, string text, byte[] attachment, string filename, string mailgunBaseUrl, string mailgunApiKey, string emailFrom,string domain,string endpoint,string displayName)
        {
            var options = new RestClientOptions
            {
                BaseUrl = new Uri(mailgunBaseUrl),
                Authenticator = new HttpBasicAuthenticator("api", mailgunApiKey)
            };

            RestClient client = new RestClient(options);

            RestRequest request = new RestRequest(endpoint);
            request.AddParameter("domain", domain, ParameterType.UrlSegment);
            request.AddParameter("from", displayName + " <" + emailFrom + ">");
            request.AddParameter("h:Reply-To", emailFrom);
            request.AddParameter("to", recipient);
            request.AddParameter("subject", subject);
            // request.AddParameter("text", text);
            request.AddParameter("html", text);
            request.AddFile("attachment", attachment, filename);

            request.Method = Method.Post;
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                return true;

            return false;
        }

        public static async Task<bool> SMTPSendEmailAsync(string toEmail, string subject, string body, CompanySetting companySetting, byte[] attachment = null, string attachmentFileName = null)
        {
            try
            {
                using (var client = new SmtpClient())
                {
                    // Configure SMTP settings
                    client.Host = companySetting.SmtpHost;
                    client.Port = companySetting.SmtpPort;
                    client.EnableSsl = companySetting.EnableSsl;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(companySetting.SmtpUsername, companySetting.SmtpPassword);

                    using (var message = new MailMessage(companySetting.DefaultSenderEmail, toEmail))
                    {
                        message.Subject = companySetting.CompanyName + " " + subject;
                        message.Body = body;
                        message.IsBodyHtml = true;

                        if (attachment != null && !string.IsNullOrEmpty(attachmentFileName))
                        {
                            using (var stream = new MemoryStream(attachment))
                            {
                                message.Attachments.Add(new Attachment(stream, attachmentFileName));
                                await client.SendMailAsync(message);
                            }
                        }
                        else
                        {
                            await client.SendMailAsync(message);
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Handle exception or log error
                return false;
            }
        }

    }
}
