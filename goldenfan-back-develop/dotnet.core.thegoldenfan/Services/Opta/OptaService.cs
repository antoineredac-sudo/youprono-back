using dotnet.core.thegoldenfan.Models;
using dotnet.core.utils;
using dotnet.core.utils.Extensions;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services.Opta
{
    public class OptaService
    {
        //https://documentation.statsperform.com/docs/rh/sdapi/Topics/soccer/

        public JsonSerializerOptions JsonOpts = JsonHelper.TextJsonIgnoreCaseOptions();

        public static string OptaKey
        {
            get
            {
                return Constant.Application.Services["opta"]["key"].DecodeFrom64();
            }
        }
        public string Mode(string model)
        {
            return model.Equals("b2b") ? "b" : "c";
        }
        public string IsDetailed(bool detailed = false)
        {
            return detailed ? "&detailed=yes" : "";
        }
        public bool BoolConverter(string model)
        {
            bool res = false;
            if (model != null && model.Length > 0)
            {
                model = model.Trim().ToUpper();
                res = (model.Equals("YES") || model.Equals("ACTIVE") ? true : false);
            }
            return res;
        }
    }
}
