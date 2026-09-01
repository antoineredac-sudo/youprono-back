namespace dotnet.core.thegoldenfan.Models
{
    public class TournamentCalendarModel
    {
        public string Id { get; set; }
        public string IncludesVenues { get; set; }
        public string OcId { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Active { get; set; }
        public DateTime LastUpdated { get; set; }
        public string IncludesStandings { get; set; }
    }
}
