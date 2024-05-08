﻿using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using WebApp.Services;

namespace WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly MongoDBService<User> _userService;
        private readonly MongoDBService<Course> _courseService;

        public AccountController(ILogger<AccountController> logger, MongoDBService<User> settings, MongoDBService<Course> courseService)
        {
            _logger = logger;
            _userService = settings;
            _courseService = courseService;
        }

        [AllowAnonymous]
        [HttpGet]
        public ActionResult SignIn() => View();

        [AllowAnonymous]
        [HttpPost("/account/signin")]
        public async Task<ActionResult> SignInAsync([FromForm] User user)
        {
            List<User> users = await _userService.GetAsync();
            User? foundedUser = users.FirstOrDefault(u => u.Username == user.Username && u.Password == user.Password);

            if (foundedUser == null) return BadRequest();

            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, foundedUser.Id),
                new Claim(ClaimTypes.Role, foundedUser.Role)
            };

            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "Cookies");

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
            _logger.LogInformation("Authorization has been successful");

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public ActionResult Register() => View();

        [Authorize]
        public async Task<ActionResult> LogOutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet("user/{id}")]
        public async Task<IActionResult> UserProfile(string id = null)
        {
            if (id == null) id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var user = await _userService.GetAsync(id);
            if (user == null) return Ok(user);

            var createdCourses = new List<Course>();
            foreach (var courseId in user.CreatedCourses)
            {
                var course = await _courseService.GetAsync(courseId);
                if (course != null) createdCourses.Add(course);
            }

            var enrolledCourses = new List<Course>();
            foreach (var courseId in user.EnrolledCourses)
            {
                var course = await _courseService.GetAsync(courseId);
                if (course != null)
                {
                    enrolledCourses.Add(course);
                }
            }

            var model = new UserProfileViewModel
            {
                User = user,
                CreatedCourses = createdCourses,
                EnrolledCourses = enrolledCourses
            };

            return View(model);
        }
        
        [Authorize]
        [HttpPost("user/{id}")]
        public async Task<IActionResult> UpdateProfile(User model, string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != id)
            {
                return Forbid();
            }

            var user = await _userService.GetAsync(id);
            if (user == null) return NotFound();

            if (model.Email != null)
            {
                user.Email = model.Email;
            }

            if (model.Username != null)
            {
                user.Username = model.Username;
            }

            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = model.Password;
            }

            user.AllowAccessToAgeRestrictedContent = model.AllowAccessToAgeRestrictedContent;
            user.UseDataToImproveIShariu = model.UseDataToImproveIShariu;
            
            if (model.ProfileColor != null)
            {
                user.ProfileColor = model.ProfileColor;
            }

            await _userService.PutAsync(user);

            return RedirectToAction("UserProfile", new { id = model.Id });
        }
        
        [Authorize]
        [HttpGet("user/{id}/settings")]
        public async Task<IActionResult> UserSettings(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != id) return Forbid();

            var user = await _userService.GetAsync(id);
            if (user == null) return NotFound();

            // Fetch the list of countries
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("https://restcountries.com/v3.1/all");
            var responseString = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            var countries = JsonSerializer.Deserialize<List<Country>>(responseString, options);

            // Pass the list of countries to the view
            ViewData["Countries"] = countries;

            return View(user);
        }
        
        [Authorize]
        [HttpPost("/account/changepassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.GetAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (user.Password != model.CurrentPassword)
            {
                return Json(new { success = false, message = "Current password is incorrect." });
            }

            if (model.NewPassword == model.CurrentPassword)
            {
                return Json(new { success = false, message = "New password cannot be the same as the current password." });
            }

            user.Password = model.NewPassword;
            await _userService.PutAsync(user);

            return Json(new { success = true });
        }
        
        [Authorize]
        [HttpPost("/account/updatesetting")]
        public async Task<IActionResult> UpdateSetting([FromBody] UpdateSettingModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.GetAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            switch (model.SettingName)
            {
                case "AllowAccessToAgeRestrictedContent":
                    user.AllowAccessToAgeRestrictedContent = model.SettingValue;
                    break;
                case "UseDataToImproveIShariu":
                    user.UseDataToImproveIShariu = model.SettingValue;
                    break;
                default:
                    return BadRequest();
            }

            await _userService.PutAsync(user);

            return Ok();
        }
        
        [Authorize]
        [HttpPost("/account/deleteaccount")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.GetAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (user.Password != model.Password)
            {
                return Json(new { success = false, message = "Incorrect password." });
            }

            await _userService.DeleteAsync(user.Id);

            // Sign out the user
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Json(new { success = true });
        }

        // [HttpGet("courses")]
        // public async Task<IActionResult> Courses()
        // {
        //     // Implement your courses logic here...
        //     return View();
        // }
        
        
    }
}