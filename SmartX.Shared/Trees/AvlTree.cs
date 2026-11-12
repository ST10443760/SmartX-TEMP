namespace SmartX.Shared.Trees;

/// <summary>
/// A self-balancing binary search tree (AVL tree) keyed by timestamp, used to
/// answer "every reading between 14:00 and 16:00" without scanning a device's
/// whole history. This is a hand-written tree, not a wrapper around
/// SortedDictionary or SortedSet.
///
/// AVL rule: at every node the heights of the left and right subtrees differ
/// by at most one. Each node stores its own height; after an insert or a
/// removal the nodes on the way back up the path are re-measured, and any node
/// whose balance factor reaches +2 or -2 is fixed with one of four rotations
/// (right, left, left-right, right-left). That keeps the height at
/// O(log n), so insert, remove-min and the start of a range query are O(log n).
///
/// Duplicate timestamps: two readings with exactly the same timestamp share
/// one node and are kept in insertion order in that node's value list. They
/// do not create a second node, so the tree shape depends only on the
/// distinct timestamps, and a range query returns both readings.
///
/// Not thread-safe on its own. <see cref="DeviceHistoryTrees{TValue}"/> wraps
/// every call in a lock, in the same way as the Part 2 stores.
///
/// Source: written from general knowledge of the AVL algorithm, first
/// described by G. M. Adelson-Velsky and E. M. Landis (1962), "An algorithm
/// for the organization of information". The four rotation cases are the
/// standard ones taught for AVL trees.
/// </summary>
public sealed class AvlTree<TValue>
{
    private AvlNode? _root;

    /// <summary>Number of readings stored (duplicates counted individually).</summary>
    public int Count { get; private set; }

    /// <summary>Number of distinct timestamps, which is the number of tree nodes.</summary>
    public int NodeCount { get; private set; }

    /// <summary>Height of the tree: 0 when empty, 1 for a single node.</summary>
    public int Height => HeightOf(_root);

    /// <summary>The timestamp at the root, or null when empty. Used by the rotation tests.</summary>
    public DateTime? RootKey => _root?.Key;

    public int RightRotations { get; private set; }

    public int LeftRotations { get; private set; }

    public int LeftRightRotations { get; private set; }

    public int RightLeftRotations { get; private set; }

    /// <summary>Inserts a reading. O(log n).</summary>
    public void Insert(DateTime key, TValue value)
    {
        _root = Insert(_root, key, value);
        Count++;
    }

    /// <summary>How many tree nodes the most recent <see cref="RangeQuery"/> touched.</summary>
    public int LastQueryNodesVisited { get; private set; }

    /// <summary>
    /// Every reading with <paramref name="from"/> &lt;= timestamp &lt;= <paramref name="to"/>
    /// (both ends inclusive), in timestamp order.
    ///
    /// It walks the tree in order but prunes: it only goes left when the
    /// current key is after <paramref name="from"/> (anything smaller could
    /// still be in range) and only goes right when the current key is before
    /// <paramref name="to"/>. Whole subtrees that sit entirely outside the
    /// range are never entered. Cost is O(log n + k) nodes for k results,
    /// instead of the O(n) of scanning every reading.
    /// </summary>
    public RangeQueryResult<TValue> RangeQuery(DateTime from, DateTime to)
    {
        var items = new List<KeyValuePair<DateTime, TValue>>();
        var visited = 0;

        if (from <= to)
        {
            RangeQuery(_root, from, to, items, ref visited);
        }

        LastQueryNodesVisited = visited;
        return new RangeQueryResult<TValue>(items, visited, Count);
    }

    private static void RangeQuery(AvlNode? node, DateTime from, DateTime to, List<KeyValuePair<DateTime, TValue>> items, ref int visited)
    {
        if (node is null)
        {
            return;
        }

        visited++;

        // Only the left subtree can hold keys smaller than this one.
        if (from < node.Key)
        {
            RangeQuery(node.Left, from, to, items, ref visited);
        }

        if (from <= node.Key && node.Key <= to)
        {
            foreach (var value in node.Values)
            {
                items.Add(new KeyValuePair<DateTime, TValue>(node.Key, value));
            }
        }

        // Only the right subtree can hold keys larger than this one.
        if (node.Key < to)
        {
            RangeQuery(node.Right, from, to, items, ref visited);
        }
    }

