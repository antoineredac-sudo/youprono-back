namespace dotnet.core.thegoldenfan.Models
{
    public class TeamSquadModel
    {
        public string ContestantId { get; set; }
        public string OcContestantId { get; set; }
        public string OpContestantId { get; set; }
        public string ContestantName { get; set; }
        public string ContestantShortName { get; set; }
        public string ContestantClubName { get; set; }
        public string ContestantCode { get; set; }
        public string TournamentCalendarId { get; set; }
        public string TournamentCalendarStartDate { get; set; }
        public string TournamentCalendarEndDate { get; set; }
        public string CompetitionName { get; set; }
        public string CompetitionId { get; set; }
        public string OcId { get; set; }
        public string OpId { get; set; }
        public string Type { get; set; }
        public string TeamType { get; set; }
        public string VenueName { get; set; }
        public string VenueId { get; set; }
        public List<PersonModel> Person { get; set; }
    }
}
