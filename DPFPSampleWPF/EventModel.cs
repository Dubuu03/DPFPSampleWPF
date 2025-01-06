using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DPFPSampleWPF.Models  
{
    public class EventModel
    {
        public int event_id { get; set; }
        public string event_name { get; set; }
        public DateTime event_start_date { get; set; }
        public DateTime event_end_date { get; set; }

        public string participants_course { get; set; } // just raw CSV
        public string participants_year { get; set; } // just raw CSV
    }

}
