using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Advantive.Dispatcher.Office.Domain
{
    public class WebPages
    {
        public string webSearchUrl { get; set; }
        public int totalEstimatedMatches { get; set; }
        public WebPage[] value { get; set; }
    }
}
