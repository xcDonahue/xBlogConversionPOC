using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Xcentium.xBlog2FlexMigrationBeta.Models
{
    public class AuthorCard
    {
        public string Name { get; set; }
        public string Email { get; set; }

        public Sitecore.Data.Fields.ImageField ProfileImage { get; set; }

        public string Location { get; set; }
        public string Title { get; set; }

        public string Bio { get; set; }



    }
}