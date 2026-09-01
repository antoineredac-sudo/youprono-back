using dotnet.core.utils;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils.Models;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("Opta")]
    [ApiController]
    //[Authorize]
    public class OptaTournamentCalendarController : ControllerBase
    {
        private readonly OptaTournamentCalendarService service;

        public OptaTournamentCalendarController(OptaTournamentCalendarService service)
        {
            this.service = service;
        }

        [HttpPut("TournamentCalendar/UpdateDb")]
        public async Task<ResponseModel<bool>> UpdateDbAsync()
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                await service.UpdateDbAsync();
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }

        [HttpGet("TournamentCalendar")]
        public async Task<ResponseModel<PaginationModel<CompetitionModel>>> TournamentCalendarAsync(int page=1, int pageSize=10)
        {
            ResponseModel<PaginationModel<CompetitionModel>> res = ResponseModel<PaginationModel<CompetitionModel>>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarAsync(page, pageSize);
                res = new ResponseModel<PaginationModel<CompetitionModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<CompetitionModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("TournamentCalendar/ByName/{name}")]
        public async Task<ResponseModel<PaginationModel<CompetitionModel>>> TournamentCalendarByNameAsync(string name, int page = 1, int pageSize = 10)
        {
            ResponseModel<PaginationModel<CompetitionModel>> res = ResponseModel<PaginationModel<CompetitionModel>>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarByNameAsync(name, page, pageSize);
                res = new ResponseModel<PaginationModel<CompetitionModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<CompetitionModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("TournamentCalendar/ByCountryCode/{countryCode}")]
        public async Task<ResponseModel<PaginationModel<CompetitionModel>>> TournamentCalendarByCountryCodeAsync(string countryCode, int page = 1, int pageSize = 10)
        {
            ResponseModel<PaginationModel<CompetitionModel>> res = ResponseModel<PaginationModel<CompetitionModel>>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarByCountryCodeAsync(countryCode, page, pageSize);
                res = new ResponseModel<PaginationModel<CompetitionModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<CompetitionModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("TournamentCalendar/ByCountryCode/{countryCode}/ByName/{name}")]
        public async Task<ResponseModel<PaginationModel<CompetitionModel>>> TournamentCalendarByCountryCodeAndNameAsync(string countryCode, string name, int page = 1, int pageSize = 10)
        {
            ResponseModel<PaginationModel<CompetitionModel>> res = ResponseModel<PaginationModel<CompetitionModel>>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarByCountryCodeAndNameAsync(countryCode, name, page, pageSize);
                res = new ResponseModel<PaginationModel<CompetitionModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<CompetitionModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("TournamentCalendar/ByCompetition/{competitionId}")]
        public async Task<ResponseModel<CompetitionModel>> TournamentCalendarByCompetitionAsync(string competitionId)
        {
            ResponseModel<CompetitionModel> res = ResponseModel<CompetitionModel>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarByCompetitionAsync(competitionId);
                res = new ResponseModel<CompetitionModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<CompetitionModel>.Exception(ex);
            }
            return res;
        }
        [HttpGet("TournamentCalendar/ByContestant/{contestantId}")]
        public async Task<ResponseModel<CompetitionModel>> TournamentCalendarByContestantAsync(string contestantId)
        {
            ResponseModel<CompetitionModel> res = ResponseModel<CompetitionModel>.CreateDefault();
            try
            {
                var obj = await service.TournamentCalendarByContestantAsync(contestantId);
                res = new ResponseModel<CompetitionModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<CompetitionModel>.Exception(ex);
            }
            return res;
        }
    }
}