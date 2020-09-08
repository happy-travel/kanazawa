namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public class ChargeOptions
    {
        public string Url { get; set; }
        public int ChunkSize { get; set; }
        public int DaysBeforeDeadline { get; set; }
    }
}