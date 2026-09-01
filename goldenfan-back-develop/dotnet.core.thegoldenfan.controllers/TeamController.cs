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
    public class TeamController : ControllerBase
    {
        private readonly TeamService service;


        public TeamController(TeamService service)
        {
            this.service = service;
        }


        [HttpPost("{teamId}/Squad")]
        public async Task<ResponseModel<bool>> SetSquadAsync(string teamId, TeamService.SetSquadInput input)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.SetSquadAsync(teamId, input);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByName/{name}")]
        public async Task<ResponseModel<PaginationModel<Team>>> ByMatchAsync(string name, int page=1, int pageSize=10)
        {
            ResponseModel<PaginationModel<Team>> res = ResponseModel<PaginationModel<Team>>.CreateDefault();
            try
            {
                var obj = await service.ByNameAsync(name, page, pageSize);
                res = new ResponseModel<PaginationModel<Team>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel< Team >>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ById/{id}")]
        public async Task<ResponseModel<BaseTeamResult>> ByIdAsync(string id, bool showOldPlayers = false)
        {
            ResponseModel<BaseTeamResult> res = ResponseModel<BaseTeamResult>.CreateDefault();
            try
            {
                var obj = await service.ByIdAsync(id, showOldPlayers);
                res = new ResponseModel<BaseTeamResult>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<BaseTeamResult>.Exception(ex);
            }
            return res;
        }
    }
}
