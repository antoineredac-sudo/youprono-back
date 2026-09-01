namespace dotnet.core.thegoldenfan.Models
{
    public class LiveDataModel
    {
        public MatchdetailsModel MatchDetails { get; set; }
        public List<GoalModel> Goal { get; set; }
        public List<CardModel> Card { get; set; }
        public List<SubstituteModel> Substitute { get; set; }
        public List<LineupModel> LineUp { get; set; }
        public MatchdetailsExtraModel MatchDetailsExtra { get; set; }
    }
}
