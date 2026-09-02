using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class GroupService
    {
        private readonly AppDbContext dbContext;

        public GroupService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        private const int MinMembers = 2;
        private const int MaxMembers = 10;
        private const int SeasonLength = 10;

        public class CreateGroupInput
        {
            public string Name { get; set; } = null!;
        }

        public class GroupResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public DateTime CreatedDate { get; set; }
            public int MemberCount { get; set; }
        }

        public class GroupMemberRankingResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public double TotalScore { get; set; }
            public int MatchesPlayed { get; set; }
            public int Rank { get; set; }
        }

        public class GroupDetailsResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public DateTime CreatedDate { get; set; }
            public bool IsSeasonComplete { get; set; }
            public int MatchesCounted { get; set; }
            public int MatchesPlayed { get; set; }
            public int SeasonLength { get; set; }
            public List<GroupMemberRankingResult> Ranking { get; set; } = new();
        }

        // Un code court, lisible, sans caractères ambigus (pas de 0/O ni de 1/I)
        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        public async Task<GroupResult> CreateAsync(Guid creatorId, CreateGroupInput input)
        {
            string src = "GroupService.CreateAsync";
            if (StringHelper.IsNull(input.Name)) { throw BaseException.InvalidModel(-1, src); }

            var creator = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(creatorId));
            if (creator == null) { throw BaseException.NotFound(-2, src); }

            string inviteCode;
            do { inviteCode = GenerateInviteCode(); }
            while (await dbContext.Groups.AnyAsync(w => w.InviteCode.Equals(inviteCode)));

            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = input.Name,
                InviteCode = inviteCode,
                CreatorId = creatorId,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.Groups.Add(group);

            dbContext.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = creatorId,
                DateJoined = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();

            return new GroupResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                MemberCount = 1
            };
        }

        public async Task<GroupResult> JoinAsync(string inviteCode, Guid userId)
        {
            string src = "GroupService.JoinAsync";
            var group = await dbContext.Groups
                .Include(i => i.Members)
                .FirstOrDefaultAsync(w => w.InviteCode.Equals(inviteCode));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null) { throw BaseException.NotFound(-2, src); }

            if (group.Members.Any(m => m.UserId.Equals(userId)))
            { throw BaseException.AlreadyInDb(-3, src); }

            if (group.Members.Count >= MaxMembers)
            { throw BaseException.InvalidModel(-4, src); }

            dbContext.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = userId,
                DateJoined = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            return new GroupResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                MemberCount = group.Members.Count + 1
            };
        }

        public async Task<GroupDetailsResult> ByIdAsync(Guid groupId)
        {
            string src = "GroupService.ByIdAsync";
            var group = await dbContext.Groups
                .Include(i => i.Members)
                .ThenInclude(i => i.User)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            // Les 10 premiers matchs (toutes compétitions confondues) depuis la création du groupe
            // On récupère aussi le statut, pour savoir combien d'entre eux ont réellement été joués.
            var seasonMatches = await dbContext.Matches
                .Where(w => w.DateTime >= group.CreatedDate)
                .OrderBy(o => o.DateTime)
                .Take(SeasonLength)
                .Select(s => new { s.Id, s.Status })
                .ToListAsync();

            var seasonMatchIds = seasonMatches.Select(s => s.Id).ToList();

            // Un match est "joué" dès lors que son statut n'est plus "Fixture" (match à venir).
            // Comparaison insensible à la casse pour ne dépendre d'aucune convention d'écriture.
            var matchesPlayed = seasonMatches.Count(c =>
                !string.IsNullOrWhiteSpace(c.Status) &&
                !c.Status.Trim().Equals("Fixture", StringComparison.OrdinalIgnoreCase));

            var memberIds = group.Members.Select(m => m.UserId).ToList();

            var scores = await dbContext.UserMatches
                .Where(w => memberIds.Contains(w.UserId) && seasonMatchIds.Contains(w.MatchId))
                .ToListAsync();

            var ranking = group.Members.Select(m => new GroupMemberRankingResult
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName ?? "?",
                TotalScore = scores.Where(s => s.UserId.Equals(m.UserId)).Sum(s => s.ResultFinalTotal ?? 0),
                MatchesPlayed = scores.Count(s => s.UserId.Equals(m.UserId))
            })
            .OrderByDescending(o => o.TotalScore)
            .ToList();

            for (int i = 0; i < ranking.Count; i++) { ranking[i].Rank = i + 1; }

            return new GroupDetailsResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                IsSeasonComplete = seasonMatchIds.Count >= SeasonLength,
                MatchesCounted = seasonMatchIds.Count,
                MatchesPlayed = matchesPlayed,
                SeasonLength = SeasonLength,
                Ranking = ranking
            };
        }

        public async Task<List<GroupResult>> ByUserIdAsync(Guid userId)
        {
            var memberships = await dbContext.GroupMembers
                .Where(w => w.UserId.Equals(userId))
                .Include(i => i.Group)
                .ThenInclude(i => i.Members)
                .ToListAsync();

            return memberships.Select(m => new GroupResult
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                InviteCode = m.Group.InviteCode,
                CreatedDate = m.Group.CreatedDate,
                MemberCount = m.Group.Members.Count
            }).ToList();
        }
    }
}
