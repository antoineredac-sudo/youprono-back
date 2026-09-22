using dotnet.core.utils;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.thegoldenfan.Services;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils.Models;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Services.UserService;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]")]
    [ApiController]
    //[Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserService service;


        public UserController(UserService service)
        {
            this.service = service;
        }

        [HttpPost("Register")]
        public async Task<ResponseModel<string>> RegisterAsync(RegisterInputModel input)
        {
            ResponseModel<string> res = ResponseModel<string>.CreateDefault();
            try
            {
                var token = await service.RegisterAsync(input);
                res = new ResponseModel<string>(0, token);
            }
            catch (Exception ex)
            {
                res = ResponseModel<string>.Exception(ex);
            }
            return res;
        }

        // Le site l'interroge apres chaque connexion : si le joueur n'a pas
        // d'adresse, il lui presente l'ecran qui la demande.
        [HttpGet("Email/{userId}")]
        public async Task<ResponseModel<EmailStatusResult>> EmailStatusAsync(Guid userId)
        {
            ResponseModel<EmailStatusResult> res = ResponseModel<EmailStatusResult>.CreateDefault();
            try
            {
                var result = await service.EmailStatusAsync(userId);
                res = new ResponseModel<EmailStatusResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<EmailStatusResult>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Email/{userId}")]
        public async Task<ResponseModel<bool>> SetEmailAsync([FromBody] EmailInputModel input, Guid userId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                var result = await service.SetEmailAsync(userId, input);
                res = new ResponseModel<bool>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        // Les trois routes d'envoi acceptent aussi HEAD : UptimeRobot interroge
        // ainsi sur l'offre gratuite, et sans cela le serveur repondrait 405 sans
        // jamais executer le code. Une requete HEAD declenche le meme travail,
        // seul le corps de la reponse n'est pas renvoye.
        // Le courriel de bienvenue du soir. Appelee par UptimeRobot a 20 h,
        // heure de Paris. Le code dans l'adresse evite qu'un passant la declenche.
        [HttpGet("Welcome/{accessCode}/{teamId}")]
        [HttpHead("Welcome/{accessCode}/{teamId}")]
        public async Task<ResponseModel<UserService.WelcomeResult>> WelcomeAsync(string accessCode, string teamId)
        {
            ResponseModel<UserService.WelcomeResult> res = ResponseModel<UserService.WelcomeResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.WelcomeResult>.CreateDefault(); }

                var result = await service.WelcomeAsync(teamId);
                res = new ResponseModel<UserService.WelcomeResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.WelcomeResult>.Exception(ex);
            }
            return res;
        }

        // L'annonce « YouProno devient The Golden Fan ». Usage prive du fondateur,
        // depuis Swagger, en POST : aucune visite d'adresse ne peut la declencher.
        // Test : un seul destinataire, choisi par son pseudo, rien n'est note.
        [HttpPost("Announce/Test/{accessCode}/{displayName}")]
        public async Task<ResponseModel<UserService.AnnounceResult>> AnnounceTestAsync(string accessCode, string displayName)
        {
            ResponseModel<UserService.AnnounceResult> res = ResponseModel<UserService.AnnounceResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.AnnounceResult>.CreateDefault(); }

                var result = await service.AnnounceTestAsync(displayName);
                res = new ResponseModel<UserService.AnnounceResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.AnnounceResult>.Exception(ex);
            }
            return res;
        }

        // L'essai de delivrabilite de la future adresse. On envoie le meme courriel
        // d'annonce, mot pour mot, DEPUIS contact@thegoldenfan.fr, a l'adresse
        // jetable de mail-tester.com. MAIL_FROM n'est pas touche : tous les autres
        // courriels continuent de partir de contact@youprono.fr pendant l'essai.
        // Rien n'est inscrit en base, aucun inscrit n'est concerne.
        [HttpPost("Announce/MailTester/{accessCode}/{adresse}")]
        public async Task<ResponseModel<UserService.AnnounceResult>> AnnounceMailTesterAsync(string accessCode, string adresse)
        {
            ResponseModel<UserService.AnnounceResult> res = ResponseModel<UserService.AnnounceResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.AnnounceResult>.CreateDefault(); }

                var result = await service.AnnounceMailTesterAsync(adresse);
                res = new ResponseModel<UserService.AnnounceResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.AnnounceResult>.Exception(ex);
            }
            return res;
        }

        // Envoi a tous ceux qui ne l'ont pas encore recu. La relancer ne reprend
        // que les oublies et les echecs : personne ne le recoit deux fois.
        [HttpPost("Announce/All/{accessCode}")]
        public async Task<ResponseModel<UserService.AnnounceResult>> AnnounceAllAsync(string accessCode)
        {
            ResponseModel<UserService.AnnounceResult> res = ResponseModel<UserService.AnnounceResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.AnnounceResult>.CreateDefault(); }

                var result = await service.AnnounceAllAsync();
                res = new ResponseModel<UserService.AnnounceResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.AnnounceResult>.Exception(ex);
            }
            return res;
        }

        // Le rappel du matin d'un jour de match. Appelee par UptimeRobot a 8 h.
        // Ne fait rien si le PSG ne joue pas aujourd'hui.
        // L'annonce automatique. Appelee en GET par le service de surveillance,
        // toutes les cinq minutes : elle ne fait rien avant l'heure prevue, et
        // une fois l'envoi fait, les passages suivants ne trouvent plus personne.
        [HttpGet("Announce/Auto/{accessCode}")]
        [HttpHead("Announce/Auto/{accessCode}")]
        public async Task<ResponseModel<UserService.AnnounceResult>> AnnounceAutoAsync(string accessCode)
        {
            ResponseModel<UserService.AnnounceResult> res = ResponseModel<UserService.AnnounceResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.AnnounceResult>.CreateDefault(); }

                var result = await service.AnnounceAutoAsync();
                res = new ResponseModel<UserService.AnnounceResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.AnnounceResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("Reminder/{accessCode}/{teamId}")]
        [HttpHead("Reminder/{accessCode}/{teamId}")]
        public async Task<ResponseModel<UserService.ReminderResult>> ReminderAsync(string accessCode, string teamId)
        {
            ResponseModel<UserService.ReminderResult> res = ResponseModel<UserService.ReminderResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.ReminderResult>.CreateDefault(); }

                var result = await service.ReminderAsync(teamId);
                res = new ResponseModel<UserService.ReminderResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.ReminderResult>.Exception(ex);
            }
            return res;
        }

        // Le courriel du lendemain de match, meme creneau que le rappel.
        [HttpGet("ResultMail/{accessCode}/{teamId}")]
        [HttpHead("ResultMail/{accessCode}/{teamId}")]
        public async Task<ResponseModel<UserService.ResultMailResult>> ResultMailAsync(string accessCode, string teamId)
        {
            ResponseModel<UserService.ResultMailResult> res = ResponseModel<UserService.ResultMailResult>.CreateDefault();
            try
            {
                if (!string.Equals(accessCode, "psg2026", StringComparison.Ordinal))
                { return ResponseModel<UserService.ResultMailResult>.CreateDefault(); }

                var result = await service.ResultMailAsync(teamId);
                res = new ResponseModel<UserService.ResultMailResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.ResultMailResult>.Exception(ex);
            }
            return res;
        }

        // Le lien de desabonnement du pied de page des rappels.
        [HttpPost("Unsubscribe/{userId}")]
        public async Task<ResponseModel<bool>> UnsubscribeAsync(Guid userId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                var result = await service.UnsubscribeAsync(userId);
                res = new ResponseModel<bool>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        // Remet l'abonnement de tous les comptes qui portent une adresse.
        // Reservee a l'administration : elle demande le code d'acces.
        [HttpPost("Resubscribe/{accessCode}/{adresse}")]
        public async Task<ResponseModel<UserService.ResubscribeResult>> ResubscribeAsync(string accessCode, string adresse)
        {
            ResponseModel<UserService.ResubscribeResult> res = ResponseModel<UserService.ResubscribeResult>.CreateDefault();
            try
            {
                var result = await service.ResubscribeAsync(accessCode, adresse);
                res = new ResponseModel<UserService.ResubscribeResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserService.ResubscribeResult>.Exception(ex);
            }
            return res;
        }

        // Mot de passe ou pseudo oublie : on envoie le pseudo et un lien.
        [HttpPost("Forgot")]
        public async Task<ResponseModel<bool>> ForgotAsync([FromBody] UserService.ForgotModel model)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                var result = await service.ForgotAsync(model);
                res = new ResponseModel<bool>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        // Le nouveau mot de passe, avec le jeton recu par courriel.
        [HttpPost("Reset")]
        public async Task<ResponseModel<bool>> ResetAsync([FromBody] UserService.ResetModel model)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                var result = await service.ResetAsync(model);
                res = new ResponseModel<bool>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Login")]
        public async Task<ResponseModel<string>> LoginAsync(LoginInputModel input)
        {
            ResponseModel<string> res = ResponseModel<string>.CreateDefault();
            try
            {
                var token = await service.LoginAsync(input);
                res = new ResponseModel<string>(0, token);
            }
            catch (Exception ex)
            {
                res = ResponseModel<string>.Exception(ex);
            }
            return res;
        }

        // Liste de tous les inscrits, avec leur nombre. Usage privé du fondateur :
        // l'accès n'est possible qu'avec le bon code d'accès dans l'adresse.
        [HttpGet("All/{accessCode}")]
        public async Task<ResponseModel<AllUsersResult>> AllAsync(string accessCode)
        {
            ResponseModel<AllUsersResult> res = ResponseModel<AllUsersResult>.CreateDefault();
            try
            {
                var obj = await service.AllAsync(accessCode);
                res = new ResponseModel<AllUsersResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<AllUsersResult>.Exception(ex);
            }
            return res;
        }

        // Suppression d'un compte vierge. Usage privé du fondateur, depuis Swagger :
        // le code d'accès, l'identifiant et le pseudo doivent concorder, et un
        // compte qui a déjà pronostiqué est refusé. En POST et jamais en GET,
        // pour qu'aucune visite d'adresse (navigateur, robot, UptimeRobot) ne
        // puisse déclencher une suppression.
        [HttpPost("Delete/{accessCode}/{userId}/{displayName}")]
        public async Task<ResponseModel<DeleteAccountResult>> DeleteAccountAsync(string accessCode, Guid userId, string displayName)
        {
            ResponseModel<DeleteAccountResult> res = ResponseModel<DeleteAccountResult>.CreateDefault();
            try
            {
                var obj = await service.DeleteAccountAsync(accessCode, userId, displayName);
                res = new ResponseModel<DeleteAccountResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<DeleteAccountResult>.Exception(ex);
            }
            return res;
        }

        // Suppression d'un compte qui a deja joue. Le mot SUPPRIMER doit figurer
        // dans l'adresse : sans lui, rien n'est touche.
        [HttpPost("DeleteFull/{accessCode}/{userId}/{displayName}/{confirmation}")]
        public async Task<ResponseModel<DeleteAccountResult>> DeleteAccountForceAsync(string accessCode, Guid userId, string displayName, string confirmation)
        {
            ResponseModel<DeleteAccountResult> res = ResponseModel<DeleteAccountResult>.CreateDefault();
            try
            {
                var obj = await service.DeleteAccountForceAsync(accessCode, userId, displayName, confirmation);
                res = new ResponseModel<DeleteAccountResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<DeleteAccountResult>.Exception(ex);
            }
            return res;
        }

        // Changement de pseudo d'un compte. Usage privé du fondateur, depuis
        // Swagger, en POST comme la suppression. L'historique du compte est gardé.
        [HttpPost("Rename/{accessCode}/{ancienPseudo}/{nouveauPseudo}")]
        public async Task<ResponseModel<RenameResult>> RenameAsync(string accessCode, string ancienPseudo, string nouveauPseudo)
        {
            ResponseModel<RenameResult> res = ResponseModel<RenameResult>.CreateDefault();
            try
            {
                var obj = await service.RenameAsync(accessCode, ancienPseudo, nouveauPseudo);
                res = new ResponseModel<RenameResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<RenameResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("AttendanceBonus/{userId}/{teamId}")]
        public async Task<ResponseModel<double>> AttendanceBonusAsync(Guid userId, string teamId)
        {
            ResponseModel<double> res = ResponseModel<double>.CreateDefault();
            try
            {
                var obj = await service.AttendanceBonusAsync(userId, teamId);
                res = new ResponseModel<double>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<double>.Exception(ex);
            }
            return res;
        }

        [HttpPost("create/{userId}")]
        public async Task<ResponseModel<bool>> CreateAsync([FromBody]UserCreateInputModel model, Guid userId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                var obj = await service.CreateAsync(userId, model);
                res = new ResponseModel<bool>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpGet("Friends/{userId}/{teamId}")]
        public async Task<ResponseModel<PaginationModel<FriendResult>>> FriendsAsync(Guid userId, string teamId, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<FriendResult>> res = ResponseModel<PaginationModel<FriendResult>>.CreateDefault();
            try
            {
                var obj = await service.FriendsAsync(userId, teamId, page, limit);
                res = new ResponseModel<PaginationModel<FriendResult>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<FriendResult>>.Exception(ex);
            }
            return res;
        }
        [HttpPost("Friends/Add/{userId}/{friendId}")]
        public async Task<ResponseModel<Friend>> AddFriendsAsync(Guid userId, Guid friendId)
        {
            ResponseModel<Friend> res = ResponseModel<Friend>.CreateDefault();
            try
            {
                var obj = await service.AddFriendsAsync(userId, friendId);
                res = new ResponseModel<Friend>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<Friend>.Exception(ex);
            }
            return res;
        }
    }
}
