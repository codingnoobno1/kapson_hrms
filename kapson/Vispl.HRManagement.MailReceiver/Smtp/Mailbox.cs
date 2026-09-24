namespace Vispl.HRManagement.MailReceiver.Smtp;

/// <summary>
/// Everything this server has caught, newest first.
///
/// In memory and capped. This is a development sink: persisting captured mail would mean
/// writing real people's addresses to disk on a laptop, and losing it on restart is the
/// correct behaviour rather than a limitation.
/// </summary>
public sealed class Mailbox
{
    private const int Capacity = 200;

    private readonly LinkedList<CapturedMessage> messages = new();
    private readonly Lock gate = new();

    public event Action? Changed;

    public void Add(CapturedMessage message)
    {
        lock (gate)
        {
            messages.AddFirst(message);

            while (messages.Count > Capacity)
            {
                messages.RemoveLast();
            }
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<CapturedMessage> All()
    {
        lock (gate)
        {
            return messages.ToList();
        }
    }

    public CapturedMessage? Find(string id)
    {
        lock (gate)
        {
            return messages.FirstOrDefault(message => message.Id == id);
        }
    }

    /// <summary>The newest message to an address — what a test asks for after triggering one.</summary>
    public CapturedMessage? Latest(string toAddress)
    {
        lock (gate)
        {
            return messages.FirstOrDefault(message => message.To.Any(
                recipient => recipient.Contains(toAddress, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public int Clear()
    {
        int removed;

        lock (gate)
        {
            removed = messages.Count;
            messages.Clear();
            seen.Clear();
        }

        Changed?.Invoke();
        return removed;
    }

    /// <summary>
    /// Oldest first, which is the order POP3 and IMAP number messages in. The web inbox wants
    /// the opposite; giving each what it expects beats making one of them reverse the other.
    /// </summary>
    public IReadOnlyList<CapturedMessage> Ordered()
    {
        lock (gate)
        {
            return messages.Reverse().ToList();
        }
    }

    public bool Remove(string id)
    {
        bool removed;

        lock (gate)
        {
            var node = messages.FirstOrDefault(message => message.Id == id);
            removed = node is not null && messages.Remove(node);
            seen.Remove(id);
        }

        if (removed)
        {
            Changed?.Invoke();
        }

        return removed;
    }

    // ---- Read state -------------------------------------------------------
    // IMAP clients expect \Seen to persist across sessions, and the web viewer marks a message
    // read when it is opened. One set serves both, so opening a message in the browser and
    // opening it in Thunderbird agree with each other.

    private readonly HashSet<string> seen = [];

    public void MarkSeen(string id)
    {
        bool changed;

        lock (gate)
        {
            changed = seen.Add(id);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Puts a message back to unread, the way any mail client lets you.</summary>
    public void MarkUnseen(string id)
    {
        bool changed;

        lock (gate)
        {
            changed = seen.Remove(id);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public bool IsSeen(string id)
    {
        lock (gate)
        {
            return seen.Contains(id);
        }
    }

    public int UnseenCount
    {
        get
        {
            lock (gate)
            {
                return messages.Count(message => !seen.Contains(message.Id));
            }
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return messages.Count;
            }
        }
    }
}
