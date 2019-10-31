using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.SecurityModel;
using Xcentium.xBlog2FlexMigrationBeta.Models;

namespace Xcentium.xBlog2FlexMigrationBeta.Controllers
{
    public class MigrationController : Controller
    {
        readonly static Sitecore.Data.Database _db = Sitecore.Configuration.Factory.GetDatabase("master");
        readonly Item _blogSrcItem = _db.GetItem("/sitecore/content/Home/origBlog");
        readonly Item _blogDestItem = _db.GetItem("/sitecore/content/Home/Blog");
        readonly Item _blogCategory = _db.GetItem("{F66EAEA8-E3BD-4D05-84BE-DF6282D10A56}");
        //readonly Item _blogPostDataMaster = _db.GetItem("{53376817-B2A7-462F-89FD-3E8130491E4D}");
        readonly Item _blogPostDataMaster = _db.GetItem("/sitecore/content/Home/Insights/Blogs/mdTest/Data");
        readonly Item _blogPostTemplateItem = _db.GetItem("/sitecore/content/Home/Insights/Blogs/mdTest");
        readonly Item _authorCardMaster = _db.GetItem("/sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_5112");
        readonly Item _flexCardMaster = _db.GetItem("/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards/Michael Donahue");

        public ActionResult BeginMigration()
        {
            
            var message = "process complete";

            try
            {
                if (Request.QueryString["process"] == "true")
                {
                    foreach (Item item in _blogSrcItem.Children)
                    {
                        processChild(item);
                    }
                }
                else
                {
                    message = "skip Processing";
                }
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Error.LogError(ex.Message);
            }

            

            return View("~/Views/Migration/Blog.cshtml",(object)message);
        }

        private void processChild(Item item)
        {
            //if child is folder, then (unless exists) create new folder in dest
            //then if this item has a child process child again
            //if item is blob post create new Post

            if(item.TemplateID.ToString() == "{ADB6CA4F-03EF-4F47-B9AC-9CE2BA53FF97}")
            {
                //this is a bucket so create folder and process children
                createFolder(item);
                if(item.HasChildren)
                {
                    foreach(Item child in item.Children)
                    {
                        processChild(child);
                    }
                }
            }
            else
            {
                //this is a blog post item so we need to create new post
                processPost(item);
            }

        }

        private void createFolder(Item item)
        {
            if(item.ParentID == _blogSrcItem.ID)
            {
                //new folder under dest root.
                var newYearFolder = createItem(_blogDestItem, "{A87A00B1-E6DB-45AB-8B54-636FEC3B5523}", item.Name);
            }
            else if(item.Parent.ParentID == _blogSrcItem.ID)
            {
                //this means that it is a month.
                //get year folder unter destination root, and create new month folder
                var yearFolder = _db.GetItem(_blogDestItem.Paths.Path + "/" + item.Parent.Name);
                var newMonthFolder = createItem(yearFolder, "{A87A00B1-E6DB-45AB-8B54-636FEC3B5523}", item.Name);
            }
            else
            {
                //this is a day folder
                //get year month unter destination root, and create new day folder
                //var yearFolder = _db.GetItem(_blogDestItem.Paths.Path + "/" + item.Parent.Name);
                var monthFolder = _db.GetItem(_blogDestItem.Paths.Path + "/" + item.Parent.Parent.Name + "/" + item.Parent.Name);
                var newDayFolder = createItem(monthFolder, "{A87A00B1-E6DB-45AB-8B54-636FEC3B5523}", item.Name);
            }
        }

        private void processPost(Item item)
        {
            var destinationPath = _blogDestItem.Paths.Path + "/" + item.Parent.Parent.Parent.Name + "/" + item.Parent.Parent.Name + "/" + item.Parent.Name;
            var destinationParent = _db.GetItem(destinationPath);

            var post = _db.GetItem(destinationPath + "/" + item.Name);

            if (post == null)
            {
                createPost(item, destinationParent);
            }
            else
            {
                updatePost(post, item);
            }
                
        }

