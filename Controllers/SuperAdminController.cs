using CyberChatDemo.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace CyberChatDemo.Controllers
{
    [Route("SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly IConfiguration _configuration;

        public SuperAdminController(
            FirestoreDb firestoreDb,
            IConfiguration configuration)
        {
            _firestoreDb = firestoreDb;
            _configuration = configuration;
        }


        // =========================================================
        // LOGIN PAGE
        // =========================================================

        [HttpGet("Login")]
        public IActionResult Login()
        {
            if (IsSuperAdminLoggedIn())
            {
                return RedirectToAction("Index");
            }

            return View();
        }


        // =========================================================
        // LOGIN
        // =========================================================

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public IActionResult Login(
            string username,
            string password)
        {
            username = username?.Trim() ?? "";
            password = password ?? "";

            string expectedUsername =
                _configuration["SuperAdmin:Username"]
                ?? "";

            string expectedPassword =
                _configuration["SuperAdmin:Password"]
                ?? "";


            if (string.IsNullOrWhiteSpace(expectedUsername) ||
                string.IsNullOrWhiteSpace(expectedPassword))
            {
                ViewBag.Error =
                    "SuperAdmin credentials are not configured.";

                return View();
            }


            if (username != expectedUsername ||
                password != expectedPassword)
            {
                ViewBag.Error =
                    "Incorrect SuperAdmin username or password.";

                return View();
            }


            HttpContext.Session.SetString(
                "SuperAdminAuthenticated",
                "true");

            HttpContext.Session.SetString(
                "SuperAdminUsername",
                username);


            return RedirectToAction("Index");
        }


        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(
                "SuperAdminAuthenticated");

            HttpContext.Session.Remove(
                "SuperAdminUsername");

            return RedirectToAction("Login");
        }


        // =========================================================
        // USER LIST + SEARCH
        // =========================================================

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            string? search)
        {
            if (!IsSuperAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }


            QuerySnapshot snapshot =
                await _firestoreDb
                    .Collection("users")
                    .GetSnapshotAsync();


            List<SuperAdminUserViewModel> users =
                new List<SuperAdminUserViewModel>();


            foreach (DocumentSnapshot document
                     in snapshot.Documents)
            {
                AppUser user =
                    document.ConvertTo<AppUser>();


                users.Add(
                    new SuperAdminUserViewModel
                    {
                        Id =
                            document.Id,

                        InstagramId =
                            user.InstagramId ?? "",

                        DisplayName =
                            user.DisplayName ?? "",

                        DemoPassword =
                            user.DemoPassword ?? ""
                    });
            }


            // =====================================================
            // TOTAL BEFORE SEARCH
            // =====================================================

            int totalUsers =
                users.Count;


            // =====================================================
            // SEARCH
            //
            // Search works with:
            // jack
            // @jack
            // display name
            //
            // It is case insensitive.
            // =====================================================

            search =
                search?.Trim() ?? "";


            if (!string.IsNullOrWhiteSpace(search))
            {
                string cleanSearch =
                    search.TrimStart('@');


                users =
                    users
                        .Where(
                            x =>
                                (
                                    x.InstagramId ?? ""
                                )
                                .Contains(
                                    cleanSearch,
                                    StringComparison.OrdinalIgnoreCase)

                                ||

                                (
                                    x.DisplayName ?? ""
                                )
                                .Contains(
                                    cleanSearch,
                                    StringComparison.OrdinalIgnoreCase)
                        )
                        .ToList();
            }


            // =====================================================
            // ALPHABETICAL ORDER
            // =====================================================

            users =
                users
                    .OrderBy(
                        x => x.DisplayName)
                    .ThenBy(
                        x => x.InstagramId)
                    .ToList();


            ViewBag.SearchQuery =
                search;

            ViewBag.TotalUsers =
                totalUsers;


            SuperAdminIndexViewModel model =
                new SuperAdminIndexViewModel
                {
                    Users = users
                };


            return View(model);
        }


        // =========================================================
        // DELETE USER
        // =========================================================

        [HttpPost("User/{userId}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(
            string userId)
        {
            if (!IsSuperAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }


            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["AdminError"] =
                    "Invalid user.";

                return RedirectToAction("Index");
            }


            try
            {
                DocumentReference userReference =
                    _firestoreDb
                        .Collection("users")
                        .Document(userId);


                DocumentSnapshot userDocument =
                    await userReference
                        .GetSnapshotAsync();


                if (!userDocument.Exists)
                {
                    TempData["AdminError"] =
                        "User does not exist.";

                    return RedirectToAction("Index");
                }


                AppUser deletingUser =
                    userDocument
                        .ConvertTo<AppUser>();


                string deletedUsername =
                    deletingUser.InstagramId ?? "User";


                // =================================================
                // DELETE CONVERSATIONS + THEIR MESSAGES
                // =================================================

                QuerySnapshot conversations =
                    await _firestoreDb
                        .Collection("conversations")
                        .WhereArrayContains(
                            "Participants",
                            userId)
                        .GetSnapshotAsync();


                foreach (DocumentSnapshot conversation
                         in conversations.Documents)
                {
                    // Firestore does NOT automatically delete
                    // subcollections when parent is deleted.
                    // So messages must be deleted first.

                    QuerySnapshot messages =
                        await conversation
                            .Reference
                            .Collection("messages")
                            .GetSnapshotAsync();


                    foreach (DocumentSnapshot message
                             in messages.Documents)
                    {
                        await message
                            .Reference
                            .DeleteAsync();
                    }


                    await conversation
                        .Reference
                        .DeleteAsync();
                }


                // =================================================
                // DELETE FRIENDSHIPS
                // =================================================

                QuerySnapshot friendships =
                    await _firestoreDb
                        .Collection("friendships")
                        .WhereArrayContains(
                            "Users",
                            userId)
                        .GetSnapshotAsync();


                foreach (DocumentSnapshot friendship
                         in friendships.Documents)
                {
                    await friendship
                        .Reference
                        .DeleteAsync();
                }


                // =================================================
                // DELETE FRIEND REQUESTS SENT BY USER
                // =================================================

                QuerySnapshot sentRequests =
                    await _firestoreDb
                        .Collection("friendRequests")
                        .WhereEqualTo(
                            "SenderId",
                            userId)
                        .GetSnapshotAsync();


                HashSet<string> deletedRequestIds =
                    new HashSet<string>();


                foreach (DocumentSnapshot request
                         in sentRequests.Documents)
                {
                    await request
                        .Reference
                        .DeleteAsync();


                    deletedRequestIds.Add(
                        request.Id);
                }


                // =================================================
                // DELETE FRIEND REQUESTS RECEIVED BY USER
                // =================================================

                QuerySnapshot receivedRequests =
                    await _firestoreDb
                        .Collection("friendRequests")
                        .WhereEqualTo(
                            "ReceiverId",
                            userId)
                        .GetSnapshotAsync();


                foreach (DocumentSnapshot request
                         in receivedRequests.Documents)
                {
                    if (deletedRequestIds.Contains(
                            request.Id))
                    {
                        continue;
                    }


                    await request
                        .Reference
                        .DeleteAsync();
                }


                // =================================================
                // FINALLY DELETE USER
                // =================================================

                await userReference
                    .DeleteAsync();


                TempData["AdminMessage"] =
                    $"@{deletedUsername} was deleted successfully.";
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Delete user error:");

                Console.WriteLine(
                    exception);


                TempData["AdminError"] =
                    "The account could not be deleted. Please try again.";
            }


            return RedirectToAction("Index");
        }


        // =========================================================
        // USER CONVERSATIONS
        // =========================================================

        [HttpGet("User/{userId}/Conversations")]
        public async Task<IActionResult> Conversations(
            string userId)
        {
            if (!IsSuperAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }


            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("Index");
            }


            DocumentSnapshot selectedUserDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(userId)
                    .GetSnapshotAsync();


            if (!selectedUserDocument.Exists)
            {
                return RedirectToAction("Index");
            }


            AppUser selectedUser =
                selectedUserDocument
                    .ConvertTo<AppUser>();


            QuerySnapshot conversationsSnapshot =
                await _firestoreDb
                    .Collection("conversations")
                    .WhereArrayContains(
                        "Participants",
                        userId)
                    .GetSnapshotAsync();


            List<SuperAdminConversationItemViewModel>
                conversations =
                    new List<SuperAdminConversationItemViewModel>();


            foreach (DocumentSnapshot document
                     in conversationsSnapshot.Documents)
            {
                if (!document.ContainsField(
                        "Participants"))
                {
                    continue;
                }


                List<string> participants;

                try
                {
                    participants =
                        document
                            .GetValue<List<string>>(
                                "Participants");
                }
                catch
                {
                    continue;
                }


                string otherUserId =
                    participants
                        .FirstOrDefault(
                            x => x != userId)
                    ?? "";


                if (string.IsNullOrWhiteSpace(
                        otherUserId))
                {
                    continue;
                }


                DocumentSnapshot otherUserDocument =
                    await _firestoreDb
                        .Collection("users")
                        .Document(otherUserId)
                        .GetSnapshotAsync();


                string otherDisplayName =
                    "Unknown User";

                string otherInstagramId =
                    "unknown";


                if (otherUserDocument.Exists)
                {
                    AppUser otherUser =
                        otherUserDocument
                            .ConvertTo<AppUser>();


                    otherDisplayName =
                        otherUser.DisplayName;

                    otherInstagramId =
                        otherUser.InstagramId;
                }


                string lastMessage =
                    "";

                DateTime? updatedAt =
                    null;


                if (document.ContainsField(
                        "LastMessage"))
                {
                    lastMessage =
                        document.GetValue<string>(
                            "LastMessage");
                }


                if (document.ContainsField(
                        "UpdatedAt"))
                {
                    Timestamp timestamp =
                        document.GetValue<Timestamp>(
                            "UpdatedAt");


                    updatedAt =
                        timestamp
                            .ToDateTime()
                            .ToLocalTime();
                }


                conversations.Add(
                    new SuperAdminConversationItemViewModel
                    {
                        ConversationId =
                            document.Id,

                        OtherUserId =
                            otherUserId,

                        OtherDisplayName =
                            otherDisplayName,

                        OtherInstagramId =
                            otherInstagramId,

                        LastMessage =
                            lastMessage,

                        UpdatedAt =
                            updatedAt
                    });
            }


            conversations =
                conversations
                    .OrderByDescending(
                        x => x.UpdatedAt)
                    .ToList();


            SuperAdminConversationsViewModel model =
                new SuperAdminConversationsViewModel
                {
                    SelectedUserId =
                        userId,

                    SelectedInstagramId =
                        selectedUser.InstagramId,

                    SelectedDisplayName =
                        selectedUser.DisplayName,

                    Conversations =
                        conversations
                };


            return View(model);
        }


        // =========================================================
        // VIEW CONVERSATION MESSAGES
        // =========================================================

        [HttpGet("Conversation/{conversationId}")]
        public async Task<IActionResult> Messages(
            string conversationId)
        {
            if (!IsSuperAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }


            if (string.IsNullOrWhiteSpace(
                    conversationId))
            {
                return RedirectToAction("Index");
            }


            DocumentReference conversationReference =
                _firestoreDb
                    .Collection("conversations")
                    .Document(conversationId);


            DocumentSnapshot conversation =
                await conversationReference
                    .GetSnapshotAsync();


            if (!conversation.Exists)
            {
                return RedirectToAction("Index");
            }


            List<string> participants;

            try
            {
                participants =
                    conversation
                        .GetValue<List<string>>(
                            "Participants");
            }
            catch
            {
                return RedirectToAction("Index");
            }


            if (participants.Count < 2)
            {
                return RedirectToAction("Index");
            }


            string userOneId =
                participants[0];

            string userTwoId =
                participants[1];


            // =====================================================
            // USER ONE
            // =====================================================

            DocumentSnapshot userOneDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(userOneId)
                    .GetSnapshotAsync();


            string userOneName =
                "Unknown User";

            string userOneInstagram =
                "unknown";


            if (userOneDocument.Exists)
            {
                AppUser userOne =
                    userOneDocument
                        .ConvertTo<AppUser>();


                userOneName =
                    userOne.DisplayName;

                userOneInstagram =
                    userOne.InstagramId;
            }


            // =====================================================
            // USER TWO
            // =====================================================

            DocumentSnapshot userTwoDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(userTwoId)
                    .GetSnapshotAsync();


            string userTwoName =
                "Unknown User";

            string userTwoInstagram =
                "unknown";


            if (userTwoDocument.Exists)
            {
                AppUser userTwo =
                    userTwoDocument
                        .ConvertTo<AppUser>();


                userTwoName =
                    userTwo.DisplayName;

                userTwoInstagram =
                    userTwo.InstagramId;
            }


            // =====================================================
            // LOAD MESSAGES
            // =====================================================

            QuerySnapshot messageSnapshot =
                await conversationReference
                    .Collection("messages")
                    .OrderBy("SentAt")
                    .GetSnapshotAsync();


            List<SuperAdminMessageItemViewModel> messages =
                new List<SuperAdminMessageItemViewModel>();


            foreach (DocumentSnapshot document
                     in messageSnapshot.Documents)
            {
                string senderId =
                    document.ContainsField(
                        "SenderId")
                    ? document.GetValue<string>(
                        "SenderId")
                    : "";


                string text =
                    document.ContainsField(
                        "Text")
                    ? document.GetValue<string>(
                        "Text")
                    : "";


                Timestamp timestamp =
                    document.ContainsField(
                        "SentAt")
                    ? document.GetValue<Timestamp>(
                        "SentAt")
                    : Timestamp.GetCurrentTimestamp();


                string senderName;


                if (senderId == userOneId)
                {
                    senderName =
                        userOneName;
                }
                else if (senderId == userTwoId)
                {
                    senderName =
                        userTwoName;
                }
                else
                {
                    senderName =
                        "Unknown";
                }


                messages.Add(
                    new SuperAdminMessageItemViewModel
                    {
                        Id =
                            document.Id,

                        SenderId =
                            senderId,

                        SenderName =
                            senderName,

                        Text =
                            text,

                        SentAt =
                            timestamp
                                .ToDateTime()
                                .ToLocalTime()
                    });
            }


            SuperAdminMessagesViewModel model =
                new SuperAdminMessagesViewModel
                {
                    ConversationId =
                        conversationId,

                    UserOneId =
                        userOneId,

                    UserOneName =
                        userOneName,

                    UserOneInstagramId =
                        userOneInstagram,

                    UserTwoId =
                        userTwoId,

                    UserTwoName =
                        userTwoName,

                    UserTwoInstagramId =
                        userTwoInstagram,

                    Messages =
                        messages
                };


            return View(model);
        }


        // =========================================================
        // AUTH CHECK
        // =========================================================

        private bool IsSuperAdminLoggedIn()
        {
            return HttpContext
                .Session
                .GetString(
                    "SuperAdminAuthenticated")
                == "true";
        }
    }
}