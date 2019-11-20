using System;

namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public readonly struct ProcessPaymentsInfo
    {
        public ProcessPaymentsInfo(DateTime date)
        {
            Date = date;
        }

        public DateTime Date { get; }
    }
}