    /// <summary>Every reading in timestamp order (in-order traversal). O(n).</summary>
    public List<KeyValuePair<DateTime, TValue>> InOrder()
    {
        var result = new List<KeyValuePair<DateTime, TValue>>(Count);
        InOrder(_root, result);
        return result;
    }

    /// <summary>
    /// True when every node satisfies the AVL rule and its stored height is
    /// correct, and the keys are in search-tree order. Used by the tests.
    /// </summary>
    public bool IsBalanced() => Check(_root, null, null).Ok;

    private AvlNode Insert(AvlNode? node, DateTime key, TValue value)
    {
        if (node is null)
        {
            NodeCount++;
            return new AvlNode(key, value);
        }

        var comparison = key.CompareTo(node.Key);
        if (comparison < 0)
        {
            node.Left = Insert(node.Left, key, value);
        }
        else if (comparison > 0)
        {
            node.Right = Insert(node.Right, key, value);
        }
        else
        {
            // Same timestamp: keep both readings in the existing node.
            node.Values.Add(value);
            return node;
        }

        return Rebalance(node);
    }

    /// <summary>
    /// Re-measures a node after one of its subtrees changed and applies the
    /// rotation for whichever of the four unbalanced shapes it is in.
    /// </summary>
    private AvlNode Rebalance(AvlNode node)
    {
        UpdateHeight(node);
        var balance = BalanceOf(node);

        if (balance > 1)
        {
            // Left-heavy.
            if (BalanceOf(node.Left) >= 0)
            {
                // Left-left: a single right rotation fixes it.
                RightRotations++;
                return RotateRight(node);
            }

            // Left-right: rotate the left child left, then this node right.
            LeftRightRotations++;
            node.Left = RotateLeft(node.Left!);
            return RotateRight(node);
        }

        if (balance < -1)
        {
            // Right-heavy.
            if (BalanceOf(node.Right) <= 0)
            {
                // Right-right: a single left rotation fixes it.
                LeftRotations++;
                return RotateLeft(node);
            }

            // Right-left: rotate the right child right, then this node left.
            RightLeftRotations++;
            node.Right = RotateRight(node.Right!);
            return RotateLeft(node);
        }

        return node;
    }

    //       y              x
    //      / \            / \
    //     x   C   --->   A   y
    //    / \                / \
    //   A   B              B   C
    private static AvlNode RotateRight(AvlNode y)
    {
        var x = y.Left!;
        y.Left = x.Right;
        x.Right = y;
        UpdateHeight(y);
        UpdateHeight(x);
        return x;
    }

    //     x                  y
    //    / \                / \
    //   A   y     --->     x   C
    //      / \            / \
    //     B   C          A   B
    private static AvlNode RotateLeft(AvlNode x)
    {
        var y = x.Right!;
        x.Right = y.Left;
        y.Left = x;
        UpdateHeight(x);
        UpdateHeight(y);
        return y;
    }

    private static int HeightOf(AvlNode? node) => node?.Height ?? 0;

    private static int BalanceOf(AvlNode? node) => node is null ? 0 : HeightOf(node.Left) - HeightOf(node.Right);

    private static void UpdateHeight(AvlNode node) => node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));

    private static void InOrder(AvlNode? node, List<KeyValuePair<DateTime, TValue>> result)
    {
        if (node is null)
        {
            return;
        }

        InOrder(node.Left, result);
        foreach (var value in node.Values)
        {
            result.Add(new KeyValuePair<DateTime, TValue>(node.Key, value));
        }

        InOrder(node.Right, result);
    }

    private static (bool Ok, int Height) Check(AvlNode? node, DateTime? lower, DateTime? upper)
    {
        if (node is null)
        {
            return (true, 0);
        }

        if ((lower is { } lo && node.Key <= lo) || (upper is { } hi && node.Key >= hi))
        {
            return (false, 0);
        }

        var left = Check(node.Left, lower, node.Key);
        var right = Check(node.Right, node.Key, upper);
        var height = 1 + Math.Max(left.Height, right.Height);
        var ok = left.Ok && right.Ok && Math.Abs(left.Height - right.Height) <= 1 && node.Height == height;
        return (ok, height);
    }

    private sealed class AvlNode(DateTime key, TValue value)
    {
        public DateTime Key { get; } = key;

        /// <summary>Readings that share this exact timestamp, in insertion order.</summary>
        public List<TValue> Values { get; } = [value];

        public AvlNode? Left { get; set; }

        public AvlNode? Right { get; set; }

        public int Height { get; set; } = 1;
    }
}
