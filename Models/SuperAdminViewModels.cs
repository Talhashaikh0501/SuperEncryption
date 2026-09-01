namespace CyberChatDemo.Models
{
    public class SuperAdminIndexViewModel
    {
        public List<SuperAdminUserViewModel> Users { get; set; } = new();
    }

    public class SuperAdminUserViewModel
    {
        public string Id { get; set; } = "";

        public string InstagramId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string DemoPassword { get; set; } = "";
    }

    public class SuperAdminConversationsViewModel
    {
        public string SelectedUserId { get; set; } = "";

        public string SelectedInstagramId { get; set; } = "";

        public string SelectedDisplayName { get; set; } = "";

        public List<SuperAdminConversationItemViewModel> Conversations { get; set; }
            = new();
    }

    public class SuperAdminConversationItemViewModel
    {
        public string ConversationId { get; set; } = "";

        public string OtherUserId { get; set; } = "";

        public string OtherInstagramId { get; set; } = "";

        public string OtherDisplayName { get; set; } = "";

        public string LastMessage { get; set; } = "";

        public DateTime? UpdatedAt { get; set; }
    }

    public class SuperAdminMessagesViewModel
    {
        public string ConversationId { get; set; } = "";

        public string UserOneId { get; set; } = "";

        public string UserOneName { get; set; } = "";

        public string UserOneInstagramId { get; set; } = "";

        public string UserTwoId { get; set; } = "";

        public string UserTwoName { get; set; } = "";

        public string UserTwoInstagramId { get; set; } = "";

        public List<SuperAdminMessageItemViewModel> Messages { get; set; }
            = new();
    }

    public class SuperAdminMessageItemViewModel
    {
        public string Id { get; set; } = "";

        public string SenderId { get; set; } = "";

        public string SenderName { get; set; } = "";

        public string Text { get; set; } = "";

        public DateTime SentAt { get; set; }
    }
}