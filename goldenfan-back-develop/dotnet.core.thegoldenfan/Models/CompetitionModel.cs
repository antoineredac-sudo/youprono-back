namespace dotnet.core.thegoldenfan.Models
{
    public class CompetitionModel
    {
        public string Id { get; set; }
        public string OcId { get; set; }
        public string OpId { get; set; }
        public string Name { get; set; }
        public string CompetitionCode { get; set; }
        public int DisplayOrder { get; set; }
        public string Country { get; set; }
        public string CountryId { get; set; }
        public string CountryCode { get; set; }
        public string IsFriendly { get; set; }
        public string CompetitionFormat { get; set; }
        public string Type { get; set; }
        public List<TournamentCalendarModel> TournamentCalendar { get; set; }
        public string CompetitionType { get; set; }
    }
}
