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
