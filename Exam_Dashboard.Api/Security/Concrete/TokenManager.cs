﻿using Exam_Dashboard.Api.Models;
using Exam_Dashboard.Api.Security.Abstract;

using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Exam_Dashboard.Api.Security.Concrete
{
    public class TokenManager : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> _userManager;

        public TokenManager(IConfiguration configuration, UserManager<User> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<Token> CreateAccessTokenAsync(User User, List<string> roles)
        {
            Token token = new();
            //var claims = await GetValidClaims(user);
            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                 new Claim("Id", User.Id.ToString()),
                     new Claim(JwtRegisteredClaimNames.Email, User.Email),
 

            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Token:SecurityKey"]));

            token.Expiration = DateTime.UtcNow.AddMinutes(2).AddHours(4);
            JwtSecurityToken securityToken = new(
                issuer: _configuration["Token:Audience"],
                audience: _configuration["Token:Issuer"],
                expires: token.Expiration,
                notBefore: DateTime.Now,
                claims: claims,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));


            JwtSecurityTokenHandler tokenHandler = new();

            token.AccessToken = tokenHandler.WriteToken(securityToken);
            
            token.RefreshToken = CreateRefreshToken();


            await _userManager.AddClaimsAsync(User, claims: claims);

            return token;
        }

        public string CreateRefreshToken()
        {
            byte[] number = new byte[32];
            using RandomNumberGenerator random = RandomNumberGenerator.Create();
            random.GetBytes(number);
            return Convert.ToBase64String(number);
        }

        public string TokenDecoded(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            return jsonToken==null? null:
                 jsonToken.Claims.FirstOrDefault(x => x.Type == "Id")?.Value;
               
            
        }

        public async Task<string> UpdateRefreshTokenAsync(string refreshToken, User user)
        {
            if (user is not null)
            {
                user.RefreshToken = refreshToken;

                user.RefreshTokenExpiredDate = DateTime.UtcNow.AddDays(1).AddHours(4);

                IdentityResult identityResult = await _userManager.UpdateAsync(user);
                string responseMessage = string.Empty;
                if (identityResult.Succeeded)
                    return refreshToken;
                else
                {
                    foreach (var error in identityResult.Errors)
                        responseMessage += $"{error.Description}. ";
                    return null;
                }
            }
            else
                return null;
        }

    }
}
