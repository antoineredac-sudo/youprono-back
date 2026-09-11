using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        // ===== LE COURRIEL DE BIENVENUE =====
        // Envoye chaque soir a 20 h, heure de Paris, a tous ceux qui se sont
        // inscrits depuis le dernier envoi. Le serveur ne sait pas se reveiller
        // seul : c'est UptimeRobot qui appelle la route, et la colonne
        // WelcomeSentAt garantit qu'un inscrit ne recoit le message qu'une fois,
        // meme si la route est appelee dix fois.
        //
        // Deux versions du texte : celui qui est arrive seul, a qui on explique
        // comment defier ses amis, et celui qui est arrive par une invitation,
        // qui a deja son groupe. Les textes sont d'Antoine.

        public class WelcomeResult
        {
            public int Envoyes { get; set; }
            public int SansAdresse { get; set; }
            public List<string> Pseudos { get; set; } = new();
        }

        public async Task<WelcomeResult> WelcomeAsync()
        {
            var result = new WelcomeResult();

            // Tous ceux qui n'ont jamais recu le message. Un inscrit de la nuit
            // ou d'un jour ou la route n'a pas ete appelee n'est pas oublie.
            var nouveaux = await dbContext.Users
                .Where(w => w.WelcomeSentAt == null && w.Email != null && w.Email != "")
                .ToListAsync();

            if (nouveaux.Count == 0) { return result; }

            var ids = nouveaux.Select(s => s.Id).ToList();

            // Qui appartient deja a un groupe : celui-la est arrive par une invitation.
            var accompagnes = await dbContext.GroupMembers
                .Where(w => ids.Contains(w.UserId))
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var user in nouveaux)
            {
                bool dansUnGroupe = accompagnes.Contains(user.Id);
                await EnvoyerBienvenueAsync(user.Email!, user.DisplayName ?? "", dansUnGroupe);

                user.WelcomeSentAt = DateTime.UtcNow;
                result.Envoyes++;
                result.Pseudos.Add(user.DisplayName ?? "");
            }

            // Ceux qui n'ont pas d'adresse sont marques aussi, pour ne pas etre
            // repasses en revue chaque soir jusqu'a la fin des temps.
            var sansAdresse = await dbContext.Users
                .Where(w => w.WelcomeSentAt == null && (w.Email == null || w.Email == ""))
                .ToListAsync();
            foreach (var user in sansAdresse) { user.WelcomeSentAt = DateTime.UtcNow; }
            result.SansAdresse = sansAdresse.Count;

            await dbContext.SaveChangesAsync();
            return result;
        }

        private static async Task EnvoyerBienvenueAsync(string adresse, string pseudo, bool dansUnGroupe)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string nom = System.Net.WebUtility.HtmlEncode(pseudo);

            // Le troisieme paragraphe et la chute changent selon que le joueur
            // est arrive seul ou par une invitation.
            string fin = dansUnGroupe
                ? "<p style=\"font-size:16px;line-height:1.7;\">YouProno est un jeu qui se joue entre experts du PSG "
                  + "et surtout entre amis, et tu as bien fait de ne pas venir seul. Tout seul, tu as une note et une "
                  + "place au classement. &Agrave; cinq, tu as une revanche &agrave; prendre tous les trois jours.</p>"
                  + "<p style=\"font-size:16px;line-height:1.7;\">Ton groupe t'attend d&eacute;j&agrave;. Rendez-vous au "
                  + "prochain match, on verra qui sont les vrais experts du PSG parmi vous. Allez Paris</p>"
                : "<p style=\"font-size:16px;line-height:1.7;\">YouProno est un jeu qui se joue entre experts du PSG "
                  + "et surtout entre amis. Tout seul, tu as une note et une place au classement. &Agrave; cinq, tu as "
                  + "une revanche &agrave; prendre tous les trois jours.</p>"
                  + "<p style=\"font-size:16px;line-height:1.7;\">D&eacute;fie tes amis et invite-les sur "
                  + "WhatsApp, ta comp&eacute;tition de groupe se construira automatiquement.</p>"
                  + "<p style=\"text-align:center;margin:24px 0;\">"
                  + "<a href=\"https://youprono.fr/#groups\" style=\"background:#da1f3d;color:#ffffff;"
                  + "text-decoration:none;padding:14px 26px;border-radius:8px;font-weight:bold;"
                  + "display:inline-block;\">D&eacute;fie tes amis</a></p>"
                  + "<p style=\"font-size:16px;line-height:1.7;\">Allez Paris</p>";

            string corps =
                "<div style=\"font-family:Arial,sans-serif;background:#0b2265;padding:28px;color:#ffffff;\">"
              + "<div style=\"max-width:520px;margin:0 auto;background:#14306f;border:1px solid #26478e;"
              + "border-radius:12px;padding:26px;\">"
              + "<div style=\"color:#e8b923;font-size:22px;font-weight:bold;margin-bottom:18px;\">YouProno</div>"
              + "<p style=\"font-size:16px;line-height:1.7;\">Salut " + nom + ",</p>"
              + "<p style=\"font-size:16px;line-height:1.7;\">Tu viens de rejoindre YouProno, un terrain o&ugrave; le "
              + "match se joue avant qu'il ne commence. &Agrave; toi de deviner le onze de d&eacute;part d'Enrique, la "
              + "possession, les tirs, les fautes, les centres et le score. Deux heures avant le coup d'envoi les jeux "
              + "sont faits, et tes pr&eacute;dictions seront compar&eacute;es aux stats officielles quelques minutes "
              + "apr&egrave;s la fin du match.</p>"
              + fin
              + "<p style=\"font-size:16px;line-height:1.7;margin-top:22px;\">Antoine</p>"
              + "</div></div>";

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                subject = "Bienvenue sur YouProno",
                htmlContent = corps
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.Add("api-key", cle);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(JsonSerializer.Serialize(charge), Encoding.UTF8, "application/json");
                await http.SendAsync(req);
            }
            catch
            {
                // Un envoi qui echoue ne bloque pas les suivants.
            }
        }

        // ===== MOT DE PASSE OU PSEUDO OUBLIE =====
        // Le joueur donne son adresse. On lui renvoie son pseudo ET un lien de
        // reinitialisation valable une heure. La reponse est toujours la meme,
        // que l'adresse existe ou non : sans ca, le formulaire dirait qui est
        // inscrit.

        private static readonly HttpClient http = new HttpClient();

        public class ForgotModel
        {
            public string Email { get; set; } = null!;
        }

        public class ResetModel
        {
            public string Token { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        public async Task<bool> ForgotAsync(ForgotModel model)
        {
            string src = "UserService.ForgotAsync";
            if (model == null || StringHelper.IsNull(model.Email))
            { throw BaseException.InvalidModel(-1, src); }

            string adresse = model.Email.Trim().ToLower();

            var user = await dbContext.Users
                .FirstOrDefaultAsync(w => w.Email != null && w.Email.ToLower().Equals(adresse));

            // Adresse inconnue : on ne dit rien et on renvoie le meme succes.
            if (user == null) { return true; }

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpires = DateTime.UtcNow.AddHours(1);
            await dbContext.SaveChangesAsync();

            await EnvoyerCourrielAsync(user.Email!, user.DisplayName ?? "", user.ResetToken);
            return true;
        }

        public async Task<bool> ResetAsync(ResetModel model)
        {
            string src = "UserService.ResetAsync";
            if (model == null || StringHelper.IsNull(model.Token) || StringHelper.IsNull(model.Password))
            { throw BaseException.InvalidModel(-1, src); }

            if (model.Password.Length < 4) { throw BaseException.InvalidModel(-2, src); }

            var user = await dbContext.Users
                .FirstOrDefaultAsync(w => w.ResetToken != null && w.ResetToken.Equals(model.Token));

            if (user == null || !user.ResetTokenExpires.HasValue
                || user.ResetTokenExpires.Value < DateTime.UtcNow)
            { throw BaseException.NotFound(-3, src); }

            user.Password = PasswordHelper.HashPassword(model.Password);

            // Le jeton ne sert qu'une fois.
            user.ResetToken = null;
            user.ResetTokenExpires = null;
            await dbContext.SaveChangesAsync();
            return true;
        }

        // L'envoi passe par l'API de Brevo. La cle vit dans une variable
        // d'environnement Render : elle n'apparait jamais dans le code.
        // Sans cle, l'envoi est simplement ignore — le reste du jeu continue.
        private static async Task EnvoyerCourrielAsync(string adresse, string pseudo, string jeton)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string lien = "https://youprono.fr/#reset/" + jeton;

            string corps =
                "<div style=\"font-family:Arial,sans-serif;background:#0b2265;padding:28px;color:#ffffff;\">"
              + "<div style=\"max-width:520px;margin:0 auto;background:#14306f;border:1px solid #26478e;"
              + "border-radius:12px;padding:26px;\">"
              + "<div style=\"color:#e8b923;font-size:22px;font-weight:bold;\">YouProno</div>"
              + "<p style=\"font-size:16px;line-height:1.6;\">Ton pseudo est <b style=\"color:#e8b923;\">"
              + System.Net.WebUtility.HtmlEncode(pseudo) + "</b>.</p>"
              + "<p style=\"font-size:16px;line-height:1.6;\">Si tu as aussi oubli&eacute; ton mot de passe, "
              + "choisis-en un nouveau ici :</p>"
              + "<p style=\"text-align:center;margin:26px 0;\">"
              + "<a href=\"" + lien + "\" style=\"background:#da1f3d;color:#ffffff;text-decoration:none;"
              + "padding:14px 26px;border-radius:8px;font-weight:bold;display:inline-block;\">"
              + "Choisir un nouveau mot de passe</a></p>"
              + "<p style=\"font-size:13px;color:#9fb0d8;line-height:1.6;\">Ce lien est valable une heure. "
              + "Si tu n'as rien demand&eacute;, ignore ce message : ton compte n'a pas boug&eacute;.</p>"
              + "</div></div>";

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                subject = "Ton pseudo YouProno et ton lien de mot de passe",
                htmlContent = corps
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.Add("api-key", cle);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(JsonSerializer.Serialize(charge), Encoding.UTF8, "application/json");
                await http.SendAsync(req);
            }
            catch
            {
                // Un envoi qui echoue ne doit jamais faire echouer la demande :
                // le joueur verra le meme message et pourra reessayer.
            }
        }

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
