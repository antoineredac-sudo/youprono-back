namespace dotnet.core.thegoldenfan.Models
{
    public class MatchModel
    {
        public string Id { get; set; }
        public string CoverageLevel { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string HomeContestantId { get; set; }
        public string AwayContestantId { get; set; }
        public string HomeContestantName { get; set; }
        public string AwayContestantName { get; set; }
    }
}
