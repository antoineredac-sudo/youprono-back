using dotnet.core.thegoldenfan.Models;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils.Models;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("Opta/TournamentSchedule")]
    [ApiController]
    //[Authorize]
    public class OptaTournamentScheduleController : ControllerBase
    {
        private readonly OptaTournamentScheduleService service;

        public OptaTournamentScheduleController(OptaTournamentScheduleService service)
        {
            this.service = service;
        }

        [HttpPut("ByTournamentCalendar/{tournamentCalendarId}")]
        public async Task<ResponseModel<bool>> UpdateDbByCalendarIdAsync(string tournamentCalendarId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateDbByCalendarIdAsync(tournamentCalendarId);
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }


        [HttpGet("ByTournamentCalendar/{tournamentCalendarId}")]
        public async Task<ResponseModel<OptaTournamentSchedule>> All(string tournamentCalendarId)
        {
            ResponseModel<OptaTournamentSchedule> res = ResponseModel<OptaTournamentSchedule>.CreateDefault();
            try
            {
                var obj = await service.All(tournamentCalendarId);
                res = new ResponseModel<OptaTournamentSchedule>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTournamentSchedule>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendarAndContestantName/{tournamentCalendarId}/{contestantName}")]
        public async Task<ResponseModel<OptaTournamentSchedule>> ByContestantName(string tournamentCalendarId, string contestantName)
        {
            ResponseModel<OptaTournamentSchedule> res = ResponseModel<OptaTournamentSchedule>.CreateDefault();
            try
            {
                var obj = await service.ByContestantName(tournamentCalendarId, contestantName);
                res = new ResponseModel<OptaTournamentSchedule>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTournamentSchedule>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendarAndContestantId/{tournamentCalendarId}/{contestantId}")]
        public async Task<ResponseModel<OptaTournamentSchedule>> ByContestantId(string tournamentCalendarId, string contestantId)
        {
            ResponseModel<OptaTournamentSchedule> res = ResponseModel<OptaTournamentSchedule>.CreateDefault();
            try
            {
                var obj = await service.ByContestantId(tournamentCalendarId, contestantId);
                res = new ResponseModel<OptaTournamentSchedule>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTournamentSchedule>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendarAndDate/{tournamentCalendarId}")]
        public async Task<ResponseModel<OptaTournamentSchedule>> ByDate(string tournamentCalendarId, DateTime date)
        {
            ResponseModel<OptaTournamentSchedule> res = ResponseModel<OptaTournamentSchedule>.CreateDefault();
            try
            {
                var obj = await service.ByDate(tournamentCalendarId, date);
                res = new ResponseModel<OptaTournamentSchedule>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<OptaTournamentSchedule>.Exception(ex);
            }
            return res;
        }
    }
}
