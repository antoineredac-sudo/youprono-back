using dotnet.core.utils;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaTournamentCalendarService : OptaService
    {
        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/opta-sdapi-soccer-api-tournament-calendars.htm


        private readonly AppDbContext dbContext;


        public OptaTournamentCalendarService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        private async Task<List<CompetitionModel>> TournamentCalendarAllAsync(string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            List<CompetitionModel> res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = "";
                url = $"http://api.performfeeds.com/soccerdata/tournamentcalendar/{OptaKey}?_rt={mode}&_fmt={format}";
                var httpres = await client.GetAsync(url);
                var str = await httpres.Content.ReadAsStreamAsync();
                var obj = await JsonSerializer.DeserializeAsync<TournamentCalendar>(str, JsonOpts);
                if (obj != null && obj.Competition != null)
                {
                    res = obj.Competition;
                }
            }
            return res;
        }

        public async Task UpdateDbAsync(string format = "json", string mode = "b2b")
        {
            var res = await TournamentCalendarAllAsync(format, mode);
            List<Dbs.Country> newCountries = new List<Dbs.Country>();
            var lcountries = dbContext.Countries.Select(s => s.Id).ToHashSet().ToList();
            var lnotincountries = res.Where(w => !lcountries.Any(a => w.CountryId.Equals(a))).ToList();
            lnotincountries = lnotincountries.GroupBy(gb => gb.CountryId).Select(s => s.FirstOrDefault()).ToList();
            foreach (var item in lnotincountries)
            {
                Dbs.Country newObj = new Country();
                newObj.Id = item.CountryId;
                newObj.Name = item.Country;
                newObj.NormalizedName = StringHelper.NormalizeString(item.Country);
                newObj.Code = item.CountryCode;
                newCountries.Add(newObj);
            }
            if (newCountries.Count > 0)
            {
                await dbContext.Countries.AddRangeAsync(newCountries);
                await dbContext.SaveChangesAsync();
            }

            var competitionsId = dbContext.Competitions.Select(s => s.Id).ToList();
            var notInDb = res.Where(w => !competitionsId.Any(a => w.Id.Equals(a))).ToList();
            if (notInDb.Count > 0)
            {
                List<Dbs.Competition> newCompetitions = new List<Dbs.Competition>();
                foreach (var item in notInDb)
                {
                    Dbs.Competition newObj = new Competition();
                    newObj.Id = item.Id;
                    newObj.Name = item.Name;
                    newObj.NormalizedName = StringHelper.NormalizeString(item.Name);
                    newObj.OpId = item.OpId;
                    newObj.OcId = item.OcId;
                    newObj.CompetitionCode = item.CompetitionCode;
                    newObj.NormalizedCompetitionCode = StringHelper.NormalizeString(item.CompetitionCode);
                    newObj.CompetitionFormat = item.CompetitionFormat;
                    newObj.NormalizedCompetitionFormat = StringHelper.NormalizeString(item.CompetitionFormat);
                    newObj.CompetitionType = item.CompetitionType;
                    newObj.NormalizedCompetitionType = StringHelper.NormalizeString(item.CompetitionType);
                    newObj.Type = item.Type;
                    newObj.NormalizedType = StringHelper.NormalizeString(item.Type);
                    newObj.DisplayOrder = item.DisplayOrder;
                    newObj.IsFriendly = BoolConverter(item.IsFriendly);
                    newObj.CountryId = item.CountryId;
                    newCompetitions.Add(newObj);
                }
                await dbContext.Competitions.AddRangeAsync(newCompetitions);
            }

            var calendarsId = dbContext.Calendars.Select(s => s.Id).ToList();
            var o = res.Where(s => s.TournamentCalendar.FirstOrDefault() == null).ToList();
            var competitions = res.Where(s => s.TournamentCalendar.FirstOrDefault() != null && !calendarsId.Contains(s.TournamentCalendar.FirstOrDefault().Id)).ToList();
            if (competitions != null)
            {
                List<Dbs.Calendar> newCalendars = new List<Dbs.Calendar>();
                foreach (var competition in competitions)
                {
                    var item = competition.TournamentCalendar.FirstOrDefault();
                    Dbs.Calendar newObj = new Dbs.Calendar();
                    newObj.Id = item.Id;
                    newObj.OcId = item.OcId;
                    newObj.Name = item.Name;
                    newObj.NormalizedName = StringHelper.NormalizeString(item.Name);
                    newObj.IncludesVenues = BoolConverter(item.IncludesVenues);
                    newObj.Active = BoolConverter(item.Active);
                    newObj.IncludesStandings = BoolConverter(item.IncludesStandings);
                    DateTime dt = DateTime.Parse(item.StartDate);
                    DateTime dt2 = DateTime.Parse(dt.ToString("yyyy-MM-ddThh:mm:ssZ"));
                    dt2 = dt2.ToUniversalTime();
                    dt2 = dt2.AddHours(-dt2.Hour).AddMinutes(-dt2.Minute).AddSeconds(-dt2.Second);
                    newObj.StartDate = dt2;
                    dt = DateTime.Parse(item.EndDate);
                    dt2 = DateTime.Parse(dt.ToString("yyyy-MM-ddThh:mm:ssZ"));
                    dt2 = dt2.ToUniversalTime();
                    dt2 = dt2.AddHours(-dt2.Hour).AddMinutes(-dt2.Minute).AddSeconds(-dt2.Second);
                    newObj.EndDate = dt2;
                    newObj.LastUpdated = item.LastUpdated.ToUniversalTime();
                    newObj.CompetitionId = competition.Id;
                    newCalendars.Add(newObj);
                }
                await dbContext.Calendars.AddRangeAsync(newCalendars);
            }

            await dbContext.SaveChangesAsync();
        }


        public async Task<PaginationModel<CompetitionModel>> TournamentCalendarAsync(int page = 1, int pageSize = 10, string format = "json", string mode = "b2b")
        {
            PaginationModel<CompetitionModel> res = new PaginationModel<CompetitionModel>();
            var lo = await TournamentCalendarAllAsync(format, mode);
            if (lo != null)
            {
                res = PaginationModel<CompetitionModel>.CreatePage(lo, page, pageSize);
            }
            return res;
        }
        public async Task<PaginationModel<CompetitionModel>> TournamentCalendarByNameAsync(string model, int page = 1, int pageSize = 10, string format = "json", string mode = "b2b")
        {
            model = model.ToUpperInvariant();
            PaginationModel<CompetitionModel> res = new PaginationModel<CompetitionModel>();
            var lo = await TournamentCalendarAllAsync(format, mode);
            if (lo != null)
            {
                lo = lo.Where(w => w.Name != null && w.Name.ToUpperInvariant().Contains(model)).ToList();
                res = PaginationModel<CompetitionModel>.CreatePage(lo, page, pageSize);
            }
            return res;
        }
        public async Task<PaginationModel<CompetitionModel>> TournamentCalendarByCountryCodeAsync(string model, int page = 1, int pageSize = 10, string format = "json", string mode = "b2b")
        {
            model = model.ToUpperInvariant();
            PaginationModel<CompetitionModel> res = new PaginationModel<CompetitionModel>();
            var lo = await TournamentCalendarAllAsync(format, mode);
            if (lo != null)
            {
                lo = lo.Where(w => w.CountryCode != null && w.CountryCode.ToUpperInvariant().Contains(model)).ToList();
                res = PaginationModel<CompetitionModel>.CreatePage(lo, page, pageSize);
            }
            return res;
        }
        public async Task<PaginationModel<CompetitionModel>> TournamentCalendarByCountryCodeAndNameAsync(string countryCode, string name, int page = 1, int pageSize = 10, string format = "json", string mode = "b2b")
        {
            countryCode = countryCode.ToUpperInvariant();
            name = name.ToUpperInvariant();
            PaginationModel<CompetitionModel> res = new PaginationModel<CompetitionModel>();
            var lo = await TournamentCalendarAllAsync(format, mode);
            if (lo != null)
            {
                lo = lo.Where(w =>
                w.CountryCode != null && w.CountryCode.ToUpperInvariant().Contains(countryCode) &&
                w.Name != null && w.Name.ToUpperInvariant().Contains(name)).ToList();
                res = PaginationModel<CompetitionModel>.CreatePage(lo, page, pageSize);
            }
            return res;
        }
        public async Task<CompetitionModel?> TournamentCalendarByCompetitionAsync(string model, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            CompetitionModel? res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = "";
                url = $"http://api.performfeeds.com/soccerdata/tournamentcalendar/{OptaKey}/active?_rt={mode}&_fmt={format}&comp={model}";
                var httpres = await client.GetAsync(url);
                var str = await httpres.Content.ReadAsStreamAsync();
                var obj = await JsonSerializer.DeserializeAsync<TournamentCalendar>(str, JsonOpts);
                if (obj != null && obj.Competition != null) { res = obj.Competition.FirstOrDefault(); }
            }
            return res;
        }
        public async Task<CompetitionModel?> TournamentCalendarByContestantAsync(string model, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            CompetitionModel? res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = "";
                url = $"http://api.performfeeds.com/soccerdata/tournamentcalendar/{OptaKey}/active?_rt={mode}&_fmt={format}&ctst={model}";
                var httpres = await client.GetAsync(url);
                var str = await httpres.Content.ReadAsStreamAsync();
                var obj = await JsonSerializer.DeserializeAsync<TournamentCalendar>(str, JsonOpts);
                if (obj != null && obj.Competition != null) { res = obj.Competition.FirstOrDefault(); }
            }
            return res;
        }
    }
}
