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
    }
}
