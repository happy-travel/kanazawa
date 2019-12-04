using System;

namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public readonly struct ListOfBookingIds
    {
        public ListOfBookingIds(int[] bookingIds)
        {
            BookingIds = bookingIds ?? Array.Empty<int>();
        }


        /// <summary>
        ///     List of booking ids
        /// </summary>
        public int[] BookingIds { get; }
    }
}