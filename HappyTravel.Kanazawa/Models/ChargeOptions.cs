namespace HappyTravel.Kanazawa.Models
{
    public class ChargeOptions
    {
        public string RequestUrl { get; set; }
        public string ProcessingUrl { get; set; }
        public int ChunkSize { get; set; }
        public int DaysBeforeDeadline { get; set; }
    }
}