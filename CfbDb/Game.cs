using System;
using System.Collections.Generic;
using System.Text;

namespace CfbDb
{
    public class Game
    {
        public DateTime GameDate { get; set; }
        public String VisitingTeamName { get; set; }
        public int VisitingTeamScore { get; set; }
        public String HomeTeamName { get; set; }
        public int HomeTeamScore { get; set; }
    }
}
