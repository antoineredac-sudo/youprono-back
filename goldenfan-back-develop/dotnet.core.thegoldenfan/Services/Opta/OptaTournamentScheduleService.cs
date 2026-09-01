using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaTournamentScheduleService : OptaService
    {
        private readonly AppDbContext dbContext;
        private readonly OptaTeamService optaTeamService;


        public OptaTournamentScheduleService(AppDbContext dbContext,
            OptaTeamService optaTeamService)
        {
            this.dbContext = dbContext;
            this.optaTeamService = optaTeamService;
        }


        public async Task UpdateDbByCalendarIdAsync(string model, string format = "json", string mode = "b2b")
        {
            var res = await All(model, format, mode);
            DateTime now = DateTime.UtcNow;

            //create new team in db if not present
            List<string> newTeams = new List<string>();
            foreach(var matchdate in res.MatchDate)
            {
                foreach(var match in matchdate.Match)
                {
                    newTeams.Add(match.AwayContestantId);
                    newTeams.Add(match.HomeContestantId);
                }
            }
            newTeams = newTeams.ToHashSet().ToList();
            var inDbTeams = dbContext.Teams.AsEnumerable().Where(w => newTeams.Any(a => w.Id.Equals(a))).ToList();
            newTeams.RemoveAll(w => inDbTeams.Any(a => w.Equals(a.Id)));
            if(newTeams!=null && newTeams.Count > 0)
            {
               foreach(var item in newTeams)
               {
                    await optaTeamService.AddDbByContestanAsync(item, true);
               }
            }

            bool executeSave = false;
            List<MatchDate> newMatchDates = new List<MatchDate>();
            List<Match> newMatches = new List<Match>();
            List<TeamMatch> newTeamMatches = new List<TeamMatch>();

            newMatchDates = res.MatchDate.Select(s =>
            {
                return new MatchDate()
                {
                    Date = DateTime.Parse(s.Date.ToUpper().Replace("Z", "T00:00:00z")).ToUniversalTime(),
                    CalendarId = res.TournamentCalendar.Id
                };
            }).ToList();
            //get all matchdates
            var inDbMatchDates = dbContext
                .MatchDates
                .AsEnumerable()
                .Where(w => newMatchDates.Any(a => w.Date.Equals(a.Date) && w.CalendarId.Equals(a.CalendarId)))
                .ToList();
            //remove matchdates already in db
            newMatchDates.RemoveAll(w => inDbMatchDates.Any(a => w.Date.Equals(a.Date) && w.CalendarId.Equals(a.CalendarId)));

            foreach(var matchDate in res.MatchDate)
            {
                MatchDate newMatchDate;
                var tmpDate = DateTime.Parse(matchDate.Date.ToUpper().Replace("Z", "T00:00:00z")).ToUniversalTime();
                var inDb = inDbMatchDates.FirstOrDefault(w => w.Date.Equals(tmpDate) && w.CalendarId.Equals(res.TournamentCalendar.Id));
                if (inDb == null) //if new matchdates (not in db)
                {
                    newMatchDate = newMatchDates.FirstOrDefault(w => w.Date.Equals(tmpDate) && w.CalendarId.Equals(res.TournamentCalendar.Id));
                    newMatchDate.Id = Guid.NewGuid();
                } //else already in db so update
                else { newMatchDate = inDb; }

                foreach (var match in matchDate.Match)
                {
                    TeamMatch newAwayTeamMatch = new TeamMatch();
                    newAwayTeamMatch.Id = Guid.NewGuid();
                    newAwayTeamMatch.TeamId = match.AwayContestantId;
                    newAwayTeamMatch.CreateDate = now;
                    newTeamMatches.Add(newAwayTeamMatch);

                    TeamMatch newHomeTeamMatch = new TeamMatch();
                    newHomeTeamMatch.Id = Guid.NewGuid();
                    newHomeTeamMatch.TeamId = match.HomeContestantId;
                    newHomeTeamMatch.CreateDate = now;
                    newTeamMatches.Add(newHomeTeamMatch);

                    Match newMatch = new Match();
                    newMatch.Id = match.Id;
                    newMatch.MatchDateId = newMatchDate.Id;
                    newMatch.AwayTeam = newAwayTeamMatch;
                    newMatch.AwayTeamId = newAwayTeamMatch.Id;
                    newMatch.HomeTeam = newHomeTeamMatch;
                    newMatch.HomeTeamId = newHomeTeamMatch.Id;
                    newMatch.CoverageLevel = match.CoverageLevel;
                    var d = StringHelper.IsNull(match.Time) ? match.Date : match.Date.Replace("Z", string.Concat("T", match.Time));
                    newMatch.DateTime = DateTime.Parse(d).ToUniversalTime();
                    newMatches.Add(newMatch);
                }
            }

            //add only new matchDates
            if(newMatchDates != null && newMatchDates.Count > 0)
            { await dbContext.MatchDates.AddRangeAsync(newMatchDates); executeSave = true; }

            //get matches in db
            var inDbMatches = dbContext
                .Matches
                .Include(i => i.AwayTeam)
                .Include(i => i.HomeTeam)
                .AsEnumerable()
                .Where(w => newMatches.Any(a => w.Id.Equals(a.Id))).ToList();
            //remove matches already in db with team
            foreach(var item in inDbMatches)
            {
                var o = newMatches.FirstOrDefault(w => w.Id.Equals(item.Id));
                if(o!=null)
                {
                    newMatches.Remove(o);
                    newTeamMatches.Remove(o.AwayTeam);
                    newTeamMatches.Remove(o.HomeTeam);
                }
            }
            if(newTeamMatches!=null && newTeamMatches.Count>0)
            { await dbContext.TeamMatches.AddRangeAsync(newTeamMatches); executeSave = true; }
            if(newMatches!=null && newMatches.Count>0)
            { await dbContext.Matches.AddRangeAsync(newMatches); executeSave = true; }

            if (executeSave) { await dbContext.SaveChangesAsync(); }
        }


        public async Task<OptaTournamentSchedule> All(string model, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            OptaTournamentSchedule res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/tournamentschedule/{OptaService.OptaKey}/{model}?_rt={mode}&_fmt={format}";
                var httpres = await client.GetAsync(url);
                if (httpres.IsSuccessStatusCode)
                {
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await System.Text.Json.JsonSerializer.DeserializeAsync<OptaTournamentSchedule>(str, JsonOpts);
                }
                else { throw new Exception(httpres.ReasonPhrase); }
            }
            return res;
        }
        public async Task<OptaTournamentSchedule> ByContestantName(string tournamentCalendarId, string contestantName, string format = "json", string mode = "b2b")
        {
            contestantName = contestantName.ToUpperInvariant();
            mode = Mode(mode);
            OptaTournamentSchedule res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/tournamentschedule/{OptaService.OptaKey}/{tournamentCalendarId}?_rt={mode}&_fmt={format}";
                var httpres = await client.GetAsync(url);
                if(httpres.IsSuccessStatusCode)
                { 
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await JsonSerializer.DeserializeAsync<OptaTournamentSchedule>(str, JsonOpts);
                    foreach(var item in res.MatchDate)
                    {
                        item.Match = item.Match.Where(w => (w.AwayContestantName != null && w.HomeContestantName != null) &&
                                (contestantName.Contains(w.AwayContestantName.ToUpperInvariant()) || contestantName.Contains(w.HomeContestantName.ToUpperInvariant())))
                            .ToList();
                    }
                    res.MatchDate = res.MatchDate.Where(w => w.Match != null && w.Match.Count > 0).ToList();
                }
                else { throw new Exception(httpres.ReasonPhrase); }
            }
            return res;
        }
        public async Task<OptaTournamentSchedule> ByContestantId(string tournamentCalendarId, string contestantId, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            OptaTournamentSchedule res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/tournamentschedule/{OptaService.OptaKey}/{tournamentCalendarId}?_rt={mode}&_fmt={format}";
                var httpres = await client.GetAsync(url);
                if(httpres.IsSuccessStatusCode)
                { 
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await JsonSerializer.DeserializeAsync<OptaTournamentSchedule>(str, JsonOpts);
                    foreach (var item in res.MatchDate)
                    {
                        item.Match = item.Match.Where(w => w.AwayContestantId.Equals(contestantId) || w.HomeContestantId.Equals(contestantId))
                            .ToList();
                    }
                    res.MatchDate = res.MatchDate.Where(w => w.Match != null && w.Match.Count > 0).ToList();
                }
                else { throw new Exception(httpres.ReasonPhrase); }
            }
            return res;
        }
        public async Task<OptaTournamentSchedule> ByDate(string tournamentCalendarId, DateTime date, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            OptaTournamentSchedule res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/tournamentschedule/{OptaService.OptaKey}/{tournamentCalendarId}?_rt={mode}&_fmt={format}";
                var httpres = await client.GetAsync(url);
                if(httpres.IsSuccessStatusCode)
                { 
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await JsonSerializer.DeserializeAsync<OptaTournamentSchedule>(str, JsonOpts);
                    res.MatchDate = res.MatchDate.Where(w=>DateTime.Parse(w.Date).Date.Equals(date.Date)).ToList();
                }
                else { throw new Exception(httpres.ReasonPhrase); }
            }
            return res;
        }
    }
}
