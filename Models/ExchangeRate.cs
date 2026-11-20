using System;
using System.ComponentModel.DataAnnotations;

namespace InvoiceScanner.Models
{
    
    public class ExchangeRate
    {

        public long Id { get; set; }
        [Required(ErrorMessage = "Rate amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Rate amount must be greater than 0.")]
        public decimal RateAmount { get; set; }

        public long BusinessId { get; set; } 
        public virtual Business Business { get; set; }

        [Required]
        public string TemplateType { get; set; }
        [Required]
        public string RateName { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [StartDateValidation(ErrorMessage = "Start date must be in the future.")]
        public DateTime StartDate { get; set; } // Start date of the range

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [EndDateValidation(ErrorMessage = "End date must be greater than or equal to the start date.")]
        public DateTime EndDate { get; set; } // End date of the range
    }

    public class StartDateValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is DateTime startDate)
            {
                // Check if the start date is in the future
                return startDate >= DateTime.Now.Date;
            }

            return false;
        }
    }

    public class EndDateValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is DateTime endDate)
            {
                // You can customize this validation logic based on your requirements
                return endDate >= DateTime.Now.Date;
            }

            return false;
        }
    }
}

