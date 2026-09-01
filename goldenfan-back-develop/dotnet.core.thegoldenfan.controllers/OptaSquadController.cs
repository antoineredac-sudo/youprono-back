using dotnet.core.thegoldenfan.Models;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("Opta/Squad")]
    [ApiController]
    //[Authorize]
    public class OptaSquadController : ControllerBase
    {
        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/opta-sdapi-soccer-api-squads.htm

        private readonly OptaSquadService service;

        public OptaSquadController(OptaSquadService service)
        {
            this.service = service;
        }

        [HttpPut("UpdateDb/ByContestant/{contestantId}")]
        public async Task<ResponseModel<bool>> UpdateDbByContestantAsync(string contestantId, bool detailed = false)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateDbByContestantAsync(contestantId, detailed);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByContestant/{contestantId}")]
        public async Task<ResponseModel<OptaTeamSquad?>> SquadByContestantAsync(string contestantId, bool detailed = false)
        {
            ResponseModel<OptaTeamSquad?> res = ResponseModel<OptaTeamSquad?>.CreateDefault();
            try
            {
                var obj = await service.SquadByContestantAsync(contestantId, detailed);
                res = new ResponseModel<OptaTeamSquad?>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTeamSquad?>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendar/{tournamentCalendarId}")]
        public async Task<ResponseModel<OptaTeamSquad?>> SquadByTournamentCalendarAsync(string tournamentCalendarId, bool detailed = false)
        {
            ResponseModel<OptaTeamSquad?> res = ResponseModel<OptaTeamSquad?>.CreateDefault();
            try
            {
                var obj = await service.SquadByTournamentCalendarAsync(tournamentCalendarId, detailed);
                res = new ResponseModel<OptaTeamSquad?>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTeamSquad?>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendarAndConstestant/{tournamentCalendarId}/{contestantId}")]
        public async Task<ResponseModel<OptaTeamSquad?>> SquadByTournamentCalendarAndContestantAsync(string tournamentCalendarId, string contestantId, bool detailed = false)
        {
            ResponseModel<OptaTeamSquad?> res = ResponseModel<OptaTeamSquad?>.CreateDefault();
            try
            {
                var obj = await service.SquadByTournamentCalendarAndContestantAsync(tournamentCalendarId, contestantId, detailed);
                res = new ResponseModel<OptaTeamSquad?>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTeamSquad?>.Exception(ex);
            }
            return res;
        }
    }
}
