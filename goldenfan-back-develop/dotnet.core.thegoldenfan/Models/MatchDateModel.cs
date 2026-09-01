namespace dotnet.core.thegoldenfan.Models
{
    public class MatchDateModel
    {
        public string Date { get; set; }
        public string NumberOfGames { get; set; }
        public List<MatchModel> Match { get; set; }
    }
}
