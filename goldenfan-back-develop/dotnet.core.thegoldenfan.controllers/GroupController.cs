using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Services.GroupService;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]")]
    [ApiController]
    public class GroupController : ControllerBase
    {
        private readonly GroupService service;

        public GroupController(GroupService service)
        {
            this.service = service;
        }

        [HttpPost("Create/{creatorId}")]
        public async Task<ResponseModel<GroupResult>> CreateAsync(Guid creatorId, CreateGroupInput input)
        {
            ResponseModel<GroupResult> res = ResponseModel<GroupResult>.CreateDefault();
            try
            {
                var group = await service.CreateAsync(creatorId, input);
                res = new ResponseModel<GroupResult>(0, group);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupResult>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Join/{inviteCode}/{userId}")]
        public async Task<ResponseModel<GroupResult>> JoinAsync(string inviteCode, Guid userId)
        {
            ResponseModel<GroupResult> res = ResponseModel<GroupResult>.CreateDefault();
            try
            {
                var group = await service.JoinAsync(inviteCode, userId);
                res = new ResponseModel<GroupResult>(0, group);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupResult>.Exception(ex);
            }
            return res;
        }

        [HttpPost("Leave/{groupId}/{userId}")]
        public async Task<ResponseModel<LeaveGroupResult>> LeaveAsync(Guid groupId, Guid userId)
        {
            ResponseModel<LeaveGroupResult> res = ResponseModel<LeaveGroupResult>.CreateDefault();
            try
            {
                var result = await service.LeaveAsync(groupId, userId);
                res = new ResponseModel<LeaveGroupResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<LeaveGroupResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ById/{groupId}")]
        public async Task<ResponseModel<GroupDetailsResult>> ByIdAsync(Guid groupId)
        {
            ResponseModel<GroupDetailsResult> res = ResponseModel<GroupDetailsResult>.CreateDefault();
            try
            {
                var group = await service.ByIdAsync(groupId);
                res = new ResponseModel<GroupDetailsResult>(0, group);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupDetailsResult>.Exception(ex);
            }
            return res;
        }

        // Le salon du groupe, pour un match donné.
        [HttpGet("Salon/{teamId}/{matchId}/{userId}/{groupId}")]
        public async Task<ResponseModel<SalonResult>> SalonAsync(string teamId, string matchId, Guid userId, Guid groupId)
        {
            ResponseModel<SalonResult> res = ResponseModel<SalonResult>.CreateDefault();
            try
            {
                var salon = await service.SalonAsync(teamId, matchId, userId, groupId);
                res = new ResponseModel<SalonResult>(0, salon);
            }
            catch (Exception ex)
            {
                res = ResponseModel<SalonResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByUserId/{userId}")]
        public async Task<ResponseModel<List<GroupResult>>> ByUserIdAsync(Guid userId)
        {
            ResponseModel<List<GroupResult>> res = ResponseModel<List<GroupResult>>.CreateDefault();
            try
            {
                var groups = await service.ByUserIdAsync(userId);
                res = new ResponseModel<List<GroupResult>>(0, groups);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<GroupResult>>.Exception(ex);
            }
            return res;
        }

        // Ce qui s'est passe pour un joueur sur un match : podiums de groupe,
        // record personnel, progression au classement. Lu avec la note.
        [HttpGet("MatchEvents/{teamId}/{matchId}/{userId}")]
        public async Task<ResponseModel<MatchEventsResult>> MatchEventsAsync(string teamId, string matchId, Guid userId)
        {
            ResponseModel<MatchEventsResult> res = ResponseModel<MatchEventsResult>.CreateDefault();
            try
            {
                var result = await service.MatchEventsAsync(teamId, matchId, userId);
                res = new ResponseModel<MatchEventsResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<MatchEventsResult>.Exception(ex);
            }
            return res;
        }

        // Ce que designe un code d'invitation, avant toute adhesion.
        [HttpGet("ByCode/{inviteCode}")]
        public async Task<ResponseModel<GroupByCodeResult>> ByCodeAsync(string inviteCode)
        {
            ResponseModel<GroupByCodeResult> res = ResponseModel<GroupByCodeResult>.CreateDefault();
            try
            {
                var result = await service.ByCodeAsync(inviteCode, null);
                res = new ResponseModel<GroupByCodeResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupByCodeResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("ByCode/{inviteCode}/{userId}")]
        public async Task<ResponseModel<GroupByCodeResult>> ByCodeForUserAsync(string inviteCode, Guid userId)
        {
            ResponseModel<GroupByCodeResult> res = ResponseModel<GroupByCodeResult>.CreateDefault();
            try
            {
                var result = await service.ByCodeAsync(inviteCode, userId);
                res = new ResponseModel<GroupByCodeResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupByCodeResult>.Exception(ex);
            }
            return res;
        }

        // La page d'un kop. Consultable sans compte : c'est ce que le fondateur
        // partage sur X, et ce qu'un visiteur voit avant de decider de jouer.
        [HttpGet("Kop/{teamId}/{groupId}")]
        public async Task<ResponseModel<KopResult>> KopPublicAsync(string teamId, Guid groupId)
        {
            ResponseModel<KopResult> res = ResponseModel<KopResult>.CreateDefault();
            try
            {
                var result = await service.KopAsync(teamId, groupId, null);
                res = new ResponseModel<KopResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<KopResult>.Exception(ex);
            }
            return res;
        }

        // La meme page pour un joueur connecte : sa ligne est marquee, ce qui permet
        // au site de centrer le classement sur lui.
        [HttpGet("Kop/{teamId}/{groupId}/{userId}")]
        public async Task<ResponseModel<KopResult>> KopAsync(string teamId, Guid groupId, Guid userId)
        {
            ResponseModel<KopResult> res = ResponseModel<KopResult>.CreateDefault();
            try
            {
                var result = await service.KopAsync(teamId, groupId, userId);
                res = new ResponseModel<KopResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<KopResult>.Exception(ex);
            }
            return res;
        }

        // La liste des kops de supporters, triee par nombre de membres.
        [HttpGet("Kops/{userId}")]
        public async Task<ResponseModel<KopListResult>> KopsAsync(Guid userId)
        {
            ResponseModel<KopListResult> res = ResponseModel<KopListResult>.CreateDefault();
            try
            {
                var result = await service.KopListAsync(userId);
                res = new ResponseModel<KopListResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<KopListResult>.Exception(ex);
            }
            return res;
        }

        [HttpGet("Trophies/{userId}")]
        public async Task<ResponseModel<TrophiesResult>> TrophiesAsync(Guid userId)
        {
            ResponseModel<TrophiesResult> res = ResponseModel<TrophiesResult>.CreateDefault();
            try
            {
                var result = await service.TrophiesAsync(userId);
                res = new ResponseModel<TrophiesResult>(0, result);
            }
            catch (Exception ex)
            {
                res = ResponseModel<TrophiesResult>.Exception(ex);
            }
            return res;
        }
    }
}