        private void createPost(Item oldPost, Item destinationParent)
        {
            var newPost = createItem(destinationParent, "{10B61026-6659-44D5-AD22-03EC72776DAC}", oldPost.Name);

            var blogModel = getNewPostDetailsFromOld(newPost, oldPost);

            //add datasource folder
            var newDatasource = CopyTemplateAssets(newPost);

            //createAuthor card and attach
            //var globalAuthCardItem = blogModel.Author;

            using (new Sitecore.SecurityModel.SecurityDisabler())
            {
                var old1 = _db.GetItem(newPost.Paths.Path + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2/drop-zone-single_1");
                var old2 = _db.GetItem(newPost.Paths.Path + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2/drop-zone-single_2");
                deleteItem(old1);
                deleteItem(old2);
            }


            //if (blogModel.Author != null)
            //{
                //create card
                Item card = createAuthorCardItem(blogModel.Author);

                //attach card
                attachCard(newPost, card);

            //Make sure new card gets added to presentation details down below
            //
            //}

            updatePostRenderings(newDatasource, blogModel);



            updateBlogTagsAndCategory(newPost, oldPost);

            //add renderings and set datasources
            addRenderings(newPost);
        }

        private void updatePostRenderings(Item datafolder, BlogPost newPost)
        {
            //updateRenderings:

            //update fields
            //**newDatasource.Paths.Path**/flex-placeholder_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-heading_2
            //update "Single-Line Text" field - use "Main Title" field from previous
            updateField(datafolder.Paths.Path + "/flex-placeholder_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-heading_2", "Single-Line Text", newPost.Title);

            //**newDatasource.Paths.Path**/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-heading-rich_1
            //update "Rich Text" field with empty string (no subheaders at the moment)
            updateField(datafolder.Paths.Path + "/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-heading-rich_1", "Rich Text", string.Empty);

            //**newDatasource.Paths.Path**/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-multi-line_3
            //update "Multi-Line Text" field with Article Summary field " ***!!clean paragraph tags and &nbsp;!!****
            updateField(datafolder.Parent.Paths.Path, "Article Summary", newPost.Summary);

            //**newDatasource.Paths.Path**/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-multi-line_2
            //update "Single-Line Text" field with the content's Publish Date ex "October 17th, 2019"
            updateField(datafolder.Paths.Path + "/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-single-line_2", "Single-Line Text", newPost.FriendlyDate);

            //**newDatasource.Paths.Path**/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_1/drop-zone-single_1/a-rich-text_1
            //update "Rich Text" field with the content's Article Body field
            ///sitecore/content/Home/Blog New/201.../flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_1/drop-zone-single_1/a-rich-text_1
            updateField(datafolder.Paths.Path + "/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_1/drop-zone-single_1/a-rich-text_1", "Rich Text", newPost.Body);

            //update category/type
            ///sitecore/content/Home/Blog New/2012/04/30/Why is my Sitecore site running so slow Check the front end performance/Data/flex-placeholder_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-single-line_1
            updateField(datafolder.Paths.Path + "/flex-placeholder_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-single-line_1", "Single-Line Text", newPost.Category);


            //NOPE - These are author card items... Delete for now
            //**newDatasource.Paths.Path**/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2/drop-zone-single_1
            //DELETE CHILDREN - can cirlce back later
        }

        private BlogPost getNewPostDetailsFromOld(Item newPost, Item oldPost)
        {
            var post = new BlogPost();
            post.Title = oldPost["Main Title"];
            post.Body = CleanRTE(oldPost["Article Body"]);
            post.Author = getAuthorCard(_db.GetItem(oldPost["Author"]));
            post.Category = _blogCategory.Name;
            post.Tags = getNewTags(oldPost,newPost);
            post.Summary = HTMLToText(oldPost["Article Summary"]);

            DateField dateField = (DateField)oldPost.Fields["Publish Date"];

            post.FriendlyDate = ToStringWithSuffix(dateField.DateTime.Date);

            return post;
        }

        private void updatePost(Item newPost, Item oldPost)
        {
            try
            {
                //update post
                var newPostPath = newPost.Paths.Path;
                var blogModel = getNewPostDetailsFromOld(newPost, oldPost);
                var dataFolder = _db.GetItem(newPost.Paths.Path + "/Data");
                //update field values on main post
                updatePostRenderings(dataFolder, blogModel);

                //update Linked Template Datasource field on /Data/flex-placeholder_2
                // value needs to be {5D0046BA-D14A-4762-8EA4-8145C7FFA0AC}
                updateField(newPostPath + "/Data/flex-placeholder_2", "Linked Template Datasource", "{5D0046BA-D14A-4762-8EA4-8145C7FFA0AC}");

                //remove summary child
                ///**CURRENT POST PATH/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-multi-line_3
                var summaryChild = _db.GetItem(newPostPath + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_1/a-text-multi-line_3");
                deleteItem(summaryChild);

                //remove extra drop zone:
                ///**CURRENT POST PATH/Data/Blogs/previousLaunch/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_2
                var extraDropZone = _db.GetItem(newPostPath + "/Data/Blogs/previousLaunch/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_1/drop-zone-single_1/drop-zone-single_2");
                deleteItem(extraDropZone);

                //update **CURRENT POST PATH/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2
                //Editor name shoule read "Author Card"
                updateField(newPostPath + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2", "Editor name", "Author Card");

                //update **CURRENT POST PATH/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2
                //classes need to be fu-boxed f-mb-0 f-w-1/1
                updateField(newPostPath + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2", "Classes", "fu-boxed f-mb-0 f-w-1/1");

                //update author card /sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_ali_karaki_author/drop-zone-single_1_7657_4082_1492/a-image_1
                //needs specific classes:fu-aspect-ratio-1/1 f-rounded-full f-shadow-none f--mt-6 f-mb-0.75 f-mx-0 mob:f--mt-3 tab:f--mt-5 lap:f--mt-3 f-p-0/0 f-px-1/5 f-w-1/1

                createAuthorCardItem(blogModel.Author);
            }
            catch(Exception ex)
            {
                Sitecore.Diagnostics.Error.LogError(ex.Message);
            }
            

        }

        private void deleteItem(Item item)
        {
            using (new SecurityDisabler())
            {
                item?.Delete();
            }
        }

        private void attachCard(Item newPost, Item card)
        {
            //sitecore/content/Home/Insights/Blogs/mdTest/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2

            updateField(newPost.Paths.Path + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3/drop-zone-multi_2/drop-zone-single_2", "Linked Component", card.ID.ToString());

        }

        private Item createAuthorCardItem(AuthorCard globalAuthCardItem)
        {
            //card gets made first, then added to flex menu
            var cardsFolder = _db.GetItem("/sitecore/content/Data/Global/Global Content/Author Cards");
            //duplicate cardFromMike
            var newCard = copyAuthCardSample(cardsFolder, globalAuthCardItem.Name);
            //update card fields
            //Link card to self
            var cardZonePointer = _db.GetItem(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492");
            updateField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492", "Linked Component", cardZonePointer.ID.ToString());

            //sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_5112/drop-zone-single_1_7657_4082_1492/a-heading-rich_1 "Rich Text"
            updateField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-heading-rich_1", "Rich Text", $"<h2>{globalAuthCardItem.Name}</h2>");
            //sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_5112/drop-zone-single_1_7657_4082_1492/a-image_1 "Image"
            updateImageField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-image_1", "Image", globalAuthCardItem.ProfileImage);
            updateField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-image_1", "Classes", $"fu-aspect-ratio-1/1 f-rounded-full f-shadow-none f--mt-6 f-mb-0.75 f-mx-0 mob:f--mt-3 tab:f--mt-5 lap:f--mt-3 f-p-0/0 f-px-1/5 f-w-1/1");

            //sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_5112/drop-zone-single_1_7657_4082_1492/a-rich-text_4 "Rich text"
            //updateField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-rich-text_4", "Rich Text", globalAuthCardItem.Bio);
            stripImagesFromBio(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-rich-text_4", "Rich Text", globalAuthCardItem.Bio);
            //sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_5112/drop-zone-single_1_7657_4082_1492/a-text-single-line_2 "Single-Line Text"
            updateField(newCard.Paths.Path + "/drop-zone-single_1_7657_4082_1492/a-text-single-line_2", "Single-Line Text", globalAuthCardItem.Title);

            updateField(newCard.Paths.Path, "Editor name", "Author Card");

            //var flexAuthCard4Menu = _db.GetItem("/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards");
            //copy existing flex menu item to new
            var newCardMenuItem = copyAuthNavToNew(newCard);
            //update nav card fields
            updateAuthCardNavItem(newCardMenuItem, newCard);


            //update card field "Linked Menu Item" after menu item is created
            updateField(newCard.Paths.Path, "Linked Component", newCard.ID.ToString());

            //Update Flex menu

            ///sitecore/content/Data/Global/Global Content/Author Cards/drop-zone-single_aaron_bickle_author
            ////sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards/Aaron Bickle
            
            var flexMenuItem = _db.GetItem($"/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards/{newCard.DisplayName.Replace(".","")}");
            updateField(newCard.Paths.Path, "Linked Menu Item", flexMenuItem.ID.ToString());


            return newCard;
        }

        private void stripImagesFromBio(string itemPath, string fieldname, string bio)
        {
            var cleanMarkup = string.Empty;

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml("<html><body>" + bio + "</body></html>");

            HtmlNode bodyContent = document.DocumentNode.SelectSingleNode("//body");
            var all_images = bodyContent.SelectNodes("//img");
            
            if (all_images != null)
            {
                foreach (var node in all_images)
                {
                    node.Remove();
                }
            }

            cleanMarkup = document.DocumentNode.OuterHtml;
            cleanMarkup = cleanMarkup.Replace("<html><body>", "");
            cleanMarkup = cleanMarkup.Replace("</body></html>", "");

            updateField(itemPath, fieldname, cleanMarkup);
        }

        private void updateAuthCardNavItem(Item authCardNavItem, Item authorCard)
        {
            var data = new NameValueCollection();
            var cardID = authorCard.ID.ToString().Replace("{", "").Replace("}", "");

            data.Add("ComponentItemID", cardID);
            data.Add("IsSharedContent", "true");

            using (new Sitecore.SecurityModel.SecurityDisabler())
            {
                authCardNavItem.Editing.BeginEdit();
                try
                {
                    //where data is NameValueCollection
                    //where '$' is divider 
                    authCardNavItem["Props"] = StringUtil.NameValuesToString(data, "&");
                }
                finally
                {
                    authCardNavItem.Editing.EndEdit();
                }
            }
        }

        private Item copyAuthNavToNew(Item newCard)
        {
            //use template:		/sitecore/templates/Feature/FlexElements/Menu/Flex Menu Item 
            //var safeNavName = $"drop-zone-single_{newCard.Name.ToLower().Replace(" ", "_")}_author";
            var safeDisplayname = newCard.DisplayName.Replace(".", "");
            var destination = _db.GetItem("/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards");
            var newNavItem = _db.GetItem("/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards/" + safeDisplayname);
            if (newNavItem == null)
            {
                using (new SecurityDisabler())
                {
                    
                    newNavItem = _flexCardMaster.CopyTo(destination, safeDisplayname);
                    return newNavItem;
                }
            }

            return _db.GetItem("/sitecore/content/Data/Flex Elements Menu/Global Content/Author Cards/" + safeDisplayname);
        }

        private Item copyAuthCardSample(Item container, string name)
        {
            var cardContainerPath = "/sitecore/content/Data/Global/Global Content/Author Cards/";
            var safeNavName = $"drop-zone-single_{name.ToLower().Replace(" ", "_")}_author";

            Regex regex = new Regex("[^a-zA-Z0-9_-]");
            safeNavName = regex.Replace(safeNavName, "");
            safeNavName = safeNavName.Replace(".", "");

            var newCard = _db.GetItem(cardContainerPath + safeNavName);

            if (newCard == null)
            {
                using (new SecurityDisabler())
                {
                    newCard = _authorCardMaster.CopyTo(container, safeNavName);
                    newCard.Editing.BeginEdit();
                    newCard.Appearance.DisplayName = name.Replace(".", "");
                    newCard.Editing.EndEdit();
                    return newCard;
                }
            }

            return _db.GetItem(cardContainerPath + safeNavName);
        }


        private AuthorCard getAuthorCard(Item authorSrc)
        {
            //getAuthor from old source
            var authorCard = new AuthorCard();
            if(authorSrc!=null)
            {
                authorCard.Bio = authorSrc["Bio"];
                authorCard.Email = authorSrc["Email Address"];
                authorCard.Location = authorSrc["Location"];
                authorCard.Name = authorSrc["Full Name"];
                authorCard.Title = authorSrc["Title"];
                authorCard.ProfileImage = authorSrc.Fields["Profile Image"];

                return authorCard;
            }

            return null;
        }

        private Item CopyTemplateAssets(Item newPost)
        {
            if(_db.GetItem(newPost.Paths.Path + "/Data")== null)
            {
                using (new SecurityDisabler())
                {
                    return _blogPostDataMaster.CopyTo(newPost, "Data");
                }
            }

            return _db.GetItem(newPost.Paths.Path + "/Data");
        }

        private string removeTags(string source)
        {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }

        private void updateImageField(string itemPath, string fieldname, Sitecore.Data.Fields.ImageField image)
        {
            var item = _db.GetItem(itemPath);

            using (new SecurityDisabler())
            {
                Sitecore.Data.Fields.ImageField imageField = item.Fields[fieldname];
                item.Editing.BeginEdit();

                imageField.MediaID = image.MediaID;

                item.Editing.EndEdit();
            }
        }

        private void updateField(string itemPath, string fieldname, string value)
        {
            var item = _db.GetItem(itemPath);
            using (new SecurityDisabler())
            {
                item.Editing.BeginEdit();

                item[fieldname] = value;
                item.Editing.EndEdit();
            }
        }

        private void addRenderings(Item item)
        {
            //var blogTemplateItem = _db.GetItem("{ECE28233-B82E-4B9B-9D6A-F5956C69E391}");

            var templateFinalLayout = _blogPostTemplateItem.Fields[Sitecore.FieldIDs.FinalLayoutField];

            var outputXml = updateDataPaths(templateFinalLayout, item);

            var finalLayoutField = item.Fields[Sitecore.FieldIDs.FinalLayoutField];
            using (new SecurityDisabler())
            {
                item.Editing.BeginEdit();
                LayoutField.SetFieldValue(finalLayoutField, outputXml);
                item.Editing.EndEdit();
            }
        }

        private string updateDataPaths(Field templateFinalLayout, Item newPost)
        {
            string finalXml = LayoutField.GetFieldValue(templateFinalLayout);
            var finalDetails = Sitecore.Layouts.LayoutDefinition.Parse(finalXml);
            string templateXml = finalDetails.ToXml();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(templateXml);

            XmlNode renderingContainer= xmlDoc.DocumentElement.SelectSingleNode("/r/d");

            foreach (XmlNode node in renderingContainer.ChildNodes)
            {
                var newPh1Item = _db.GetItem(newPost.Paths.Path + "/Data/flex-placeholder_2");
                var newPh2Item = _db.GetItem(newPost.Paths.Path + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_2");
                var newPh3Item = _db.GetItem(newPost.Paths.Path + "/Data/flex-placeholder_2/drop-zone-multi_1/flex-layout_3");

                string ds = node.Attributes["ds"]?.InnerText;
                if(!string.IsNullOrWhiteSpace(ds))
                {
                    var templateItem = _db.GetItem(ds);
                    if(templateItem?.Paths.Path.Contains("/sitecore/content/Home/Insights/Blogs/mdTest/") == true)
                    {
                        var newLeft = newPost.Paths.Path;
                        var newPath = templateItem.Paths.Path.Replace("/sitecore/content/Home/Insights/Blogs/mdTest", newLeft);
                        var newItemsId = _db.GetItem(newPath);

                        node.Attributes["ds"].InnerText = newItemsId.ID.ToString();
                    }
                }

                if (node.Attributes["ph"].InnerText != null)
                {
                    node.Attributes["ph"].InnerText = node.Attributes["ph"].InnerText.Replace("{600CFC59-A345-44B8-A1AC-6A9BA64ED529}", newPh1Item.ID.ToString());
                    node.Attributes["ph"].InnerText = node.Attributes["ph"].InnerText.Replace("{FDBCA015-5B4B-4667-B084-6CDF6C4F28B7}", newPh2Item.ID.ToString());
                    node.Attributes["ph"].InnerText = node.Attributes["ph"].InnerText.Replace("{9E135461-3687-44A7-9F63-78D530CD5EE9}", newPh3Item.ID.ToString());
                }

            }

            return xmlDoc.OuterXml;
        }

        private void createPostComponent(Item item)
        {
            //create component and set datasource on post
        }

        private Item createItem(Item destinationParentItem, string templateId, string itemName)
        {

            using (new SecurityDisabler())
            {

                if (_db != null)
                {
                    TemplateItem template = _db.GetTemplate(new ID(templateId));
                    if (template != null && _db.GetItem(destinationParentItem?.Paths.Path + "/" + itemName) == null)
                    {
                        return destinationParentItem?.Add(itemName, template);
                    }
                    else
                    {
                        return _db.GetItem(destinationParentItem.Paths.Path + "/" + itemName);
                    }
                }
            }

            return null;
        }

        private void updateBlogTagsAndCategory(Item item, Item srcItem)
        {
            var newTagStrings = getNewTags(srcItem, item);
            
            using (new SecurityDisabler())
            {
                item.Editing.BeginEdit();
                item["Tags"] = newTagStrings;
                item["category"] = _blogCategory.Name;
                item[Sitecore.FieldIDs.Created] = srcItem[Sitecore.FieldIDs.Created];
                item.Editing.EndEdit();
            }
        }

        private string getNewTags(Item oldPost, Item newPost)
        {
            var oldTagStrings = oldPost["Tags"];
            var newTagStrings = string.Empty;

            if (!string.IsNullOrEmpty(oldTagStrings))
            {
                newTagStrings = getUpdatedTags(oldTagStrings);
            }

            return newTagStrings;
        }

        private string getUpdatedTags(string oldTagStrings)
        {
            JObject mappingJson = JObject.Parse(System.IO.File.ReadAllText(@"C:\projects\XC.com\Static\Flex.V2.Sitecore-master\src\External\code\Content\tagsConversionDictionary.json"));
            var dictionary = mappingJson.ToObject<Dictionary<string, string>>();

            var oldArray = oldTagStrings.Split('|').ToArray();
            var tagCount = 0;
            var newTagString = string.Empty;

            foreach(var id in oldArray)
            {
                var oldTag = _db.GetItem(id);
                var oldTagName = oldTag?.Name;

                if (dictionary.ContainsKey(oldTagName))
                {
                    var value = dictionary[oldTagName];
                    var newTagItem = _db.GetItem($"/sitecore/content/Global Settings/Taxonomy/{value}");

                    if(newTagItem != null)
                    {
                        if(tagCount == 0)
                        {
                            newTagString += newTagItem.ID.ToString();
                            tagCount++;
                        }
                        else
                        {
                            newTagString += $"|{newTagItem.ID.ToString()}";
                            tagCount++;
                        }
                        
                    }
                    
                }

            }

            newTagString = removeDuplicateGuids(newTagString);

            return newTagString;

        }

        private string removeDuplicateGuids(string newTagString)
        {
            string[] ids = newTagString.Split('|').Distinct().ToArray();
            var tagCount = 0;
            var dedupedString = string.Empty;
            foreach (var id in ids)
            {

                if (tagCount == 0)
                {
                    dedupedString += id;
                    tagCount++;
                }
                else
                {
                    dedupedString += $"|{id}";
                    tagCount++;
                }
            }

            return dedupedString;
        }

        private static string ToStringWithSuffix(DateTime dt)
        {
            // The format parameter MUST contain [$suffix] within it, which will be replaced.   
            int day = dt.Day; string suffix = "";
            // Exception for the 11th, 12th, & 13th   
            // (which would otherwise end up as 11st, 12nd, 13rd)   
            if (day % 100 >= 11 && day % 100 <= 13)
            {
                suffix = "th";
            }
            else
            {
                switch (day % 10)
                {
                    case 1:
                        suffix = "st";
                        break;
                    case 2:
                        suffix = "nd";
                        break;
                    case 3:
                        suffix = "rd";
                        break;
                    default:
                        suffix = "th";
                        break;
                }
            }
            // Convert the date to the format required, then add the suffix.  
            var cleanMonth = dt.ToString("MMMM");
            var cleanDay = dt.ToString("dd");
            var cleanYear = dt.ToString("yyyy");

            var dateFormatted = $"{cleanMonth} {cleanDay}{suffix}, {cleanYear}";
            return dateFormatted;
        }
        

        private string CleanRTE(string html)
        {
            var cleanMarkup = string.Empty;

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml("<html><body>" + html + "</body></html>");

            HtmlNode bodyContent = document.DocumentNode.SelectSingleNode("//body");
            var all_text = bodyContent.SelectNodes("//div | //ul | //p | //table | //img | //li | //span");

            if(all_text !=null)
            {
                foreach (var node in all_text)
                {
                    node.Attributes.Remove("class");
                    node.Attributes.Remove("id");
                    node.Attributes.Remove("stlye");
                    node.Attributes.Remove("height");
                    node.Attributes.Remove("width");
                }
            }

            //find brush class and encode it's contents
            var formattedNodes = bodyContent.SelectNodes("//pre[contains(@class, 'brush:xml')]");

            if (formattedNodes != null)
            {
                foreach (var node in formattedNodes)
                {
                    node.InnerHtml = HttpUtility.HtmlEncode(node.InnerHtml);
                }
            }

            cleanMarkup = document.DocumentNode.OuterHtml;
            cleanMarkup = cleanMarkup.Replace("<html><body>", "");
            cleanMarkup = cleanMarkup.Replace("</body></html>", "");
            cleanMarkup = cleanMarkup.Replace("&amp;", "&");

            return cleanMarkup;
        }

        public static string HTMLToText(string HTMLCode)
        {
            // Remove new lines since they are not visible in HTML
            HTMLCode = HTMLCode.Replace("\n", " ");

            // Remove tab spaces
            HTMLCode = HTMLCode.Replace("\t", " ");

            // Remove multiple white spaces from HTML
            HTMLCode = Regex.Replace(HTMLCode, "\\s+", " ");

            // Remove HEAD tag
            HTMLCode = Regex.Replace(HTMLCode, "<head.*?</head>", ""
                                , RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove any JavaScript
            HTMLCode = Regex.Replace(HTMLCode, "<script.*?</script>", ""
              , RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Replace special characters like &, <, >, " etc.
            StringBuilder sbHTML = new StringBuilder(HTMLCode);
            // Note: There are many more special characters, these are just
            // most common. You can add new characters in this arrays if needed
            string[] OldWords = {"&nbsp;", "&amp;", "&quot;", "&rsquo;","&lsquo;", "&lt;",
   "&gt;", "&reg;", "&copy;", "&trade;","&#39;","&hellip;"};
            string[] NewWords = { " ", "&", "\"", "\'", "\'", "<", ">", "®", "©", "™", "\'", "…" };
            for (int i = 0; i < OldWords.Length; i++)
            {
                sbHTML.Replace(OldWords[i], NewWords[i]);
            }

            // Check if there are line breaks (<br>) or paragraph (<p>)
            sbHTML.Replace("<br>", "\n<br>");
            sbHTML.Replace("<br ", "\n<br ");
            sbHTML.Replace("<p ", "\n<p ");

            // Finally, remove all HTML tags and return plain text
            return System.Text.RegularExpressions.Regex.Replace(
              sbHTML.ToString(), "<[^>]*>", "");
        }

    }
}
 