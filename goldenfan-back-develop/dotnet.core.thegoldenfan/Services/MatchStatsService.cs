using dotnet.core.utils;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils.server.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    
    public class MatchStatsService
    {
        private readonly AppDbContext dbContext;


        public MatchStatsService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public PaginationModel<MatchResult> ByTeamId(string model, int page = 1, int limit = 10)
        {
            var inDb = dbContext
                .Matches
                .Include(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Where(w => w.AwayTeam.TeamId.Equals(model) || w.HomeTeam.TeamId.Equals(model))
                .ToList();
            List<MatchResult> lo = MatchResult.ListFromDb(inDb);
            lo = lo.OrderBy(ob => ob.DateTime).ToList();
            PaginationModel<MatchResult> res = PaginationModel<MatchResult>.CreatePage(lo, page, limit);
            return res;
        }

        public async Task<BaseMatchResult> ByIdAsync(string model)
        {
            var inDb = await dbContext
                .Matches
                .Include(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .FirstOrDefaultAsync(w=>w.Id.Equals(model));

            var res = MatchResult.FromDb(inDb);
            return res;
        }
    }
}
