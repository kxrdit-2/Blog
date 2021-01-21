﻿using Meowv.Blog.Domain.Users;
using Meowv.Blog.Dto.Authorize;
using Meowv.Blog.Extensions;
using Meowv.Blog.Options.Authorize;
using Meowv.Blog.Users;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Volo.Abp.DependencyInjection;

namespace Meowv.Blog.Authorize.OAuth
{
    public class OAuthGithubService : IOAuthService<AccessTokenBase, UserInfoBase>, ITransientDependency
    {
        private readonly GithubOptions _githubOptions;
        private readonly IHttpClientFactory _httpClient;
        private readonly IUserService _userService;

        public OAuthGithubService(IOptions<GithubOptions> githubOptions, IHttpClientFactory httpClient, IUserService userService)
        {
            _githubOptions = githubOptions.Value;
            _httpClient = httpClient;
            _userService = userService;
        }

        public async Task<string> GetAuthorizeUrl(string state = "")
        {
            var param = BuildAuthorizeUrlParams(state);
            var url = $"{_githubOptions.AuthorizeUrl}?{param.ToQueryString()}";

            return await Task.FromResult(url);
        }

        public async Task<User> GetUserByOAuthAsync(string type, string code, string state)
        {
            var accessToken = await GetAccessTokenAsync(code, state);
            var userInfo = await GetUserInfoAsync(accessToken);

            return await _userService.CreateUserAsync(userInfo.Login, type, userInfo.Id, userInfo.Name, userInfo.Avatar, userInfo.Email);
        }

        public async Task<AccessTokenBase> GetAccessTokenAsync(string code, string state = "")
        {
            var param = BuildAccessTokenParams(code, state);

            var content = new StringContent(param.ToQueryString());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using var client = _httpClient.CreateClient();
            var httpResponse = await client.PostAsync(_githubOptions.AccessTokenUrl, content);

            var response = await httpResponse.Content.ReadAsStringAsync();

            var qscoll = HttpUtility.ParseQueryString(response);

            return new AccessTokenBase
            {
                AccessToken = qscoll["access_token"],
                Scope = qscoll["scope"],
                TokenType = qscoll["token_type"]
            };
        }

        public async Task<UserInfoBase> GetUserInfoAsync(AccessTokenBase accessToken)
        {
            using var client = _httpClient.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"token {accessToken.AccessToken}");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36 Edg/87.0.664.66");

            var response = await client.GetStringAsync(_githubOptions.UserInfoUrl);

            var userInfo = response.DeserializeToObject<UserInfoBase>();
            return userInfo;
        }

        protected Dictionary<string, string> BuildAuthorizeUrlParams(string state)
        {
            return new Dictionary<string, string>
            {
                ["client_id"] = _githubOptions.ClientId,
                ["redirect_uri"] = _githubOptions.RedirectUrl,
                ["scope"] = _githubOptions.Scope,
                ["state"] = state
            };
        }

        protected Dictionary<string, string> BuildAccessTokenParams(string code, string state)
        {
            return new Dictionary<string, string>()
            {
                ["client_id"] = _githubOptions.ClientId,
                ["client_secret"] = _githubOptions.ClientSecret,
                ["redirect_uri"] = _githubOptions.RedirectUrl,
                ["code"] = code,
                ["state"] = state
            };
        }
    }
}