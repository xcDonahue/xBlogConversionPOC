using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Newtonsoft.Json.Linq;
using Sitecore;
using Sitecore.Data.Items;
using Sitecore.SecurityModel;

namespace Xcentium.xBlog2FlexMigrationBeta.Controllers
{
    public class TaxonomyController : Controller
    {
        readonly static Sitecore.Data.Database _db = Sitecore.Configuration.Factory.GetDatabase("master");
        readonly static TemplateItem _tagItemTemplate = _db.GetTemplate("{D068257D-F1F2-490C-ACD0-B35AA6B3D5EB}");
        readonly static Item _taxonomyRootItem = _db.GetItem("/sitecore/content/Global Settings/Taxonomy");


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

        public ActionResult CreateTags()
        {
            var message = "process complete";
            if (Request.QueryString["process"] == "true")
            {
                var tagsFile = System.IO.File.ReadAllLines(@"C:\projects\XC.com\Static\Flex.V2.Sitecore-master\src\External\code\Content\TagsList.csv");
                var tagList = new List<string>(tagsFile);

                foreach (var tag in tagList)
                {
                    UpsertTag(tag);
                }
            }
            else
            {
                message = "skip Processing";
            }

            return View("~/Views/Taxonomy/CreateTags.cshtml", (object)message);

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

        private void UpsertTag(string tagName)
        {
            if (_db.GetItem(_taxonomyRootItem.Paths.Path + "/" + tagName) == null)
            {
                using (new SecurityDisabler())
                {
                    if (_tagItemTemplate != null && _db.GetItem(_taxonomyRootItem.Paths.Path + "/" + tagName) == null)
                    {
                        var item =_taxonomyRootItem?.Add(tagName, _tagItemTemplate);
                        item.Editing.BeginEdit();
                        item["Tag Name"] = tagName;
                        item.Editing.EndEdit();

                    }
                }
            }
           


        }

    }
}