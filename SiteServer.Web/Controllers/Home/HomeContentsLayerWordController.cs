﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;
using Datory.Utils;
using SiteServer.Abstractions;
using SiteServer.API.Context;
using SiteServer.CMS.Core;
using SiteServer.CMS.Core.Office;
using SiteServer.CMS.Framework;

namespace SiteServer.API.Controllers.Home
{
    
    [RoutePrefix("home/contentsLayerWord")]
    public class HomeContentsLayerWordController : ApiController
    {
        private const string Route = "";
        private const string RouteUpload = "actions/upload";

        private readonly ICreateManager _createManager;
        
        public HomeContentsLayerWordController(ICreateManager createManager)
        {
            _createManager = createManager;
        }

        [HttpGet, Route(Route)]
        public async Task<IHttpActionResult> GetConfig()
        {
            var request = await AuthenticatedRequest.GetAuthAsync();

            var siteId = request.GetQueryInt("siteId");
            var channelId = request.GetQueryInt("channelId");

            if (!request.IsUserLoggin ||
                !await request.UserPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                    Constants.ChannelPermissions.ContentAdd))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

            var (isChecked, checkedLevel) = await CheckManager.GetUserCheckLevelAsync(request.AdminPermissionsImpl, site, siteId);
            var checkedLevels = CheckManager.GetCheckedLevels(site, isChecked, checkedLevel, false);

            return Ok(new
            {
                Value = checkedLevels,
                CheckedLevel = CheckManager.LevelInt.CaoGao
            });
        }

        [HttpPost, Route(RouteUpload)]
        public async Task<IHttpActionResult> Upload()
        {
            var request = await AuthenticatedRequest.GetAuthAsync();

            var siteId = request.GetQueryInt("siteId");
            var channelId = request.GetQueryInt("channelId");

            if (!request.IsUserLoggin ||
                !await request.UserPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                    Constants.ChannelPermissions.ContentAdd))
            {
                return Unauthorized();
            }

            var fileName = request.HttpRequest["fileName"];

            var fileCount = request.HttpRequest.Files.Count;

            string filePath = null;

            if (fileCount > 0)
            {
                var file = request.HttpRequest.Files[0];

                if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(file.FileName);

                var extendName = fileName.Substring(fileName.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                if (extendName == ".doc" || extendName == ".docx")
                {
                    filePath = PathUtility.GetTemporaryFilesPath(fileName);
                    DirectoryUtils.CreateDirectoryIfNotExists(filePath);
                    file.SaveAs(filePath);
                }
            }

            FileInfo fileInfo = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                fileInfo = new FileInfo(filePath);
            }
            if (fileInfo != null)
            {
                return Ok(new
                {
                    fileName,
                    length = fileInfo.Length,
                    ret = 1
                });
            }

            return Ok(new
            {
                ret = 0
            });
        }

        [HttpPost, Route(Route)]
        public async Task<IHttpActionResult> Submit()
        {
            var request = await AuthenticatedRequest.GetAuthAsync();

            var siteId = request.GetPostInt("siteId");
            var channelId = request.GetPostInt("channelId");
            var isFirstLineTitle = request.GetPostBool("isFirstLineTitle");
            var isFirstLineRemove = request.GetPostBool("isFirstLineRemove");
            var isClearFormat = request.GetPostBool("isClearFormat");
            var isFirstLineIndent = request.GetPostBool("isFirstLineIndent");
            var isClearFontSize = request.GetPostBool("isClearFontSize");
            var isClearFontFamily = request.GetPostBool("isClearFontFamily");
            var isClearImages = request.GetPostBool("isClearImages");
            var checkedLevel = request.GetPostInt("checkedLevel");
            var fileNames = Utilities.GetStringList(request.GetPostString("fileNames"));

            if (!request.IsUserLoggin ||
                !await request.UserPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                    Constants.ChannelPermissions.ContentAdd))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

            var tableName = await DataProvider.ChannelRepository.GetTableNameAsync(site, channelInfo);
            var styleList = await DataProvider.TableStyleRepository.GetContentStyleListAsync(channelInfo, tableName);
            var isChecked = checkedLevel >= site.CheckContentLevel;

            var contentIdList = new List<int>();

            foreach (var fileName in fileNames)
            {
                if (string.IsNullOrEmpty(fileName)) continue;

                var filePath = PathUtility.GetTemporaryFilesPath(fileName);
                var (title, content) = await WordManager.GetWordAsync(site, isFirstLineTitle, isClearFormat, isFirstLineIndent, isClearFontSize, isClearFontFamily, isClearImages, filePath);

                if (string.IsNullOrEmpty(title)) continue;

                var dict = await ColumnsManager.SaveAttributesAsync(site, styleList, new NameValueCollection(), ContentAttribute.AllAttributes.Value);

                var contentInfo = new Content(dict)
                {
                    ChannelId = channelInfo.Id,
                    SiteId = siteId,
                    AddDate = DateTime.Now,
                    SourceId = SourceManager.User,
                    AdminId = request.AdminId,
                    UserId = request.UserId,
                    LastEditAdminId = request.AdminId,
                    Checked = isChecked,
                    CheckedLevel = checkedLevel
                };

                contentInfo.LastEditDate = contentInfo.AddDate;

                contentInfo.Title = title;
                contentInfo.Set(ContentAttribute.Content, content);

                contentInfo.Id = await DataProvider.ContentRepository.InsertAsync(site, channelInfo, contentInfo);

                contentIdList.Add(contentInfo.Id);
            }

            if (isChecked)
            {
                foreach (var contentId in contentIdList)
                {
                    await _createManager.CreateContentAsync(siteId, channelInfo.Id, contentId);
                }
                await _createManager.TriggerContentChangedEventAsync(siteId, channelInfo.Id);
            }

            return Ok(new
            {
                Value = contentIdList
            });
        }
    }
}