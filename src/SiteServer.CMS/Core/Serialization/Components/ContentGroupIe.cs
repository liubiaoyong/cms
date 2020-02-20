﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SiteServer.Abstractions;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Repositories;
using SiteServer.CMS.Serialization.Atom.Atom.Core;

namespace SiteServer.CMS.Serialization.Components
{
    public static class ContentGroupIe
    {
        public static AtomEntry Export(ContentGroup @group)
        {
            var entry = AtomUtility.GetEmptyEntry();

            AtomUtility.AddDcElement(entry.AdditionalElements, "IsContentGroup", true.ToString());
            AtomUtility.AddDcElement(entry.AdditionalElements, new List<string> { nameof(ContentGroup.GroupName), "ContentGroupName" }, @group.GroupName);
            AtomUtility.AddDcElement(entry.AdditionalElements, nameof(ContentGroup.Taxis), @group.Taxis.ToString());
            AtomUtility.AddDcElement(entry.AdditionalElements, nameof(ContentGroup.Description), @group.Description);

            return entry;
        }

        public static async Task ImportAsync(AtomFeed feed, int siteId, string guid)
        {
            var groups = new List<ContentGroup>();

            foreach (AtomEntry entry in feed.Entries)
            {
                var isNodeGroup = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(entry.AdditionalElements, "IsContentGroup"));
                if (!isNodeGroup) continue;

                var groupName = AtomUtility.GetDcElementContent(entry.AdditionalElements, new List<string> { nameof(ContentGroup.GroupName), "ContentGroupName" });
                if (string.IsNullOrEmpty(groupName)) continue;

                if (await DataProvider.ContentGroupRepository.IsExistsAsync(siteId, groupName)) continue;

                var taxis = TranslateUtils.ToInt(AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(ContentGroup.Taxis)));
                var description = AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(ContentGroup.Description));
                groups.Add(new ContentGroup
                {
                    GroupName = groupName,
                    SiteId = siteId,
                    Taxis = taxis,
                    Description = description
                });
            }

            foreach (var group in groups)
            {
                Caching.SetProcess(guid, $"导入内容组: {group.GroupName}");
                await DataProvider.ContentGroupRepository.InsertAsync(group);
            }
        }
    }
}