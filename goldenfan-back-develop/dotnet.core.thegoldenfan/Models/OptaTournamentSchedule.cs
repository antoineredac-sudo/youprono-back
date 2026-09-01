using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Models
{
    public class OptaTournamentSchedule
    {
        public CompetitionModel Competition { get; set; }
        public TournamentCalendarModel TournamentCalendar { get; set; }
        public List<MatchDateModel> MatchDate { get; set; }
    }
}
