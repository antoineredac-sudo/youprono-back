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
using static dotnet.core.thegoldenfan.Services.UserStatsService;

namespace dotnet.core.thegoldenfan.Services
{
    public class UserService
    {
        private readonly AppDbContext dbContext;


        public UserService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public async Task<double> AttendanceBonusAsync(Guid userId, string teamId)
        {
            double totalPrediction = 0;
            double totalMatch = 0;

            var prediction = await dbContext
                .UserMatches
                .Include(i => i.Match)
                .Where(w => w.UserId.Equals(userId) &&
                    w.TeamId.Equals(teamId))
                .OrderBy(ob => ob.Match.DateTime)
                .ToListAsync();

            if (prediction != null && prediction.Count > 0)
            {
                prediction.RemoveAll(w => w.ResultTotal == null || w.ResultFinalTotal == null);
                totalPrediction = prediction.Count;
                if(totalPrediction>0)
                { 
                    DateTime now = DateTime.UtcNow;
                    var matches = await dbContext
                        .Matches
                        .Include(i => i.AwayTeam)
                        .Include(i => i.HomeTeam)
                        .Where(w => (w.AwayTeam.TeamId.Equals(teamId) || w.HomeTeam.TeamId.Equals(teamId)) && w.DateTime >= prediction.FirstOrDefault().Match.DateTime && w.DateTime < now)
                        .OrderBy(ob => ob.DateTime)
                        .ToListAsync();
                    matches.RemoveAll(w => w.Status == null);
                    totalMatch = matches.Count;
                }
            }
            double res = Math.Round((totalPrediction == 0 || totalMatch == 0 ? 1 : totalPrediction / totalMatch), 4);
            return res;
        }


        // --- Inscription et connexion par pseudo (remplace Twitter Connect) ---
        public sealed class RegisterInputModel
        {
            public string DisplayName { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        public async Task<string> RegisterAsync(RegisterInputModel model)
        {
            string src = "UserService.RegisterAsync";
            if (StringHelper.IsNull(model.DisplayName) || StringHelper.IsNull(model.Password))
            { throw BaseException.InvalidModel(-1, src); }

            var normalized = StringHelper.NormalizeString(model.DisplayName);
            var existing = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName!.Equals(normalized));
            if (existing != null) { throw BaseException.AlreadyInDb(-2, src); }

            var newObj = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = model.DisplayName,
                NormalizedDisplayName = normalized,
                Password = PasswordHelper.HashPassword(model.Password),
                DateCreated = DateTime.UtcNow
            };
            dbContext.Users.Add(newObj);

            var friend = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName!.Equals("LEPSGDANTOINE"));
            if (friend != null)
            {
                dbContext.Friends.Add(new Friend { Id = Guid.NewGuid(), User0Id = newObj.Id, User1Id = friend.Id });
            }

            await dbContext.SaveChangesAsync();
            return TokenHelper.GenerateToken(newObj.Id.ToString(), newObj.DisplayName, GetRole(normalized));
        }

        public sealed class LoginInputModel
        {
            public string DisplayName { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        public async Task<string> LoginAsync(LoginInputModel model)
        {
            string src = "UserService.LoginAsync";
            if (StringHelper.IsNull(model.DisplayName) || StringHelper.IsNull(model.Password))
            { throw BaseException.InvalidModel(-1, src); }

            var normalized = StringHelper.NormalizeString(model.DisplayName);
            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName!.Equals(normalized));
            if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.Password))
            { throw BaseException.NotFound(-2, src); }

