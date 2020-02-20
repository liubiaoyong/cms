﻿using SiteServer.Abstractions;
using SiteServer.CMS.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SiteServer.CMS.Repositories
{
    public partial class LogRepository
    {
        public  async Task AddAdminLogAsync(Administrator adminInfo, string action, string summary = "")
        {
            var config = await _configRepository.GetAsync();
            if (!config.IsLogAdmin) return;

            try
            {
                await DeleteIfThresholdAsync();

                if (!string.IsNullOrEmpty(action))
                {
                    action = WebUtils.MaxLengthText(action, 250);
                }
                if (!string.IsNullOrEmpty(summary))
                {
                    summary = WebUtils.MaxLengthText(summary, 250);
                }

                var logInfo = new Log
                {
                    Id = 0,
                    AdminId = adminInfo.Id,
                    IpAddress = string.Empty,
                    AddDate = DateTime.Now,
                    Action = action,
                    Summary = summary
                };

                await InsertAsync(logInfo);

                await _administratorRepository.UpdateLastActivityDateAsync(adminInfo);
            }
            catch (Exception ex)
            {
                await _errorLogRepository.AddErrorLogAsync(ex);
            }
        }
    }
}