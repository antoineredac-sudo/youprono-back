using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class TeamService
    {
        private readonly AppDbContext dbContext;

        public TeamService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public class SquadPlayerInput
        {
            public string FirstName { get; set; } = null!;
            public string LastName { get; set; } = null!;
            public int ShirtNumber { get; set; }
            public string? Position { get; set; }
        }

        public class SetSquadInput
        {
            public List<SquadPlayerInput> Players { get; set; } = new();
        }

        // --- Peupler l'effectif permanent d'une équipe (remplace le flux Opta qui alimentait ceci) ---
        public async Task SetSquadAsync(string teamId, SetSquadInput input)
        {
            string src = "TeamService.SetSquadAsync";
            var team = await dbContext.Teams.FirstOrDefaultAsync(w => w.Id.Equals(teamId));
            if (team == null) { throw BaseException.NotFound(-1, src); }

            // On repart d'un effectif propre à chaque saisie, pour pouvoir corriger sans dupliquer
            var existingPlayers = await dbContext.Players.Where(w => w.TeamId == teamId).ToListAsync();
            dbContext.Players.RemoveRange(existingPlayers);

            foreach (var p in input.Players)
            {
                var person = await dbContext.People.FirstOrDefaultAsync(w =>
                    w.FirstName.Equals(p.FirstName) && w.LastName.Equals(p.LastName));

                if (person == null)
                {
                    person = new Person
                    {
                        Id = Guid.NewGuid().ToString(),
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        // Trim : un joueur sans prenom ne doit pas s'appeler « <espace>Nom ».
                        MatchName = $"{p.FirstName} {p.LastName}".Trim(),
                        Active = true
                    };
                    dbContext.People.Add(person);
                }

                var player = new Dbs.Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    PersonId = person.Id,
                    ShirtNumber = p.ShirtNumber,
                    Position = p.Position,
                    Active = true
                };
                dbContext.Players.Add(player);
            }

            await dbContext.SaveChangesAsync();
        }

        public async Task<PaginationModel<Team>> ByNameAsync(string model, int page = 1, int pageSize = 10)
        {
            string src = "TeamService.ByNameAsync";
            if(StringHelper.IsNull(model)) { throw BaseException.InvalidModel(-1, src); }

            model = StringHelper.NormalizeString(model);
            var lo = dbContext
                .Teams
                .Where(w => w.NormalizedName.Contains(model) ||
                w.NormalizedOfficialName.Contains(model) ||
                w.NormalizedShortName.Contains(model))
                .OrderBy(ob => ob.NormalizedName);

            PaginationModel<Team> res = await PaginationModel<Team>.CreatePageAsync(lo, page, pageSize);
            return res;
        }

        public async Task<BaseTeamResult> ByIdAsync(string model, bool showOldPlayers=false)
        {
            string src = "TeamService.ByNameAsync";
            if (StringHelper.IsNull(model)) { throw BaseException.InvalidModel(-1, src); }

            var inDb = await dbContext
                .Teams
                .Include(i=>i.Players)
                .ThenInclude(i=>i.Person)
                .FirstOrDefaultAsync(w=>w.Id.Equals(model));
            if (inDb == null) { throw BaseException.NotFound(-2, src); }

            IEnumerable<Player> players;
            if(inDb.Players==null)
            {
                players = inDb.Players;
            }
            else
            { 
                if(showOldPlayers)
                {
                    players = inDb.Players;
                }
                else
                {
                    players = inDb.Players.Where(w => w.Active.HasValue ? w.Active.Value : false);
                }
            }
            BaseTeamResult res = new BaseTeamResult();
            res.Id = inDb.Id;
            res.Name = inDb.Name;
            res.ShortName = inDb.ShortName;
            res.OfficialName = inDb.OfficialName;
            res.Code = inDb.Code;
            res.Type = inDb.Type;
            res.TeamType= inDb.TeamType;
            foreach(var item in players)
            {
                PlayerForMatchResult player = new PlayerForMatchResult()
                {
                    Id = item.Person.Id,
                    MatchName = item.Person.MatchName,
                    LastName = item.Person.LastName,
                    FirstName = item.Person.FirstName,
                    Position = item.Position,
                    ShirtNumber = item.ShirtNumber.HasValue?item.ShirtNumber.Value:0,
                    PositionSide = item.Position
                };
                res.Players.Add(player);
            }

            return res;
        }
    }
}
