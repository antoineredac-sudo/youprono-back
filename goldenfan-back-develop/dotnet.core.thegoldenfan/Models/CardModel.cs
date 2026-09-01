namespace dotnet.core.thegoldenfan.Models
{
    public class CardModel
    {
        public string ContestantId { get; set; }
        public int PeriodId { get; set; }
        public int TimeMin { get; set; }
        public string timeMinSec { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public string OptaEventId { get; set; }
        public string CardReason { get; set; }
        public string TeamOfficialId { get; set; }
        public string OfficialName { get; set; }
    }
}
