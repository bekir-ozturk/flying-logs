namespace FlyingLogs.Sinks.Seq;

internal sealed class MultipleInsertSingleDeleteLinkedList<T>
{
    internal enum DeletionOption
    {
        Delete = 0,
        Retain = 1,
    };

    private Node? _root;

    private record Node(T Data)
    {
        public Node? Next { get; set; }
    }

    public void Insert(T data) {
        Node node = new (data);

        while (true)
        {
            Node? currentRoot = _root;
            node.Next = currentRoot;
            if (Interlocked.CompareExchange(ref _root, node.Next, currentRoot) == currentRoot)
                break;
        }
    }

    /// <summary>
    /// Empties the linked list and returns the root item with all the other items attached.
    /// </summary>
    /// <returns>Root node where next nodes are the other elements in the linked list before it was cleared.</returns>
    public async Task DeleteAllAsync(Func<T, Task<DeletionOption>> predicate)
    {
        Node? currentRoot;
        while (true)
        {
            currentRoot = _root;
            if (Interlocked.CompareExchange(ref _root, null, currentRoot) == null)
                break;
        }

        if (currentRoot == null)
            return; // Linked list turned out to be empty.

        // We stole the root and all the attached elements. We can do the deletions safely.
        // If a new node is added in the meantime, it will be added to the original _root field.
        // When we are done, we'll merge the two linked lists.
        
        Node? lastNode = null, currentNode = currentRoot;
        while (currentNode != null)
        {
            if (await predicate(currentNode.Data) == DeletionOption.Delete)
            {
                if (lastNode == null)
                {
                    // The very first element deleted. Just replace the root.
                    currentRoot = currentNode.Next;
                }
                else
                {
                    lastNode.Next = currentNode.Next;
                }
            }
            else
            {
                lastNode = currentNode;
            }

            currentNode = currentNode.Next;
        }

        // Merge the stolen list back into the _root.
        if (lastNode == null)
        {
             // We deleted everything. Nothing to merge.
             return;
        }

        while (true)
        {
            Node? newRoot = _root;
            lastNode.Next = _root;
            if (Interlocked.CompareExchange(ref _root, currentRoot, newRoot) == newRoot)
                break;
        }
    }
}