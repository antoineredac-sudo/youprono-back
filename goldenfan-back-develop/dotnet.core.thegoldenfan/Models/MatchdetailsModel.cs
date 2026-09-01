namespace dotnet.core.thegoldenfan.Models
{
    public class MatchdetailsModel
    {
        public int PeriodId { get; set; }
        public string MatchStatus { get; set; }
        public string Winner { get; set; }
        public int MatchLengthMin { get; set; }
        public int MatchLengthSec { get; set; }
        public List<PeriodModel> Period { get; set; }
        public ScoresModel Scores { get; set; }
    }
}
