using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils;
using dotnet.core.utils.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Services.GroupService;

namespace dotnet.core.thegoldenfan.controllers
{
    // Deux outils des tournois (26 septembre 2026) :
    //  - le nom libre, pour le site : deux tournois ne portent pas le meme nom ;
    //  - la suppression d'un tournoi par Antoine, depuis Swagger, avec le code
    //    d'administration de la variable ADMIN_CODE de Render.
    [Route("Group")]
    [ApiController]
    public class GroupOutilsController : ControllerBase
    {
        private readonly GroupService service;

        public GroupOutilsController(GroupService service)
        {
            this.service = service;
        }

        // Le nom demande s'il est libre, sinon le meme suivi d'un numero.
        // sauf : le tournoi qu'on renomme, qui ne se gene pas lui-meme.
        [HttpGet("NomLibre")]
        public async Task<ResponseModel<string>> NomLibreAsync([FromQuery] string nom, [FromQuery] Guid? sauf)
        {
            ResponseModel<string> res = ResponseModel<string>.CreateDefault();
            try
            {
                var r = await service.NomLibreAsync(nom, sauf);
                res = new ResponseModel<string>(0, r);
            }
            catch (Exception ex)
            {
                res = ResponseModel<string>.Exception(ex);
            }
            return res;
        }

        // Etape 1 : les tournois crees par un pseudo, avec leur identifiant.
        [HttpGet("Admin/ParCreateur/{accessCode}/{pseudo}")]
        public async Task<ResponseModel<List<TournoiAdminResult>>> ParCreateurAsync(string accessCode, string pseudo)
        {
            ResponseModel<List<TournoiAdminResult>> res = ResponseModel<List<TournoiAdminResult>>.CreateDefault();
            try
            {
                if (!GroupService.CodeAdminValide(accessCode))
                { throw BaseException.NotFound(-1, "GroupOutilsController.ParCreateurAsync"); }
                var r = await service.TournoisDuCreateurAsync(pseudo);
                res = new ResponseModel<List<TournoiAdminResult>>(0, r);
            }
            catch (Exception ex)
            {
                res = ResponseModel<List<TournoiAdminResult>>.Exception(ex);
            }
            return res;
        }

        // Etape 2 : la suppression d'un tournoi, par son identifiant.
        [HttpPost("Admin/Supprimer/{accessCode}/{groupId}")]
        public async Task<ResponseModel<bool>> SupprimerAsync(string accessCode, Guid groupId)
        {
            ResponseModel<bool> res = ResponseModel<bool>.CreateDefault();
            try
            {
                if (!GroupService.CodeAdminValide(accessCode))
                { throw BaseException.NotFound(-1, "GroupOutilsController.SupprimerAsync"); }
                var r = await service.SupprimerTournoiAsync(groupId);
                res = new ResponseModel<bool>(0, r);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
            }
            return res;
        }
    }
}
