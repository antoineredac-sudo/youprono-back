using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Services.GroupService;

namespace dotnet.core.thegoldenfan.controllers
{
    // La relance des tournois prives termines (25 septembre 2026). Un fichier a
    // part plutot qu'une retouche de GroupController : ses routes vivent sous le
    // meme prefixe « Group », et aucune n'existe deja la-bas.
    [Route("Group")]
    [ApiController]
    public class GroupRelanceController : ControllerBase
    {
        private readonly GroupService service;

        public GroupRelanceController(GroupService service)
        {
            this.service = service;
        }

        // Les tournois prives que ce joueur peut relancer, pour le bandeau de
        // l'accueil.
        [HttpGet("ARelancer/{userId}")]
        public async Task<ResponseModel<List<TournoiResult>>> ARelancerAsync(Guid userId)
        {
            ResponseModel<List<TournoiResult>> res = ResponseModel<List<TournoiResult>>.CreateDefault();
            try
            {
                var r = await service.ARelancerAsync(userId);
                res = new ResponseModel<List<TournoiResult>>(0, r);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<TournoiResult>>.Exception(ex);
            }
            return res;
        }

        // Le createur relance son tournoi : un nouveau tournoi, memes membres.
        [HttpPost("Relancer/{groupId}/{userId}")]
        public async Task<ResponseModel<GroupResult>> RelancerAsync(Guid groupId, Guid userId)
        {
            ResponseModel<GroupResult> res = ResponseModel<GroupResult>.CreateDefault();
            try
            {
                var r = await service.RelancerAsync(groupId, userId);
                res = new ResponseModel<GroupResult>(0, r);
            }
            catch (Exception ex)
            {
                res = ResponseModel<GroupResult>.Exception(ex);
            }
            return res;
        }
    }
}
