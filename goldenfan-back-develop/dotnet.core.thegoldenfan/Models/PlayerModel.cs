namespace dotnet.core.thegoldenfan.Models
{
    public class PlayerModel
    {
        public string PlayerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ShortFirstName { get; set; }
        public string ShortLastName { get; set; }
        public string KnownName { get; set; }
        public string MatchName { get; set; }
        public int ShirtNumber { get; set; }
        public string Position { get; set; }
        public string PositionSide { get; set; }
        public string FormationPlace { get; set; }
        public List<PlayerStatModel> Stat { get; set; }
        public string Captain { get; set; }
        public string SubPosition { get; set; }
    }
}
