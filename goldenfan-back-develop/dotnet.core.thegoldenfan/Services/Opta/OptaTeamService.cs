using dotnet.core.utils;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaTeamService : OptaService
    {
        private readonly AppDbContext dbContext;


        public OptaTeamService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/opta-sdapi-soccer-api-teams.htm
        private async Task<OptaTeam?> TeamAsync(string opt, string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            OptaTeam? res = null;
            string isDetailed = IsDetailed(detailed);
            using (HttpClient client = new HttpClient())
            {
                string url = "";
                url = $"http://api.performfeeds.com/soccerdata/team/{OptaService.OptaKey}?_rt={mode}&_fmt={format}&{opt}={model}{isDetailed}";
                var httpres = await client.GetAsync(url);
                if (httpres.IsSuccessStatusCode)
                {
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await System.Text.Json.JsonSerializer.DeserializeAsync<OptaTeam>(str, JsonOpts);
                }
                else
                {
                    string error = await httpres.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }
            }
            return res;
        }

        public async Task UpdateDbAsync(bool detailed = true, string format = "json", string mode = "b2b")
        {
            var countries = dbContext.Countries.ToList();
            var inDbTeamsId = dbContext.Teams.Select(s => s.Id).ToList();
            List<Team> newTeams = new List<Team>();
            foreach(var country in countries)
            {
                var obj = await TeamAsync("ctry", country.Id, detailed, format, mode);
                if(obj!=null && obj.Contestant!=null && obj.Contestant.Count>0)
                {
                    Console.WriteLine("=====================================");
                    Console.WriteLine(country.Name);
                    obj.Contestant = obj.Contestant.Where(w => !inDbTeamsId.Any(a => w.Id.Equals(a))).ToList();
                    foreach(var item in obj.Contestant)
                    {
                        Console.WriteLine(country.Name + " - " + item.Name);
                        Team newObj = new Team();
                        newObj.Id = item.Id;
                        newObj.Name = item.Name;
                        newObj.NormalizedName = StringHelper.NormalizeString(item.Name);
                        newObj.OfficialName = item.OfficialName;
                        newObj.NormalizedOfficialName = StringHelper.NormalizeString(item.OfficialName);
                        newObj.ShortName = item.ShortName;
                        newObj.NormalizedShortName = StringHelper.NormalizeString(item.ShortName);
                        newObj.Code = item.Code;
                        newObj.Type = item.Type;
                        newObj.TeamType = item.TeamType;
                        newObj.CountryId = item.CountryId;
                        newObj.City = item.City;
                        newObj.PostalAddress = item.PostalAddress;
                        newObj.AddressZip = item.AddressZip;
                        newObj.Founded = item.Founded;
                        newObj.Details = item.Details;
                        newObj.Status = BoolConverter(item.Status);
                        newObj.LastUpdated = item.LastUpdated;
                        newTeams.Add(newObj);
                    }
                    if (newTeams != null && newTeams.Count > 0)
                    {
                        await dbContext.Teams.AddRangeAsync(newTeams);
                        await dbContext.SaveChangesAsync();
                        newTeams.Clear();
                    }
                }
            }
        }
        public async Task AddDbByContestanAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            var res = await TeamByContestantAsync(model, detailed, format, mode);
            Team newObj = new Team();
            newObj.Id = res.Id;
            newObj.Name = res.Name;
            newObj.NormalizedName = StringHelper.NormalizeString(res.Name);
            newObj.OfficialName = res.OfficialName;
            newObj.NormalizedOfficialName = StringHelper.NormalizeString(res.OfficialName);
            newObj.ShortName = res.ShortName;
            newObj.NormalizedShortName = StringHelper.NormalizeString(res.ShortName);
            newObj.Code = res.Code;
            newObj.Type = res.Type;
            newObj.TeamType = res.TeamType;
            newObj.CountryId = res.CountryId;
            newObj.City = res.City;
            newObj.PostalAddress = res.PostalAddress;
            newObj.AddressZip = res.AddressZip;
            newObj.Founded = res.Founded;
            newObj.Details = res.Details;
            newObj.Status = BoolConverter(res.Status);
            newObj.LastUpdated = res.LastUpdated;

            var inDb = dbContext.Countries.FirstOrDefault(w => w.Id.Equals(res.CountryId));
            if (inDb == null)
            {
                Country newCountry = new Country();
                newCountry.Id = res.CountryId;
                newCountry.Name = res.Country;
                newCountry.NormalizedName = StringHelper.NormalizeString(res.Country);
                dbContext.Countries.Add(newCountry);
            }
            dbContext.Teams.Add(newObj);
            await dbContext.SaveChangesAsync();
        }


        public async Task<PaginationModel<ContestantModel>> TeamByCountryAsync(string model, bool detailed = false, int page=1, int limit=10, string format = "json", string mode = "b2b")
        {
            PaginationModel<ContestantModel> res = null;
            try
            {
                var obj = await TeamAsync("ctry", model, detailed, format, mode);
                var lo = obj.Contestant.Where(w => w.Name.ToUpper().Contains("MONACO")).ToList();
                if (obj != null && obj.Contestant != null)
                {
                    res = PaginationModel<ContestantModel>.CreatePage(obj.Contestant, page, limit);
                }
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
        public async Task<ContestantModel?> TeamByContestantAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            ContestantModel? res = null;
            try
            {
                var obj = await TeamAsync("ctst", model, detailed, format, mode);
                if (obj != null && obj.Contestant != null)
                {
                    res = obj.Contestant.FirstOrDefault();
                }
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
        public async Task<PaginationModel<ContestantModel>> TeamByTournamentCalendarAsync(string model, bool detailed = false, int page=1, int limit=10, string format = "json", string mode = "b2b")
        {
            PaginationModel<ContestantModel> res = null;
            try
            {
                var obj = await TeamAsync("tmcl", model, detailed, format, mode);
                if (obj != null && obj.Contestant != null)
                {
                    res = PaginationModel<ContestantModel>.CreatePage(obj.Contestant, page, limit);
                }
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
        public async Task<PaginationModel<ContestantModel>> TeamByStageAsync(string model, bool detailed = false, int page=1, int limit=10, string format = "json", string mode = "b2b")
        {
            PaginationModel<ContestantModel> res = null;
            try
            {
                var obj = await TeamAsync("stg", model, detailed, format, mode);
                if (obj != null && obj.Contestant != null)
                {
                    res = PaginationModel<ContestantModel>.CreatePage(obj.Contestant, page, limit);
                }
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
        public async Task<PaginationModel<ContestantModel>> TeamBySerieAsync(string model, bool detailed = false, int page=1, int limit=10, string format = "json", string mode = "b2b")
        {
            PaginationModel<ContestantModel> res = null;
            try
            {
                var obj = await TeamAsync("srs", model, detailed, format, mode);
                if (obj != null && obj.Contestant != null)
                {
                    res = PaginationModel<ContestantModel>.CreatePage(obj.Contestant, page, limit);
                }
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
    }
}
