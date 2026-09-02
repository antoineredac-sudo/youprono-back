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

        // Un mini-championnat dure 5 matchs du PSG, toutes compétitions confondues.
        private const int SeasonLength = 5;

        // Durée d'affichage du vainqueur avant que le cycle suivant démarre.
        // Le point de départ est le coup d'envoi du 5e match (le back ne connaît pas
        // l'heure du coup de sifflet final), donc l'affichage dure en pratique une
        // vingtaine d'heures.
        private const int ChampionDisplayHours = 24;

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
            public double ExpertCoef { get; set; }
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
            public int CycleNumber { get; set; }
            public string? ChampionName { get; set; } = null;
            public List<GroupMemberRankingResult> Ranking { get; set; } = new();
        }

        // Un match est "joué" dès lors que son statut n'est plus "Fixture" (match à venir).
        // Comparaison insensible à la casse pour ne dépendre d'aucune convention d'écriture.
        private static bool IsPlayed(string? status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   !status.Trim().Equals("Fixture", StringComparison.OrdinalIgnoreCase);
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

            // Tous les matchs programmés depuis la création du groupe, dans l'ordre chronologique.
            var allMatches = await dbContext.Matches
                .Where(w => w.DateTime >= group.CreatedDate)
                .OrderBy(o => o.DateTime)
                .Select(s => new { s.Id, s.Status, s.DateTime })
                .ToListAsync();

            DateTime now = DateTime.UtcNow;
            int memberCount = group.Members.Count;

            // Les matchs sont découpés en cycles de 5. On avance au cycle suivant
            // seulement quand le cycle courant est complet (5 matchs joués) ET que
            // les 24h d'affichage du vainqueur sont écoulées.
            int cycleIndex = 0;
            while (true)
            {
                var block = allMatches.Skip(cycleIndex * SeasonLength).Take(SeasonLength).ToList();
                if (block.Count < SeasonLength) { break; }
                if (block.Any(a => !IsPlayed(a.Status))) { break; }
                if (now < block.Last().DateTime.AddHours(ChampionDisplayHours)) { break; }
                cycleIndex++;
            }

            var cycleMatches = allMatches.Skip(cycleIndex * SeasonLength).Take(SeasonLength).ToList();
            var cycleMatchIds = cycleMatches.Select(s => s.Id).ToList();
            var playedMatches = cycleMatches.Where(w => IsPlayed(w.Status)).ToList();

            bool isComplete = cycleMatches.Count >= SeasonLength &&
                              playedMatches.Count >= SeasonLength;

            var memberIds = group.Members.Select(m => m.UserId).ToList();

            // On récupère la note DU MATCH (ResultTotal), et non la moyenne de saison
            // (ResultFinalTotal), qui ne sert ici qu'à départager les ex aequo.
            var predictions = await dbContext.UserMatches
                .Where(w => memberIds.Contains(w.UserId) && cycleMatchIds.Contains(w.MatchId))
                .Select(s => new { s.UserId, s.MatchId, s.ResultTotal, s.ResultFinalTotal })
                .ToListAsync();

            var points = memberIds.ToDictionary(k => k, v => 0);
            var played = memberIds.ToDictionary(k => k, v => 0);

            // Barème positionnel calé sur la TAILLE DU GROUPE, pas sur le nombre de présents :
            // dans un groupe de 5, le vainqueur du soir marque 6 points et le dernier
            // participant 2, que trois joueurs aient joué ou cinq. L'absent marque 0.
            foreach (var m in playedMatches)
            {
                var noted = predictions
                    .Where(w => w.MatchId.Equals(m.Id) && w.ResultTotal.HasValue)
                    .OrderByDescending(o => o.ResultTotal.Value)
                    .ToList();

                int rank = 0;
                for (int i = 0; i < noted.Count; i++)
                {
                    // Deux notes identiques donnent le même rang, donc les mêmes points.
                    if (i > 0 && noted[i].ResultTotal.Value != noted[i - 1].ResultTotal.Value)
                    { rank = i; }

                    int score = memberCount + 1 - rank;
                    if (score < 2) { score = 2; }

                    points[noted[i].UserId] += score;
                    played[noted[i].UserId] += 1;
                }
            }

            var ranking = group.Members.Select(m => new GroupMemberRankingResult
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName ?? "?",
                TotalScore = points.ContainsKey(m.UserId) ? points[m.UserId] : 0,
                MatchesPlayed = played.ContainsKey(m.UserId) ? played[m.UserId] : 0,
                ExpertCoef = predictions
                    .Where(w => w.UserId.Equals(m.UserId) && w.ResultFinalTotal.HasValue)
                    .Select(s => s.ResultFinalTotal.Value)
                    .DefaultIfEmpty(0)
                    .Max()
            })
            .OrderByDescending(o => o.TotalScore)
            .ThenByDescending(o => o.ExpertCoef)
            .ThenBy(o => o.DisplayName)
            .ToList();

            for (int i = 0; i < ranking.Count; i++) { ranking[i].Rank = i + 1; }

            // Le vainqueur n'est proclamé qu'une fois les 5 matchs joués,
            // et seulement s'il a réellement marqué des points.
            string? championName = null;
            if (isComplete && ranking.Count > 0 && ranking[0].TotalScore > 0)
            { championName = ranking[0].DisplayName; }

            return new GroupDetailsResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                IsSeasonComplete = isComplete,
                MatchesCounted = cycleMatches.Count,
                MatchesPlayed = playedMatches.Count,
                SeasonLength = SeasonLength,
                CycleNumber = cycleIndex + 1,
                ChampionName = championName,
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
