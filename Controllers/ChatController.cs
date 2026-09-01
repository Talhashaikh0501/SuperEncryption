using CyberChatDemo.Hubs;
using CyberChatDemo.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CyberChatDemo.Controllers
{
    public class ChatController : Controller
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly IHubContext<ChatHub> _chatHub;


        public ChatController(
            FirestoreDb firestoreDb,
            IHubContext<ChatHub> chatHub)
        {
            _firestoreDb = firestoreDb;
            _chatHub = chatHub;
        }


        // =========================================================
        // USER PANEL
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            string currentInstagramId =
                HttpContext.Session.GetString("InstagramId")
                ?? "";


            string currentDisplayName =
                HttpContext.Session.GetString("DisplayName")
                ?? currentInstagramId;


            // =====================================================
            // LOAD ALL USERS
            // =====================================================

            QuerySnapshot usersSnapshot =
                await _firestoreDb
                    .Collection("users")
                    .GetSnapshotAsync();


            Dictionary<string, AppUser> allUsers =
                new Dictionary<string, AppUser>();


            foreach (DocumentSnapshot document
                     in usersSnapshot.Documents)
            {
                AppUser user =
                    document.ConvertTo<AppUser>();


                user.Id =
                    document.Id;


                allUsers[document.Id] =
                    user;
            }


            // =====================================================
            // LOAD FRIENDSHIPS
            // =====================================================

            QuerySnapshot friendshipSnapshot =
                await _firestoreDb
                    .Collection("friendships")
                    .GetSnapshotAsync();


            HashSet<string> friendIds =
                new HashSet<string>();


            foreach (DocumentSnapshot document
                     in friendshipSnapshot.Documents)
            {
                if (!document.ContainsField("Users"))
                {
                    continue;
                }


                List<string> users;


                try
                {
                    users =
                        document.GetValue<List<string>>(
                            "Users");
                }
                catch
                {
                    continue;
                }


                if (!users.Contains(currentUserId))
                {
                    continue;
                }


                foreach (string userId in users)
                {
                    if (userId != currentUserId)
                    {
                        friendIds.Add(userId);
                    }
                }
            }


            // =====================================================
            // BUILD FRIEND / DM LIST
            // =====================================================

            List<ChatUserItemViewModel> friends =
                new List<ChatUserItemViewModel>();


            foreach (string friendId in friendIds)
            {
                if (!allUsers.TryGetValue(
                        friendId,
                        out AppUser? friend))
                {
                    continue;
                }


                string lastMessage =
                    "";


                DateTime? lastMessageAt =
                    null;


                int unreadCount =
                    0;


                string conversationId =
                    BuildPairId(
                        currentUserId,
                        friendId);


                DocumentReference conversationReference =
                    _firestoreDb
                        .Collection("conversations")
                        .Document(conversationId);


                DocumentSnapshot conversation =
                    await conversationReference
                        .GetSnapshotAsync();


                if (conversation.Exists)
                {
                    // =================================================
                    // LAST MESSAGE
                    // =================================================

                    if (conversation.ContainsField(
                            "LastMessage"))
                    {
                        lastMessage =
                            conversation.GetValue<string>(
                                "LastMessage");
                    }


                    // =================================================
                    // LAST MESSAGE TIME
                    // =================================================

                    if (conversation.ContainsField(
                            "UpdatedAt"))
                    {
                        Timestamp timestamp =
                            conversation.GetValue<Timestamp>(
                                "UpdatedAt");


                        lastMessageAt =
                            timestamp
                                .ToDateTime()
                                .ToLocalTime();
                    }


                    // =================================================
                    // UNREAD COUNT
                    // =================================================

                    QuerySnapshot messagesSnapshot =
                        await conversationReference
                            .Collection("messages")
                            .GetSnapshotAsync();


                    foreach (DocumentSnapshot messageDocument
                             in messagesSnapshot.Documents)
                    {
                        if (!messageDocument.ContainsField(
                                "SenderId"))
                        {
                            continue;
                        }


                        string senderId =
                            messageDocument.GetValue<string>(
                                "SenderId");


                        // Never count our own messages.
                        if (senderId == currentUserId)
                        {
                            continue;
                        }


                        bool seen =
                            false;


                        if (messageDocument.ContainsField(
                                "Seen"))
                        {
                            seen =
                                messageDocument.GetValue<bool>(
                                    "Seen");
                        }


                        if (!seen)
                        {
                            unreadCount++;
                        }
                    }
                }


                DateTime? lastSeenAt =
                    null;


                try
                {
                    lastSeenAt =
                        friend.LastSeen
                            .ToDateTime()
                            .ToLocalTime();
                }
                catch
                {
                    lastSeenAt =
                        null;
                }


                friends.Add(
                    new ChatUserItemViewModel
                    {
                        Id =
                            friendId,

                        InstagramId =
                            friend.InstagramId ?? "",

                        DisplayName =
                            friend.DisplayName ?? "",

                        IsOnline =
                            IsUserOnline(friend),

                        LastSeenAt =
                            lastSeenAt,

                        PresenceText =
                            GetPresenceText(friend),

                        LastMessage =
                            lastMessage,

                        LastMessageAt =
                            lastMessageAt,

                        UnreadCount =
                            unreadCount
                    });
            }


            // =====================================================
            // NEWEST CONVERSATION FIRST
            // =====================================================

            friends =
                friends
                    .OrderByDescending(
                        x => x.LastMessageAt.HasValue)
                    .ThenByDescending(
                        x => x.LastMessageAt)
                    .ThenBy(
                        x => x.DisplayName)
                    .ToList();


            // =====================================================
            // LOAD FRIEND REQUESTS
            // =====================================================

            QuerySnapshot requestSnapshot =
                await _firestoreDb
                    .Collection("friendRequests")
                    .GetSnapshotAsync();


            List<FriendRequestItemViewModel> incomingRequests =
                new List<FriendRequestItemViewModel>();


            List<string> outgoingPendingUserIds =
                new List<string>();


            foreach (DocumentSnapshot document
                     in requestSnapshot.Documents)
            {
                if (!document.ContainsField("SenderId") ||
                    !document.ContainsField("ReceiverId") ||
                    !document.ContainsField("Status"))
                {
                    continue;
                }


                string senderId =
                    document.GetValue<string>(
                        "SenderId");


                string receiverId =
                    document.GetValue<string>(
                        "ReceiverId");


                string status =
                    document.GetValue<string>(
                        "Status");


                if (!status.Equals(
                        "Pending",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                // =================================================
                // INCOMING
                // =================================================

                if (receiverId == currentUserId)
                {
                    if (allUsers.TryGetValue(
                            senderId,
                            out AppUser? sender))
                    {
                        incomingRequests.Add(
                            new FriendRequestItemViewModel
                            {
                                RequestId =
                                    document.Id,

                                SenderId =
                                    senderId,

                                InstagramId =
                                    sender.InstagramId ?? "",

                                DisplayName =
                                    sender.DisplayName ?? "",

                                IsOnline =
                                    IsUserOnline(sender)
                            });
                    }
                }


                // =================================================
                // OUTGOING
                // =================================================

                if (senderId == currentUserId)
                {
                    outgoingPendingUserIds.Add(
                        receiverId);
                }
            }


            incomingRequests =
                incomingRequests
                    .OrderBy(
                        x => x.DisplayName)
                    .ToList();


            // =====================================================
            // SEARCH USERS
            // =====================================================

            List<ChatUserItemViewModel> searchResults =
                new List<ChatUserItemViewModel>();


            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchText =
                    search
                        .Trim()
                        .ToLower();


                foreach (KeyValuePair<string, AppUser>
                         pair in allUsers)
                {
                    string userId =
                        pair.Key;


                    AppUser user =
                        pair.Value;


                    if (userId == currentUserId)
                    {
                        continue;
                    }


                    if (friendIds.Contains(userId))
                    {
                        continue;
                    }


                    string instagramId =
                        user.InstagramId ?? "";


                    string displayName =
                        user.DisplayName ?? "";


                    bool matches =
                        instagramId
                            .ToLower()
                            .Contains(searchText)

                        ||

                        displayName
                            .ToLower()
                            .Contains(searchText);


                    if (!matches)
                    {
                        continue;
                    }


                    DateTime? lastSeenAt =
                        null;


                    try
                    {
                        lastSeenAt =
                            user.LastSeen
                                .ToDateTime()
                                .ToLocalTime();
                    }
                    catch
                    {
                        lastSeenAt =
                            null;
                    }


                    searchResults.Add(
                        new ChatUserItemViewModel
                        {
                            Id =
                                userId,

                            InstagramId =
                                instagramId,

                            DisplayName =
                                displayName,

                            IsOnline =
                                IsUserOnline(user),

                            LastSeenAt =
                                lastSeenAt,

                            PresenceText =
                                GetPresenceText(user)
                        });
                }


                searchResults =
                    searchResults
                        .OrderBy(
                            x => x.DisplayName)
                        .ThenBy(
                            x => x.InstagramId)
                        .ToList();
            }


            // =====================================================
            // BUILD MODEL
            // =====================================================

            ChatIndexViewModel model =
                new ChatIndexViewModel
                {
                    CurrentUserId =
                        currentUserId,

                    CurrentInstagramId =
                        currentInstagramId,

                    CurrentDisplayName =
                        currentDisplayName,

                    SearchQuery =
                        search ?? "",

                    Friends =
                        friends,

                    SearchResults =
                        searchResults,

                    IncomingRequests =
                        incomingRequests,

                    OutgoingPendingUserIds =
                        outgoingPendingUserIds
                };


            return View(model);
        }



        // =========================================================
        // SEND FRIEND REQUEST
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> SendFriendRequest(
            string receiverId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            if (string.IsNullOrWhiteSpace(receiverId) ||
                receiverId == currentUserId)
            {
                return RedirectToAction("Index");
            }


            DocumentSnapshot receiver =
                await _firestoreDb
                    .Collection("users")
                    .Document(receiverId)
                    .GetSnapshotAsync();


            if (!receiver.Exists)
            {
                return RedirectToAction("Index");
            }


            if (await AreFriendsAsync(
                    currentUserId,
                    receiverId))
            {
                TempData["FriendMessage"] =
                    "You are already friends.";


                return RedirectToAction("Index");
            }


            QuerySnapshot requests =
                await _firestoreDb
                    .Collection("friendRequests")
                    .GetSnapshotAsync();


            foreach (DocumentSnapshot request
                     in requests.Documents)
            {
                if (!request.ContainsField("SenderId") ||
                    !request.ContainsField("ReceiverId") ||
                    !request.ContainsField("Status"))
                {
                    continue;
                }


                string senderId =
                    request.GetValue<string>(
                        "SenderId");


                string existingReceiverId =
                    request.GetValue<string>(
                        "ReceiverId");


                string status =
                    request.GetValue<string>(
                        "Status");


                if (!status.Equals(
                        "Pending",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                if (senderId == currentUserId &&
                    existingReceiverId == receiverId)
                {
                    TempData["FriendMessage"] =
                        "Friend request already sent.";


                    return RedirectToAction("Index");
                }


                if (senderId == receiverId &&
                    existingReceiverId == currentUserId)
                {
                    TempData["FriendMessage"] =
                        "This user already sent you a friend request.";


                    return RedirectToAction("Index");
                }
            }


            DocumentReference requestDocument =
                _firestoreDb
                    .Collection("friendRequests")
                    .Document();


            Dictionary<string, object> requestData =
                new Dictionary<string, object>
                {
                    {
                        "SenderId",
                        currentUserId
                    },

                    {
                        "ReceiverId",
                        receiverId
                    },

                    {
                        "Status",
                        "Pending"
                    },

                    {
                        "CreatedAt",
                        Timestamp.GetCurrentTimestamp()
                    }
                };


            await requestDocument
                .SetAsync(requestData);


            TempData["FriendMessage"] =
                "Friend request sent.";


            return RedirectToAction("Index");
        }



        // =========================================================
        // ACCEPT FRIEND REQUEST
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> AcceptFriendRequest(
            string requestId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            if (string.IsNullOrWhiteSpace(requestId))
            {
                return RedirectToAction("Index");
            }


            DocumentReference requestReference =
                _firestoreDb
                    .Collection("friendRequests")
                    .Document(requestId);


            DocumentSnapshot request =
                await requestReference
                    .GetSnapshotAsync();


            if (!request.Exists)
            {
                return RedirectToAction("Index");
            }


            if (!request.ContainsField("SenderId") ||
                !request.ContainsField("ReceiverId") ||
                !request.ContainsField("Status"))
            {
                return RedirectToAction("Index");
            }


            string senderId =
                request.GetValue<string>(
                    "SenderId");


            string receiverId =
                request.GetValue<string>(
                    "ReceiverId");


            string status =
                request.GetValue<string>(
                    "Status");


            if (receiverId != currentUserId)
            {
                return RedirectToAction("Index");
            }


            if (!status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index");
            }


            string friendshipId =
                BuildPairId(
                    senderId,
                    receiverId);


            DocumentReference friendshipReference =
                _firestoreDb
                    .Collection("friendships")
                    .Document(friendshipId);


            string[] users =
            {
                senderId,
                receiverId
            };


            Array.Sort(
                users,
                StringComparer.Ordinal);


            Dictionary<string, object> friendshipData =
                new Dictionary<string, object>
                {
                    {
                        "Users",
                        users
                    },

                    {
                        "CreatedAt",
                        Timestamp.GetCurrentTimestamp()
                    }
                };


            await friendshipReference
                .SetAsync(friendshipData);


            Dictionary<string, object> requestUpdates =
                new Dictionary<string, object>
                {
                    {
                        "Status",
                        "Accepted"
                    },

                    {
                        "RespondedAt",
                        Timestamp.GetCurrentTimestamp()
                    }
                };


            await requestReference
                .UpdateAsync(requestUpdates);


            TempData["FriendMessage"] =
                "Friend request accepted.";


            return RedirectToAction("Index");
        }



        // =========================================================
        // DECLINE FRIEND REQUEST
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> DeclineFriendRequest(
            string requestId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            if (string.IsNullOrWhiteSpace(requestId))
            {
                return RedirectToAction("Index");
            }


            DocumentReference requestReference =
                _firestoreDb
                    .Collection("friendRequests")
                    .Document(requestId);


            DocumentSnapshot request =
                await requestReference
                    .GetSnapshotAsync();


            if (!request.Exists)
            {
                return RedirectToAction("Index");
            }


            if (!request.ContainsField("ReceiverId") ||
                !request.ContainsField("Status"))
            {
                return RedirectToAction("Index");
            }


            string receiverId =
                request.GetValue<string>(
                    "ReceiverId");


            string status =
                request.GetValue<string>(
                    "Status");


            if (receiverId != currentUserId)
            {
                return RedirectToAction("Index");
            }


            if (!status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index");
            }


            Dictionary<string, object> updates =
                new Dictionary<string, object>
                {
                    {
                        "Status",
                        "Declined"
                    },

                    {
                        "RespondedAt",
                        Timestamp.GetCurrentTimestamp()
                    }
                };


            await requestReference
                .UpdateAsync(updates);


            TempData["FriendMessage"] =
                "Friend request declined.";


            return RedirectToAction("Index");
        }



        // =========================================================
        // START CONVERSATION
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> StartConversation(
            string otherUserId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            if (string.IsNullOrWhiteSpace(otherUserId) ||
                otherUserId == currentUserId)
            {
                return RedirectToAction("Index");
            }


            bool areFriends =
                await AreFriendsAsync(
                    currentUserId,
                    otherUserId);


            if (!areFriends)
            {
                TempData["FriendMessage"] =
                    "You can only chat with accepted friends.";


                return RedirectToAction("Index");
            }


            DocumentSnapshot otherUserDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(otherUserId)
                    .GetSnapshotAsync();


            if (!otherUserDocument.Exists)
            {
                return RedirectToAction("Index");
            }


            string conversationId =
                BuildPairId(
                    currentUserId,
                    otherUserId);


            DocumentReference conversationDocument =
                _firestoreDb
                    .Collection("conversations")
                    .Document(conversationId);


            DocumentSnapshot existingConversation =
                await conversationDocument
                    .GetSnapshotAsync();


            if (!existingConversation.Exists)
            {
                string[] participantIds =
                {
                    currentUserId,
                    otherUserId
                };


                Array.Sort(
                    participantIds,
                    StringComparer.Ordinal);


                Dictionary<string, object> conversationData =
                    new Dictionary<string, object>
                    {
                        {
                            "Participants",
                            participantIds
                        },

                        {
                            "CreatedAt",
                            Timestamp.GetCurrentTimestamp()
                        },

                        {
                            "UpdatedAt",
                            Timestamp.GetCurrentTimestamp()
                        },

                        {
                            "LastMessage",
                            ""
                        },

                        {
                            "LastSenderId",
                            ""
                        }
                    };


                await conversationDocument
                    .SetAsync(conversationData);
            }


            return RedirectToAction(
                "Room",
                new
                {
                    conversationId
                });
        }



        // =========================================================
        // CHAT ROOM
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Room(
            string conversationId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return RedirectToAction(
                    "Login",
                    "Auth");
            }


            if (string.IsNullOrWhiteSpace(conversationId))
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
                    conversation.GetValue<List<string>>(
                        "Participants");
            }
            catch
            {
                return RedirectToAction("Index");
            }


            if (!participants.Contains(currentUserId))
            {
                return RedirectToAction("Index");
            }


            string otherUserId =
                participants
                    .FirstOrDefault(
                        x => x != currentUserId)
                ?? "";


            if (string.IsNullOrWhiteSpace(otherUserId))
            {
                return RedirectToAction("Index");
            }


            bool areFriends =
                await AreFriendsAsync(
                    currentUserId,
                    otherUserId);


            if (!areFriends)
            {
                TempData["FriendMessage"] =
                    "You are no longer allowed to access this conversation.";


                return RedirectToAction("Index");
            }


            DocumentSnapshot otherUserDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(otherUserId)
                    .GetSnapshotAsync();


            if (!otherUserDocument.Exists)
            {
                return RedirectToAction("Index");
            }


            AppUser otherUser =
                otherUserDocument
                    .ConvertTo<AppUser>();


            // =====================================================
            // LOAD MESSAGES
            // =====================================================

            QuerySnapshot messageSnapshot =
                await conversationReference
                    .Collection("messages")
                    .OrderBy("SentAt")
                    .GetSnapshotAsync();


            List<ChatMessageViewModel> messages =
                new List<ChatMessageViewModel>();


            foreach (DocumentSnapshot document
                     in messageSnapshot.Documents)
            {
                string senderId =
                    "";


                string text =
                    "";


                bool seen =
                    false;


                Timestamp sentTimestamp =
                    Timestamp.GetCurrentTimestamp();


                if (document.ContainsField("SenderId"))
                {
                    senderId =
                        document.GetValue<string>(
                            "SenderId");
                }


                if (document.ContainsField("Text"))
                {
                    text =
                        document.GetValue<string>(
                            "Text");
                }


                if (document.ContainsField("SentAt"))
                {
                    sentTimestamp =
                        document.GetValue<Timestamp>(
                            "SentAt");
                }


                if (document.ContainsField("Seen"))
                {
                    seen =
                        document.GetValue<bool>(
                            "Seen");
                }


                messages.Add(
                    new ChatMessageViewModel
                    {
                        Id =
                            document.Id,

                        SenderId =
                            senderId,

                        Text =
                            text,

                        SentAt =
                            sentTimestamp.ToDateTime(),

                        IsMine =
                            senderId == currentUserId,

                        Seen =
                            seen
                    });
            }


            DateTime? otherLastSeen =
                null;


            try
            {
                otherLastSeen =
                    otherUser.LastSeen
                        .ToDateTime()
                        .ToLocalTime();
            }
            catch
            {
                otherLastSeen =
                    null;
            }


            ChatRoomViewModel model =
                new ChatRoomViewModel
                {
                    ConversationId =
                        conversationId,

                    CurrentUserId =
                        currentUserId,

                    OtherUserId =
                        otherUserId,

                    OtherInstagramId =
                        otherUser.InstagramId ?? "",

                    OtherDisplayName =
                        otherUser.DisplayName ?? "",

                    OtherIsOnline =
                        IsUserOnline(otherUser),

                    OtherLastSeenAt =
                        otherLastSeen,

                    OtherPresenceText =
                        GetPresenceText(otherUser),

                    Messages =
                        messages
                };


            return View(model);
        }



        // =========================================================
        // SEND MESSAGE
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
            string conversationId,
            string message)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(
                    new
                    {
                        success = false,
                        error = "You are not logged in."
                    });
            }


            conversationId =
                conversationId?.Trim() ?? "";


            message =
                message?.Trim() ?? "";


            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        error = "Conversation is missing."
                    });
            }


            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        error = "Message cannot be empty."
                    });
            }


            if (message.Length > 2000)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        error = "Message is too long."
                    });
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
                return NotFound(
                    new
                    {
                        success = false,
                        error = "Conversation does not exist."
                    });
            }


            List<string> participants;


            try
            {
                participants =
                    conversation.GetValue<List<string>>(
                        "Participants");
            }
            catch
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        error = "Invalid conversation."
                    });
            }


            if (!participants.Contains(currentUserId))
            {
                return Forbid();
            }


            string otherUserId =
                participants
                    .FirstOrDefault(
                        x => x != currentUserId)
                ?? "";


            if (string.IsNullOrWhiteSpace(otherUserId))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        error = "Invalid conversation."
                    });
            }


            bool areFriends =
                await AreFriendsAsync(
                    currentUserId,
                    otherUserId);


            if (!areFriends)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        error = "You can only message accepted friends."
                    });
            }


            Timestamp now =
                Timestamp.GetCurrentTimestamp();


            DocumentReference messageReference =
                conversationReference
                    .Collection("messages")
                    .Document();


            Dictionary<string, object> messageData =
                new Dictionary<string, object>
                {
                    {
                        "SenderId",
                        currentUserId
                    },

                    {
                        "Text",
                        message
                    },

                    {
                        "SentAt",
                        now
                    },

                    {
                        "Seen",
                        false
                    }
                };


            await messageReference
                .SetAsync(messageData);


            Dictionary<string, object> conversationUpdates =
                new Dictionary<string, object>
                {
                    {
                        "LastMessage",
                        message
                    },

                    {
                        "LastSenderId",
                        currentUserId
                    },

                    {
                        "UpdatedAt",
                        now
                    }
                };


            await conversationReference
                .UpdateAsync(conversationUpdates);


            DateTime sentAt =
                now.ToDateTime();


            // =====================================================
            // OPEN CHAT REAL-TIME UPDATE
            // =====================================================

            await _chatHub
                .Clients
                .Group(conversationId)
                .SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        id =
                            messageReference.Id,

                        senderId =
                            currentUserId,

                        receiverId =
                            otherUserId,

                        text =
                            message,

                        sentAt =
                            sentAt.ToString("o"),

                        seen =
                            false
                    });


            // =====================================================
            // INBOX UPDATE
            // =====================================================

            var inboxMessage =
                new
                {
                    conversationId =
                        conversationId,

                    senderId =
                        currentUserId,

                    receiverId =
                        otherUserId,

                    text =
                        message,

                    sentAt =
                        sentAt.ToString("o")
                };


            await _chatHub
                .Clients
                .Group(
                    $"inbox:{currentUserId}")
                .SendAsync(
                    "InboxMessage",
                    inboxMessage);


            await _chatHub
                .Clients
                .Group(
                    $"inbox:{otherUserId}")
                .SendAsync(
                    "InboxMessage",
                    inboxMessage);


            return Json(
                new
                {
                    success = true,

                    messageId =
                        messageReference.Id
                });
        }



        // =========================================================
        // HEARTBEAT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Heartbeat()
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(
                    new
                    {
                        success = false
                    });
            }


            DocumentReference userReference =
                _firestoreDb
                    .Collection("users")
                    .Document(currentUserId);


            DocumentSnapshot userDocument =
                await userReference
                    .GetSnapshotAsync();


            if (!userDocument.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false
                    });
            }


            Timestamp now =
                Timestamp.GetCurrentTimestamp();


            Dictionary<string, object> updates =
                new Dictionary<string, object>
                {
                    {
                        "IsOnline",
                        true
                    },

                    {
                        "LastSeen",
                        now
                    }
                };


            await userReference
                .UpdateAsync(updates);


            return Json(
                new
                {
                    success = true,

                    lastSeen =
                        now
                            .ToDateTime()
                            .ToString("o")
                });
        }



        // =========================================================
        // GET USER PRESENCE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Presence(
            string userId)
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }


            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest();
            }


            // Friends can inspect each other's presence.
            if (userId != currentUserId)
            {
                bool areFriends =
                    await AreFriendsAsync(
                        currentUserId,
                        userId);


                if (!areFriends)
                {
                    return Forbid();
                }
            }


            DocumentSnapshot userDocument =
                await _firestoreDb
                    .Collection("users")
                    .Document(userId)
                    .GetSnapshotAsync();


            if (!userDocument.Exists)
            {
                return NotFound();
            }


            AppUser user =
                userDocument
                    .ConvertTo<AppUser>();


            bool isOnline =
                IsUserOnline(user);


            string presenceText =
                GetPresenceText(user);


            return Json(
                new
                {
                    success = true,

                    isOnline,

                    presenceText
                });
        }



        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            string? currentUserId =
                HttpContext.Session.GetString("UserId");


            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                DocumentReference userReference =
                    _firestoreDb
                        .Collection("users")
                        .Document(currentUserId);


                DocumentSnapshot user =
                    await userReference
                        .GetSnapshotAsync();


                if (user.Exists)
                {
                    Dictionary<string, object> updates =
                        new Dictionary<string, object>
                        {
                            {
                                "IsOnline",
                                false
                            },

                            {
                                "LastSeen",
                                Timestamp.GetCurrentTimestamp()
                            }
                        };


                    await userReference
                        .UpdateAsync(updates);
                }
            }


            HttpContext.Session.Clear();


            return RedirectToAction(
                "Login",
                "Auth");
        }



        // =========================================================
        // FRIEND CHECK
        // =========================================================

        private async Task<bool> AreFriendsAsync(
            string userId1,
            string userId2)
        {
            if (string.IsNullOrWhiteSpace(userId1) ||
                string.IsNullOrWhiteSpace(userId2))
            {
                return false;
            }


            string friendshipId =
                BuildPairId(
                    userId1,
                    userId2);


            DocumentSnapshot friendship =
                await _firestoreDb
                    .Collection("friendships")
                    .Document(friendshipId)
                    .GetSnapshotAsync();


            return friendship.Exists;
        }



        // =========================================================
        // ONLINE CHECK
        // =========================================================

        private static bool IsUserOnline(
            AppUser user)
        {
            if (!user.IsOnline)
            {
                return false;
            }


            try
            {
                DateTime lastSeenUtc =
                    user.LastSeen
                        .ToDateTime();


                TimeSpan difference =
                    DateTime.UtcNow
                    -
                    lastSeenUtc;


                // Browser heartbeat runs every 25 seconds.
                // 70 seconds allows temporary delays.
                return difference.TotalSeconds <= 70;
            }
            catch
            {
                return false;
            }
        }



        // =========================================================
        // PRESENCE TEXT
        // =========================================================

        private static string GetPresenceText(
            AppUser user)
        {
            if (IsUserOnline(user))
            {
                return "Active now";
            }


            try
            {
                DateTime lastSeen =
                    user.LastSeen
                        .ToDateTime()
                        .ToLocalTime();


                DateTime now =
                    DateTime.Now;


                if (lastSeen.Date == now.Date)
                {
                    return
                        $"Last seen {lastSeen:h:mm tt}";
                }


                if (lastSeen.Date ==
                    now.Date.AddDays(-1))
                {
                    return
                        $"Last seen yesterday at {lastSeen:h:mm tt}";
                }


                return
                    $"Last seen {lastSeen:dd MMM, h:mm tt}";
            }
            catch
            {
                return "Offline";
            }
        }



        // =========================================================
        // CONSISTENT PAIR ID
        // =========================================================

        private static string BuildPairId(
            string userId1,
            string userId2)
        {
            string[] ids =
            {
                userId1,
                userId2
            };


            Array.Sort(
                ids,
                StringComparer.Ordinal);


            return $"{ids[0]}__{ids[1]}";
        }
    }
}