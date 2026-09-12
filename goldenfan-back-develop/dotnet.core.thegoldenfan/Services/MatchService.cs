using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace dotnet.core.thegoldenfan.Services
{
    public class StatResult
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
    }
    public class PlayerForMatchResult
    {
        public string? Id { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MatchName { get; set; }
        public int ShirtNumber { get; set; }
        public string? FormationPlace { get; set; }
        public string? Position { get; set; }
        public string? PositionSide { get; set; }

        // Rang de gauche a droite dans la ligne. 0 = non renseigne.
        public int PositionOrder { get; set; }

        public List<StatResult> Stats { get; set; } = new List<StatResult>();
    }
    public class CalendarResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    public class CompetitionResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public CalendarResult Calendar { get; set; }
    }
    public class SimpleTeamResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
    public class BaseTeamResult:SimpleTeamResult
    {
        public string? ShortName { get; set; }
        public string? OfficialName { get; set; }
        public string? Code { get; set; }
        public string? Type { get; set; }
        public string? TeamType { get; set; }
        public string? FormatType { get; set; }
        public List<PlayerForMatchResult> Players { get; set; } = new List<PlayerForMatchResult>();
    }
    public class TeamResult: BaseTeamResult
    {
        public int Score { get; set; }
        public int Crosses { get; set; }
        public int Shots { get; set; }
        public int Fouls { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int Cards { get; set; }
        public double Possession { get; set; }
        public List<StatResult> Stats { get; set; } = new List<StatResult>();
        
    }
    public class PlaceResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
    // --- Saisie manuelle (remplace le flux Opta qui n'existe plus) ---
    public class CreateMatchInput
    {
        public DateTime DateTime { get; set; }
        public string HomeTeamName { get; set; } = null!;
        public string AwayTeamName { get; set; } = null!;
    }

    public class PlayerCompositionInput
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int ShirtNumber { get; set; }
        public string? Position { get; set; }
        public string? FormationPlace { get; set; }
    }

    public class SetCompositionInput
    {
        // true = équipe qui reçoit (HomeTeam), false = équipe qui se déplace (AwayTeam)
        public bool IsHomeTeam { get; set; }
        public List<PlayerCompositionInput> Players { get; set; } = new();
    }

    public class TeamResultInput
    {
        public int Score { get; set; }
        public double Possession { get; set; }
        public int Shots { get; set; }
        public int Fouls { get; set; }
        public int Crosses { get; set; }
    }

    public class SetResultsInput
    {
        public TeamResultInput HomeTeam { get; set; } = null!;
        public TeamResultInput AwayTeam { get; set; } = null!;
    }

    public class MatchResult: BaseMatchResult
    {
        public SimpleTeamResult AwayTeam { get; set; } = null!;
        public SimpleTeamResult HomeTeam { get; set; } = null!;
    }
    public class BaseMatchResult
    {
        public string Id { get; set; } = null!;
        public string? CoverageLevel { get; set; }
        public DateTime DateTime { get; set; }
        public string? Status { get; set; }
        public string? Winner { get; set; }
        public CompetitionResult Competition { get; set; } = null!;
        public PlaceResult? Place { get; set; }
        public TeamResult AwayTeam { get; set; } = null!;
        public TeamResult HomeTeam { get; set; } = null!;

        private static void FillStats(TeamResult model)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            var cross = model.Stats.FirstOrDefault(w => w.Type.Equals("TOTALCROSS"));
            if (cross != null) { model.Crosses = int.Parse(cross.Value, ci); }

            var shot = model.Stats.FirstOrDefault(w => w.Type.Equals("TOTALSCORINGATT"));
            if (shot != null) { model.Shots = int.Parse(shot.Value, ci); }

            var possession = model.Stats.FirstOrDefault(w => w.Type.Equals("POSSESSIONPERCENTAGE"));
            if (possession != null) { model.Possession = double.Parse(possession.Value, ci); }

            var foul = model.Stats.FirstOrDefault(w => w.Type.Equals("FKFOULLOST"));
            if (foul != null) { model.Fouls = int.Parse(foul.Value, ci); }

            var yellowCards = model.Stats.FirstOrDefault(w => w.Type.Equals("TOTALYELLOWCARD"));
            if (yellowCards != null) { model.YellowCards = int.Parse(yellowCards.Value); }

            model.Cards = model.YellowCards + model.RedCards;
        }
        private static PlayerForMatchResult CreatePlayer(PlayerForMatch player)
        {
            PlayerForMatchResult pfm = new PlayerForMatchResult();
            pfm.Id = player.PersonId;
            pfm.FirstName = player.Person.FirstName;
            pfm.LastName = player.Person.LastName;
            pfm.ShirtNumber = player.ShirtNumber;
            pfm.FormationPlace = player.FormationPlace;
            pfm.Position = player.Position;
            pfm.PositionSide = player.PositionSide;
            if (player.Stats != null)
            {
                pfm.Stats = System.Text.Json.JsonSerializer.Deserialize<List<StatResult>>(player.Stats);
                pfm.Stats.ForEach(fe => { fe.Type = fe.Type.ToUpper(); });
            }
            return pfm;
        }
        private static TeamResult CreateTeamMatch(TeamMatch model, bool detailed=false)
        {
            var newObj = new TeamResult()
            {
                Id = model.Team.Id,
                Name = model.Team.Name,
                ShortName = model.Team.ShortName,
                OfficialName = model.Team.OfficialName,
                Code = model.Team.Code,
                Type = model.Team.Type,
                TeamType = model.Team.TeamType,
                FormatType = model.FormatType,
                Score = model.Score
            };

            if (model.Stats != null)
            { newObj.Stats = System.Text.Json.JsonSerializer.Deserialize<List<StatResult>>(model.Stats); }
            newObj.Stats.ForEach(fe => { fe.Type = fe.Type.ToUpper(); });
            FillStats(newObj);
            if (!detailed) { newObj.Stats = null; }
            foreach (var player in model.PlayerForMatches)
            {
                var pfm = CreatePlayer(player);
                if (!detailed) { pfm.Stats = null; }
                newObj.Players.Add(pfm);
            }
            if(newObj.Players!=null)
            { newObj.Players = newObj.Players.OrderBy(ob => ob.Id).ToList(); }
            return newObj;
        }
        public static BaseMatchResult FromDb(Dbs.Match model, bool detailed=false)
        {
            BaseMatchResult newObj = new BaseMatchResult();
            newObj.Id = model.Id;
            newObj.CoverageLevel = model.CoverageLevel;
            newObj.DateTime = model.DateTime;
            newObj.Status = model.Status;
            newObj.Winner = model.Winner;
            if (model.MatchDate != null && model.MatchDate.Calendar != null && model.MatchDate.Calendar.Competition != null)
            {
                var calendar = model.MatchDate.Calendar;
                newObj.Competition = new CompetitionResult()
                {
                    Id = calendar.Competition.Id,
                    Name = calendar.Competition.Name,
                    Calendar = new CalendarResult()
                    {
                        Id = calendar.Id,
                        Name = calendar.Name,
                        StartDate = calendar.StartDate,
                        EndDate = calendar.EndDate
                    }
                };
            }
            if (model.Place != null)
            {
                newObj.Place = new PlaceResult()
                {
                    Id = model.Place.Id,
                    Name = model.Place.Name
                };
            }
            if (model.AwayTeam != null)
            {
                newObj.AwayTeam = CreateTeamMatch(model.AwayTeam, detailed);
            }
            if (model.HomeTeam != null)
            {
                newObj.HomeTeam = CreateTeamMatch(model.HomeTeam, detailed);
            }
            return newObj;
        }
        public static List<MatchResult> ListFromDb(List<Dbs.Match> model)
        {
            List<MatchResult> res = new List<MatchResult>();
            if(model!=null)
            {
                foreach(var match in model)
                {
                    var tmp = MatchResult.FromDb(match);
                    MatchResult newObj = new MatchResult()
                    {
                        Id = tmp.Id,
                        CoverageLevel = tmp.CoverageLevel,
                        DateTime = tmp.DateTime,
                        Status = tmp.Status,
                        Winner = tmp.Winner,
                        Competition = tmp.Competition,
                        Place = tmp.Place,
                        AwayTeam = new SimpleTeamResult()
                        {
                            Id = tmp.AwayTeam.Id,
                            Name = tmp.AwayTeam.Name
                        },
                        HomeTeam = new SimpleTeamResult()
                        {
                            Id = tmp.HomeTeam.Id,
                            Name = tmp.HomeTeam.Name
                        }
                    };
                    res.Add(newObj);
                }
            }
            return res;
        }
    }

    public class MatchService
    {
        // ===== LA MEMOIRE COURTE DU CALENDRIER =====
        // Chaque joueur redemande le calendrier du PSG toutes les trois a cinq
        // minutes, et la lecture charge TOUS les matchs de l'equipe avec leurs
        // equipes, lieux et competitions. C'est la meme reponse pour tout le
        // monde : on la garde une minute.
        //
        // Rien n'est range en base. La memoire est effacee des qu'un match
        // change — creation, suppression, saisie des resultats, notation — donc
        // elle ne peut jamais servir une photo perimee.
        private const int CALENDRIER_MEMOIRE_SECONDES = 60;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,
            (DateTime Heure, List<Match> Matchs)> memoireCalendrier = new();

        public static void OublierCalendrier()
        {
            memoireCalendrier.Clear();
        }

        // Tous les matchs de l'equipe, avec ce qu'il faut pour les afficher.
        private async Task<List<Match>> MatchsDeLEquipeAsync(string teamId)
        {
            if (memoireCalendrier.TryGetValue(teamId, out var entree)
                && (DateTime.UtcNow - entree.Heure).TotalSeconds < CALENDRIER_MEMOIRE_SECONDES)
            {
                return entree.Matchs;
            }

            var matchs = await dbContext
                .Matches
                .Include(i => i.Place)
                .Include(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Where(w => w.AwayTeam.TeamId.Equals(teamId) || w.HomeTeam.TeamId.Equals(teamId))
                .ToListAsync();

            memoireCalendrier[teamId] = (DateTime.UtcNow, matchs);
            return matchs;
        }

        private readonly AppDbContext dbContext;


        public MatchService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public async Task<PaginationModel<MatchResult>> ByTeamIdAsync(string model, bool OnlyNextMatches = false, int page = 1, int limit = 10)
        {
            var inDb = await MatchsDeLEquipeAsync(model);
            List<MatchResult> lo = MatchResult.ListFromDb(inDb);
            if (OnlyNextMatches)
            {
                DateTime now = DateTime.UtcNow;
                lo = lo.Where(w => w.DateTime > now).ToList();
            }
            lo = lo.OrderBy(ob => ob.DateTime).ToList();
            PaginationModel<MatchResult> res = PaginationModel<MatchResult>.CreatePage(lo, page, limit);
            return res;
        }

        // --- Créer un match à l'avance (le "calendrier") ---
        private async Task<Country> GetOrCreateFranceAsync()
        {
            var country = await dbContext.Countries.FirstOrDefaultAsync(w => w.Name.Equals("France"));
            if (country == null)
            {
                country = new Country { Id = Guid.NewGuid().ToString(), Name = "France", Code = "FRA" };
                dbContext.Countries.Add(country);
            }
            return country;
        }

        private async Task<Dbs.Team> FindOrCreateTeamAsync(string name, Country country)
        {
            var team = await dbContext.Teams.FirstOrDefaultAsync(w => w.Name.Equals(name));
            if (team == null)
            {
                team = new Dbs.Team
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    NormalizedName = StringHelper.NormalizeString(name),
                    ShortName = name,
                    NormalizedShortName = StringHelper.NormalizeString(name),
                    CountryId = country.Id,
                    Status = true,
                    LastUpdated = DateTime.UtcNow
                };
                dbContext.Teams.Add(team);
            }
            return team;
        }

        // --- Modifier un match existant (date, équipe qui reçoit, équipe qui se déplace) ---
        public async Task UpdateAsync(string matchId, CreateMatchInput input)
        {
            var match = await dbContext.Matches
                .Include(i => i.HomeTeam)
                .Include(i => i.AwayTeam)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-1, "Match"); }

            var country = await GetOrCreateFranceAsync();
            var homeTeam = await FindOrCreateTeamAsync(input.HomeTeamName, country);
            var awayTeam = await FindOrCreateTeamAsync(input.AwayTeamName, country);

            match.DateTime = input.DateTime;
            match.HomeTeam.TeamId = homeTeam.Id;
            match.AwayTeam.TeamId = awayTeam.Id;

            await dbContext.SaveChangesAsync();
            OublierCalendrier();
        }

        public async Task<string> CreateAsync(CreateMatchInput input)
        {
            var country = await GetOrCreateFranceAsync();
            var homeTeam = await FindOrCreateTeamAsync(input.HomeTeamName, country);
            var awayTeam = await FindOrCreateTeamAsync(input.AwayTeamName, country);

            var homeTeamMatch = new TeamMatch { Id = Guid.NewGuid(), TeamId = homeTeam.Id, Score = 0 };
            var awayTeamMatch = new TeamMatch { Id = Guid.NewGuid(), TeamId = awayTeam.Id, Score = 0 };
            dbContext.TeamMatches.Add(homeTeamMatch);
            dbContext.TeamMatches.Add(awayTeamMatch);

            var match = new Dbs.Match
            {
                Id = Guid.NewGuid().ToString(),
                DateTime = input.DateTime,
                HomeTeamId = homeTeamMatch.Id,
                AwayTeamId = awayTeamMatch.Id,
                Status = "Fixture"
            };
            dbContext.Matches.Add(match);

            await dbContext.SaveChangesAsync();
            OublierCalendrier();
            return match.Id;
        }

        // --- Entrer la composition officielle (~1h avant le coup d'envoi) ---
        public async Task SetCompositionAsync(string matchId, SetCompositionInput input)
        {
            var match = await dbContext.Matches
                .Include(i => i.HomeTeam)
                .Include(i => i.AwayTeam)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));

            if (match == null) { throw BaseException.NotFound(-1, "Match"); }

            var teamMatch = input.IsHomeTeam ? match.HomeTeam : match.AwayTeam;

            // On repart d'une liste propre à chaque saisie, pour pouvoir corriger une composition
            // sans dupliquer les joueurs déjà enregistrés.
            var existingPlayers = await dbContext.PlayerForMatches
                .Where(w => w.TeamMatchId == teamMatch.Id)
                .ToListAsync();
            dbContext.PlayerForMatches.RemoveRange(existingPlayers);

            foreach (var playerInput in input.Players)
            {
                var person = await dbContext.People.FirstOrDefaultAsync(w =>
                    w.FirstName.Equals(playerInput.FirstName) && w.LastName.Equals(playerInput.LastName));

                if (person == null)
                {
                    person = new Person
                    {
                        Id = Guid.NewGuid().ToString(),
                        FirstName = playerInput.FirstName,
                        LastName = playerInput.LastName,
                        MatchName = $"{playerInput.FirstName} {playerInput.LastName}",
                        Active = true
                    };
                    dbContext.People.Add(person);
                }

                var playerForMatch = new PlayerForMatch
                {
                    Id = Guid.NewGuid(),
                    PersonId = person.Id,
                    CreateDate = DateTime.UtcNow,
                    TeamMatchId = teamMatch.Id,
                    ShirtNumber = playerInput.ShirtNumber,
                    Position = playerInput.Position,
                    FormationPlace = playerInput.FormationPlace
                };
                dbContext.PlayerForMatches.Add(playerForMatch);
            }

            await dbContext.SaveChangesAsync();
            OublierCalendrier();
        }

        // --- Entrer les résultats officiels (après le match) ---
        public async Task SetResultsAsync(string matchId, SetResultsInput input)
        {
            var match = await dbContext.Matches
                .Include(i => i.HomeTeam)
                .Include(i => i.AwayTeam)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));

            if (match == null) { throw BaseException.NotFound(-1, "Match"); }

            void ApplyResult(TeamMatch teamMatch, TeamResultInput resultInput)
            {
                teamMatch.Score = resultInput.Score;
                var stats = new List<StatResult>
                {
                    new StatResult { Type = "POSSESSIONPERCENTAGE", Value = resultInput.Possession.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new StatResult { Type = "TOTALSCORINGATT", Value = resultInput.Shots.ToString() },
                    new StatResult { Type = "FKFOULLOST", Value = resultInput.Fouls.ToString() },
                    new StatResult { Type = "TOTALCROSS", Value = resultInput.Crosses.ToString() }
                };
                teamMatch.Stats = System.Text.Json.JsonSerializer.Serialize(stats);
            }

            ApplyResult(match.HomeTeam, input.HomeTeam);
            ApplyResult(match.AwayTeam, input.AwayTeam);

            match.Status = "Played";

            await dbContext.SaveChangesAsync();
            OublierCalendrier();
        }

        public async Task<BaseMatchResult> ByIdAsync(string model, bool detailed=false)
        {
            var inDb = await dbContext
                .Matches
                .Include(i => i.Place)
                .Include(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.AwayTeam)
                .ThenInclude(i => i.PlayerForMatches)
                .ThenInclude(i => i.Person)
                .Include(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam)
                .ThenInclude(i => i.PlayerForMatches)
                .ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.Id.Equals(model));
            var res = MatchResult.FromDb(inDb, detailed);
            return res;
        }

        public async Task<BaseMatchResult> PreviousAsync(string teamId)
        {
            BaseMatchResult res = null;
            DateTime now = DateTime.UtcNow;
            var lo = (await MatchsDeLEquipeAsync(teamId))
                .Where(w => w.DateTime < now)
                .OrderByDescending(ob => ob.DateTime)
                .ToList();
            if(lo!=null && lo.Count>0)
            {
                var obj = lo.FirstOrDefault();
                res = MatchResult.FromDb(obj);
            }
            return res;
        }


        public async Task DeleteAsync(string matchId)
        {
            string src = "MatchService.DeleteAsync";
            var inDb = await dbContext
                .Matches
                .Include(i => i.AwayTeam)
                .Include(i => i.HomeTeam)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            // Les pronostics des joueurs sur ce match doivent partir avant le match lui-même
            var userMatches = await dbContext.UserMatches.Where(w => w.MatchId.Equals(matchId)).ToListAsync();
            var userMatchIds = userMatches.Select(s => s.Id).ToList();
            var userPlayerForMatches = await dbContext.UserPlayerForMatches.Where(w => userMatchIds.Contains(w.UserMatchId)).ToListAsync();
            dbContext.UserPlayerForMatches.RemoveRange(userPlayerForMatches);
            dbContext.UserMatches.RemoveRange(userMatches);

            // La composition officielle (les joueurs alignés) doit partir avant les équipes du match
            var teamMatchIds = new[] { inDb.HomeTeam.Id, inDb.AwayTeam.Id };
            var playersForMatch = await dbContext.PlayerForMatches.Where(w => teamMatchIds.Contains(w.TeamMatchId)).ToListAsync();
            dbContext.PlayerForMatches.RemoveRange(playersForMatch);

            dbContext.Matches.Remove(inDb);
            dbContext.TeamMatches.Remove(inDb.AwayTeam);
            dbContext.TeamMatches.Remove(inDb.HomeTeam);
            await dbContext.SaveChangesAsync();
            OublierCalendrier();
        }
    }
}
