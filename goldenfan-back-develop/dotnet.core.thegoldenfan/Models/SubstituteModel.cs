namespace dotnet.core.thegoldenfan.Models
{
    public class SubstituteModel
    {
        public string ContestantId { get; set; }
        public int PeriodId { get; set; }
        public int TimeMin { get; set; }
        public string TimeMinSec { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Timestamp { get; set; }
        public string PlayerOnId { get; set; }
        public string PlayerOnName { get; set; }
        public string PlayerOffId { get; set; }
        public string PlayerOffName { get; set; }
        public string SubReason { get; set; }
    }
}
