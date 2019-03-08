using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Advantive.Dispatcher.Office.Domain
{
    public class WebPage
    {
        public string name { get; set; }
        public string url { get; set; }
        public string displayUrl { get; set; }
        public string snippet { get; set; }
        public DateTime dateLastCrawled { get; set; }
        public string cachedPageUrl { get; set; }
        public OpenGraphImage openGraphImage { get; set; }
    }
}
