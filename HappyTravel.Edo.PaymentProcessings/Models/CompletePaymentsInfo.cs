using System;

namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public readonly struct CompletePaymentsInfo
    {
        public CompletePaymentsInfo(int[] bookingIds)
        {
            BookingIds = bookingIds ?? Array.Empty<int>();
        }


        /// <summary>
        ///     List of booking ids that should be completed
        /// </summary>
        public int[] BookingIds { get; }
    }
}