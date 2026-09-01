namespace CyberChatDemo.Models
{
    public class ChatIndexViewModel
    {
        public string CurrentUserId { get; set; } = "";

        public string CurrentInstagramId { get; set; } = "";

        public string CurrentDisplayName { get; set; } = "";

        public string SearchQuery { get; set; } = "";

        public List<ChatUserItemViewModel> Friends { get; set; } = new();

        public List<ChatUserItemViewModel> SearchResults { get; set; } = new();

        public List<FriendRequestItemViewModel> IncomingRequests { get; set; } = new();

        public List<string> OutgoingPendingUserIds { get; set; } = new();
    }


    public class ChatUserItemViewModel
    {
        public string Id { get; set; } = "";

        public string InstagramId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public bool IsOnline { get; set; }

        public DateTime? LastSeenAt { get; set; }

        public string PresenceText { get; set; } = "Offline";

        public string LastMessage { get; set; } = "";

        public DateTime? LastMessageAt { get; set; }

        public int UnreadCount { get; set; }
    }


    public class FriendRequestItemViewModel
    {
        public string RequestId { get; set; } = "";

        public string SenderId { get; set; } = "";

        public string InstagramId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public bool IsOnline { get; set; }
    }
}