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


        // Ce que le site affiche sous la forme « 6/7 » : les pronostics notés du
        // joueur, et les matchs du PSG disputés depuis son tout premier pronostic.
        public sealed class AttendanceResult
        {
            public int Played { get; set; }
            public int Total { get; set; }
        }

        public async Task<AttendanceResult> AttendanceAsync(Guid userId, string teamId)
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
            return new AttendanceResult { Played = (int)totalPrediction, Total = (int)totalMatch };
        }

        // Conservé parce que UserStatsService range encore cette valeur en base
        // (ResultBonus). Elle n'entre plus dans le coefficient expert.
        public async Task<double> AttendanceBonusAsync(Guid userId, string teamId)
        {
            var a = await AttendanceAsync(userId, teamId);
            return Math.Round((a.Played == 0 || a.Total == 0 ? 1 : (double)a.Played / a.Total), 4);
        }


        // --- Coefficient expert, calculé à la demande ---
        // La moyenne de toutes les notes de match du joueur, et rien d'autre : le
        // bonus d'assiduité a été retiré le 5 septembre. Il valait moins d'un point
        // sur une moyenne d'environ 75, et il rendait le coefficient invérifiable —
        // un joueur qui refaisait le calcul ne tombait pas sur le chiffre affiché.
        //
        // Pourquoi ne pas lire la valeur rangée en base (ResultFinalTotal) : elle n'est
        // écrite que lorsqu'un pronostic est noté. Un joueur qui saute un match ne
        // déclenche aucune écriture, donc son coefficient reste figé alors que son
        // assiduité vient de baisser. Recalculer à l'affichage supprime ce gel.
        public async Task<double> ExpertCoefAsync(Guid userId, string teamId)
        {
            var notes = await dbContext
                .UserMatches
                .Where(w => w.UserId.Equals(userId)
                         && w.TeamId.Equals(teamId)
                         && w.ResultTotal.HasValue)
                .Select(s => s.ResultTotal.Value)
                .ToListAsync();

            if (notes.Count == 0) { return 0; }

            return Math.Round(notes.Average(), 4);
        }

        // Même calcul pour tout le monde d'un coup, pour les classements.
        // Une seule requête suffit désormais : sans bonus, plus rien n'est individuel.
        // excludeMatchId permet de reconstituer le classement TEL QU'IL ETAIT avant
        // un match donne, pour dire au joueur combien de places il vient de gagner.
        // Le calcul reste ecrit ici et nulle part ailleurs.
        public async Task<Dictionary<Guid, double>> ExpertCoefAllAsync(string teamId, string? excludeMatchId = null)
        {
            var moyennes = await dbContext
                .UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue
                         && (excludeMatchId == null || !w.MatchId.Equals(excludeMatchId)))
                .GroupBy(gb => gb.UserId)
                .Select(g => new { UserId = g.Key, Moyenne = g.Average(a => a.ResultTotal.Value) })
                .ToListAsync();

            var res = new Dictionary<Guid, double>();
            foreach (var item in moyennes)
            {
                res[item.UserId] = Math.Round(item.Moyenne, 4);
            }
            return res;
        }


        // --- Liste des inscrits (usage privé du fondateur) ---
        // Protégée par un code d'accès simple, pour que la liste des pseudos
        // ne soit pas lisible par n'importe qui connaissant l'adresse.
        private const string AllUsersAccessCode = "psg2026";

        public sealed class UserSummaryResult
        {
            public Guid Id { get; set; }
            public string? DisplayName { get; set; } = null;
            public DateTime? DateCreated { get; set; }

            // Sous code d'acces uniquement : cette liste alimente le tableau de
            // bord local d'Antoine, pas le site des joueurs.
            public string? Email { get; set; }
            public bool EmailOptIn { get; set; }
        }

        public sealed class AllUsersResult
        {
            public int Count { get; set; }
            public List<UserSummaryResult> Users { get; set; } = new();
        }

        public async Task<AllUsersResult> AllAsync(string accessCode)
        {
            string src = "UserService.AllAsync";
            if (StringHelper.IsNull(accessCode) ||
                !accessCode.Trim().Equals(AllUsersAccessCode, StringComparison.OrdinalIgnoreCase))
            { throw BaseException.InvalidModel(-1, src); }

            var users = await dbContext
                .Users
                .Select(s => new UserSummaryResult
                {
                    Id = s.Id,
                    DisplayName = s.DisplayName,
                    DateCreated = s.DateCreated,
                    Email = s.Email,
                    EmailOptIn = s.EmailOptIn
                })
                .ToListAsync();

            users = users.OrderByDescending(o => o.DateCreated).ToList();

            return new AllUsersResult
            {
                Count = users.Count,
                Users = users
            };
        }


        // --- Inscription et connexion par pseudo (remplace Twitter Connect) ---
        public sealed class RegisterInputModel
        {
            public string DisplayName { get; set; } = null!;
            public string Password { get; set; } = null!;
            public string Email { get; set; } = null!;

            // Accord explicite pour le rappel avant match. Sans lui, l'adresse ne
            // sert qu'a recuperer son compte.
            public bool EmailOptIn { get; set; }
        }

        public sealed class EmailInputModel
        {
            public string Email { get; set; } = null!;
            public bool EmailOptIn { get; set; }
        }

        public sealed class EmailStatusResult
        {
            public bool HasEmail { get; set; }
            public string? Email { get; set; }
            public bool EmailOptIn { get; set; }
        }

        // Controle volontairement large : il attrape la faute de frappe evidente
        // sans rejeter une adresse valide mais inhabituelle. La verification reelle
        // se fera par le lien de confirmation, quand le service d'envoi existera.
        private static bool EmailPlausible(string? email)
        {
            if (StringHelper.IsNull(email)) { return false; }
            string e = email!.Trim();
            int at = e.IndexOf('@');
            if (at <= 0 || at != e.LastIndexOf('@')) { return false; }
            string domaine = e.Substring(at + 1);
            return domaine.Length >= 3 && domaine.Contains('.')
                   && !domaine.StartsWith(".") && !domaine.EndsWith(".")
                   && !e.Contains(' ');
        }

        // Ce que le site demande apres chaque connexion, pour savoir s'il doit
        // reclamer l'adresse a un joueur inscrit avant cette version.
        public async Task<EmailStatusResult> EmailStatusAsync(Guid userId)
        {
            string src = "UserService.EmailStatusAsync";
            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null) { throw BaseException.NotFound(-1, src); }

            // On ne renvoie pas l'adresse elle-meme : cette route n'est protegee
            // par rien, et le site n'a besoin que de savoir si elle existe.
            // La liste des adresses passe par AllAsync, sous code d'acces.
            return new EmailStatusResult
            {
                HasEmail = !StringHelper.IsNull(user.Email),
                EmailOptIn = user.EmailOptIn
            };
        }

        public async Task<bool> SetEmailAsync(Guid userId, EmailInputModel model)
        {
            string src = "UserService.SetEmailAsync";
            if (!EmailPlausible(model.Email)) { throw BaseException.InvalidModel(-1, src); }

            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null) { throw BaseException.NotFound(-2, src); }

            user.Email = model.Email.Trim();
            user.EmailOptIn = model.EmailOptIn;
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> RegisterAsync(RegisterInputModel model)
        {
            string src = "UserService.RegisterAsync";
            if (StringHelper.IsNull(model.DisplayName) || StringHelper.IsNull(model.Password))
            { throw BaseException.InvalidModel(-1, src); }

            // L'adresse est obligatoire depuis cette version : sans elle, un joueur
            // qui oublie son pseudo perd son compte sans aucun recours.
            if (!EmailPlausible(model.Email)) { throw BaseException.InvalidModel(-3, src); }

            var normalized = StringHelper.NormalizeString(model.DisplayName);
            var existing = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName!.Equals(normalized));
            if (existing != null) { throw BaseException.AlreadyInDb(-2, src); }

            var newObj = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = model.DisplayName,
                NormalizedDisplayName = normalized,
                Password = PasswordHelper.HashPassword(model.Password),
                Email = model.Email.Trim(),
                EmailOptIn = model.EmailOptIn,
                DateCreated = DateTime.UtcNow
            };
            dbContext.Users.Add(newObj);

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
            var coefs = await ExpertCoefAllAsync(teamId);
            var gb = await dbContext
                .UserMatches
                .Include(i => i.User)
                .Include(i => i.Match)
                .GroupBy(gb => gb.UserId)
                .ToListAsync();

            foreach (var item in gb)
            {
                if (!coefs.ContainsKey(item.Key)) { continue; }
                FriendResult obj = new FriendResult()
                {
                    Id = item.Key,
                    DisplayName = item.First().User.DisplayName,
                    ExpertCoef = coefs[item.Key]
                };
                res.Add(obj);
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
