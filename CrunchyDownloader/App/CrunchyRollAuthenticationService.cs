﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchyDownloader.Exceptions;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace CrunchyDownloader.App
{
    public class CrunchyRollAuthenticationService
    {
        public CrunchyRollAuthenticationService(Browser browser, ILogger<CrunchyRollAuthenticationService> logger)
        {
            Browser = browser;
            Logger = logger;
        }

        private Browser Browser { get; }
        
        private ILogger<CrunchyRollAuthenticationService> Logger { get; }
        
        public async Task<string> GetCookies(string userName, string password)
        {
            await using var loginPage = await Browser.NewPageAsync();
            await loginPage.GoToAsync("https://www.crunchyroll.com/login");
            
            await using var emailInput = await loginPage.QuerySelectorAsync("#login_form_name");
            await emailInput.TypeAsync(userName);

            await using var passwordInput = await loginPage.QuerySelectorAsync("#login_form_password");
            await passwordInput.TypeAsync(password);
            
            var captcha = await loginPage.XPathAsync("//*[@id=\"recaptcha-anchor-label\"]");
            if (captcha.Any())
            {
                Logger.LogWarning("Please solve the captcha and press any key to continue");
                Console.ReadLine();
            }

            await using var loginButton = await loginPage.QuerySelectorAsync("#login_submit_button");
            await loginButton.ClickAsync();

            await loginPage.WaitForNavigationAsync();

            var errors = await loginPage.XPathAsync("//*[@id=\"login_form\"]/ul[@class='messages']/li[@class='error']");
            if (errors.Any())
            {
                throw new InvalidLoginException(userName, password);
            }

            var cookies = await loginPage.GetCookiesAsync();
            var cookieStringBuilder = new StringBuilder();
            cookieStringBuilder.AppendLine("# Netscape HTTP Cookie File");
            
            foreach (var cookie in cookies)
            {
                var expireAt = cookie.Expires.HasValue ? (int) (cookie.Expires.Value < 0 ? 0 : cookie.Expires.Value) : 0;
                cookieStringBuilder.Append(cookie.Domain);
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append("FALSE");
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append(cookie.Path);
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append(cookie.HttpOnly ?? false ? "TRUE" : "FALSE");
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append(expireAt);
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append(cookie.Name);
                cookieStringBuilder.Append('\t');
                cookieStringBuilder.Append(cookie.Value);
                cookieStringBuilder.Append(Environment.NewLine);
            }

            return cookieStringBuilder.ToString();
        }
    }
}