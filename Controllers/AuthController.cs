using CyberChatDemo.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace CyberChatDemo.Controllers
{
    public class AuthController : Controller
    {
        private readonly FirestoreDb _firestoreDb;

        public AuthController(
            FirestoreDb firestoreDb)
        {
            _firestoreDb =
                firestoreDb;
        }


        // =========================================================
        // LOGIN PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        // =========================================================
        // LOGIN / CREATE ACCOUNT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string instagramId,
            string demoPassword,
            string displayName)
        {
            instagramId =
                NormalizeUsername(
                    instagramId);

            demoPassword =
                demoPassword?.Trim()
                ?? "";

            displayName =
                displayName?.Trim()
                ?? "";


            // =====================================================
            // BASIC VALIDATION
            // =====================================================

            if (string.IsNullOrWhiteSpace(
                    instagramId))
            {
                ViewBag.Error =
                    "Please enter a username.";

                return View();
            }


            if (string.IsNullOrWhiteSpace(
                    demoPassword))
            {
                ViewBag.Error =
                    "Please enter your password.";

                return View();
            }


            if (instagramId.Length < 2)
            {
                ViewBag.Error =
                    "Username must contain at least 2 characters.";

                return View();
            }


            if (instagramId.Length > 50)
            {
                ViewBag.Error =
                    "Username cannot contain more than 50 characters.";

                return View();
            }


            if (demoPassword.Length > 200)
            {
                ViewBag.Error =
                    "Password is too long.";

                return View();
            }


            if (string.IsNullOrWhiteSpace(
                    displayName))
            {
                displayName =
                    instagramId;
            }


            CollectionReference usersCollection =
                _firestoreDb
                    .Collection("users");


            // =====================================================
            // FIND EXISTING USER
            //
            // IMPORTANT:
            //
            // Firestore string comparisons are case-sensitive.
            //
            // So:
            //
            // jack
            // JACK
            // Jack
            //
            // could otherwise become different accounts.
            //
            // We load the users and compare normalized usernames.
            // =====================================================

            QuerySnapshot usersSnapshot =
                await usersCollection
                    .GetSnapshotAsync();


            DocumentSnapshot? existingDocument =
                null;

            AppUser? existingUser =
                null;


            foreach (DocumentSnapshot document
                     in usersSnapshot.Documents)
            {
                AppUser databaseUser;

                try
                {
                    databaseUser =
                        document.ConvertTo<AppUser>();
                }
                catch
                {
                    continue;
                }


                string databaseUsername =
                    NormalizeUsername(
                        databaseUser.InstagramId);


                if (databaseUsername.Equals(
                        instagramId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingDocument =
                        document;

                    existingUser =
                        databaseUser;

                    break;
                }
            }


            AppUser user;


            // =====================================================
            // EXISTING USER
            // =====================================================

            if (existingDocument != null &&
                existingUser != null)
            {
                // -------------------------------------------------
                // SAME USERNAME ALREADY EXISTS
                //
                // Do NOT create another account.
                // Password must match the existing account.
                // -------------------------------------------------

                if (existingUser.DemoPassword
                    != demoPassword)
                {
                    ViewBag.Error =
                        "This username already exists. Enter the correct password to log in.";

                    return View();
                }


                Dictionary<string, object> updates =
                    new Dictionary<string, object>
                    {
                        {
                            "IsOnline",
                            true
                        },

                        {
                            "LastSeen",
                            Timestamp.GetCurrentTimestamp()
                        },

                        {
                            "InstagramId",
                            instagramId
                        }
                    };


                await existingDocument
                    .Reference
                    .UpdateAsync(
                        updates);


                existingUser.Id =
                    existingDocument.Id;

                existingUser.InstagramId =
                    instagramId;


                user =
                    existingUser;
            }


            // =====================================================
            // NEW USER
            // =====================================================

            else
            {
                DocumentReference newUserDocument =
                    usersCollection
                        .Document();


                Timestamp now =
                    Timestamp.GetCurrentTimestamp();


                user =
                    new AppUser
                    {
                        Id =
                            newUserDocument.Id,

                        InstagramId =
                            instagramId,

                        DemoPassword =
                            demoPassword,

                        DisplayName =
                            displayName,

                        Role =
                            "User",

                        IsOnline =
                            true,

                        CreatedAt =
                            now,

                        LastSeen =
                            now
                    };


                await newUserDocument
                    .SetAsync(
                        user);
            }


            // =====================================================
            // CREATE SESSION
            // =====================================================

            HttpContext.Session.SetString(
                "UserId",
                user.Id);


            HttpContext.Session.SetString(
                "InstagramId",
                user.InstagramId);


            HttpContext.Session.SetString(
                "DisplayName",
                user.DisplayName);


            HttpContext.Session.SetString(
                "Role",
                user.Role);


            // =====================================================
            // CHAT
            // =====================================================

            return RedirectToAction(
                "Index",
                "Chat");
        }


        // =========================================================
        // NORMALIZE USERNAME
        // =========================================================

        private static string NormalizeUsername(
            string? username)
        {
            username =
                username?.Trim()
                ?? "";


            // Remove @ from beginning.
            //
            // @jack
            // jack
            //
            // become the same username.

            username =
                username.TrimStart('@');


            // Usernames are stored lowercase.
            //
            // JACK
            // Jack
            // jack
            //
            // become:
            //
            // jack

            username =
                username.ToLowerInvariant();


            return username;
        }
    }
}