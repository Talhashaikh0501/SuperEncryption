using Google.Cloud.Firestore;
using Microsoft.AspNetCore.SignalR;

namespace CyberChatDemo.Hubs
{
    public class ChatHub : Hub
    {
        private readonly FirestoreDb _firestoreDb;


        public ChatHub(
            FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }


        // =========================================================
        // GET CURRENT USER
        // =========================================================

        private string? GetCurrentUserId()
        {
            HttpContext? httpContext =
                Context.GetHttpContext();


            return httpContext?
                .Session
                .GetString("UserId");
        }


        // =========================================================
        // JOIN PERSONAL INBOX
        // =========================================================

        public async Task JoinInbox()
        {
            string? currentUserId =
                GetCurrentUserId();


            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                throw new HubException(
                    "You are not logged in.");
            }


            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"inbox:{currentUserId}");
        }


        // =========================================================
        // JOIN CONVERSATION
        // =========================================================

        public async Task JoinConversation(
            string conversationId)
        {
            string? currentUserId =
                GetCurrentUserId();


            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                throw new HubException(
                    "You are not logged in.");
            }


            if (string.IsNullOrWhiteSpace(
                    conversationId))
            {
                throw new HubException(
                    "Conversation is missing.");
            }


            DocumentSnapshot conversation =
                await _firestoreDb
                    .Collection("conversations")
                    .Document(conversationId)
                    .GetSnapshotAsync();


            if (!conversation.Exists)
            {
                throw new HubException(
                    "Conversation does not exist.");
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
                throw new HubException(
                    "Invalid conversation.");
            }


            if (!participants.Contains(
                    currentUserId))
            {
                throw new HubException(
                    "You cannot access this conversation.");
            }


            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                conversationId);
        }


        // =========================================================
        // TYPING STATUS
        // =========================================================

        public async Task SetTyping(
            string conversationId,
            bool isTyping)
        {
            string? currentUserId =
                GetCurrentUserId();


            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                throw new HubException(
                    "You are not logged in.");
            }


            if (string.IsNullOrWhiteSpace(
                    conversationId))
            {
                return;
            }


            // =====================================================
            // VERIFY CONVERSATION
            // =====================================================

            DocumentSnapshot conversation =
                await _firestoreDb
                    .Collection("conversations")
                    .Document(conversationId)
                    .GetSnapshotAsync();


            if (!conversation.Exists)
            {
                return;
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
                return;
            }


            if (!participants.Contains(
                    currentUserId))
            {
                throw new HubException(
                    "You cannot access this conversation.");
            }


            // =====================================================
            // SEND ONLY TO OTHER USER(S)
            // =====================================================

            await Clients
                .OthersInGroup(conversationId)
                .SendAsync(
                    "TypingChanged",
                    new
                    {
                        userId =
                            currentUserId,

                        isTyping =
                            isTyping
                    });
        }


        // =========================================================
        // MARK ONE MESSAGE AS SEEN
        // =========================================================

        public async Task MarkSeen(
            string conversationId,
            string messageId)
        {
            string? currentUserId =
                GetCurrentUserId();


            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                throw new HubException(
                    "You are not logged in.");
            }


            if (string.IsNullOrWhiteSpace(
                    conversationId)
                ||
                string.IsNullOrWhiteSpace(
                    messageId))
            {
                return;
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
                return;
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
                return;
            }


            if (!participants.Contains(
                    currentUserId))
            {
                throw new HubException(
                    "You cannot access this conversation.");
            }


            DocumentReference messageReference =
                conversationReference
                    .Collection("messages")
                    .Document(messageId);


            DocumentSnapshot message =
                await messageReference
                    .GetSnapshotAsync();


            if (!message.Exists)
            {
                return;
            }


            if (!message.ContainsField(
                    "SenderId"))
            {
                return;
            }


            string senderId =
                message.GetValue<string>(
                    "SenderId");


            // User cannot mark their own
            // message as seen.
            if (senderId == currentUserId)
            {
                return;
            }


            bool alreadySeen =
                false;


            if (message.ContainsField(
                    "Seen"))
            {
                alreadySeen =
                    message.GetValue<bool>(
                        "Seen");
            }


            if (alreadySeen)
            {
                return;
            }


            Timestamp seenAt =
                Timestamp.GetCurrentTimestamp();


            Dictionary<string, object> updates =
                new Dictionary<string, object>
                {
                    {
                        "Seen",
                        true
                    },

                    {
                        "SeenAt",
                        seenAt
                    }
                };


            await messageReference
                .UpdateAsync(updates);


            // =====================================================
            // NOTIFY OPEN CHAT
            // =====================================================

            await Clients
                .Group(conversationId)
                .SendAsync(
                    "MessageSeen",
                    new
                    {
                        messageId =
                            messageId,

                        seenByUserId =
                            currentUserId,

                        seenAt =
                            seenAt
                                .ToDateTime()
                                .ToString("o")
                    });
        }


        // =========================================================
        // MARK ALL INCOMING MESSAGES AS SEEN
        // =========================================================

        public async Task MarkConversationSeen(
            string conversationId)
        {
            string? currentUserId =
                GetCurrentUserId();


            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                throw new HubException(
                    "You are not logged in.");
            }


            if (string.IsNullOrWhiteSpace(
                    conversationId))
            {
                return;
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
                return;
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
                return;
            }


            if (!participants.Contains(
                    currentUserId))
            {
                throw new HubException(
                    "You cannot access this conversation.");
            }


            QuerySnapshot messages =
                await conversationReference
                    .Collection("messages")
                    .GetSnapshotAsync();


            foreach (
                DocumentSnapshot message
                in messages.Documents)
            {
                if (!message.ContainsField(
                        "SenderId"))
                {
                    continue;
                }


                string senderId =
                    message.GetValue<string>(
                        "SenderId");


                // Only incoming messages.
                if (senderId ==
                    currentUserId)
                {
                    continue;
                }


                bool seen =
                    false;


                if (message.ContainsField(
                        "Seen"))
                {
                    seen =
                        message.GetValue<bool>(
                            "Seen");
                }


                if (seen)
                {
                    continue;
                }


                Timestamp seenAt =
                    Timestamp.GetCurrentTimestamp();


                Dictionary<string, object> updates =
                    new Dictionary<string, object>
                    {
                        {
                            "Seen",
                            true
                        },

                        {
                            "SeenAt",
                            seenAt
                        }
                    };


                await message
                    .Reference
                    .UpdateAsync(updates);


                await Clients
                    .Group(conversationId)
                    .SendAsync(
                        "MessageSeen",
                        new
                        {
                            messageId =
                                message.Id,

                            seenByUserId =
                                currentUserId,

                            seenAt =
                                seenAt
                                    .ToDateTime()
                                    .ToString("o")
                        });
            }
        }
    }
}