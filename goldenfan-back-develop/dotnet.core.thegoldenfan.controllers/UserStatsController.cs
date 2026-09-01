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
using static dotnet.core.thegoldenfan.Services.UserStatsService;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]")]
    [ApiController]
    //[Authorize]
    public class UserStatsController : ControllerBase
    {
        private readonly UserStatsService service;


        public UserStatsController(UserStatsService service)
        {
            this.service = service;
        }


        [HttpPut("Update/{userId}/{matchId}/{teamId}")]
        public async Task<ResponseModel<bool>> UpdateAsync(Guid userId, string matchId, string teamId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateAsync(userId, matchId, teamId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
        [HttpPut("UpdateAll/{teamId}")]
        public async Task<ResponseModel<bool>> UpdateAllAsync(string teamId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateAllAsync(teamId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
        [HttpPut("UpdateMatch/{teamId}/{matchId}")]
        public async Task<ResponseModel<bool>> UpdateTeamMatchAsync(string teamId, string matchId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateTeamMatchAsync(teamId, matchId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByUserMatchTeam/{userId}/{matchId}/{teamId}")]
        public async Task<ResponseModel<UserStatsResult>> ByUserMatchTeamAsync(Guid userId, string matchId, string teamId)
        {
            ResponseModel<UserStatsResult> res = ResponseModel<UserStatsResult>.CreateDefault();
            try
            {
                var obj = await service.ByUserMatchTeamAsync(userId, matchId, teamId);
                res = new ResponseModel<UserStatsResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserStatsResult>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ById/{id}")]
        public async Task<ResponseModel<UserStatsResult>> ByIdAsync(Guid id)
        {
            ResponseModel<UserStatsResult> res = ResponseModel<UserStatsResult>.CreateDefault();
            try
            {
                var obj = await service.ByIdAsync(id);
                res = new ResponseModel<UserStatsResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<UserStatsResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByUserTeam/{userId}/{teamId}")]
        public async Task<ResponseModel<PaginationModel<UserListStatsResult>>> ByUserTeamAsync(Guid userId, string teamId, int page=1, int limit=10)
        {
            ResponseModel<PaginationModel<UserListStatsResult>> res = ResponseModel<PaginationModel<UserListStatsResult>>.CreateDefault();
            try
            {
                var obj = await service.ByUserTeamAsync(userId, teamId, page, limit);
                res = new ResponseModel<PaginationModel<UserListStatsResult>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<UserListStatsResult>>.Exception(ex);
            }
            return res;
        }

        [HttpGet("GlobalStats/{userId}/{teamId}")]
        public async Task<ResponseModel<GlobalStatsResult>> GlobalStatsAsync(Guid userId, string teamId)
        {
            ResponseModel<GlobalStatsResult> res = ResponseModel<GlobalStatsResult>.CreateDefault();
            try
            {
                var obj = await service.GlobalStatsAsync(userId, teamId);
                res = new ResponseModel<GlobalStatsResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GlobalStatsResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("RankingByResultFinalTotal/{teamId}")]
        public async Task<ResponseModel<List<UserRanking>>> RankingByResultFinalTotalAsync(string teamId)
        {
            ResponseModel<List<UserRanking>> res = ResponseModel<List<UserRanking>>.CreateDefault();
            try
            {
                var obj = await service.RankingByResultFinalTotalAsync(teamId);
                res = new ResponseModel<List<UserRanking>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<UserRanking>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("RankingByResultTotal/{teamId}")]
        public async Task<ResponseModel<List<UserRanking>>> RankingByResultTotalAsync(string teamId)
        {
            ResponseModel<List<UserRanking>> res = ResponseModel<List<UserRanking>>.CreateDefault();
            try
            {
                var obj = await service.RankingByResultTotalAsync(teamId);
                res = new ResponseModel<List<UserRanking>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<UserRanking>>.Exception(ex);
            }
            return res;
        }

        //public class TTestModel
        //{
        //    public class TTTestModel
        //    {
        //        public int Team { get; set; }
        //        public int Opponent { get; set; }
        //    }
        //    public int SelectedTeam { get; set; }
        //    public int OpponentTeam { get; set; }
        //    public List<TTTestModel> Prediction { get; set; } = new();
        //}
        //[HttpPost("Test")]
        //public List<double> TestCalculScore([FromBody]TTestModel model)
        //{
        //    List<double> res = new List<double>();
        //    TestModel o = new TestModel()
        //    {
        //        SelectedTeam = new BaseTeamResult() { Score = model.SelectedTeam },
        //        OpponentTeam = new BaseTeamResult() { Score = model.OpponentTeam},
        //    };
        //    foreach(var item in model.Prediction)
        //    {
        //        o.UserPrediction = new UserPredictionModel()
        //        {
        //            Team = new PredictionValueModel()
        //            {
        //                Score = item.Team
        //            },
        //            Opponent = new PredictionValueModel()
        //            {
        //                Score = item.Opponent
        //            }
        //        };
        //        var obj = service.TestCalculScore(o);
        //        res.Add(obj);
        //    }
        //    return res;
        //}
    }
}
