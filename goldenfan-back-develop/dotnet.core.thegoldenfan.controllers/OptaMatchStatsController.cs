using dotnet.core.thegoldenfan.Models;
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
    [Route("Opta/Stats")]
    [ApiController]
    //[Authorize]
    public class OptaMatchStatsController : ControllerBase
    {
        private readonly OptaMatchStatsService service;


        public OptaMatchStatsController(OptaMatchStatsService service)
        {
            this.service = service;
        }


        [HttpPut("ByMatch/{matchId}")]
        public async Task<ResponseModel<bool>> UpdateDbByMatchAsync(string matchId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateDbByMatchAsync(matchId, true);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
        [HttpPut("UpdatePrevieousMatches/{teamId}")]
        public async Task<ResponseModel<bool>> UpdatePreviousDbAsync(string teamId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdatePreviousDbAsync(teamId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByMatch/{matchId}")]
        public async Task<ResponseModel<OptaMatchStats>> ByMatchAsync(string matchId , bool detailed = false)
        {
            ResponseModel<OptaMatchStats> res = ResponseModel<OptaMatchStats>.CreateDefault();
            try
            {
                var obj = await service.ByMatchAsync(matchId, detailed);
                res = new ResponseModel<OptaMatchStats>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaMatchStats>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByMatch/{matchId}/{date}/{status}")]
        public async Task<ResponseModel<OptaMatchStats>> ByMatchAsync(string matchId, DateTime date, string status, bool detailed = false)
        {
            ResponseModel<OptaMatchStats> res = ResponseModel<OptaMatchStats>.CreateDefault();
            try
            {
                var obj = await service.ByMatchAsync(matchId, date, status, detailed);
                res = new ResponseModel<OptaMatchStats>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaMatchStats>.Exception(ex);
            }
            return res;
        }
    }
}
