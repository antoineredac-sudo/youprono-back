using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaMatchStatsService : OptaService
    {
        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/opta-sdapi-soccer-api-match-statistics.htm

        private readonly AppDbContext dbContext;


        public OptaMatchStatsService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public async Task<OptaMatchStats?> ByMatchAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            string isDetailed = IsDetailed(detailed);
            OptaMatchStats res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/matchstats/{OptaService.OptaKey}/{model}?_rt={mode}&_fmt={format}{isDetailed}";
                var httpres = await client.GetAsync(url);
                if (httpres.IsSuccessStatusCode)
                {
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await System.Text.Json.JsonSerializer.DeserializeAsync<OptaMatchStats>(str, JsonOpts);
                }
                else
                {
                    throw new Exception(httpres.ReasonPhrase);
                }
            }
            return res;
        }
        public async Task<OptaMatchStats?> ByMatchAsync(string model, DateTime date, string status, bool detailed = false, string format = "json", string mode = "b2b")
        {
            mode = Mode(mode);
            string isDetailed = IsDetailed(detailed);
            OptaMatchStats res = null;
            using (HttpClient client = new HttpClient())
            {
                string url = $"http://api.performfeeds.com/soccerdata/matchstats/{OptaService.OptaKey}/{model}?_rt={mode}&_fmt={format}&_dlt={date}&status={status}{isDetailed}";
                var httpres = await client.GetAsync(url);
                if (httpres.IsSuccessStatusCode)
                {
                    var str = await httpres.Content.ReadAsStreamAsync();
                    res = await System.Text.Json.JsonSerializer.DeserializeAsync<OptaMatchStats>(str, JsonOpts);
                }
                else
                {
                    throw new Exception(httpres.ReasonPhrase);
                }
            }
            return res;
        }

        public async Task UpdateDbByMatchAsync(string model, bool detailed = false, string format = "json", string mode = "b2b")
        {
            string src = "";
            var res = await ByMatchAsync(model, detailed, format, mode);
            if(res!=null && res.MatchInfo.Id.Equals(model) && !res.LiveData.MatchDetails.MatchStatus.ToUpper().Equals("FIXTURE"))
            {
                Dbs.Match workingObj;
                var inDb = await dbContext
                    .Matches
                    .Include(i => i.AwayTeam)
                    .Include(i => i.HomeTeam)
                    .FirstOrDefaultAsync(w => w.Id.Equals(model));
                if (inDb == null) { throw BaseException.NotFound(-1, src); }

                List<TeamMatch> teams = new List<TeamMatch>();
                List<PlayerModel> players = new List<PlayerModel>();
                teams.Add(inDb.AwayTeam);
                teams.Add(inDb.HomeTeam);
                if(res.MatchInfo!=null)
                {
                    var infos = res.MatchInfo;
                    if(infos.Venue!=null)
                    {
                        var venue = infos.Venue;
                        var inDbPlace = await dbContext
                            .Places
                            .FirstOrDefaultAsync(w => w.Id.Equals(venue.Id));
                        if (inDbPlace == null)
                        {
                            inDbPlace = new Place();
                            inDbPlace.Id = venue.Id;
                            inDbPlace.Name = venue.LongName;
                            inDbPlace.NormalizedName = StringHelper.NormalizeString(venue.LongName);
                            inDbPlace.ShortName = venue.ShortName;
                            inDbPlace.NormalizedShortName = StringHelper.NormalizeString(venue.ShortName);
                            await dbContext.Places.AddAsync(inDbPlace);
                        }
                        inDb.PlaceId = inDbPlace.Id;
                    }
                    inDb.CoverageLevel = infos.CoverageLevel;
                }
                //if(res.LiveData!=null && res.LiveData.MatchDetails!=null && res.LiveData.MatchDetails.MatchStatus.ToUpper().Equals("PLAYED"))
                if (res.LiveData != null && res.LiveData.MatchDetails != null)
                {
                    var matchDetails = res.LiveData.MatchDetails;
                    inDb.Winner = matchDetails.Winner;
                    inDb.Status = matchDetails.MatchStatus;
                    inDb.AwayTeam.Score = matchDetails.Scores.Total.Away;
                    inDb.HomeTeam.Score = matchDetails.Scores.Total.Home;
                    inDb.Goals = res.LiveData.Goal == null ? null : System.Text.Json.JsonSerializer.Serialize(res.LiveData.Goal);
                    inDb.Cards = res.LiveData.Card == null ? null : System.Text.Json.JsonSerializer.Serialize(res.LiveData.Card);
                    inDb.Substitute = res.LiveData.Substitute == null ? null : System.Text.Json.JsonSerializer.Serialize(res.LiveData.Substitute);

                    if (res.LiveData.LineUp!=null)
                    {
                        List<PlayerForMatch> newPlayersForMatch = new List<PlayerForMatch>();
                        var lineup = res.LiveData.LineUp;
                        var team = lineup.FirstOrDefault(w => w.ContestantId.Equals(inDb.AwayTeam.TeamId));
                        if (team != null)
                        {
                            inDb.AwayTeam.Stats = (team.Stat == null ? null : System.Text.Json.JsonSerializer.Serialize(team.Stat));
                            inDb.AwayTeam.FormatType = team.FormationUsed;
                            players.AddRange(team.Player);
                            foreach (var player in team.Player)
                            {
                                PlayerForMatch newObj = new PlayerForMatch();
                                newObj.Id = Guid.NewGuid();
                                newObj.PersonId = player.PlayerId;
                                newObj.TeamMatchId = inDb.AwayTeamId;
                                newObj.ShirtNumber = player.ShirtNumber;
                                newObj.Position = player.Position;
                                newObj.PositionSide = player.PositionSide;
                                newObj.FormationPlace = player.FormationPlace;
                                newObj.Stats = (player.Stat == null ? null : System.Text.Json.JsonSerializer.Serialize(player.Stat));
                                newPlayersForMatch.Add(newObj);
                            }
                        }
                        team = lineup.FirstOrDefault(w => w.ContestantId.Equals(inDb.HomeTeam.TeamId));
                        if (team != null)
                        {
                            inDb.HomeTeam.Stats = (team.Stat == null ? null : System.Text.Json.JsonSerializer.Serialize(team.Stat));
                            inDb.HomeTeam.FormatType = team.FormationUsed;
                            players.AddRange(team.Player);
                            foreach (var player in team.Player)
                            {
                                PlayerForMatch newObj = new PlayerForMatch();
                                newObj.Id = Guid.NewGuid();
                                newObj.PersonId = player.PlayerId;
                                newObj.TeamMatchId = inDb.HomeTeamId;
                                newObj.ShirtNumber = player.ShirtNumber;
                                newObj.Position = player.Position;
                                newObj.PositionSide = player.PositionSide;
                                newObj.FormationPlace = player.FormationPlace;
                                newObj.Stats = (player.Stat == null ? null : System.Text.Json.JsonSerializer.Serialize(player.Stat));
                                newPlayersForMatch.Add(newObj);
                            }
                        }
                        var inDbPlayerForMatches = dbContext
                            .PlayerForMatches
                            .AsEnumerable()
                            .Where(w => newPlayersForMatch.Any(a => w.TeamMatchId.Equals(w.TeamMatchId) && w.PersonId.Equals(w.PersonId)))
                            .ToList();
                        newPlayersForMatch.RemoveAll(w => inDbPlayerForMatches.Any(a => w.TeamMatchId.Equals(a.TeamMatchId) && w.PersonId.Equals(a.PersonId)));
                        await dbContext.PlayerForMatches.AddRangeAsync(newPlayersForMatch);

                        var inDbPlayers = dbContext
                            .People
                            .AsEnumerable()
                            .Where(w => players.Any(a => w.Id.Equals(a.PlayerId)))
                            .ToList();
                        players.RemoveAll(w => inDbPlayers.Any(a => w.PlayerId.Equals(a.Id)));
                        if(players!=null && players.Count > 0)
                        {
                            List<Person> newPersons = new List<Person>();
                            foreach (var player in players)
                            { 
                                Person newPerson = new Person();
                                newPerson.Id = player.PlayerId;
                                newPerson.FirstName = player.FirstName;
                                newPerson.NormalizedFirstName = StringHelper.NormalizeString(player.FirstName);
                                newPerson.LastName = player.LastName;
                                newPerson.NormalizedLastName = StringHelper.NormalizeString(player.LastName);
                                newPerson.MatchName = player.MatchName;
                                newPerson.NormalizedMatchName = StringHelper.NormalizeString(player.MatchName);
                                newPerson.KnownName = player.KnownName;
                                newPerson.NormalizedKnownName = StringHelper.NormalizeString(player.KnownName);
                                newPersons.Add(newPerson);
                            }
                            await dbContext.People.AddRangeAsync(newPersons);
                        }
                    }
                }

                dbContext.TeamMatches.Update(inDb.AwayTeam);
                dbContext.TeamMatches.Update(inDb.HomeTeam);
                dbContext.Matches.Update(inDb);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<string>> UpdatePreviousDbAsync(string teamId)
        {
            DateTime now = DateTime.UtcNow;
            var matchesId = await dbContext
                .Matches
                .Where(w => w.DateTime < now && w.Status == null)
                .Select(s => s.Id)
                .ToListAsync();
            foreach (var item in matchesId)
            {
                await UpdateDbByMatchAsync(item, true);
            }
            return matchesId;
        }
    }
}
