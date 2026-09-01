namespace dotnet.core.thegoldenfan.Models
{
    public class MatchInfoModel
    {
        public string Id { get; set; }
        public string CoverageLevel { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string LocalDate { get; set; }
        public string LocalTime { get; set; }
        public string Week { get; set; }
        public string PostMatch { get; set; }
        public string AttendanceInfoId { get; set; }
        public string AttendanceInfo { get; set; }
        public int NumberOfPeriods { get; set; }
        public int PeriodLength { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Description { get; set; }
        public SportModel Sport { get; set; }
        public RulesetModel Ruleset { get; set; }
        public MatchInfoCompetitionModel Competition { get; set; }
        public TournamentCalendarModel TournamentCalendar { get; set; }
        public StageModel Stage { get; set; }
        public List<MatchInfoContestantModel> Contestant { get; set; }
        public VenueModel Venue { get; set; }
    }
}
