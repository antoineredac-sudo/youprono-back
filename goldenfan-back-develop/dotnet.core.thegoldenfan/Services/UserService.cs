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

        // ===== L'ASSIDUITE =====
        // Ce calcul ne decide plus qui figure au classement — tout le monde y
        // figure depuis le 14 septembre 2026, la note de forfait ayant remplace le
        // seuil des deux tiers. Il ne sert plus qu'a produire la fraction affichee
        // par le site a cote de chaque joueur : « 3/4 », soit trois matchs joues
        // sur les quatre qu'il pouvait jouer.
        //
        // Ce qu'il pouvait jouer : les matchs notes dont la CLOTURE tombe apres son
        // inscription. Le match en cours, non encore note, ne compte pas.
        public sealed class EligibiliteResult
        {
            public int Joues { get; set; }
            public int Total { get; set; }
            public bool Classe { get; set; }
        }

        // Calcule en une fois pour tout le monde : a trois cents joueurs, une
        // requete par joueur serait ruineuse.
        public async Task<Dictionary<Guid, EligibiliteResult>> EligibiliteAllAsync(string teamId)
        {
            // Les matchs de l'equipe reellement notes, avec leur coup d'envoi.
            var matchsNotes = await dbContext.UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Include(i => i.Match)
                .Select(s => new { s.MatchId, s.Match.DateTime })
                .Distinct()
                .ToListAsync();

            // Chaque match ramene a l'heure de sa cloture, en heure universelle.
            var clotures = matchsNotes
                .GroupBy(g => g.MatchId)
                .Select(g => GroupService.ParisToUtc(
                    g.First().DateTime.AddHours(-GroupService.ClotureAvantHeures)))
                .ToList();

            // Ce que chacun a reellement joue.
            var joues = await dbContext.UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .GroupBy(g => g.UserId)
                .Select(g => new { UserId = g.Key, Nb = g.Count() })
                .ToListAsync();

            var inscriptions = await dbContext.Users
                .Select(s => new { s.Id, s.DateCreated })
                .ToListAsync();

            var res = new Dictionary<Guid, EligibiliteResult>();
            foreach (var u in inscriptions)
            {
                int total = clotures.Count(c => c >= u.DateCreated);
                int nb = joues.Where(w => w.UserId.Equals(u.Id)).Select(s => s.Nb).FirstOrDefault();

                res[u.Id] = new EligibiliteResult
                {
                    Joues = nb,
                    Total = total,
                    // Depuis le 14 septembre 2026, tout le monde est classe. La regle
                    // des deux tiers a ete remplacee par la note de forfait : un
                    // absent voit sa moyenne baisser, il n'a pas en plus a etre grise
                    // au classement. Une seule sanction, progressive, et une seule
                    // regle a expliquer.
                    // Joues et Total restent renseignes : le site les affiche sous
                    // forme de fraction, « 3/4 », qui dit l'assiduite sans punir.
                    Classe = true
                };
            }
            return res;
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
        // Un seul texte pour tout le monde, y compris ceux qui arrivent par une
        // invitation : eux aussi peuvent vouloir inviter leurs propres amis.
        // Le texte est d'Antoine.

        public class WelcomeResult
        {
            public int Envoyes { get; set; }
            public int SansAdresse { get; set; }
            public List<string> Pseudos { get; set; } = new();
        }

        // L'envoi immediat, declenche par l'inscription. Il est ATTENDU : lancer la
        // tache en arriere-plan reviendrait a travailler avec une connexion a la base
        // deja fermee, puisqu'elle vit le temps de la requete. Le joueur attend donc
        // une demi-seconde de plus, une seule fois dans sa vie, au moment ou il vient
        // de creer son compte.
        // Rien ne remonte a lui en cas d'echec : WelcomeSentAt reste vide et le
        // rattrapage appele par UptimeRobot reprendra ce joueur au passage suivant,
        // puis au suivant, jusqu'a ce que ca passe. Le try/catch est large a dessein
        // — une exception ici ne doit jamais empecher une inscription d'aboutir.
        private async Task BienvenueImmediateAsync(Guid userId, string pseudo, string adresse)
        {
            try
            {
                // La tentative est comptee AVANT l'envoi : si Brevo leve une
                // exception, le compteur a quand meme avance et les rattrapages
                // savent ou ils en sont.
                var enBase = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
                if (enBase == null) { return; }
                enBase.WelcomeTries++;
                await dbContext.SaveChangesAsync();

                await EnvoyerBienvenueAsync(adresse, pseudo, userId);

                // La marque n'est posee qu'une fois l'envoi parti. Si Brevo a
                // refuse, elle reste vide et le rattrapage reprendra ce joueur.
                if (enBase.WelcomeSentAt == null)
                {
                    enBase.WelcomeSentAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch { /* le rattrapage prendra le relais */ }
        }

        // Le rattrapage de la bienvenue, appele en continu par UptimeRobot. Depuis
        // que le message part des l'inscription, cette route ne sert plus qu'a
        // ramasser les envois qui ont echoue — Brevo indisponible, coupure reseau,
        // quota du jour atteint. Elle n'a donc plus de creneau horaire : quelqu'un
        // qui s'inscrit a vingt heures ne doit pas attendre le lendemain matin.
        // Le rappel avant match et le courriel de resultat, eux, gardent leur
        // creneau de 8 h a 11 h, heure de Paris.
        // teamId n'est plus utilise depuis que le message ne parle plus du prochain
        // match. Il reste dans la signature parce qu'il vient de l'adresse appelee
        // par UptimeRobot : le retirer obligerait a changer cette adresse et le
        // reglage du service de surveillance.
        public async Task<WelcomeResult> WelcomeAsync(string teamId)
        {
            var result = new WelcomeResult();

            // Tous ceux qui n'ont jamais recu le message. Un inscrit de la nuit
            // ou d'un jour ou la route n'a pas ete appelee n'est pas oublie.
            var nouveaux = await dbContext.Users
                .Where(w => w.WelcomeSentAt == null && w.Email != null && w.Email != ""
                         && w.WelcomeTries < BIENVENUE_ESSAIS_MAX)
                .ToListAsync();

            if (System.Threading.Interlocked.CompareExchange(ref enCoursBienvenue, 1, 0) != 0)
            { return result; }

            try
            {
                foreach (var user in nouveaux)
                {
                    // Comptee avant l'envoi, pour la meme raison que plus haut.
                    user.WelcomeTries++;
                    await dbContext.SaveChangesAsync();

                    await EnvoyerBienvenueAsync(user.Email!, user.DisplayName ?? "", user.Id);

                    // Note apres chaque envoi : une interruption ne fait plus
                    // perdre que le message en cours.
                    user.WelcomeSentAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();

                    result.Envoyes++;
                    result.Pseudos.Add(user.DisplayName ?? "");

                    if (result.Envoyes < nouveaux.Count)
                    { await Task.Delay(ENVOI_ESPACEMENT_MS); }
                }
            }
            finally { System.Threading.Interlocked.Exchange(ref enCoursBienvenue, 0); }

            // Ceux qui n'ont pas d'adresse sont marques aussi, pour ne pas etre
            // repasses en revue chaque soir jusqu'a la fin des temps.
            var sansAdresse = await dbContext.Users
                .Where(w => w.WelcomeSentAt == null && (w.Email == null || w.Email == ""))
                .ToListAsync();
            foreach (var user in sansAdresse) { user.WelcomeSentAt = DateTime.UtcNow; }
            result.SansAdresse = sansAdresse.Count;

            if (result.Envoyes > 0 || result.SansAdresse > 0)
            { await dbContext.SaveChangesAsync(); }

            return result;
        }

        private static async Task EnvoyerBienvenueAsync(string adresse, string pseudo, Guid userId)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string nom = System.Net.WebUtility.HtmlEncode(pseudo);

            // Le meme lien que dans les rappels : un seul interrupteur, une seule
            // facon de s'en aller.
            string lienStop = "https://youprono.fr/#stop/" + userId.ToString();

            // Ce message ne dit qu'une chose : bienvenue, et voila l'esprit du jeu.
            // Ni affiche du prochain match, ni bouton : le joueur vient de s'inscrire,
            // il est deja dans le jeu. Le rappel du matin de match s'occupe de le
            // faire jouer, chacun son role.

            string contenu =
                "<p style=\"font-family:" + POLICE + ";font-size:17px;line-height:1.7;"
              + "color:" + C_OR + ";margin:0 0 16px;font-weight:bold;\">Bienvenue "
              + nom + ",</p>"

              + PARA + "Un supporter &eacute;tait persuad&eacute; qu'Enrique allait faire tourner. "
              + "Un autre voyait une large victoire parisienne. Et puis il y a celui qui avait "
              + "devin&eacute; que le match serait engag&eacute;. Tout le monde avait raison et "
              + "personne n'avait tort. D&eacute;sormais nous pouvons savoir qui avait vu juste.</p>"

              + PARA + "Sur YouProno, tu fais tes pr&eacute;dictions jusqu'&agrave; 2 heures avant "
              + "le coup d'envoi et elles seront compar&eacute;es aux stats officielles juste "
              + "apr&egrave;s la fin du match pour te donner une note.</p>"

              + PARA + "YouProno est un jeu gratuit et sans publicit&eacute; cr&eacute;&eacute; "
              + "par des supporters du PSG depuis de longues ann&eacute;es.</p>"

              + "<p style=\"font-family:" + POLICE + ";font-size:16px;line-height:1.7;"
              + "color:" + C_OR + ";margin:22px 0 0;font-weight:bold;\">Bons pronos et allez Paris</p>";

            string corps = CadreHtml("Bienvenue", contenu, lienStop);

            // La version texte du meme message. Un courriel qui n'existe qu'en
            // HTML est un signal de campagne : les vrais messages portent les deux.
            string texteBrut =
                "Bienvenue " + pseudo + ",\n\n"
              + "Un supporter etait persuade qu'Enrique allait faire tourner. Un autre voyait une "
              + "large victoire parisienne. Et puis il y a celui qui avait devine que le match "
              + "serait engage. Tout le monde avait raison et personne n'avait tort. Desormais "
              + "nous pouvons savoir qui avait vu juste.\n\n"
              + "Sur YouProno, tu fais tes predictions jusqu'a 2 heures avant le coup d'envoi et "
              + "elles seront comparees aux stats officielles juste apres la fin du match pour te "
              + "donner une note.\n\n"
              + "YouProno est un jeu gratuit et sans publicite cree par des supporters du PSG "
              + "depuis de longues annees.\n\n"
              + "Bons pronos et allez Paris\n\n"
              + "@lepsgdantoine\n\n"
              + "---\n"
              + "Ne plus recevoir de rappel avant match : " + lienStop;

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                replyTo = new { email = expediteur, name = "YouProno" },
                subject = "Bienvenue sur ton nouveau terrain de jeu",
                htmlContent = corps,
                textContent = texteBrut,
                // Exige par Google et Yahoo depuis 2024 sur tout envoi groupe.
                // Son absence suffit a faire classer le message en indesirables.
                headers = new Dictionary<string, string>
                {
                    { "List-Unsubscribe", "<" + lienStop + ">" },
                    { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" }
                }
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

        // ===== LE RAPPEL AVANT MATCH =====
        // Envoye le matin d'un jour de match, a 8 h, par un appel d'UptimeRobot.
        // Deux textes : celui qui n'a pas encore pronostique, et celui qui l'a
        // deja fait — a qui on ne dit pas « fais tes predictions ».
        //
        // Ne part qu'a ceux qui ont coche « previens-moi », et une seule fois par
        // match : LastReminderMatchId garde la trace.
        //
        // Les textes sont d'Antoine.

        // Le creneau d'envoi des courriels, heure de Paris. Il vaut pour les trois :
        // la bienvenue, le rappel du matin de match, et le lendemain de match.
        // 8 signifie « a partir de 8 h 00 », 9 signifie « jusqu'a 8 h 59 ».
        // Le nom officiel est vide pour une equipe creee depuis la console admin :
        // « Stade Brestois 29 » n'existe que dans Name. Sans ce repli, le rappel
        // annoncait « le coup d'envoi face a » suivi de rien, et le courriel de
        // resultat affichait « PSG - ». On essaie les trois champs connus, comme
        // le site et le record du groupe.
        private static string NomEquipe(TeamMatch? cote)
        {
            var t = cote?.Team;
            if (t == null) { return ""; }
            if (!string.IsNullOrWhiteSpace(t.OfficialName)) { return t.OfficialName!; }
            if (!string.IsNullOrWhiteSpace(t.Name)) { return t.Name; }
            if (!string.IsNullOrWhiteSpace(t.ShortName)) { return t.ShortName!; }
            return "";
        }


        // ===== L'HABILLAGE DES COURRIELS =====
        // Un courriel n'est pas une page web : Gmail retire les SVG, ignore les
        // mises en page modernes et ne charge aucune police web. Tout passe donc
        // par des tableaux, des styles ecrits sur chaque balise, et des images
        // hebergees sur le site. Les couleurs sont celles du jeu.
        private const string C_FOND     = "#0b2265";
        private const string C_CARTE    = "#14306f";
        private const string C_BANDE    = "#0a1c48";
        private const string C_BORD     = "#26478e";
        private const string C_OR       = "#e8b923";
        private const string C_ROUGE    = "#da1f3d";
        private const string C_VERT     = "#25d366";
        private const string C_VERTENCRE= "#04341a";
        private const string C_TEXTE    = "#ffffff";
        private const string C_GRIS     = "#c2cae8";
        private const string POLICE     = "Arial,Helvetica,sans-serif";

        private const string PARA =
            "<p style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.7;"
          + "color:#ffffff;margin:0 0 16px;\">";

        // Le bandeau du haut : le blason hexagonal, le nom en or, un filet dore.
        // Le logo est deja en ligne sur le site, aucune image a televerser.
        private static string EnteteHtml(string surtitre)
        {
            return
              "<tr><td style=\"background:" + C_BANDE + ";border-bottom:2px solid " + C_OR + ";"
            + "padding:20px 24px;text-align:center;\">"
            + "<img src=\"https://youprono.fr/icone-192.png\" width=\"44\" height=\"44\" alt=\"\" "
            + "style=\"display:block;margin:0 auto 8px;border:0;\">"
            + "<div style=\"font-family:" + POLICE + ";font-size:21px;font-weight:bold;"
            + "letter-spacing:2px;color:" + C_OR + ";\">YOUPRONO</div>"
            + (string.IsNullOrEmpty(surtitre) ? ""
               : "<div style=\"font-family:" + POLICE + ";font-size:11px;letter-spacing:2px;"
                 + "color:" + C_GRIS + ";margin-top:6px;text-transform:uppercase;\">" + surtitre + "</div>")
            + "</td></tr>";
        }

        // Le bloc de l'affiche, dessine comme la carte du prochain match dans le
        // jeu : fond plus sombre, filet dore, l'affiche en gros et l'horaire dessous.
        private static string BlocMatchHtml(string affiche, string sousTitre)
        {
            return
              "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
            + "style=\"margin:0 0 20px;\"><tr>"
            + "<td style=\"background:" + C_BANDE + ";border:1px solid " + C_OR + ";border-radius:10px;"
            + "padding:16px;text-align:center;\">"
            + "<div style=\"font-family:" + POLICE + ";font-size:19px;font-weight:bold;"
            + "letter-spacing:.5px;color:" + C_TEXTE + ";\">" + affiche + "</div>"
            + "<div style=\"font-family:" + POLICE + ";font-size:13px;color:" + C_OR + ";"
            + "margin-top:7px;\">" + sousTitre + "</div>"
            + "</td></tr></table>";
        }

        // Un bouton. Rouge par defaut, vert pour WhatsApp — la meme regle que dans
        // le jeu, ou le rouge est reserve aux actions du jeu.
        private static string BoutonHtml(string url, string libelle, bool vert, bool picto)
        {
            string fond = vert ? C_VERT : C_ROUGE;
            string encre = vert ? C_VERTENCRE : C_TEXTE;
            string image = picto
                ? "<img src=\"https://youprono.fr/whatsapp.png\" width=\"18\" height=\"18\" alt=\"\" "
                  + "style=\"vertical-align:-3px;margin-right:8px;border:0;\">"
                : "";

            return
              "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" "
            + "style=\"margin:0 auto 14px;\"><tr>"
            + "<td style=\"background:" + fond + ";border-radius:8px;\">"
            + "<a href=\"" + url + "\" style=\"display:inline-block;padding:15px 30px;"
            + "font-family:" + POLICE + ";font-size:16px;font-weight:bold;letter-spacing:.5px;"
            + "color:" + encre + ";text-decoration:none;\">" + image + libelle + "</a>"
            + "</td></tr></table>";
        }

        // Le pied : le filet, la signature, le lien de desabonnement.
        private static string PiedHtml(string lienStop)
        {
            return
              "<tr><td style=\"background:" + C_BANDE + ";border-top:1px solid " + C_BORD + ";"
            + "padding:18px 24px;text-align:center;\">"
            + "<div style=\"font-family:" + POLICE + ";font-size:14px;color:" + C_OR + ";"
            + "font-weight:bold;\">@lepsgdantoine</div>"
            + "<div style=\"font-family:" + POLICE + ";font-size:11px;color:#8b99d0;"
            + "margin-top:10px;line-height:1.6;\">"
            + "<a href=\"" + lienStop + "\" style=\"color:#8b99d0;\">Ne plus recevoir de rappel "
            + "avant match</a></div>"
            + "</td></tr>";
        }

        // Le cadre commun : fond marine, carte centree, entete et pied.
        private static string CadreHtml(string surtitre, string contenu, string lienStop)
        {
            return
              "<div style=\"background:" + C_FOND + ";padding:24px 12px;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
            + "style=\"max-width:520px;margin:0 auto;background:" + C_CARTE + ";"
            + "border:1px solid " + C_BORD + ";border-radius:14px;overflow:hidden;\">"
            + EnteteHtml(surtitre)
            + "<tr><td style=\"padding:26px 24px 20px;\">" + contenu + "</td></tr>"
            + PiedHtml(lienStop)
            + "</table></div>";
        }

        // Nombre maximal de tentatives d'envoi du courriel de bienvenue : l'envoi
        // immediat, plus deux rattrapages. Un incident passager — coupure reseau,
        // Brevo indisponible quelques minutes — est regle bien avant. Au-dela,
        // c'est une adresse que Brevo refuse et refusera toujours : insister ne
        // ferait qu'encombrer chaque passage.
        private const int BIENVENUE_ESSAIS_MAX = 3;

        // Les bornes du pseudo. Le site porte les memes valeurs sur son champ.
        public const int PSEUDO_MIN = 2;
        public const int PSEUDO_MAX = 16;

        private const int RAPPEL_HEURE_DEBUT = 8;

        // Le creneau va desormais jusqu'a 11 h. Les envois se font par passages
        // successifs : si un passage est interrompu, les suivants reprennent la
        // file la ou elle en est. Trois heures laissent la place a ces reprises,
        // quelle que soit la frequence a laquelle UptimeRobot appelle la route.
        private const int RAPPEL_HEURE_FIN = 11;

        // L'espacement entre deux envois. Trois secondes etaient une prudence
        // excessive : ce sont des messages transactionnels, un par destinataire,
        // au contenu personnalise — pas une campagne. A 250 ms, quatre cents
        // rappels partent en cent secondes au lieu de vingt minutes.
        private const int ENVOI_ESPACEMENT_MS = 250;

        // Le garde-fou contre le chevauchement. UptimeRobot appelle la route a
        // intervalle regulier sans savoir si le passage precedent est termine :
        // deux passages simultanes liraient la meme liste et enverraient deux
        // fois le meme message. Un seul passage a la fois, par courriel.
        private static int enCoursBienvenue = 0;
        private static int enCoursRappel = 0;
        private static int enCoursResultat = 0;

        // Les noms francais des jours et des mois. On ne se fie pas a la culture
        // du serveur : elle depend du conteneur, pas du jeu.
        private static readonly string[] JOURS_FR =
            { "dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi" };
        private static readonly string[] MOIS_FR =
            { "", "janvier", "février", "mars", "avril", "mai", "juin",
              "juillet", "août", "septembre", "octobre", "novembre", "décembre" };

        private static string DateEnClair(DateTime d)
        {
            return JOURS_FR[(int)d.DayOfWeek] + " " + d.Day + " " + MOIS_FR[d.Month]
                 + " à " + d.ToString("HH'h'mm");
        }


        public class ReminderResult
        {
            public string? MatchId { get; set; }
            public string? Affiche { get; set; }
            public int Envoyes { get; set; }
            public int DejaJoue { get; set; }
            public int Ignores { get; set; }
        }

        public async Task<ReminderResult> ReminderAsync(string teamId)
        {
            var result = new ReminderResult();

            // Le prochain match de l'equipe. On ne fait rien s'il n'a pas lieu
            // aujourd'hui, heure de Paris, ou si sa cloture est deja passee.
            DateTime maintenantParis = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, GroupService.ParisTimeZoneInfo);

            var match = await dbContext.Matches
                .Include(i => i.HomeTeam).ThenInclude(t => t.Team)
                .Include(i => i.AwayTeam).ThenInclude(t => t.Team)
                .Where(w => (w.HomeTeam.TeamId.Equals(teamId) || w.AwayTeam.TeamId.Equals(teamId))
                         && w.DateTime.Date == maintenantParis.Date)
                .OrderBy(o => o.DateTime)
                .FirstOrDefaultAsync();

            if (match == null) { return result; }

            // Le garde-fou horaire. UptimeRobot appelle la route en continu, sans
            // savoir quelle heure il est : c'est ici qu'on decide. Rien ne part en
            // dehors du creneau, donc aucun rappel a trois heures du matin.
            if (maintenantParis.Hour < RAPPEL_HEURE_DEBUT || maintenantParis.Hour >= RAPPEL_HEURE_FIN)
            { return result; }

            DateTime cloture = match.DateTime.AddHours(-GroupService.ClotureAvantHeures);
            if (maintenantParis >= cloture) { return result; }

            result.MatchId = match.Id;

            string domicile = NomEquipe(match.HomeTeam);
            string exterieur = NomEquipe(match.AwayTeam);

            bool psgRecoit = match.HomeTeam.TeamId.Equals(teamId);
            string adversaire = psgRecoit ? exterieur : domicile;

            // Dernier filet : plutot qu'une phrase amputee, on nomme l'adversaire
            // par defaut. Personne ne doit recevoir « face a  aura lieu ».
            if (string.IsNullOrWhiteSpace(adversaire)) { adversaire = "l'adversaire du jour"; }

            // L'affiche cite toujours l'equipe qui recoit en premier, comme partout
            // ailleurs dans le football. Le PSG s'abrege dans l'objet du message.
            result.Affiche = (psgRecoit ? "PSG" : domicile) + " - " + (psgRecoit ? exterieur : "PSG");

            string heureMatch = match.DateTime.ToString("HH'h'mm");
            string heureCloture = cloture.ToString("HH'h'mm");

            // Qui a deja pronostique sur ce match.
            var ontJoue = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(match.Id) && w.TeamId.Equals(teamId))
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();

            // La bienvenue part dans le meme creneau de 8 h a 9 h. Un inscrit de
            // la veille recevrait donc deux messages dans la meme minute, pour le
            // meme match. La bienvenue porte deja l'affiche, l'heure de cloture et
            // le bouton : elle suffit, le rappel se tait pour ceux-la.
            DateTime debutJournee = TimeZoneInfo.ConvertTimeToUtc(
                maintenantParis.Date, GroupService.ParisTimeZoneInfo);

            // Celui qui a deja pronostique ce match ne recoit rien : il n'a plus rien
            // a faire, et une boite qu'on encombre est une boite qu'on finit par
            // filtrer. Le rappel ne s'adresse qu'a ceux qui n'ont pas encore joue.
            var destinataires = await dbContext.Users
                .Where(w => w.EmailOptIn && w.Email != null && w.Email != ""
                         && (w.LastReminderMatchId == null || w.LastReminderMatchId != match.Id)
                         && (w.WelcomeSentAt == null || w.WelcomeSentAt < debutJournee)
                         )
                .ToListAsync();

            // Un seul passage a la fois.
            if (System.Threading.Interlocked.CompareExchange(ref enCoursRappel, 1, 0) != 0)
            { return result; }

            try
            {
                foreach (var user in destinataires)
                {
                    await EnvoyerRappelAsync(user.Email!, user.DisplayName ?? "", user.Id,
                        adversaire, result.Affiche, heureMatch, heureCloture,
                        ontJoue.Contains(user.Id));

                    // Enregistre immediatement, avant l'envoi suivant. C'est ce qui
                    // rend une interruption inoffensive : ce qui est parti est note,
                    // le passage suivant reprend la file sans jamais renvoyer deux
                    // fois le meme message.
                    user.LastReminderMatchId = match.Id;
                    await dbContext.SaveChangesAsync();

                    result.Envoyes++;

                    if (result.Envoyes < destinataires.Count)
                    { await Task.Delay(ENVOI_ESPACEMENT_MS); }
                }
            }
            finally { System.Threading.Interlocked.Exchange(ref enCoursRappel, 0); }

            return result;
        }

        // Deux messages selon qu'on a deja pronostique ou non. Celui qui n'a rien
        // pose recoit un rappel : il reste quelques heures. Celui qui a deja joue
        // recoit autre chose — le groupe et les compos probables sortent le matin
        // du match, c'est le moment d'affiner. Deux intentions differentes, donc
        // deux textes, et jamais le meme message a tout le monde.
        private static async Task EnvoyerRappelAsync(string adresse, string pseudo, Guid userId,
            string adversaire, string affiche, string heureMatch, string heureCloture,
            bool aDejaJoue)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string nom = System.Net.WebUtility.HtmlEncode(pseudo);
            string lienStop = "https://youprono.fr/#stop/" + userId.ToString();

            // « faire et modifier » pour celui qui n'a rien fait, « modifier »
            // seulement pour celui qui a deja pronostique.
            // Une seule version desormais : ce message ne part qu'a ceux qui n'ont
            // pas encore pronostique.
            string libelleBouton = aDejaJoue ? "Modifier mes pronos" : "Je fais mes pronos";

            // Celui qui a deja joue : on ne lui redemande pas de jouer, on lui donne
            // une raison de revenir. Le groupe et les compos probables sortent le
            // matin du match — c'est le seul moment ou un prono peut encore gagner
            // en precision.
            string phrase = aDejaJoue
                ? "Ce matin, tu vas conna&icirc;tre le groupe de joueurs retenus pour "
                  + System.Net.WebUtility.HtmlEncode(affiche) + " et les compos probables de la "
                  + "presse. C'est peut-&ecirc;tre le moment d'optimiser tes pronos."
                : "Aujourd'hui c'est jour de match pour les supporters du PSG.";

            string contenu =
                PARA + "Salut " + nom + ",</p>"

              + PARA + phrase + "</p>"

              + BlocMatchHtml(System.Net.WebUtility.HtmlEncode(affiche),
                    "Coup d'envoi &agrave; " + heureMatch + " &middot; pronos ferm&eacute;s &agrave; "
                  + heureCloture)

              + PARA + "Tu peux faire et modifier tes pr&eacute;dictions jusqu'&agrave; "
              + heureCloture + ".</p>"

              + BoutonHtml("https://youprono.fr", libelleBouton, false, false)

              + "<p style=\"font-family:" + POLICE + ";font-size:16px;line-height:1.7;"
              + "color:" + C_OR + ";margin:22px 0 0;font-weight:bold;\">Bon match et allez Paris</p>";

            string corps = CadreHtml("Jour de match", contenu, lienStop);

            string texteBrut =
                "Salut " + pseudo + ",\n\n"
              + (aDejaJoue
                  ? "Ce matin, tu vas connaitre le groupe de joueurs retenus pour " + affiche
                    + " et les compos probables de la presse. C'est peut-etre le moment "
                    + "d'optimiser tes pronos."
                  : "Aujourd'hui c'est jour de match pour les supporters du PSG. Le coup d'envoi "
                    + "face a " + adversaire + " aura lieu a " + heureMatch + ".")
              + " Tu peux faire et modifier tes predictions jusqu'a " + heureCloture + ".\n\n"
              + "https://youprono.fr\n\n"
              + "Bon match et allez Paris\n\n"
              + "@lepsgdantoine\n\n"
              + "---\n"
              + "Ne plus recevoir de rappel avant match : " + lienStop;

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                replyTo = new { email = expediteur, name = "YouProno" },
                subject = aDejaJoue
                    ? "Optimise tes pronos avant la clôture"
                    : "C'est jour de prono",
                htmlContent = corps,
                textContent = texteBrut,
                headers = new Dictionary<string, string>
                {
                    { "List-Unsubscribe", "<" + lienStop + ">" },
                    { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" }
                }
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.Add("api-key", cle);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(JsonSerializer.Serialize(charge), Encoding.UTF8, "application/json");
                await http.SendAsync(req);
            }
            catch { }
        }

        // Le lien du pied de page : un clic suffit, rien a saisir.
        public async Task<bool> UnsubscribeAsync(Guid userId)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null) { return false; }

            user.EmailOptIn = false;
            await dbContext.SaveChangesAsync();
            return true;
        }

        // ===== LE COURRIEL DU LENDEMAIN DE MATCH =====
        // Envoye entre 8 h et 9 h, le meme creneau que le rappel, au lendemain
        // d'un match note. Il ne part qu'a ceux qui ont pronostique : les autres
        // n'ont pas de note a decouvrir.
        //
        // On ne se cale pas sur la date du match mais sur le fait qu'il soit note
        // et qu'aucun courriel ne soit encore parti pour lui. Si la saisie des
        // statistiques a lieu tard, le message part le lendemain matin suivant
        // plutot que jamais.
        //
        // Le texte est d'Antoine.

        public class ResultMailResult
        {
            public string? MatchId { get; set; }
            public string? Affiche { get; set; }
            public int Envoyes { get; set; }
        }

        public async Task<ResultMailResult> ResultMailAsync(string teamId)
        {
            var result = new ResultMailResult();

            DateTime maintenantParis = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, GroupService.ParisTimeZoneInfo);

            if (maintenantParis.Hour < RAPPEL_HEURE_DEBUT || maintenantParis.Hour >= RAPPEL_HEURE_FIN)
            { return result; }

            // Un jour de match, ce courriel n'a rien a faire dans la boite : le
            // rappel du matin part deja, et annoncer le resultat de la rencontre
            // precedente au moment de pronostiquer la suivante n'a aucun sens
            // pour le joueur.
            bool jourDeMatch = await dbContext.Matches
                .AnyAsync(w => (w.HomeTeam.TeamId.Equals(teamId) || w.AwayTeam.TeamId.Equals(teamId))
                            && w.DateTime.Date == maintenantParis.Date);
            if (jourDeMatch) { return result; }

            // Le dernier match note de l'equipe.
            var dernier = await dbContext.UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Include(i => i.Match).ThenInclude(m => m.HomeTeam).ThenInclude(t => t.Team)
                .Include(i => i.Match).ThenInclude(m => m.AwayTeam).ThenInclude(t => t.Team)
                .OrderByDescending(o => o.Match.DateTime)
                .FirstOrDefaultAsync();

            if (dernier == null) { return result; }

            var match = dernier.Match;

            // La fenetre. Le message parle d'hier : passe deux jours, il ne parle
            // plus de rien. Sans cette borne, un match note lundi continuait de
            // partir le jeudi a quiconque ne l'avait pas encore recu.
            int joursDepuis = (maintenantParis.Date - match.DateTime.Date).Days;
            if (joursDepuis < 1 || joursDepuis > 2) { return result; }

            result.MatchId = match.Id;

            string domicile = NomEquipe(match.HomeTeam);
            string exterieur = NomEquipe(match.AwayTeam);
            bool psgRecoit = match.HomeTeam.TeamId.Equals(teamId);
            result.Affiche = (psgRecoit ? "PSG" : domicile) + " - " + (psgRecoit ? exterieur : "PSG");

            // Les notes de ce match, joueur par joueur.
            var notes = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(match.Id) && w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Select(s => new { s.UserId, Note = s.ResultTotal.Value })
                .ToListAsync();

            // Le verdict, celui-la meme qui s'affiche sur l'ecran des resultats. On
            // charge en une fois les notes par categorie de tout le monde, puis on
            // fabrique la phrase joueur par joueur, sans retoucher la base.
            var mediane = GroupService.Mediane(notes.Select(s => s.Note).ToList());
            int notesCount = notes.Count;

            var parCategorie = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(match.Id) && w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Select(s => new
                {
                    s.UserId,
                    Composition = s.ResultTeamCompositionFormula,
                    Score = s.ResultTeamScoreFormula,
                    Possession = s.ResultTeamPossessionFormula,
                    Shots = s.ResultTeamShotsFormula,
                    Fouls = s.ResultTeamFoulsFormula,
                    Crosses = s.ResultTeamCrossesFormula
                })
                .ToListAsync();

            var ids = notes.Select(s => s.UserId).ToList();

            // La note de forfait de ce match, et l'heure de sa cloture. Les absents
            // inscrits avant cette heure recoivent eux aussi un message : ils ont
            // ecope d'une note, ils doivent l'apprendre autrement qu'en decouvrant
            // leur coef expert en baisse.
            var noteForfait = NoteDeForfait(notes.Select(s => s.Note).ToList());
            DateTime clotureMatch = GroupService.ParisToUtc(
                match.DateTime.AddHours(-GroupService.ClotureAvantHeures));

            var destinataires = await dbContext.Users
                .Where(w => w.EmailOptIn && w.Email != null && w.Email != ""
                         && (w.LastResultMatchId == null || w.LastResultMatchId != match.Id)
                         && (ids.Contains(w.Id)
                             || (noteForfait != null && w.DateCreated <= clotureMatch)))
                .ToListAsync();

            if (System.Threading.Interlocked.CompareExchange(ref enCoursResultat, 1, 0) != 0)
            { return result; }

            try
            {
                foreach (var user in destinataires)
                {
                    bool aJoue = ids.Contains(user.Id);

                    // L'absent : un message different, sans note detaillee ni rang.
                    if (!aJoue)
                    {
                        await EnvoyerForfaitAsync(user.Email!, user.DisplayName ?? "", user.Id,
                            result.Affiche, noteForfait!.Value);

                        user.LastResultMatchId = match.Id;
                        await dbContext.SaveChangesAsync();
                        result.Envoyes++;

                        if (result.Envoyes < destinataires.Count)
                        { await Task.Delay(ENVOI_ESPACEMENT_MS); }
                        continue;
                    }

                    double note = notes.Where(w => w.UserId.Equals(user.Id)).Select(s => s.Note).FirstOrDefault();

                    // Sa meilleure et sa pire categorie, comme sur l'ecran des resultats.
                    string? meilleure = null, pire = null;
                    double meilleureNote = 0, pireNote = 0;
                    var sien = parCategorie.FirstOrDefault(f => f.UserId.Equals(user.Id));
                    if (sien != null)
                    {
                        var cats = new List<KeyValuePair<string, double?>>
                        {
                            new("composition", sien.Composition),
                            new("score", sien.Score),
                            new("possession", sien.Possession),
                            new("shots", sien.Shots),
                            new("fouls", sien.Fouls),
                            new("crosses", sien.Crosses)
                        };
                        var connues = cats.Where(c => c.Value.HasValue).ToList();
                        if (connues.Count > 0)
                        {
                            var haut = connues.OrderByDescending(o => o.Value!.Value).First();
                            var bas = connues.OrderBy(o => o.Value!.Value).First();
                            meilleure = haut.Key; meilleureNote = Math.Round(haut.Value!.Value, 3);
                            pire = bas.Key; pireNote = Math.Round(bas.Value!.Value, 3);
                        }
                    }

                    string verdict = GroupService.VerdictTexte(note, mediane, notesCount,
                        meilleure, meilleureNote, pire, pireNote, user.Id, match.Id);

                    await EnvoyerResultatAsync(user.Email!, user.DisplayName ?? "", user.Id,
                        result.Affiche, note, joursDepuis == 1, verdict);

                    user.LastResultMatchId = match.Id;
                    await dbContext.SaveChangesAsync();

                    result.Envoyes++;

                    if (result.Envoyes < destinataires.Count)
                    { await Task.Delay(ENVOI_ESPACEMENT_MS); }
                }
            }
            finally { System.Threading.Interlocked.Exchange(ref enCoursResultat, 0); }

            return result;
        }

        // Le message de l'absent. Pas de note en gros chiffres, pas de rang : il n'a
        // pas couru cette course. On lui dit ce qu'il a pris, pourquoi, et que le
        // prochain match est ouvert. Le bouton mene aux pronos, pas au classement —
        // ce qu'on veut de lui, c'est qu'il revienne jouer.
        private static async Task EnvoyerForfaitAsync(string adresse, string pseudo, Guid userId,
            string affiche, double forfait)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string nom = System.Net.WebUtility.HtmlEncode(pseudo);
            string aff = System.Net.WebUtility.HtmlEncode(affiche);
            string lienStop = "https://youprono.fr/#stop/" + userId.ToString();
            string noteTexte = forfait.ToString("0.000",
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

            string contenu =
                PARA + "Salut " + nom + ",</p>"

              + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
              + "style=\"margin:0 0 20px;\"><tr>"
              + "<td style=\"background:" + C_BANDE + ";border:1px solid " + C_OR + ";"
              + "border-radius:10px;padding:20px;text-align:center;\">"
              + "<div style=\"font-family:" + POLICE + ";font-size:11px;letter-spacing:2px;"
              + "color:" + C_GRIS + ";text-transform:uppercase;\">Ta note de forfait</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:42px;font-weight:bold;"
              + "color:" + C_OR + ";line-height:1.1;margin:6px 0 2px;\">" + noteTexte + "</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:12px;color:" + C_GRIS + ";\">sur 100</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:14px;color:" + C_TEXTE + ";"
              + "margin-top:12px;font-weight:bold;\">" + aff + "</div>"
              + "</td></tr></table>"

              + PARA + "Comme tu as &eacute;t&eacute; forfait sur le dernier match, tu obtiens "
              + "une note en dessous de la note moyenne obtenue par l'ensemble des participants. "
              + "Elle entre dans ton Coef Expert comme une vraie note.</p>"

              + PARA + "Les pronos pour le prochain match sont ouverts, tu vas pouvoir prendre "
              + "ta revanche.</p>"

              + BoutonHtml("https://youprono.fr", "Je fais mes pronos", false, false)

              + "<p style=\"font-family:" + POLICE + ";font-size:16px;line-height:1.7;"
              + "color:" + C_OR + ";margin:22px 0 0;font-weight:bold;\">Allez Paris</p>";

            string corps = CadreHtml("Ta note de forfait", contenu, lienStop);

            string texteBrut =
                "Salut " + pseudo + ",\n\n"
              + "Comme tu as ete forfait sur le dernier match (" + affiche + "), tu obtiens une "
              + "note en dessous de la note moyenne obtenue par l'ensemble des participants : " + noteTexte
              + ". Elle entre dans ton Coef Expert comme une vraie note.\n\n"
              + "Les pronos pour le prochain match sont ouverts, tu vas pouvoir prendre ta "
              + "revanche.\n\n"
              + "https://youprono.fr\n\n"
              + "Allez Paris\n\n"
              + "@lepsgdantoine\n\n"
              + "---\n"
              + "Ne plus recevoir de rappel avant match : " + lienStop;

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                replyTo = new { email = expediteur, name = "YouProno" },
                subject = "Prends ta revanche lors du prochain match",
                htmlContent = corps,
                textContent = texteBrut,
                headers = new Dictionary<string, string>
                {
                    { "List-Unsubscribe", "<" + lienStop + ">" },
                    { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" }
                }
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.Add("api-key", cle);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(JsonSerializer.Serialize(charge), Encoding.UTF8, "application/json");
                await http.SendAsync(req);
            }
            catch { /* un envoi rate ne doit pas arreter la file */ }
        }

        private static async Task EnvoyerResultatAsync(string adresse, string pseudo, Guid userId,
            string affiche, double note, bool hier, string verdict)
        {
            string cle = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(cle)) { return; }

            string expediteur = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "contact@youprono.fr";
            string nom = System.Net.WebUtility.HtmlEncode(pseudo);
            string aff = System.Net.WebUtility.HtmlEncode(affiche);
            string lienStop = "https://youprono.fr/#stop/" + userId.ToString();
            string noteTexte = note.ToString("0.000", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

            // « Hier » n'est vrai que le lendemain. Quand les statistiques ont ete
            // saisies tard et que le message part le surlendemain, on enleve le mot
            // plutot que de dater faux.
            string ouverture = hier ? "Hier tu as obtenu" : "Tu as obtenu";

            string contenu =
                PARA + "Salut " + nom + ",</p>"

              + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
              + "style=\"margin:0 0 20px;\"><tr>"
              + "<td style=\"background:" + C_BANDE + ";border:1px solid " + C_OR + ";"
              + "border-radius:10px;padding:20px;text-align:center;\">"
              + "<div style=\"font-family:" + POLICE + ";font-size:11px;letter-spacing:2px;"
              + "color:" + C_GRIS + ";text-transform:uppercase;\">Ta note sur ce match</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:42px;font-weight:bold;"
              + "color:" + C_OR + ";line-height:1.1;margin:6px 0 2px;\">" + noteTexte + "</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:12px;color:" + C_GRIS + ";\">"
              + "sur 100</div>"
              + "<div style=\"font-family:" + POLICE + ";font-size:14px;color:" + C_TEXTE + ";"
              + "margin-top:12px;font-weight:bold;\">" + aff + "</div>"
              + "</td></tr></table>"

              // Le verdict, dans un encadre a filet dore : c'est lui qu'on lit, pas
              // la note, qui est deja affichee en grand au-dessus.
              + (string.IsNullOrEmpty(verdict) ? ""
                 : "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
                   + "style=\"margin:0 0 20px;\"><tr>"
                   + "<td style=\"border-left:3px solid " + C_OR + ";padding:2px 0 2px 14px;\">"
                   + "<div style=\"font-family:" + POLICE + ";font-size:16px;line-height:1.6;"
                   + "color:" + C_TEXTE + ";font-weight:bold;\">"
                   + System.Net.WebUtility.HtmlEncode(verdict) + "</div>"
                   + "</td></tr></table>")

              + PARA + "D&eacute;couvre ta nouvelle position au classement et le compte &agrave; "
              + "rebours des pronos pour le prochain match.</p>"

              + BoutonHtml("https://youprono.fr/#ranking", "Mon classement", false, false)

              + "<p style=\"font-family:" + POLICE + ";font-size:16px;line-height:1.7;"
              + "color:" + C_OR + ";margin:22px 0 0;font-weight:bold;\">Allez Paris</p>";

            string corps = CadreHtml("Ta note", contenu, lienStop);

            string texteBrut =
                "Salut " + pseudo + ",\n\n"
              + ouverture + " la note de " + noteTexte + " sur le match " + affiche + ".\n\n"
              + (string.IsNullOrEmpty(verdict) ? "" : verdict + "\n\n")
              + "Decouvre ta nouvelle position au classement et le compte a rebours des pronos "
              + "pour le prochain match.\n\n"
              + "https://youprono.fr/#ranking\n\n"
              + "Allez Paris\n\n"
              + "@lepsgdantoine\n\n"
              + "---\n"
              + "Ne plus recevoir de rappel avant match : " + lienStop;

            var charge = new
            {
                sender = new { name = "YouProno", email = expediteur },
                to = new[] { new { email = adresse } },
                replyTo = new { email = expediteur, name = "YouProno" },
                subject = "Ta note sur " + affiche,
                htmlContent = corps,
                textContent = texteBrut,
                headers = new Dictionary<string, string>
                {
                    { "List-Unsubscribe", "<" + lienStop + ">" },
                    { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" }
                }
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.Add("api-key", cle);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(JsonSerializer.Serialize(charge), Encoding.UTF8, "application/json");
                await http.SendAsync(req);
            }
            catch { }
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

        // « Si tu ne joues pas, tu prends la meilleure note du dernier tiers. »
        //
        // Un joueur absent a un match note se voit attribuer la meilleure note du
        // tiers le plus faible des participants — c'est-a-dire la note au-dessus de
        // laquelle se trouvent les deux tiers du monde. Cette note entre dans sa
        // moyenne comme une vraie.
        //
        // Pourquoi ce reperage plutot qu'une mediane moins un nombre fixe : il
        // s'adapte a la dispersion du soir. Un match ou tout le monde se tient
        // coute peu au joueur absent ; un match ou les notes s'ecartent lui coute
        // cher, parce que c'est ce soir-la qu'il y avait quelque chose a gagner. Et
        // il ne contient aucun reglage arbitraire a defendre.
        //
        // Le forfait ne compte que pour les matchs dont la cloture est posterieure
        // a l'inscription du joueur : on ne reproche a personne les matchs joues
        // avant son arrivee.
        public const int FORFAIT_MIN_PARTICIPANTS = 8;

        // La note de forfait d'un match : la meilleure note du tiers le plus faible
        // des participants. Null quand il y a trop peu de participants pour que ce
        // reperage veuille dire quelque chose.
        // Publique et partagee : le coefficient expert s'en sert pour calculer, et
        // GroupService pour annoncer sa note a un absent. Une seule definition.
        public static double? NoteDeForfait(List<double> notes)
        {
            if (notes == null || notes.Count < FORFAIT_MIN_PARTICIPANTS) { return null; }

            var triees = notes.OrderBy(o => o).ToList();
            int index = (int)Math.Ceiling(triees.Count / 3.0) - 1;
            if (index < 0) { index = 0; }
            return triees[index];
        }

        public async Task<Dictionary<Guid, double>> ExpertCoefAllAsync(string teamId, string? excludeMatchId = null)
        {
            // Toutes les notes reelles, match par match.
            var notes = await dbContext
                .UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue
                         && (excludeMatchId == null || !w.MatchId.Equals(excludeMatchId)))
                .Include(i => i.Match)
                .Select(s => new { s.UserId, s.MatchId, Note = s.ResultTotal!.Value, s.Match.DateTime })
                .ToListAsync();

            // Par match : la note de forfait, et l'heure de cloture.
            var forfaitParMatch = new Dictionary<string, double>();
            var clotureParMatch = new Dictionary<string, DateTime>();

            foreach (var g in notes.GroupBy(gb => gb.MatchId))
            {
                clotureParMatch[g.Key] = GroupService.ParisToUtc(
                    g.First().DateTime.AddHours(-GroupService.ClotureAvantHeures));

                var forfait = NoteDeForfait(g.Select(s => s.Note).ToList());
                if (forfait == null) { continue; }
                forfaitParMatch[g.Key] = forfait.Value;
            }

            // Les dates d'inscription, pour ne compter que les matchs posterieurs.
            var inscriptions = await dbContext.Users
                .Select(s => new { s.Id, s.DateCreated })
                .ToListAsync();

            var notesParJoueur = notes
                .GroupBy(gb => gb.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var res = new Dictionary<Guid, double>();
            foreach (var u in inscriptions)
            {
                var valeurs = notesParJoueur.ContainsKey(u.Id)
                    ? notesParJoueur[u.Id].Select(s => s.Note).ToList()
                    : new List<double>();

                var joues = notesParJoueur.ContainsKey(u.Id)
                    ? notesParJoueur[u.Id].Select(s => s.MatchId).ToHashSet()
                    : new HashSet<string>();

                foreach (var kv in forfaitParMatch)
                {
                    if (joues.Contains(kv.Key)) { continue; }
                    if (!clotureParMatch.TryGetValue(kv.Key, out var cloture)) { continue; }
                    if (cloture < u.DateCreated) { continue; }
                    valeurs.Add(kv.Value);
                }

                if (valeurs.Count == 0) { continue; }
                res[u.Id] = Math.Round(valeurs.Average(), 4);
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


        // --- Suppression d'un compte (usage privé du fondateur) ---
        // Ajoutée le 15 septembre 2026 pour retirer les comptes de test.
        // Trois garde-fous, parce qu'une suppression est définitive :
        //   1. le code d'accès, le même que pour la liste des inscrits ;
        //   2. le pseudo doit correspondre à l'identifiant — une faute de copie
        //      sur l'id ne peut donc pas viser un autre joueur ;
        //   3. un compte qui a enregistré au moins un pronostic est refusé :
        //      ses notes font partie des classements des autres. Cette version
        //      ne supprime que des comptes vierges.
        // Un groupe créé par ce compte et qui compte d'autres membres bloque
        // aussi la suppression : le groupe perdrait son créateur en base.
        // Toutes les clés étrangères sont en ClientSetNull, sans cascade côté
        // base : chaque table liée est donc vidée explicitement, avant le compte.

        public sealed class DeleteAccountResult
        {
            public Guid Id { get; set; }
            public string? DisplayName { get; set; }
            public bool Supprime { get; set; }
            public List<string> GroupesQuittes { get; set; } = new();
            public List<string> GroupesSupprimes { get; set; } = new();
            public int AmisRetires { get; set; }
            public int AbonnementsRetires { get; set; }
            public int NotificationsRetirees { get; set; }
        }

        public async Task<DeleteAccountResult> DeleteAccountAsync(string accessCode, Guid userId, string displayName)
        {
            string src = "UserService.DeleteAccountAsync";
            if (StringHelper.IsNull(accessCode) ||
                !accessCode.Trim().Equals(AllUsersAccessCode, StringComparison.OrdinalIgnoreCase))
            { throw BaseException.InvalidModel(-1, src); }

            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null)
            { throw new BaseException(-2, src, "Aucun compte ne porte cet identifiant."); }

            string pseudoSaisi = (displayName ?? "").Trim().TrimStart('@');
            if (!string.Equals((user.DisplayName ?? "").Trim(), pseudoSaisi, StringComparison.OrdinalIgnoreCase))
            {
                throw new BaseException(-3, src,
                    "Le pseudo ne correspond pas à cet identifiant : ce compte s'appelle « "
                    + user.DisplayName + " ». Rien n'a été supprimé.");
            }

            int pronos = await dbContext.UserMatches.CountAsync(w => w.UserId.Equals(userId));
            if (pronos > 0)
            {
                throw new BaseException(-4, src,
                    user.DisplayName + " a enregistré " + pronos + " pronostic(s) : ce compte a joué, "
                    + "il n'est pas supprimé. Rien n'a été modifié.");
            }

            // Les groupes créés par ce compte.
            var groupesCrees = await dbContext.Groups
                .Include(i => i.Members)
                .Where(w => w.CreatorId.Equals(userId))
                .ToListAsync();

            var bloquants = groupesCrees
                .Where(g => g.Members.Any(m => !m.UserId.Equals(userId)))
                .Select(g => g.Name)
                .ToList();
            if (bloquants.Count > 0)
            {
                throw new BaseException(-5, src,
                    user.DisplayName + " a créé un groupe où il reste d'autres membres ("
                    + string.Join(", ", bloquants) + "). Rien n'a été supprimé.");
            }

            var res = new DeleteAccountResult { Id = user.Id, DisplayName = user.DisplayName };

            // Groupes rejoints, créés par d'autres : on retire seulement l'adhésion.
            var idsCrees = groupesCrees.Select(g => g.Id).ToHashSet();
            // Comme dans GroupService.LeaveAsync, un groupe dont il était le dernier
            // membre est supprimé : un groupe vide n'a plus de raison d'exister.
            var adhesions = await dbContext.GroupMembers
                .Include(i => i.Group)
                .ThenInclude(g => g.Members)
                .Where(w => w.UserId.Equals(userId))
                .ToListAsync();
            var groupesVides = new List<Group>();
            foreach (var a in adhesions.Where(w => !idsCrees.Contains(w.GroupId)))
            {
                if (a.Group.Members.Count <= 1)
                {
                    groupesVides.Add(a.Group);
                    res.GroupesSupprimes.Add(a.Group.Name);
                }
                else
                {
                    res.GroupesQuittes.Add(a.Group.Name);
                }
            }
            dbContext.GroupMembers.RemoveRange(adhesions);
            dbContext.Groups.RemoveRange(groupesVides);

            // Groupes créés par ce compte, où il était seul : ils disparaissent avec lui.
            foreach (var g in groupesCrees)
            {
                res.GroupesSupprimes.Add(g.Name);
            }
            dbContext.Groups.RemoveRange(groupesCrees);

            var amis = await dbContext.Friends
                .Where(w => w.User0Id.Equals(userId) || w.User1Id.Equals(userId))
                .ToListAsync();
            res.AmisRetires = amis.Count;
            dbContext.Friends.RemoveRange(amis);

            var abonnements = await dbContext.Followers
                .Where(w => w.UserId.Equals(userId) || w.FollowerId.Equals(userId))
                .ToListAsync();
            res.AbonnementsRetires = abonnements.Count;
            dbContext.Followers.RemoveRange(abonnements);

            var notifications = await dbContext.Subscriptions
                .Where(w => w.UserId.Equals(userId))
                .ToListAsync();
            res.NotificationsRetirees = notifications.Count;
            dbContext.Subscriptions.RemoveRange(notifications);

            // Deux enregistrements, dans une seule transaction : les liens d'abord,
            // le compte ensuite, pour qu'aucune contrainte de clé étrangère ne
            // bute sur l'ordre des suppressions. Si la seconde étape échoue, la
            // transaction annule aussi la première : tout ou rien.
            using (var transaction = await dbContext.Database.BeginTransactionAsync())
            {
                await dbContext.SaveChangesAsync();
                dbContext.Users.Remove(user);
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            res.Supprime = true;
            return res;
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

        // --- Une adresse, un seul compte (16 septembre 2026) ---
        // Deux adresses qui arrivent dans la même boîte sont ramenées à la même
        // forme : casse ignorée partout, et pour Gmail, points et « +suffixe »
        // ignorés, puisque Gmail les ignore lui-même (jean.dupont+2@gmail.com
        // et jeandupont@gmail.com sont une seule boîte). Les autres messageries
        // ne sont pas touchées : leurs règles d'alias varient, et une erreur
        // bloquerait un vrai joueur.
        // Les doublons créés avant cette règle restent en place : la contrainte
        // est vérifiée ici, dans le code, et non en base.
        private static string NormaliserAdresse(string email)
        {
            string e = (email ?? "").Trim().ToLowerInvariant();
            int at = e.LastIndexOf('@');
            if (at <= 0) { return e; }
            string local = e.Substring(0, at);
            string domaine = e.Substring(at + 1);
            if (domaine == "gmail.com" || domaine == "googlemail.com")
            {
                int plus = local.IndexOf('+');
                if (plus >= 0) { local = local.Substring(0, plus); }
                local = local.Replace(".", "");
                domaine = "gmail.com";
            }
            return local + "@" + domaine;
        }

        // Vrai si un autre compte que « saufUserId » porte déjà cette adresse.
        // La comparaison se fait après normalisation, donc en mémoire : on ne
        // charge que les adresses, ce qui reste léger même avec des milliers
        // d'inscrits.
        private async Task<bool> AdresseDejaPriseAsync(string email, Guid? saufUserId)
        {
            string cible = NormaliserAdresse(email);
            // Guid.Empty n'est jamais l'identifiant d'un compte : sans exclusion,
            // la condition laisse donc passer tout le monde.
            Guid exclu = saufUserId ?? Guid.Empty;
            var adresses = await dbContext.Users
                .Where(w => w.Email != null && w.Email != "" && w.Id != exclu)
                .Select(w => w.Email!)
                .ToListAsync();
            return adresses.Any(a => NormaliserAdresse(a) == cible);
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

            // Code -3 : « adresse déjà liée à un autre compte ». Le compte lui-même
            // est exclu de la recherche : réenregistrer sa propre adresse passe.
            if (await AdresseDejaPriseAsync(model.Email, userId))
            { throw BaseException.InvalidModel(-3, src); }

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

            // Entre 2 et 16 caracteres. Le site pose la meme borne sur le champ,
            // mais Swagger ne passe pas par le site : la verification doit etre
            // ici aussi. Un pseudo trop long deborde des classements et se
            // retrouve colle dans les courriels et les messages de partage.
            // Code -4 : « longueur de pseudo invalide ».
            string pseudo = model.DisplayName.Trim();
            if (pseudo.Length < PSEUDO_MIN || pseudo.Length > PSEUDO_MAX)
            { throw BaseException.InvalidModel(-4, src); }

            // Code -5 : « adresse déjà liée à un compte ». Vérifié avant le pseudo :
            // quelqu'un qui revient créer un compte a sans doute oublié le sien,
            // et c'est vers la récupération qu'il faut l'orienter, pas vers un
            // autre pseudo.
            if (await AdresseDejaPriseAsync(model.Email, null))
            { throw BaseException.InvalidModel(-5, src); }

            var normalized = StringHelper.NormalizeString(pseudo);
            var existing = await dbContext.Users.FirstOrDefaultAsync(w => w.NormalizedDisplayName!.Equals(normalized));
            if (existing != null) { throw BaseException.AlreadyInDb(-2, src); }

            var newObj = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = pseudo,
                NormalizedDisplayName = normalized,
                Password = PasswordHelper.HashPassword(model.Password),
                Email = model.Email.Trim(),
                EmailOptIn = model.EmailOptIn,
                DateCreated = DateTime.UtcNow
            };
            dbContext.Users.Add(newObj);

            await dbContext.SaveChangesAsync();

            // La bienvenue part tout de suite. Si l'envoi echoue — Brevo
            // indisponible, coupure reseau, quota du jour atteint — WelcomeSentAt
            // reste vide et le rattrapage appele par UptimeRobot reprendra ce
            // joueur au passage suivant, puis au suivant, jusqu'a ce que ca passe.
            await BienvenueImmediateAsync(newObj.Id, newObj.DisplayName ?? "", newObj.Email!);

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
