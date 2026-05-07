namespace Chatbox_Type_Shii
{
    public class ChatPackets
    {
        public enum PacketType
        {
            Message,
            File,
            UserList,
            UserJoined,
            UserLeft
        }
        public PacketType Type { get; set; }
        public string? Username { get; set; }
        public string? Content { get; set; }
        public string? FileName { get; set; }
        public string? DestinationDirectory { get; set; }
    }
}