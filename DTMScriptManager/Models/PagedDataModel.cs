using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DTMScriptManager.Models
{
    public class PagedDataModel
    {
        public int TotalRows { get; set; }
        public IEnumerable<object> obj { get; set; }
        public int PageSize { get; set; }
    }
}