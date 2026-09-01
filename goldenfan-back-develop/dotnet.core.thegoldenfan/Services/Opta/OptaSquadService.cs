using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaSquadService : OptaService
    {
        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/opta-sdapi-soccer-api-squads.htm

        private readonly AppDbContext dbContext;


        public OptaSquadService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public async Task<OptaTeamSquad?> SquadByTournamentCalendarAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            OptaTeamSquad? res = null;
            string tmcl = $"&tmcl={model}";
            try
            {
                res = await SquadByTournamentCalendarAndContestantAsync(tmcl, "", detailed, format, mode);
            }
            catch (Exception ex) { throw ex; }
            return res;
        }


        public async Task UpdateDbByContestantAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            OptaTeamSquad? res = null;
            string ctst = $"&ctst={model}";
            try
            {
                res = await SquadByTournamentCalendarAndContestantAsync("", ctst, detailed, format, mode);
                List<Person> newPersons = new List<Person>();
                List<Player> newPlayers = new List<Player>();
                List<Nationality> newNationalities = new List<Nationality>();
                List<Country> newCountries = new List<Country>();

                foreach (var squad in res.Squad)
                {
                    //Console.WriteLine("=================================================");
                    //Console.WriteLine(squad.ContestantName);
                    foreach (var person in squad.Person)
                    {
                        Console.WriteLine(person.FirstName + " - " + person.LastName);
                        Nationality newNationality = new Nationality();
                        newNationality.Id = person.NationalityId;
                        newNationality.Name = person.Nationality;
                        newNationality.NormalizedName = StringHelper.NormalizeString(person.Nationality);
                        newNationalities.Add(newNationality);

                        Country newCountry = new Country();
                        newCountry.Id = person.CountryOfBirthId;
                        newCountry.Name = person.CountryOfBirth;
                        newCountry.NormalizedName = StringHelper.NormalizeString(person.CountryOfBirth);
                        newCountries.Add(newCountry);

                        Person newObj = new Person();
                        newObj.Id = person.Id;
                        newObj.OpId = person.OpId;
                        newObj.OcId = person.OcId;
                        newObj.FirstName = person.FirstName;
                        newObj.NormalizedFirstName = StringHelper.NormalizeString(person.FirstName);
                        newObj.LastName = person.LastName;
                        newObj.NormalizedLastName = StringHelper.NormalizeString(person.LastName);
                        newObj.MiddleName = person.MiddleName;
                        newObj.NormalizedMiddleName = StringHelper.NormalizeString(person.MiddleName);
                        newObj.ShortLastName = person.ShortLastName;
                        newObj.NormalizedShortLastName = StringHelper.NormalizeString(person.ShortLastName);
                        newObj.ShortFirstName = person.ShortFirstName;
                        newObj.NormalizedShortFirstName = StringHelper.NormalizeString(person.ShortFirstName);
                        newObj.KnownName = person.KnownName;
                        newObj.NormalizedKnownName = StringHelper.NormalizeString(person.KnownName);
                        newObj.MatchName = person.MatchName;
                        newObj.NormalizedMatchName = StringHelper.NormalizeString(person.MatchName);
                        newObj.NationalityId = person.NationalityId;
                        newObj.DateOfBirth = (person.DateOfBirth == null ? null : DateTime.Parse(person.DateOfBirth));
                        newObj.DateOfDeath = (person.DateOfDeath == null ? null : DateTime.Parse(person.DateOfDeath));
                        newObj.CountryOfBirthId = person.CountryOfBirthId;
                        newObj.Status = BoolConverter(person.Status);
                        newObj.Height = person.Height;
                        newObj.Weight = person.Weight;
                        newObj.Foot = person.Foot;
                        newPersons.Add(newObj);

                        Player newPlayer = new Player();
                        newPlayer.Id = Guid.NewGuid();
                        newPlayer.PersonId = person.Id;
                        newPlayer.TeamId = squad.ContestantId;
                        newPlayer.ShirtNumber = person.ShirtNumber;
                        newPlayer.Active = BoolConverter(person.Active);
                        newPlayer.Position = person.Position;
                        newPlayer.Type = person.Type;
                        newPlayers.Add(newPlayer);
                    }
                }

                //add only new nationalities in table nationality
                newNationalities = newNationalities.GroupBy(gb => gb.Id).Select(s => s.FirstOrDefault()).ToList();
                var inDbNationalities = dbContext.Nationalities.AsEnumerable().Where(w => newNationalities.Any(a => w.Id.Equals(a.Id))).ToList();
                newNationalities.RemoveAll(w => inDbNationalities.Any(a => w.Id.Equals(a.Id)));
                if (newNationalities != null && newNationalities.Count > 0) { await dbContext.Nationalities.AddRangeAsync(newNationalities); }

                //add only new countries in table country
                newCountries = newCountries.GroupBy(gb => gb.Id).Select(s => s.FirstOrDefault()).ToList();
                var inDbCountries = dbContext.Countries.AsEnumerable().Where(w => newCountries.Any(a => w.Id.Equals(a.Id))).ToList();
                newCountries.RemoveAll(w => inDbCountries.Any(a => w.Id.Equals(a.Id)));
                if (newCountries != null && newCountries.Count > 0) { await dbContext.Countries.AddRangeAsync(newCountries); }

                //take persons already in db
                var inDbPersons = dbContext.People.AsEnumerable().Where(w => newPersons.Any(a => w.Id.Equals(a.Id))).ToList();
                //select persons already in db from the newPersons list to create the updated list
                var upPersons = newPersons.Where(w => inDbPersons.Any(a => w.Id.Equals(a.Id))).ToList();
                //remove persons to update from the new persons
                newPersons.RemoveAll(w => inDbPersons.Any(a => w.Id.Equals(a.Id)));
                foreach (var item in inDbPersons)
                {
                    var person = upPersons.FirstOrDefault(w => w.Id.Equals(item.Id));
                    if(person!=null)
                    { 
                        item.OpId = person.OpId;
                        item.OcId = person.OcId;
                        item.FirstName = person.FirstName;
                        item.NormalizedFirstName = StringHelper.NormalizeString(person.FirstName);
                        item.LastName = person.LastName;
                        item.NormalizedLastName = StringHelper.NormalizeString(person.LastName);
                        item.MiddleName = person.MiddleName;
                        item.NormalizedMiddleName = StringHelper.NormalizeString(person.MiddleName);
                        item.ShortLastName = person.ShortLastName;
                        item.NormalizedShortLastName = StringHelper.NormalizeString(person.ShortLastName);
                        item.ShortFirstName = person.ShortFirstName;
                        item.NormalizedShortFirstName = StringHelper.NormalizeString(person.ShortFirstName);
                        item.KnownName = person.KnownName;
                        item.NormalizedKnownName = StringHelper.NormalizeString(person.KnownName);
                        item.MatchName = person.MatchName;
                        item.NormalizedMatchName = StringHelper.NormalizeString(person.MatchName);
                        item.NationalityId = person.NationalityId;
                        item.DateOfBirth = person.DateOfBirth;
                        item.DateOfDeath = person.DateOfDeath;
                        item.CountryOfBirthId = person.CountryOfBirthId;
                        item.Status = person.Status;
                        item.Height = person.Height;
                        item.Weight = person.Weight;
                        item.Foot = person.Foot;
                    }
                }
                if (newPersons != null && newPersons.Count > 0) { await dbContext.People.AddRangeAsync(newPersons); }
                if (inDbPersons != null && inDbPersons.Count > 0) { dbContext.People.UpdateRange(inDbPersons); }

                var inDbAllPlayers = await dbContext.Players.Where(w => w.TeamId.Equals(model)).ToListAsync();
                var deactivePlayers = inDbAllPlayers.Where(w => !newPlayers.Any(a => a.PersonId.Equals(w.PersonId)));
                deactivePlayers = deactivePlayers.Select(s => { s.Active = false; return s; }).ToList();
                if (deactivePlayers != null && deactivePlayers.Count() > 0) { dbContext.Players.UpdateRange(deactivePlayers); }

                //var inDbPlayers = dbContext.Players.AsEnumerable().Where(w => newPlayers.Any(a => w.TeamId.Equals(a.TeamId) && w.PersonId.Equals(a.PersonId))).ToList();
                var inDbPlayers = inDbAllPlayers.Where(w => newPlayers.Any(a => w.PersonId.Equals(a.PersonId))).ToList();
                var upPlayers = newPlayers.Where(w => inDbPlayers.Any(a => w.PersonId.Equals(a.PersonId))).ToList();
                foreach (var item in inDbPlayers)
                {
                    var tmp = upPlayers.FirstOrDefault(w => w.PersonId.Equals(item.PersonId) && w.TeamId.Equals(item.TeamId));
                    item.ShirtNumber = tmp.ShirtNumber;
                    item.Active = tmp.Active;
                    item.Position = tmp.Position;
                    item.Type = tmp.Type;
                }
                newPlayers.RemoveAll(w => upPlayers.Any(a => w.TeamId.Equals(a.TeamId) && w.PersonId.Equals(a.PersonId)));
                if (newPlayers != null && newPlayers.Count > 0) { await dbContext.Players.AddRangeAsync(newPlayers); }
                if (upPlayers != null && upPlayers.Count > 0) { dbContext.Players.UpdateRange(inDbPlayers); }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex; }
        }

        public async Task<OptaTeamSquad?> SquadByContestantAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            OptaTeamSquad? res = null;
            string ctst = $"&ctst={model}";
            try
            {
                res = await SquadByTournamentCalendarAndContestantAsync("", ctst, detailed, format, mode);
            }
            catch (Exception ex) { throw ex; }
            return res;
        }
        public async Task<OptaTeamSquad?> SquadByTournamentCalendarAndContestantAsync(string tmcl, string ctst, bool detailed = false, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            OptaTeamSquad? res = null;
            string isDetailed = IsDetailed(detailed);
            using (HttpClient client = new HttpClient())
            {
                string url = "";
                url = $"http://api.performfeeds.com/soccerdata/squads/{OptaService.OptaKey}?_rt={mode}&_fmt={format}{tmcl}{ctst}{isDetailed}";
                var httpres = await client.GetAsync(url);
                if (httpres.IsSuccessStatusCode)
                {
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await System.Text.Json.JsonSerializer.DeserializeAsync<OptaTeamSquad>(str, JsonOpts);
                }
                else
                {
                    string error = await httpres.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }
            }
            return res;
        }
    }
}
