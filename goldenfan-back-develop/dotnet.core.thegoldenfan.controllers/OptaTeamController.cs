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
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("Opta/Team")]
    [ApiController]
    //[Authorize]
    public class OptaTeamController : ControllerBase
    {
        private readonly OptaTeamService service;

        public OptaTeamController(OptaTeamService service)
        {
            this.service = service;
        }

        [HttpPut("UpdateDb")]
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


        [HttpGet("ByCountry/{countryId}")]
        public async Task<ResponseModel<PaginationModel<ContestantModel>>> TeamByCountryAsync(string countryId, bool detailed = false, int page=1, int limit = 10)
        {
            ResponseModel<PaginationModel<ContestantModel>> res = ResponseModel<PaginationModel<ContestantModel>>.CreateDefault();
            try
            {
                var obj = await service.TeamByCountryAsync(countryId, detailed, page, limit);
                res = new ResponseModel<PaginationModel<ContestantModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<ContestantModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByContestant/{contestantId}")]
        public async Task<ResponseModel<ContestantModel>> TeamByContestantAsync(string contestantId, bool detailed = false)
        {
            ResponseModel<ContestantModel> res = ResponseModel<ContestantModel>.CreateDefault();
            try
            {
                var obj = await service.TeamByContestantAsync(contestantId, detailed);
                res = new ResponseModel<ContestantModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<ContestantModel>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByTournamentCalendar/{tournamentCalendarId}")]
        public async Task<ResponseModel<PaginationModel<ContestantModel>>> TeamByTournamentCalendarAsync(string tournamentCalendarId, bool detailed = false, int page=1, int limit=10)
        {
            ResponseModel<PaginationModel<ContestantModel>> res = ResponseModel<PaginationModel<ContestantModel>>.CreateDefault();
            try
            {
                var obj = await service.TeamByTournamentCalendarAsync(tournamentCalendarId, detailed, page, limit);
                res = new ResponseModel<PaginationModel<ContestantModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<ContestantModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByStage/{stageId}")]
        public async Task<ResponseModel<PaginationModel<ContestantModel>>> TeamByStageAsync(string stageId, bool detailed = false, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<ContestantModel>> res = ResponseModel<PaginationModel<ContestantModel>>.CreateDefault();
            try
            {
                var obj = await service.TeamByStageAsync(stageId, detailed, page, limit);
                res = new ResponseModel<PaginationModel<ContestantModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<ContestantModel>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("BySerie/{serieId}")]
        public async Task<ResponseModel<PaginationModel<ContestantModel>>> TeamBySerieAsync(string serieId, bool detailed = false, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<ContestantModel>> res = ResponseModel<PaginationModel<ContestantModel>>.CreateDefault();
            try
            {
                var obj = await service.TeamBySerieAsync(serieId, detailed, page, limit);
                res = new ResponseModel<PaginationModel<ContestantModel>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<ContestantModel>>.Exception(ex);
            }
            return res;
        }
    }
}