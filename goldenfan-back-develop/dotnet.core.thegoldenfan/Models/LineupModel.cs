namespace dotnet.core.thegoldenfan.Models
{
    public class LineupModel
    {
        public string ContestantId { get; set; }
        public string FormationUsed { get; set; }
        public List<PlayerModel> Player { get; set; }
        public TeamOfficialModel TeamOfficial { get; set; }
        public List<LineupStatModel> Stat { get; set; }
        public KitModel Kit { get; set; }
    }
}
