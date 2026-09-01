namespace dotnet.core.thegoldenfan.Models
{
    public class GoalModel
    {
        public string ContestantId { get; set; }
        public int PeriodId { get; set; }
        public int TimeMin { get; set; }
        public string TimeMinSec { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string ScorerId { get; set; }
        public string ScorerName { get; set; }
        public string OptaEventId { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public string AssistPlayerId { get; set; }
        public string AssistPlayerName { get; set; }
    }
}
