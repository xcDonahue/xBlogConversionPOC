using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sitecore;
using Sitecore.Data.Items;

namespace Xcentium.xBlog2FlexMigrationBeta.Controllers
{
    public class TaxonomyController : Controller
    {
        readonly static Sitecore.Data.Database _db = Sitecore.Configuration.Factory.GetDatabase("master");

        // GET: Taxonomy
        public ActionResult CategoriesList()
        {

            var catRootItem = _db.GetItem("/sitecore/content/xferData/Blog/Categories");
            var catList = getListOfItemNames(catRootItem);

            return View(catList);
        }

        public ActionResult TagsList()
        {
            var tagRootItem = _db.GetItem("/sitecore/content/xferData/Blog/Tags");
            var tagList = getListOfItemNames(tagRootItem);
            return View(tagList);
        }

        private List<string> getListOfItemNames(Item parent)
        {
            var nameList = new List<string>();
            foreach (Item item in parent.Children)
            {
                nameList.Add(item.Name);
            }

            return nameList;
        }
    }
}