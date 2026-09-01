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
    public class MatchController : ControllerBase
    {
        private readonly MatchService service;


        public MatchController(MatchService service)
        {
            this.service = service;
        }



        [HttpGet("ByTeamId/{teamId}")]
        public async Task<ResponseModel<PaginationModel<MatchResult>>> ByTeamIdAsync(string teamId, bool OnlyNextMatches = false, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<MatchResult>> res = ResponseModel<PaginationModel<MatchResult>>.CreateDefault();
            try
            {
                var obj = await service.ByTeamIdAsync(teamId, OnlyNextMatches, page, limit);
                res = new ResponseModel<PaginationModel<MatchResult>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<MatchResult>>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ById/{id}")]
        public async Task<ResponseModel<BaseMatchResult>> ByIdAsync(string id, bool detailed = false)
        {
            ResponseModel<BaseMatchResult> res = ResponseModel<BaseMatchResult>.CreateDefault();
            try
            {
                var obj = await service.ByIdAsync(id, detailed);
                res = new ResponseModel<BaseMatchResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<BaseMatchResult>.Exception(ex);
            }
            return res;
        }
        [HttpGet("Previous/{teamId}")]
        public async Task<ResponseModel<BaseMatchResult>> PreviousAsync(string teamId)
        {
            ResponseModel<BaseMatchResult> res = ResponseModel<BaseMatchResult>.CreateDefault();
            try
            {
                var obj = await service.PreviousAsync(teamId);
                res = new ResponseModel<BaseMatchResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<BaseMatchResult>.Exception(ex);
            }
            return res;
        }
        [HttpPost("Create")]
        public async Task<ResponseModel<string>> CreateAsync(CreateMatchInput input)
        {
            ResponseModel<string> res = ResponseModel<string>.CreateDefault();
            try
            {
                var matchId = await service.CreateAsync(input);
                res = new ResponseModel<string>(0, matchId);
            }
            catch (Exception ex)
            {
                res = ResponseModel<string>.Exception(ex);
            }
            return res;
        }

        [HttpPost("{matchId}/Composition")]
        public async Task<ResponseModel<bool>> SetCompositionAsync(string matchId, SetCompositionInput input)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.SetCompositionAsync(matchId, input);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpPost("{matchId}/Results")]
        public async Task<ResponseModel<bool>> SetResultsAsync(string matchId, SetResultsInput input)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.SetResultsAsync(matchId, input);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpPut("{matchId}")]
        public async Task<ResponseModel<bool>> UpdateAsync(string matchId, CreateMatchInput input)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateAsync(matchId, input);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpDelete("Delete/{matchId}")]
        public async Task<ResponseModel<bool>> DeleteAsync(string matchId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.DeleteAsync(matchId);
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
