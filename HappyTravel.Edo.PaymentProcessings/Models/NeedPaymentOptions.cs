namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public class NeedPaymentOptions
    {
        public string GetUrl { get; set; }
        public string ProcessUrl { get; set; }
        public int ChunkSize { get; set; }
    }
}