            return TokenHelper.GenerateToken(user.Id.ToString(), user.DisplayName ?? string.Empty, GetRole(normalized));
        }

        // Provisoire : le fondateur du jeu est administrateur. À remplacer par un vrai système
        // de gestion des rôles quand plusieurs administrateurs seront nécessaires.
        private static string GetRole(string normalizedDisplayName)
        {
            return normalizedDisplayName.Equals("antoine") ? "administrators" : "user";
        }

        public sealed class UserCreateInputModel
        {
            public Guid Id { get; set; }
            public string? DisplayName { get; set; } = null;
        }
        public async Task<bool> CreateAsync(Guid userId, UserCreateInputModel model)
        {
            string src = "UserService.CreateAsync";
            bool res = false;
            
            if(model==null && !model.Id.Equals(userId))
            { throw BaseException.InvalidModel(-1, src); }

            var inDb = await dbContext
                .Users
                .FirstOrDefaultAsync(w => w.Id.Equals(userId));

            if(inDb==null)
            {
                User newObj = new User();
                newObj.Id = userId;
                newObj.DisplayName = model.DisplayName;
                newObj.NormalizedDisplayName = StringHelper.NormalizeString(model.DisplayName);
                dbContext.Users.Add(newObj);

                var friend = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName.Equals("LEPSGDANTOINE"));
                if (friend != null)
                {
                    Friend newFriend = new Friend();
                    newFriend.Id = Guid.NewGuid();
                    newFriend.User0Id = userId;
                    newFriend.User1Id = friend.Id;
                    dbContext.Friends.Add(newFriend);
                }
                await dbContext.SaveChangesAsync();
                res = true;
            }
            return res;
        }

        public sealed class FriendResult
        {
            public Guid Id { get; set; }
            public string? DisplayName { get; set; } = null;
            public int Rank { get; set; }
            public double ExpertCoef { get; set; }
        }
        public async Task<List<FriendResult>> RankingByResultFinalTotalAsync(string teamId)
        {
            List<FriendResult> res = new List<FriendResult>();
            var gb = await dbContext
                .UserMatches
                .Include(i => i.User)
                .Include(i => i.Match)
                .GroupBy(gb => gb.UserId)
                .ToListAsync();

            foreach (var item in gb)
            {
                var f = item.Where(w => w.ResultFinalTotal != null).OrderByDescending(ob => ob.Match.DateTime).FirstOrDefault();
                if (f != null)
                {
                    FriendResult obj = new FriendResult()
                    {
                        Id = item.Key,
                        DisplayName = item.First().User.DisplayName,
                        ExpertCoef = f.ResultFinalTotal.HasValue ? f.ResultFinalTotal.Value : 0
                    };
                    res.Add(obj);
                }
            }
            res = res.OrderByDescending(ob => ob.ExpertCoef).ToList();
            int i = 0;
            foreach (var item in res) { item.Rank = i + 1; i++; }
            return res;
        }
        public async Task<PaginationModel<FriendResult>> FriendsAsync(Guid userId, string teamId, int page = 1, int limit = 10)
        {
            PaginationModel<FriendResult> res = null;
            List<User> lo = new List<User>();
            var inDb = await dbContext
                .Friends
                .Include(i => i.User0)
                .ThenInclude(i => i.UserMatches)
                .Include(i => i.User1)
                .ThenInclude(i => i.UserMatches)
                .Where(w => w.User0Id.Equals(userId) || w.User1Id.Equals(userId))
                .ToListAsync();
            if(inDb!=null && inDb.Count > 0)
            {
                List<Guid> lids = new List<Guid>();
                lids.AddRange(inDb.Select(s => s.User0Id));
                lids.AddRange(inDb.Select(s => s.User1Id));
                lids = lids.ToHashSet().ToList();
                lids.Remove(userId);

                var ranking = await RankingByResultFinalTotalAsync(teamId);
                ranking = ranking.Where(w => lids.Any(a => w.Id.Equals(a))).ToList();
                res = PaginationModel<FriendResult>.CreatePage(ranking, page, limit);
            }

            return res;
        }

        //public async Task<PaginationModel<FriendResult>> FriendsAsync(Guid userId, string teamId, int page=1, int limit=10)
        //{
        //    PaginationModel<FriendResult> res = null;
        //    List<User> lo = new List<User>();
        //    var inDb = await dbContext
        //        .Friends
        //        .Include(i => i.User0)
        //        .ThenInclude(i => i.UserMatches)
        //        .Include(i => i.User1)
        //        .ThenInclude(i => i.UserMatches)
        //        .Where(w => w.User0Id.Equals(userId) || w.User1Id.Equals(userId))
        //        .ToListAsync();
        //    if (inDb != null && inDb.Count > 0)
        //    {
        //        lo.AddRange(inDb.Select(s => s.User0));
        //        lo.AddRange(inDb.Select(s => s.User1));
        //        lo = lo.ToHashSet().ToList();
        //        if (lo != null && lo.Count > 0)
        //        {
        //            lo.RemoveAll(s => s.Id.Equals(userId));
        //            PaginationModel<User> tmp = PaginationModel<User>.CreatePage(lo, page, limit);
        //            res = PaginationModel<FriendResult>.ConvertTo(tmp);
        //            res.Page = new List<FriendResult>();
        //            var p = tmp.Page.Select(s => new FriendResult() { Id = s.Id, DisplayName = s.DisplayName }).ToList();
        //            if (p != null && p.Count > 0)
        //            {
        //                res.Page.AddRange(p);
        //                var dic = await GlobalStatsAsync(teamId);
        //                var lguid = dic.Keys.ToList();
        //                foreach(var item in res.Page)
        //                {
        //                    var gstats = dic.FirstOrDefault(w => w.Key.Equals(item.Id));
        //                    item.Rank = lguid.IndexOf(item.Id);
        //                    item.ExpertCoef = gstats.Value;
        //                }
        //            }
        //        }
        //    }
        //    return res;
        //}
        //public async Task<Dictionary<Guid, double>> GlobalStatsAsync(string teamId)
        //{
        //    Dictionary<Guid, double> res = new Dictionary<Guid, double>();
        //    var matches = await dbContext
        //        .UserMatches
        //        .Where(w => w.TeamId.Equals(teamId))
        //        .OrderByDescending(ob => ob.ResultFinalTotal)
        //        .ToListAsync();
        //    if (matches != null)
        //    {
        //        var gbusers = matches.GroupBy(gb => gb.UserId);
        //        foreach (var item in gbusers)
        //        {
        //            var id = item.Key;
        //            var count = item.Count();
        //            var av = item.Sum(s => s.ResultFinalTotal.HasValue ? s.ResultFinalTotal.Value : 0) / count;
        //            res.Add(id, av);
        //        }
        //        res = res.OrderByDescending(ob => ob.Value).ToDictionary(o => o.Key, o => o.Value);
        //    }
        //    return res;
        //}

        public async Task<Friend> AddFriendsAsync(Guid userId, Guid friendId)
        {
            var inDb = await dbContext
                .Friends
                .FirstOrDefaultAsync(w => (w.User1Id.Equals(userId) && w.User0Id.Equals(friendId)) ||
                (w.User1Id.Equals(friendId) && w.User0Id.Equals(userId)));
            if(inDb==null)
            {
                inDb = new Friend()
                {
                    Id = Guid.NewGuid(),
                    User0Id = userId,
                    User1Id = friendId
                };
                await dbContext.Friends.AddAsync(inDb);
                await dbContext.SaveChangesAsync();
            }
            return inDb;
        }
    }
}
