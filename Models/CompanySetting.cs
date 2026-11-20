using System.ComponentModel;

namespace InvoiceScanner.Models
{
    public class CompanySetting
    {
        public int Id { get; set; }
       
        [DisplayName("Email Provider")]
        public EmailProvider Provider { get; set; }
        [DisplayName("Exchange Rate")]
        public decimal ExchangeRate { get; set; }
        [DisplayName("Horizontal Exchange Rate")]
        public decimal HorizontalExchangeRate { get; set; }

        [DisplayName("EUR Exchange Rate ")]
        public decimal ExchangeRateEUR { get; set; }

        [DisplayName("Mailgun BaseUrl")]
        public string MailgunBaseUrl { get; set; }
        [DisplayName("Mailgun Api Key")]
        public string MailgunApiKey { get; set; }
        [DisplayName("Email From")]
        public string EmailFrom { get; set; }

        // SMTP Settings
        [DisplayName("SMTP Host")]
        public string SmtpHost { get; set; }
        [DisplayName("SMTP Port")]
        public int SmtpPort { get; set; }
        [DisplayName("SMTP Username")]
        public string SmtpUsername { get; set; }
        [DisplayName("SMTP Password")]
        public string SmtpPassword { get; set; }
        [DisplayName("Enable SSL")]
        public bool EnableSsl { get; set; }
        [DisplayName("Default Sender Email")]
        public string DefaultSenderEmail { get; set; }
        // Additional Company Settings
        [DisplayName("Company Name")]
        public string CompanyName { get; set; }

        [DisplayName("Mailgun Domain")]
        public string MailgunDomain { get; set; }

        [DisplayName("Mailgun Endpoint")]
        public string MailgunEndPoint { get; set; }
        public long? BusinessId { get; set; } // Foreign key
        public virtual Business Business { get; set; } // Navigation property
    }


    public enum EmailProvider
    {
        Default,
        SMTP,
        Mailgun
    }


}
