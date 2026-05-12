using System.Text.RegularExpressions;

namespace FFXIVSharedLibrary.Chat;

public delegate void ChatMessageHandler(ChatMessageEventArgs args);

public class ChatMessageEventArgs
{
    public required int ChatType { get; init; }
    public required int Timestamp { get; init; }
    public required string Sender { get; init; }
    public required string Message { get; init; }
    public bool IsHandled { get; set; }
}

public interface IChatMessageHandler
{
    bool CanHandle(ChatMessageEventArgs args);
    void Handle(ChatMessageEventArgs args);
    int Priority { get; }
}

public class ChatMessageProcessor
{
    private readonly List<IChatMessageHandler> handlers = new();
    private readonly object lockObject = new object();

    public event ChatMessageHandler? MessageProcessed;

    public void RegisterHandler(IChatMessageHandler handler)
    {
        lock (lockObject)
        {
            handlers.Add(handler);
            handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public void UnregisterHandler(IChatMessageHandler handler)
    {
        lock (lockObject)
        {
            handlers.Remove(handler);
        }
    }

    public void ProcessMessage(int chatType, int timestamp, string sender, string message)
    {
        var args = new ChatMessageEventArgs
        {
            ChatType = chatType,
            Timestamp = timestamp,
            Sender = sender,
            Message = message,
            IsHandled = false
        };

        lock (lockObject)
        {
            foreach (var handler in handlers)
            {
                if (handler.CanHandle(args))
                {
                    handler.Handle(args);
                    if (args.IsHandled)
                        break;
                }
            }
        }

        MessageProcessed?.Invoke(args);
    }

    public void ClearHandlers()
    {
        lock (lockObject)
        {
            handlers.Clear();
        }
    }
}

public abstract class RegexChatHandler : IChatMessageHandler
{
    protected readonly Regex pattern;
    public virtual int Priority => 0;

    protected RegexChatHandler(string regexPattern, RegexOptions options = RegexOptions.Compiled)
    {
        pattern = new Regex(regexPattern, options);
    }

    public virtual bool CanHandle(ChatMessageEventArgs args)
    {
        return pattern.IsMatch(args.Message);
    }

    public abstract void Handle(ChatMessageEventArgs args);

    protected Match GetMatch(ChatMessageEventArgs args)
    {
        return pattern.Match(args.Message);
    }
}