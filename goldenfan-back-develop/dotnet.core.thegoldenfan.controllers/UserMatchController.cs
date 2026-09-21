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

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]")]
    [ApiController]
    //[Authorize]
    public class UserMatchController : ControllerBase
    {
        private readonly UserMatchService service;


        public UserMatchController(UserMatchService service)
        {
            this.service = service;
        }

        // ===== LE DEFI CULTURE CLUB =====
        // Quatre adresses : l'etat du defi, l'ouverture d'une question (qui lance
        // le chrono du serveur), la reponse, et le classement.

        [HttpGet("Quiz/Etat/{userId}")]
        public async Task<ResponseModel<UserMatchService.QuizEtatResult>> QuizEtatAsync(Guid userId)
        {
            ResponseModel<UserMatchService.QuizEtatResult> res = ResponseModel<UserMatchService.QuizEtatResult>.CreateDefault();
            try
            {
                var obj = await service.QuizEtatAsync(userId);
                res = new ResponseModel<UserMatchService.QuizEtatResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.QuizEtatResult>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Quiz/Depart/{userId}/{questionId}")]
        public async Task<ResponseModel<UserMatchService.QuizDepartResult>> QuizDepartAsync(Guid userId, string questionId)
        {
            ResponseModel<UserMatchService.QuizDepartResult> res = ResponseModel<UserMatchService.QuizDepartResult>.CreateDefault();
            try
            {
                var obj = await service.QuizDepartAsync(userId, questionId);
                res = new ResponseModel<UserMatchService.QuizDepartResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.QuizDepartResult>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Quiz/Reponse/{userId}/{questionId}/{choix}")]
        public async Task<ResponseModel<UserMatchService.QuizReponseResult>> QuizReponseAsync(Guid userId, string questionId, int choix)
        {
            ResponseModel<UserMatchService.QuizReponseResult> res = ResponseModel<UserMatchService.QuizReponseResult>.CreateDefault();
            try
            {
                var obj = await service.QuizReponseAsync(userId, questionId, choix);
                res = new ResponseModel<UserMatchService.QuizReponseResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.QuizReponseResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("Quiz/Classement/{userId}")]
        public async Task<ResponseModel<UserMatchService.QuizClassementResult>> QuizClassementAsync(Guid userId, int limit = 20)
        {
            ResponseModel<UserMatchService.QuizClassementResult> res = ResponseModel<UserMatchService.QuizClassementResult>.CreateDefault();
            try
            {
                var obj = await service.QuizClassementAsync(userId, limit);
                res = new ResponseModel<UserMatchService.QuizClassementResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.QuizClassementResult>.Exception(ex);
            }
            return res;
        }

        // La page « La compo officielle est tombee » : le onze du coach, celui du
        // joueur, le decompte des choix et la note de composition, rarete comprise.
        [HttpGet("Compo/{teamId}/{matchId}/{userId}")]
        public async Task<ResponseModel<UserMatchService.CompoSoloResult>> CompoAsync(string teamId, string matchId, Guid userId)
        {
            ResponseModel<UserMatchService.CompoSoloResult> res = ResponseModel<UserMatchService.CompoSoloResult>.CreateDefault();
            try
            {
                var obj = await service.CompoAsync(teamId, matchId, userId);
                res = new ResponseModel<UserMatchService.CompoSoloResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.CompoSoloResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByUserMatchTeam/{userId}/{matchId}/{teamId}")]
        public async Task<ResponseModel<UserPredictionResult>> GetByUserMatchTeamAsync(Guid userId, string matchId, string teamId)
        {
            ResponseModel<UserPredictionResult> res = ResponseModel<UserPredictionResult>.CreateDefault();
            try
            {
                var obj = await service.GetByUserMatchTeamAsync(userId, matchId, teamId);
                res = new ResponseModel<UserPredictionResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserPredictionResult>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByUserTeam/{userId}/{teamId}")]
        public async Task<ResponseModel<PaginationModel<UserPredictionResult>>> GetByUserAsync(Guid userId, string teamId, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<UserPredictionResult>> res = ResponseModel<PaginationModel<UserPredictionResult>>.CreateDefault();
            try
            {
                var obj = await service.GetByUserAsync(userId, teamId, page, limit);
                res = new ResponseModel<PaginationModel<UserPredictionResult>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<UserPredictionResult>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ById/{id}")]
        public async Task<ResponseModel<UserPredictionResult>> GetByIdAsync(Guid id)
        {
            ResponseModel<UserPredictionResult> res = ResponseModel<UserPredictionResult>.CreateDefault();
            try
            {
                var obj = await service.GetByIdAsync(id);
                res = new ResponseModel<UserPredictionResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserPredictionResult>.Exception(ex);
            }
            return res;
        }
        [HttpPost]
        public async Task<ResponseModel<UserPredictionModel>> CreateAsync(UserPredictionModel model)
        {
            ResponseModel<UserPredictionModel> res = ResponseModel<UserPredictionModel>.CreateDefault();
            try
            {
                var obj = await service.CreateAsync(model);
                res = new ResponseModel<UserPredictionModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserPredictionModel>.Exception(ex);
            }
            return res;
        }
        [HttpPut]
        public async Task<ResponseModel<UserPredictionModel>> UpdateAsync(UserPredictionModel model)
        {
            ResponseModel<UserPredictionModel> res = ResponseModel<UserPredictionModel>.CreateDefault();
            try
            {
                var obj = await service.UpdateAsync(model);
                res = new ResponseModel<UserPredictionModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserPredictionModel>.Exception(ex);
            }
            return res;
        }

        [HttpDelete("Delete")]
        public async Task<ResponseModel<List<Guid>>> DeleteAsync([FromBody] List<Guid> matchesId)
        {
            ResponseModel<List<Guid>> res = ResponseModel<List<Guid>>.CreateDefault();
            try
            {
                var obj = await service.DeleteAsync(matchesId);
                res = new ResponseModel<List<Guid>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<Guid>>.Exception(ex);
            }
            return res;
        }

        // La répartition des pronos (victoire du PSG, nul, victoire de l'adversaire)
        // et le score le plus pronostiqué. Lecture seule, données agrégées, code
        // d'accès exigé. Sans matchId : le prochain match. S'ouvre dans un navigateur.
        [HttpGet("Tendance/{accessCode}/{teamId}")]
        public async Task<ResponseModel<UserMatchService.TendanceResult>> TendanceProchainMatchAsync(string accessCode, string teamId)
        {
            ResponseModel<UserMatchService.TendanceResult> res = ResponseModel<UserMatchService.TendanceResult>.CreateDefault();
            try
            {
                var obj = await service.TendanceAsync(accessCode, teamId, null);
                res = new ResponseModel<UserMatchService.TendanceResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.TendanceResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("Tendance/{accessCode}/{teamId}/{matchId}")]
        public async Task<ResponseModel<UserMatchService.TendanceResult>> TendanceMatchAsync(string accessCode, string teamId, string matchId)
        {
            ResponseModel<UserMatchService.TendanceResult> res = ResponseModel<UserMatchService.TendanceResult>.CreateDefault();
            try
            {
                var obj = await service.TendanceAsync(accessCode, teamId, matchId);
                res = new ResponseModel<UserMatchService.TendanceResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserMatchService.TendanceResult>.Exception(ex);
            }
            return res;
        }

        [HttpPut("ReplaceMatch/{originalMatchId}/{newMatchId}")]
        public async Task<ResponseModel<bool>> ReplaceMatchAsync(string originalMatchId, string newMatchId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.ReplaceMatchAsync(originalMatchId, newMatchId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
    }
}
