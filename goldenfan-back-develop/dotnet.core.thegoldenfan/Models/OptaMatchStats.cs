using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Models.OptaMatchStats;

namespace dotnet.core.thegoldenfan.Models
{
    public class OptaMatchStats
    {
        public MatchInfoModel MatchInfo { get; set; }
        public LiveDataModel LiveData { get; set; }
    }
}
