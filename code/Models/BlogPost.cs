using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Xcentium.xBlog2FlexMigrationBeta.Models
{
    public class BlogPost
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string Summary { get; set; }
        public string FriendlyDate { get; set; }
        public string Category { get; set; }
        public string Tags { get; set; }
        public AuthorCard Author { get; set; }

    }
}